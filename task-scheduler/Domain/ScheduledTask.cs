namespace task_scheduler.Domain
{
    public class ScheduledTask
    {
        public required string Name { get; set; }
        public DateTime ScheduledTime { get; set; }
        public required string Action { get; set; }
        public bool IsRecurring { get; set; }
        public TimeSpan RecurrenceTime { get; set; }
    }
}
