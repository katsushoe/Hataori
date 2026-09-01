using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hataori.Application.Control;
using Hataori.Application.Tasks;
using Hataori.Application.Sessions;
using Hataori.Application.Runs;
using Hataori.Application.Agents;
using Hataori.Application.Activation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hataori.Server;

/// <summary>
/// SQLiteを初期化し、ローカルControl Pipeを常駐提供します。
/// </summary>
public sealed class HataoriServerWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ITaskRepository _repository;
    private readonly IConversationSessionRepository _sessionRepository;
    private readonly IAgentRunRepository _runRepository;
    private readonly IAgentDefinitionRepository _agentDefinitionRepository;
    private readonly AgentDefinitionService _agentDefinitionService;
    private readonly IReadOnlyList<IAgentDriver> _agentDrivers;
    private readonly ActivationOptions _activationOptions;
    private readonly ControlCommandHandler _handler;
    private readonly DatabaseInitializationGate _initializationGate;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ServerOptions _options;
    private readonly ILogger<HataoriServerWorker> _logger;

    public HataoriServerWorker(ITaskRepository repository, IConversationSessionRepository sessionRepository, IAgentRunRepository runRepository, IAgentDefinitionRepository agentDefinitionRepository, AgentDefinitionService agentDefinitionService, IEnumerable<IAgentDriver> agentDrivers, IOptions<ActivationOptions> activationOptions, ControlCommandHandler handler, DatabaseInitializationGate initializationGate, IHostApplicationLifetime applicationLifetime, IOptions<ServerOptions> options, ILogger<HataoriServerWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(sessionRepository);
        ArgumentNullException.ThrowIfNull(runRepository);
        ArgumentNullException.ThrowIfNull(agentDefinitionRepository);
        ArgumentNullException.ThrowIfNull(agentDefinitionService);
        ArgumentNullException.ThrowIfNull(agentDrivers);
        ArgumentNullException.ThrowIfNull(activationOptions);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(initializationGate);
        ArgumentNullException.ThrowIfNull(applicationLifetime);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _sessionRepository = sessionRepository;
        _runRepository = runRepository;
        _agentDefinitionRepository = agentDefinitionRepository;
        _agentDefinitionService = agentDefinitionService;
        _agentDrivers = agentDrivers.ToArray();
        _activationOptions = activationOptions.Value;
        _handler = handler;
        _initializationGate = initializationGate;
        _applicationLifetime = applicationLifetime;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _repository.InitializeAsync(stoppingToken).ConfigureAwait(false);
            await _sessionRepository.InitializeAsync(stoppingToken).ConfigureAwait(false);
            await _runRepository.InitializeAsync(stoppingToken).ConfigureAwait(false);
            await _agentDefinitionRepository.InitializeAsync(stoppingToken).ConfigureAwait(false);
            await SeedAgentDefinitionsAsync(stoppingToken).ConfigureAwait(false);
            _initializationGate.Complete();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _initializationGate.Fail();
            return;
        }
        catch (Exception exception)
        {
            _initializationGate.Fail();
            _logger.LogCritical(exception, "[Startup][Database] Database initialization failed. Check database access, available disk space, and the configured database path. Hataori will stop safely");
            _applicationLifetime.StopApplication();
            return;
        }
        _logger.LogInformation(Hataori.Application.Localization.DisplayLanguage.Text("[起動][ControlPipe] Hataori Serverをpipe {PipeName}で開始しました", "[Startup][ControlPipe] Hataori Server started with pipe {PipeName}"), _options.ControlPipeName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AcceptClientAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException exception)
            {
                _logger.LogError(exception, "[ControlPipe] Pipe communication failed");
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "[ControlPipe] Invalid request received");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "[ControlPipe] Unexpected request processing failure");
            }
        }

        _logger.LogInformation(Hataori.Application.Localization.DisplayLanguage.Text("[停止][ControlPipe] Hataori Serverを停止しました", "[Shutdown][ControlPipe] Hataori Server stopped"));
    }

    private async Task SeedAgentDefinitionsAsync(CancellationToken cancellationToken)
    {
        foreach (var workspace in ActivationWorkspaceResolver.Resolve(_activationOptions))
        {
            if ((await _agentDefinitionRepository.ListAsync(workspace.WorkspaceId, cancellationToken).ConfigureAwait(false)).Count > 0)
            {
                continue;
            }

            foreach (var driver in _agentDrivers)
            {
                var maxRuns = _activationOptions.MaxConcurrentRuns.GetValueOrDefault(driver.AgentType);
                await _agentDefinitionService.SetAsync(workspace.WorkspaceId, driver.AgentType, true, maxRuns, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task AcceptClientAsync(CancellationToken cancellationToken)
    {
        await using var pipe = new NamedPipeServerStream(_options.ControlPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(pipe, Encoding.UTF8, false, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        var request = JsonSerializer.Deserialize<ControlRequest>(line ?? string.Empty, JsonOptions) ?? throw new JsonException("Control request is empty.");
        var response = await _handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
        await writer.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions)).ConfigureAwait(false);
    }
}
