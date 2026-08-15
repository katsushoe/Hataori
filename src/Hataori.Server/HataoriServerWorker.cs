using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hataori.Application.Control;
using Hataori.Application.Tasks;
using Hataori.Application.Sessions;
using Hataori.Application.Runs;
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
    private readonly ControlCommandHandler _handler;
    private readonly ServerOptions _options;
    private readonly ILogger<HataoriServerWorker> _logger;

    public HataoriServerWorker(ITaskRepository repository, IConversationSessionRepository sessionRepository, IAgentRunRepository runRepository, ControlCommandHandler handler, IOptions<ServerOptions> options, ILogger<HataoriServerWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(sessionRepository);
        ArgumentNullException.ThrowIfNull(runRepository);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _sessionRepository = sessionRepository;
        _runRepository = runRepository;
        _handler = handler;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _repository.InitializeAsync(stoppingToken).ConfigureAwait(false);
        await _sessionRepository.InitializeAsync(stoppingToken).ConfigureAwait(false);
        await _runRepository.InitializeAsync(stoppingToken).ConfigureAwait(false);
        _logger.LogInformation("[Startup][ControlPipe] Hataori Server started with pipe {PipeName}", _options.ControlPipeName);

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
        }

        _logger.LogInformation("[Shutdown][ControlPipe] Hataori Server stopped");
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
