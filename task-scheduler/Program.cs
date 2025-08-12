using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using task_scheduler;
using task_scheduler.Interfaces;
using task_scheduler.Services;
using task_scheduler.Util;

// Configure DI
var services = new ServiceCollection();

services.AddLogging(builder => builder.AddConsole());
services.AddScoped<IUserCLI, UserCLI>();
services.AddScoped<ITaskSchedulerService, TaskSchedulerService>();
services.AddScoped<App>();

using var serviceProvider = services.BuildServiceProvider();
var app = serviceProvider.GetRequiredService<App>();

app.Run();