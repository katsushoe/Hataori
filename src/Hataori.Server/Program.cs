using Hataori.Application.Tasks;
using Hataori.Infrastructure.Tasks;
using Hataori.Server;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("hataori.json", optional: false, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables("HATAORI_");
builder.Services.AddWindowsService();
builder.Services.AddOptions<ServerOptions>()
    .Bind(builder.Configuration.GetRequiredSection(ServerOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ServerOptions>, ServerOptionsValidator>();
builder.Services.AddSingleton<ITaskRepository>(services =>
{
    var options = services.GetRequiredService<IOptions<ServerOptions>>().Value;
    var path = ServerPaths.ResolveDatabasePath(options.DatabasePath, AppContext.BaseDirectory);
    var directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    var connectionString = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString();
    return new SqliteTaskRepository(connectionString);
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TaskService>();
builder.Services.AddSingleton<ControlCommandHandler>();
builder.Services.AddHostedService<HataoriServerWorker>();

await builder.Build().RunAsync();
