using task_scheduler.Domain;

namespace task_scheduler.Interfaces
{
    public interface ITaskSchedulerService
    {
        void ScheduleTask(string taskName, DateTime scheduledTime, string action, bool recurring, TimeSpan recurTime);
        void ListTasks();
        void Stop();
    }
}
