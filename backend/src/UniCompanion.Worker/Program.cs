using UniCompanion.Infrastructure;
using UniCompanion.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
// Shared Identity services require a routing data source even in a non-HTTP host.
builder.Services.AddRouting();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Services.AddHostedService<StudyWorker>();
builder.Services.AddHostedService<ActivityWorker>();
builder.Services.AddHostedService<ReminderWorker>();
builder.Services.AddHostedService<PushWorker>();
await builder.Build().RunAsync();
