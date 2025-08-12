using Microsoft.Extensions.Logging;
using System;
using task_scheduler.Interfaces;

namespace task_scheduler.Util
{
    public class UserCLI : IUserCLI
    {
        private readonly ITaskSchedulerService _scheduler;
        private readonly ILogger<UserCLI> _logger;

        public UserCLI(ITaskSchedulerService scheduler, ILogger<UserCLI> logger)
        {
            _scheduler = scheduler;
            _logger = logger;
        }

        public void Run()
        {
            DisplayWelcomeMessage();

            while (true)
            {
                Console.Write("> ");
                var input = Console.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(input))
                    continue;

                var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var command = parts[0].ToLower();

                switch (command)
                {
                    case "schedule":
                        if (parts.Length < 5)
                        {
                            Console.WriteLine("Usage: schedule <task_name> <yyyy-MM-dd HH:mm> <action>");
                            break;
                        }

                        var name = parts[1];
                        if (!DateTime.TryParse($"{parts[2]} {parts[3]}", out var time))
                        {
                            Console.WriteLine("Invalid time format.");
                            break;
                        }

                        var actionName = parts[4];

                        Console.Write("Is this a recurring task? (yes/no): ");
                        var isRecurringInput = Console.ReadLine()?.Trim().ToLower();
                        if (isRecurringInput != "yes" && isRecurringInput != "no" && isRecurringInput != "y" && isRecurringInput != "n")
                        {
                            Console.WriteLine("Invalid input for recurring task. Please enter 'yes' or 'no'.");
                            break;
                        }
                        var isRecurring = isRecurringInput == "yes" || isRecurringInput == "y";
                        var recurTimeValue = 0;
                        if (isRecurring)
                        {
                            Console.WriteLine("Recur time in seconds: ");
                            var recurTime = Console.ReadLine()?.Trim().ToLower();
                            recurTimeValue = int.TryParse(recurTime, out var parsedRecurTime) ? parsedRecurTime : 0;
                        }
                        
                        _scheduler.ScheduleTask(name, time, actionName, isRecurring, recurTimeValue);
                        break;

                    case "list":
                        _scheduler.ListTasks();
                        break;

                    case "exit":
                        _scheduler.Stop();
                        return;

                    case "help":
                        DisplayHelp();
                        break;

                    default:
                        Console.WriteLine("Unknown command. Type 'help' for a list of commands.");
                        break;
                }
            }
        }

        private void DisplayWelcomeMessage()
        {
            Console.WriteLine("Welcome to the Task Scheduler CLI!");
            Console.WriteLine("Type 'help' for a list of commands.");
            Console.WriteLine("Note: Caret placement for prompts can sometimes not show, so if unsure just type a command.");
        }

        private void DisplayHelp()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("schedule <task_name> <yyyy-MM-dd HH:mm> <action>");
            Console.WriteLine("list");
            Console.WriteLine("exit");
        }
    }
}
