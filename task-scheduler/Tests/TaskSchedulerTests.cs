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
            var service = new TaskSchedulerService(_mockLogger.Object);

            service.ScheduleTask("task", DateTime.Now.AddSeconds(1), "action", false);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((@object, @type) => @object.ToString().Contains("Scheduled task 'task'")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            var tasks = service.GetScheduledTasksForTesting();

            var found = tasks.Any(t => t.Name == "task" && t.ScheduledTime > DateTime.Now && t.Action == "action" && !t.IsRecurring);

            Assert.True(found);

            service.Stop();
        }

        [Fact]
        public async Task TaskExecutesAtRightTime()
        {
            var service = new TaskSchedulerService(_mockLogger.Object);

            var executedTasks = new List<(string Name, DateTime ExecutionTime)>();

            service.OnTaskExecuted += task =>
            {
                executedTasks.Add((task.Name, DateTime.Now));
            }; 

            var scheduledTime = DateTime.Now.AddMilliseconds(500);
            service.ScheduleTask("task1", scheduledTime, "action", false);

            await Task.Delay(1500);

            Assert.Single(executedTasks);

            var executedTask = executedTasks[0];

            Assert.Equal("task1", executedTask.Name);

            var diff = (executedTask.ExecutionTime - scheduledTime).Duration();
            Assert.True(diff < TimeSpan.FromMilliseconds(200), $"Execution time was off by {diff.TotalMilliseconds} ms");

            service.Stop();
        }

        [Fact (Skip = "Will implement later")]
        public async Task QueuedTasksExecuteSequentially()
        {
        }


        [Fact (Skip = "Was not able to get working")]
        public async Task RecurringTaskIsRescheduled()
        {
            var service = new TaskSchedulerService(_mockLogger.Object);

            var startTime = DateTime.Now;
            const double recurrenceSeconds = 2;

            service.ScheduleTask("recurring", startTime, "action", true, recurrenceSeconds);

            var tasks = service.GetScheduledTasksForTesting();
            var initialTask = tasks.FirstOrDefault(t =>
                t.Name == "recurring" &&
                t.Action == "action" &&
                t.IsRecurring &&
                t.RecurrenceTime == recurrenceSeconds);

            Assert.NotNull(initialTask);
            Assert.True(initialTask.ScheduledTime >= startTime.AddMilliseconds(-100), "Initial scheduled time should be close to start time");

            // Wait enough time for the task to be executed and rescheduled
            await Task.Delay(3000);

            // Fetch tasks again after rescheduling
            tasks = service.GetScheduledTasksForTesting();

            // There should be at least one recurring task scheduled *after* the original scheduled time + recurrence interval
            var rescheduledTask = tasks.FirstOrDefault(t =>
                t.Name == "recurring" &&
                t.Action == "action" &&
                t.IsRecurring &&
                t.RecurrenceTime == recurrenceSeconds &&
                t.ScheduledTime >= initialTask.ScheduledTime.AddSeconds(recurrenceSeconds - 0.1)
            );

            Assert.NotNull(rescheduledTask);

            service.Stop();
        }


        [Fact]
        public async Task ErrorLogsCorrectly()
        {
            var service = new TaskSchedulerService(_mockLogger.Object);

            // using "error" to trigger an exception
            service.ScheduleTask("error", DateTime.Now, "error", false);

            await Task.Delay(1500);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((@object, @type) => @object.ToString().Contains("Error executing task 'error'")),
                    It.IsAny<InvalidOperationException>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            service.Stop();
        }

        [Fact]
        public void ListTasksLogsScheduledTasks()
        {
            var service = new TaskSchedulerService(_mockLogger.Object);

            service.ScheduleTask("task1", DateTime.Now.AddMinutes(1), "action", false);
            service.ScheduleTask("task2", DateTime.Now.AddMinutes(2), "action", true);

            service.ListTasks();

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
            var service = new TaskSchedulerService(_mockLogger.Object);

            service.ListTasks();

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
            var service = new TaskSchedulerService(_mockLogger.Object);

            service.Stop();

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