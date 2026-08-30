using System.Net;
using Hataori.Application;
using Hataori.Application.Itoguruma;
using Hataori.Application.Localization;
using Hataori.Application.Messages;
using Hataori.Application.Sessions;
using Hataori.Application.Runs;
using Hataori.Application.Agents;
using Hataori.Application.Activation;
using Hataori.Application.Tasks;
using Hataori.Application.Codex;
using Hataori.Infrastructure.Itoguruma;
using Hataori.Infrastructure.Messages;
using Hataori.Infrastructure.Sessions;
using Hataori.Infrastructure.Runs;
using Hataori.Infrastructure.Agents.Codex;
using Hataori.Infrastructure.Agents.ClaudeCode;
using Hataori.Infrastructure.Tasks;
using Hataori.Infrastructure.Maintenance;
using Hataori.Infrastructure.Codex;
using Hataori.Server;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting.WindowsServices;
using ModelContextProtocol.Server;

ILogger? fatalLogger = null;
try
{
    var layout = InstallationLayout.Resolve(AppContext.BaseDirectory);
    layout.EnsureDirectories();
    var configurationPath = Path.GetFullPath(Environment.GetEnvironmentVariable("HATAORI_CONFIG_PATH") ?? layout.ConfigurationPath);
    await DefaultConfigurationWriter.EnsureAsync(configurationPath, CancellationToken.None);
    DisplayLanguage.ApplyFromConfiguration(configurationPath);
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = layout.RootPath });
    builder.Configuration.AddJsonFile(configurationPath, optional: false, reloadOnChange: true);
    if (WindowsServiceHelpers.IsWindowsService())
    {
        // optional: true — hataori.service.jsonはItogurumaトークン連携（`hataori service setup`）でのみ作られる。
        // 未作成でもHataori自体はItoguruma未連携（degraded）で起動できるため、file不在で起動を止めない。
        builder.Configuration.AddJsonFile(ServiceConfigurationPath.GetDefaultPath(AppContext.BaseDirectory), optional: true, reloadOnChange: true);
    }
    builder.Configuration.AddEnvironmentVariables("HATAORI_");
    var startupFileLogOptions = builder.Configuration.GetRequiredSection(FileLogOptions.SectionName).Get<FileLogOptions>()
        ?? throw new InvalidOperationException(DisplayLanguage.Text("ファイルログ設定がありません。", "File logging configuration is missing."));
    var fileLogValidation = new FileLogOptionsValidator().Validate(null, startupFileLogOptions);
    if (fileLogValidation.Failed)
    {
        throw new InvalidOperationException(string.Join(" ", fileLogValidation.Failures));
    }

    if (startupFileLogOptions.Enabled)
    {
        builder.Logging.AddProvider(new FileLoggerProvider(startupFileLogOptions, layout.RootPath));
    }

    var startupOptions = builder.Configuration.GetRequiredSection(ServerOptions.SectionName).Get<ServerOptions>()
        ?? throw new InvalidOperationException(DisplayLanguage.Text("Server設定がありません。", "Server configuration is missing."));
    builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Parse(startupOptions.McpHost), startupOptions.McpPort));
    if (WindowsServiceHelpers.IsWindowsService())
    {
        builder.Services.AddWindowsService();
    }
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
    builder.Services.AddOptions<ActivationOptions>()
        .Bind(builder.Configuration.GetRequiredSection(ActivationOptions.SectionName))
        .PostConfigure(options =>
        {
            if (options.ProviderPriority.Count == 0)
            {
                options.ProviderPriority = ActivationOptions.DefaultProviderPriority;
            }
        })
        .ValidateOnStart();
    builder.Services.AddSingleton<IValidateOptions<ActivationOptions>, ActivationOptionsValidator>();
    builder.Services.AddOptions<ReplyRetryOptions>()
        .Bind(builder.Configuration.GetRequiredSection(ReplyRetryOptions.SectionName))
        .ValidateOnStart();
    builder.Services.AddSingleton<IValidateOptions<ReplyRetryOptions>, ReplyRetryOptionsValidator>();
    builder.Services.AddOptions<FileLogOptions>()
        .Bind(builder.Configuration.GetRequiredSection(FileLogOptions.SectionName))
        .ValidateOnStart();
    builder.Services.AddSingleton<IValidateOptions<FileLogOptions>, FileLogOptionsValidator>();
    builder.Services.AddOptions<DatabaseMaintenanceOptions>()
        .Bind(builder.Configuration.GetRequiredSection(DatabaseMaintenanceOptions.SectionName))
        .ValidateOnStart();
    builder.Services.AddSingleton<IValidateOptions<DatabaseMaintenanceOptions>, DatabaseMaintenanceOptionsValidator>();
    builder.Services.AddSingleton<IItogurumaClient>(services => new McpItogurumaClient(
        services.GetRequiredService<IOptions<ItogurumaClientOptions>>().Value,
        services.GetRequiredService<ILoggerFactory>()));
    builder.Services.AddSingleton<ITaskRepository>(services =>
    {
        var options = services.GetRequiredService<IOptions<ServerOptions>>().Value;
        var path = ServerPaths.ResolveDatabasePath(options.DatabasePath, layout.RootPath);
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
        var path = ServerPaths.ResolveDatabasePath(options.DatabasePath, layout.RootPath);
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString();
        return new SqliteMessageQueueRepository(connectionString);
    });
    builder.Services.AddSingleton<ICodexTaskLaunchRepository>(services =>
    {
        var options = services.GetRequiredService<IOptions<ServerOptions>>().Value;
        var path = ServerPaths.ResolveDatabasePath(options.DatabasePath, layout.RootPath);
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString();
        return new SqliteCodexTaskLaunchRepository(connectionString);
    });
    builder.Services.AddSingleton<IConversationSessionRepository>(services =>
    {
        var options = services.GetRequiredService<IOptions<ServerOptions>>().Value;
        var path = ServerPaths.ResolveDatabasePath(options.DatabasePath, layout.RootPath);
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString();
        return new SqliteConversationSessionRepository(connectionString);
    });
    builder.Services.AddSingleton<IAgentRunRepository>(services =>
    {
        var options = services.GetRequiredService<IOptions<ServerOptions>>().Value;
        var path = ServerPaths.ResolveDatabasePath(options.DatabasePath, layout.RootPath);
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString();
        return new SqliteAgentRunRepository(connectionString);
    });
    builder.Services.AddSingleton<IConversationMutex, ConversationMutex>();
    builder.Services.AddSingleton<IAgentProcessManager, SystemAgentProcessManager>();
    builder.Services.AddSingleton<IAgentProcessProbe, SystemAgentProcessProbe>();
    builder.Services.AddSingleton<IAgentDriver>(services => new CodexDriver(
        services.GetRequiredService<IAgentProcessManager>(),
        services.GetRequiredService<IOptions<CodexDriverOptions>>().Value));
    builder.Services.AddSingleton<IAgentDriver>(services => new ClaudeCodeDriver(
        services.GetRequiredService<IAgentProcessManager>(),
        services.GetRequiredService<IOptions<ClaudeCodeDriverOptions>>().Value));
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<TaskService>();
    builder.Services.AddSingleton<CodexTaskLaunchService>();
    builder.Services.AddSingleton<ConversationSessionService>();
    builder.Services.AddSingleton<AgentRunService>();
    builder.Services.AddSingleton<ActivationManager>();
    builder.Services.AddSingleton<AgentProviderSelector>();
    builder.Services.AddSingleton(new ProviderPriorityService(configurationPath));
    builder.Services.AddSingleton(services =>
    {
        var options = services.GetRequiredService<IOptions<ReplyRetryOptions>>().Value;
        return new ReplyRetrySettings(
            options.MaxAttempts,
            TimeSpan.FromSeconds(options.InitialDelaySeconds),
            TimeSpan.FromSeconds(options.MaximumDelaySeconds),
            options.BatchSize);
    });
    builder.Services.AddSingleton<ReplyRetryManager>();
    builder.Services.AddSingleton<ItogurumaConnectionState>();
    builder.Services.AddSingleton<StartupRecoveryService>();
    builder.Services.AddSingleton<DatabaseInitializationGate>();
    builder.Services.AddSingleton<StartupRecoveryGate>();
    builder.Services.AddSingleton(services =>
    {
        var options = services.GetRequiredService<IOptions<ServerOptions>>().Value;
        var path = ServerPaths.ResolveDatabasePath(options.DatabasePath, layout.RootPath);
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString();
        return new SqliteDatabaseMaintenance(connectionString, services.GetRequiredService<TimeProvider>());
    });
    builder.Services.AddSingleton<ControlCommandHandler>();
    builder.Services.AddHostedService<HataoriServerWorker>();
    builder.Services.AddHostedService<ItogurumaConnectionWorker>();
    builder.Services.AddHostedService<StartupRecoveryWorker>();
    builder.Services.AddHostedService<ActivationWorker>();
    builder.Services.AddHostedService<ReplyRetryWorker>();
    builder.Services.AddHostedService<DatabaseMaintenanceWorker>();
    builder.Services.AddMcpServer(options => options.ServerInstructions = HataoriMcpInstructions.Text)
        .WithHttpTransport(options => options.Stateless = true)
        .WithPrompts<HataoriMcpPrompts>()
        .WithTools<ProjectMcpTools>()
        .WithTools<TaskMcpTools>()
        .WithTools<AgentRunMcpTools>()
        .WithTools<ProviderMcpTools>()
        .WithTools<CodexTaskMcpTools>()
        .WithTools<SystemMcpTools>();

    var app = builder.Build();
    fatalLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Hataori.Server.Fatal");
    app.UseHostFiltering();
    app.MapMcp(startupOptions.McpPath);
    await app.RunAsync();
    return 0;
}
catch (OperationCanceledException)
{
    return 0;
}
catch (Exception exception)
{
    try
    {
        fatalLogger?.LogCritical(exception, "[Fatal] Hataori Server stopped because an unhandled application error reached the process boundary");
    }
    catch (Exception loggingException)
    {
        Console.Error.WriteLine(DisplayLanguage.Text($"致命的エラーのロガー出力にも失敗しました: {loggingException.Message}", $"Fatal error logging also failed: {loggingException.Message}"));
    }
    var report = await FatalErrorReporter.WriteAsync(exception, CancellationToken.None);
    Console.Error.WriteLine(report.UserMessage);
    Console.Error.WriteLine(report.LogPath is null
        ? DisplayLanguage.Text("詳細ログは保存できませんでした。", "The detailed log could not be saved.")
        : DisplayLanguage.Text($"詳細ログ: {report.LogPath}", $"Detailed log: {report.LogPath}"));
    return 1;
}
