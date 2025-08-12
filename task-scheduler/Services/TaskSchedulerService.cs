using Microsoft.Extensions.Logging;
using task_scheduler.Domain;
using task_scheduler.Interfaces;

namespace task_scheduler.Services
{
    public class TaskSchedulerService : ITaskSchedulerService
    {
        private readonly ILogger<TaskSchedulerService> _logger;
        private readonly PriorityQueue<ScheduledTask, DateTime> _taskQueue = new();
        private readonly CancellationTokenSource _cts = new();
        private CancellationTokenSource _delayCts = new();

        // for list and testing
        private List<ScheduledTask> _taskList = new();

        public TaskSchedulerService(ILogger<TaskSchedulerService> logger)
        {
            _logger = logger;
            Task.Run(ProcessTasksAsync);
        }

        public IReadOnlyCollection<ScheduledTask> GetScheduledTasksForTesting() => _taskList.ToList().AsReadOnly();
        public event Action<ScheduledTask> OnTaskExecuted;

        public void ScheduleTask(string taskName, DateTime scheduledTime, string action, bool isRecurring, double recurTime = 0)
        {
            var newTask = new ScheduledTask
            {
                Name = taskName,
                ScheduledTime = scheduledTime,
                Action = action,
                IsRecurring = isRecurring,
                RecurrenceTime = recurTime
            };
            lock (_taskQueue)
            {
                _taskQueue.Enqueue(newTask, newTask.ScheduledTime);
                _taskList.Add(newTask);
            }
            _delayCts.Cancel();
            _logger.LogInformation($"Scheduled task '{taskName}' for {scheduledTime} (Recurring: {isRecurring})");
        }

        public async Task ProcessTasksAsync()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    ScheduledTask nextTask = null;
                    DateTime nextTime = DateTime.MaxValue;

                    lock (_taskQueue)
                    {
                        if (_taskQueue.Count > 0)
                        {
                            nextTask = _taskQueue.Peek();
                            nextTime = nextTask.ScheduledTime;
                        }
                    }

                    if (nextTask == null)
                    {
                        await Task.Delay(500, _cts.Token);
                        continue;
                    }

                    var delay = nextTime - DateTime.Now;
                    if (delay > TimeSpan.Zero)
                    {
                        // cancels any previous delay task
                        _delayCts.Cancel();
                        _delayCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);

                        try
                        {
                            await Task.Delay(delay, _delayCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            continue;
                        }
                    }

                    ScheduledTask taskToRun = null;
                    lock (_taskQueue)
                    {
                        if (_taskQueue.Count > 0 && _taskQueue.Peek().ScheduledTime <= DateTime.Now)
                        {
                            taskToRun = _taskQueue.Dequeue();
                            _taskList.Remove(taskToRun);
                        }
                    }

                    if (taskToRun != null)
                    {
                        _logger.LogInformation($"Executing task: {taskToRun.Name} at {taskToRun.ScheduledTime}");
                        OnTaskExecuted?.Invoke(taskToRun);

                        try
                        {
                            if (taskToRun.Action == "error")
                                throw new InvalidOperationException("Simulated task error");

                            if (taskToRun.IsRecurring)
                            {
                                taskToRun.ScheduledTime = taskToRun.ScheduledTime.AddSeconds(taskToRun.RecurrenceTime);
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
                    _logger.LogInformation("Task processing cancelled.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in task processing loop.");
                    await Task.Delay(1000, _cts.Token);
                }
            }
        }

        public void ListTasks()
        {
            var tasks = _taskList.ToList();
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
