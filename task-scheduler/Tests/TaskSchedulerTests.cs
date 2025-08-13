using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;
using System.Reflection;
using task_scheduler.Domain;
using task_scheduler.Services;
using Xunit;

namespace task_scheduler.Tests
{
    public class TaskSchedulerServiceTests
    {
        private readonly Mock<ILogger<TaskSchedulerService>> _mockLogger;

        public TaskSchedulerServiceTests()
        {
            _mockLogger = new Mock<ILogger<TaskSchedulerService>>();
        }

        [Fact]
        public void TaskRegisteredCorrectlyWithLogging()
        {
            //Arrange - create a service instance
            var service = new TaskSchedulerService(_mockLogger.Object);

            //Act - schedule a task
            service.ScheduleTask("task", DateTime.Now.AddSeconds(1), "action", false);

            //Assert - check if the task was added to the queue & logged correctly
            var tasks = service.GetScheduledTasksForTesting();
            var found = tasks.Any(t => t.Name == "task" && t.ScheduledTime > DateTime.Now && t.Action == "action" && !t.IsRecurring);
            Assert.True(found);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((@object, @type) => @object.ToString().Contains("Scheduled task 'task'")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
            
            service.Stop();
        }

        [Fact]
        public async Task TaskExecutesAtRightTime()
        {
            //Arrange - create a service instance and a list to capture executed tasks, and subscribe to the OnTaskExecuted event
            var service = new TaskSchedulerService(_mockLogger.Object);
            var executedTasks = new List<(string Name, DateTime ExecutionTime)>();
            service.OnTaskExecuted += task => executedTasks.Add((task.Name, DateTime.Now));

            //Act - Schedule a task for execution
            var scheduledTime = DateTime.Now.AddMilliseconds(500);
            service.ScheduleTask("task1", scheduledTime, "action", false);
            await Task.Delay(1500);

            //Assert - Ensure the task was executed at the right time
            Assert.Single(executedTasks);
            var executedTask = executedTasks[0];
            Assert.Equal("task1", executedTask.Name);
            var diff = (executedTask.ExecutionTime - scheduledTime).Duration();
            Assert.True(diff < TimeSpan.FromMilliseconds(200), $"Execution time was off by {diff.TotalMilliseconds} ms");

            service.Stop();
        }

        [Fact]
        public async Task QueuedTasksExecuteSequentially()
        {
            //Arrange - create a service instance and a list to capture executed tasks
            var service = new TaskSchedulerService(_mockLogger.Object);
            var executedTasks = new List<(string Name, DateTime ExecutionTime)>();
            service.OnTaskExecuted += task => executedTasks.Add((task.Name, DateTime.Now));

            //Act - Schedule multiple tasks with staggered execution times
            var start = DateTime.Now.AddMilliseconds(200);
            service.ScheduleTask("task1", start, "action", false);
            service.ScheduleTask("task2", start.AddMilliseconds(200), "action", false);
            service.ScheduleTask("task3", start.AddMilliseconds(400), "action", false);
            await Task.Delay(1500);

            //Assert - Ensure the right amount of delegates were called in the expected order 
            Assert.Equal(3, executedTasks.Count);
            Assert.Equal("task1", executedTasks[0].Name);
            Assert.Equal("task2", executedTasks[1].Name);
            Assert.Equal("task3", executedTasks[2].Name);

            service.Stop();
        }

        [Fact]
        //[Fact(Skip = "Couldnt figure out")]
        public async Task RecurringTaskIsRescheduled()
        {
            //Arrange - create a service instance and set up a recurring task
            var service = new TaskSchedulerService(_mockLogger.Object);
            var startTime = DateTime.Now;
            TimeSpan recurrenceSeconds = TimeSpan.FromSeconds(2);
            service.ScheduleTask("recurring", startTime, "action", true, recurrenceSeconds);

            //Act - wait for the task to execute and check if it was scheduled correctly
            var tasks = service.GetScheduledTasksForTesting();
            var initialTask = tasks.FirstOrDefault(t =>
                t.Name == "recurring" &&
                t.Action == "action" &&
                t.IsRecurring &&
                t.RecurrenceTime == recurrenceSeconds);
            var initialScheduledTime = initialTask.ScheduledTime;

            //Assert - Ensure the initial task was scheduled correctly
            Assert.NotNull(initialTask);
            Assert.True(Math.Abs((initialScheduledTime - startTime.AddSeconds(recurrenceSeconds.TotalSeconds)).TotalMilliseconds) < 1, "Times are within 1ms");

            //Act - wait for the task to execute and check if it was rescheduled
            await Task.Delay(3000);
            tasks = service.GetScheduledTasksForTesting();
            var rescheduledTask = tasks.FirstOrDefault(t =>
                t.Name == "recurring" &&
                t.Action == "action" &&
                t.IsRecurring &&
                t.RecurrenceTime == recurrenceSeconds &&
                t.ScheduledTime >= initialScheduledTime.AddSeconds(recurrenceSeconds.TotalSeconds - 0.1)
            );
            var rescheduledTime = rescheduledTask.ScheduledTime;

            //Assert - Ensure the recurring task was scheduled correctly
            Assert.NotNull(rescheduledTask);
            Assert.True(
                Math.Abs((rescheduledTime - initialScheduledTime.AddSeconds(recurrenceSeconds.TotalSeconds)).TotalMilliseconds) < 1,
                "Rescheduled time is within 1ms of expected"
            );
            service.Stop();
        }

        [Fact]
        public void ListTasksLogsScheduledTasks()
        {
            //Arrange - create a service instance and schedule some tasks
            var service = new TaskSchedulerService(_mockLogger.Object);

            service.ScheduleTask("task1", DateTime.Now.AddMinutes(1), "action", false);
            service.ScheduleTask("task2", DateTime.Now.AddMinutes(2), "action", true);

            //Act - call ListTasks to log the scheduled tasks
            service.ListTasks();

            //Assert - verify that the logger was called with the expected messages
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((@object, @type) => @object.ToString().StartsWith("Task: task1")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((@object, @type) => @object.ToString().StartsWith("Task: task2")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
            
            service.Stop();
        }

        [Fact]
        public void ListTasksLogsNoTasksMessageIfQueueEmpty()
        {
            //Arrange - create a service instance without scheduling any tasks
            var service = new TaskSchedulerService(_mockLogger.Object);

            //Act - call ListTasks to log the scheduled tasks
            service.ListTasks();

            //Assert - verify that the logger was called with the expected message for no tasks
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((@object, @type) => @object.ToString().Contains("No tasks scheduled.")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            service.Stop();
        }

        [Fact]
        public void StopCancelsProcessingAndLogs()
        {
            //Arrange - create a service instance
            var service = new TaskSchedulerService(_mockLogger.Object);

            //Act - call Stop to cancel processing
            service.Stop();

            //Assert - verify that the logger was called with the expected message
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((@object, @type) => @object.ToString().Contains("Stopping the task scheduler service")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}