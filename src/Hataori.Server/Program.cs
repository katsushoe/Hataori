using System.Net;
using Hataori.Application.Itoguruma;
using Hataori.Application.Messages;
using Hataori.Application.Sessions;
using Hataori.Application.Runs;
using Hataori.Application.Agents;
using Hataori.Application.Tasks;
using Hataori.Infrastructure.Itoguruma;
using Hataori.Infrastructure.Messages;
using Hataori.Infrastructure.Sessions;
using Hataori.Infrastructure.Runs;
using Hataori.Infrastructure.Agents.Codex;
using Hataori.Infrastructure.Agents.ClaudeCode;
using Hataori.Infrastructure.Tasks;
using Hataori.Server;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });
builder.Configuration.AddJsonFile("hataori.json", optional: false, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables("HATAORI_");
var startupOptions = builder.Configuration.GetRequiredSection(ServerOptions.SectionName).Get<ServerOptions>()
    ?? throw new InvalidOperationException("Server configuration is missing.");
builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Parse(startupOptions.McpHost), startupOptions.McpPort));
builder.Services.AddWindowsService();
builder.Services.AddOptions<ServerOptions>()
    .Bind(builder.Configuration.GetRequiredSection(ServerOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ServerOptions>, ServerOptionsValidator>();
builder.Services.AddOptions<ItogurumaClientOptions>()
    .Bind(builder.Configuration.GetRequiredSection(ItogurumaClientOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ItogurumaClientOptions>, ItogurumaClientOptionsValidator>();
builder.Services.AddOptions<CodexDriverOptions>()
    .Bind(builder.Configuration.GetRequiredSection(CodexDriverOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<CodexDriverOptions>, CodexDriverOptionsValidator>();
builder.Services.AddOptions<ClaudeCodeDriverOptions>()
    .Bind(builder.Configuration.GetRequiredSection(ClaudeCodeDriverOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ClaudeCodeDriverOptions>, ClaudeCodeDriverOptionsValidator>();
builder.Services.AddSingleton<IItogurumaClient>(services => new McpItogurumaClient(
    services.GetRequiredService<IOptions<ItogurumaClientOptions>>().Value,
    services.GetRequiredService<ILoggerFactory>()));
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
builder.Services.AddSingleton<IMessageQueueRepository>(services =>
{
    var options = services.GetRequiredService<IOptions<ServerOptions>>().Value;
    var path = ServerPaths.ResolveDatabasePath(options.DatabasePath, AppContext.BaseDirectory);
    var connectionString = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString();
    return new SqliteMessageQueueRepository(connectionString);
});
builder.Services.AddSingleton<IConversationSessionRepository>(services =>
{
    var options = services.GetRequiredService<IOptions<ServerOptions>>().Value;
    var path = ServerPaths.ResolveDatabasePath(options.DatabasePath, AppContext.BaseDirectory);
    var connectionString = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString();
    return new SqliteConversationSessionRepository(connectionString);
});
builder.Services.AddSingleton<IAgentRunRepository>(services =>
{
    var options = services.GetRequiredService<IOptions<ServerOptions>>().Value;
    var path = ServerPaths.ResolveDatabasePath(options.DatabasePath, AppContext.BaseDirectory);
    var connectionString = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString();
    return new SqliteAgentRunRepository(connectionString);
});
builder.Services.AddSingleton<IConversationMutex, ConversationMutex>();
builder.Services.AddSingleton<IAgentProcessManager, SystemAgentProcessManager>();
builder.Services.AddSingleton<IAgentDriver>(services => new CodexDriver(
    services.GetRequiredService<IAgentProcessManager>(),
    services.GetRequiredService<IOptions<CodexDriverOptions>>().Value));
builder.Services.AddSingleton<IAgentDriver>(services => new ClaudeCodeDriver(
    services.GetRequiredService<IAgentProcessManager>(),
    services.GetRequiredService<IOptions<ClaudeCodeDriverOptions>>().Value));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TaskService>();
builder.Services.AddSingleton<ConversationSessionService>();
builder.Services.AddSingleton<AgentRunService>();
builder.Services.AddSingleton<ControlCommandHandler>();
builder.Services.AddHostedService<HataoriServerWorker>();
builder.Services.AddHostedService<ItogurumaConnectionWorker>();
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<TaskMcpTools>();

var app = builder.Build();
app.UseHostFiltering();
app.MapMcp(startupOptions.McpPath);
await app.RunAsync();
