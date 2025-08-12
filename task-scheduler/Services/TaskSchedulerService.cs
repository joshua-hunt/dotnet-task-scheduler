using Microsoft.Extensions.Logging;
using task_scheduler.Domain;
using task_scheduler.Interfaces;

namespace task_scheduler.Services
{
    public class TaskSchedulerService : ITaskSchedulerService
    {
        private readonly PriorityQueue<ScheduledTask, DateTime> _taskQueue = new();

        //Cancellation token source to manage task processing cycle
        private readonly CancellationTokenSource _cts = new();

        //Cancellation token source for managing new task scheduling
        private CancellationTokenSource _delayCts = new();

        private readonly ILogger<TaskSchedulerService> _logger;

        public TaskSchedulerService(ILogger<TaskSchedulerService> logger)
        {
            _logger = logger;
            Task.Run(ProcessTasksAsync);
        }

        //Expose a readonly version of the task list  - this is currently only used for testing
        public IReadOnlyCollection<ScheduledTask> GetScheduledTasksForTesting() => _taskQueue.UnorderedItems.OrderBy(x => x.Priority).Select(item => item.Element).ToList().AsReadOnly();

        //Event to notify when a task is executed - this is currently only used for testing
        public event Action<ScheduledTask>? OnTaskExecuted;

        //Recur time is in seconds, default is 0 which means no recurrence
        public void ScheduleTask(string taskName, DateTime scheduledTime, string action, bool isRecurring, TimeSpan recurTime = default)
        {
            var newTask = new ScheduledTask
            {
                Name = taskName,
                ScheduledTime = scheduledTime,
                Action = action,
                IsRecurring = isRecurring,
                RecurrenceTime = recurTime
            };

            //Lock to ensure thread safety when adding tasks to the queue
            lock (_taskQueue)
            {
                _taskQueue.Enqueue(newTask, newTask.ScheduledTime);
            }

            _delayCts.Cancel();
            _logger.LogInformation($"Scheduled task '{taskName}' for {scheduledTime} (Recurring: {isRecurring})");
        }

        public async Task ProcessTasksAsync()
        {
            //Run until cancellation is requested
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    //Check for the next task in the queue
                    ScheduledTask nextTask = null;
                    DateTime nextTime = DateTime.MaxValue;

                    // Lock to safely access the task queue
                    lock (_taskQueue)
                    {
                        // Peek at the next task without removing it
                        if (_taskQueue.Count > 0)
                        {
                            nextTask = _taskQueue.Peek();
                            nextTime = nextTask.ScheduledTime;
                        }
                    }
                    // If no tasks are scheduled, wait for a while before checking again
                    if (nextTask == null)
                    {
                        await Task.Delay(500, _cts.Token);
                        continue;
                    }

                    var delay = nextTime - DateTime.Now;
                    if (delay > TimeSpan.Zero)
                    {
                        //Cancel previous delay if any, because a new task was scheduled
                        _delayCts.Cancel();
                        //Fresh token linked to stop token
                        _delayCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);

                        try
                        {
                            //Asynchronously wait until next task's time or until canceled
                            await Task.Delay(delay, _delayCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            continue;
                        }
                    }

                    //After the delay, check if the task is ready to run
                    ScheduledTask taskToRun = null;

                    //Lock to safely access the task queue
                    lock (_taskQueue)
                    {
                        //If the next task is ready to run, dequeue it
                        if (_taskQueue.Count > 0 && _taskQueue.Peek().ScheduledTime <= DateTime.Now)
                        {
                            taskToRun = _taskQueue.Dequeue();
                        }
                    }

                    //If a task is ready to run, execute it
                    if (taskToRun != null)
                    {
                        _logger.LogInformation($"Executing task: {taskToRun.Name} at {taskToRun.ScheduledTime}");

                        //Invoke the event to notify that a task is executed
                        OnTaskExecuted?.Invoke(taskToRun);

                        try
                        {
                            //Simulate task execution
                            if (taskToRun.IsRecurring)
                            {
                                //For recurring tasks, reschedule them with the new scheduled time
                                taskToRun.ScheduledTime = taskToRun.ScheduledTime.Add(taskToRun.RecurrenceTime);
                                _taskQueue.Enqueue(taskToRun, taskToRun.ScheduledTime);
                                _logger.LogInformation($"Rescheduled recurring task '{taskToRun.Name}' for {taskToRun.ScheduledTime}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error executing task '{taskToRun.Name}'");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    //Figure out how to log task cancellation
                    _logger.LogInformation("Task processing cancelled.");
                    break;
                }
                catch (Exception ex)
                {
                    //Figure out how to log unexpected task errors
                    _logger.LogError(ex, $"Unexpected error in task processing loop for task");
                    await Task.Delay(1000, _cts.Token);
                }
            }
        }

        public void ListTasks()
        {
            var tasks = GetScheduledTasksForTesting();
            if (tasks.Count == 0)
            {
                _logger.LogInformation("No tasks scheduled.");
                return;
            }

            foreach (var task in tasks)
            {
                _logger.LogInformation($"Task: {task.Name} at {task.ScheduledTime} (Recurring: {task.IsRecurring})");
            }
        }
        public void Stop()
        {
            _logger.LogInformation("Stopping the task scheduler service...");
            _cts.Cancel();
        }
    }
}
