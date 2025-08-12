using Microsoft.Extensions.Logging;
using task_scheduler.Interfaces;

namespace task_scheduler
{
    public class App
    {
        private readonly ILogger<App> _logger;
        private readonly IUserCLI _userCLI;

        public App(ITaskSchedulerService scheduler, ILogger<App> logger, IUserCLI userCLI)
        {
            _logger = logger;
            _userCLI = userCLI;
        }

        public async Task Run()
        {
            _logger.LogInformation("Starting up...");

            // CLI for user interaction
            _userCLI.Run();
            _logger.LogInformation("Shutting down...");

        }
    }
}
