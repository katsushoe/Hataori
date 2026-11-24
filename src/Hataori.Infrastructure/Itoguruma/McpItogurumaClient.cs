using System.Text.Json;
using Hataori.Application.Itoguruma;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Hataori.Infrastructure.Itoguruma;

/// <summary>
/// 公式MCP Client SDKを使用するItoguruma Adapterです。
/// </summary>
public sealed class McpItogurumaClient : IItogurumaClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ItogurumaClientOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private McpClient? _client;

    public McpItogurumaClient(ItogurumaClientOptions options, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _options = options;
        _loggerFactory = loggerFactory;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_client is not null)
        {
            return;
        }

        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client is not null)
            {
                return;
            }

            var endpoint = _options.Endpoint ?? throw new InvalidOperationException("Itoguruma endpoint is not configured.");
            var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Name = "itoguruma",
                Endpoint = endpoint,
                TransportMode = HttpTransportMode.StreamableHttp,
                ConnectionTimeout = TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds),
                EnableStandaloneGetStream = false,
                AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {_options.AuthenticationToken}" },
            }, _loggerFactory);
            _client = await McpClient.CreateAsync(transport, loggerFactory: _loggerFactory, cancellationToken: cancellationToken).ConfigureAwait(false);
            await RegisterSelfAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await ResetAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async Task<IReadOnlyList<ItogurumaMessage>> GetMessagesAsync(string agentId, int limit, int leaseSeconds, string? threadId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        var result = await CallAsync("get_messages", new Dictionary<string, object?>
        {
            ["agent_id"] = agentId,
            ["consumer_agent_id"] = _options.AgentId,
            ["limit"] = limit,
            ["lease_seconds"] = leaseSeconds,
            ["thread_id"] = threadId,
        }, cancellationToken).ConfigureAwait(false);
        return Deserialize<List<ItogurumaMessage>>(result);
    }

    public async Task<IReadOnlyList<ItogurumaProject>> ListProjectsAsync(CancellationToken cancellationToken)
    {
        var result = await CallAsync("list_projects", new Dictionary<string, object?>(), cancellationToken).ConfigureAwait(false);
        return Deserialize<List<ItogurumaProject>>(result);
    }

    public async Task RegisterProjectInboxAsync(string projectId, string displayName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        await CallAsync("register_project_inbox", new Dictionary<string, object?>
        {
            ["project_id"] = projectId,
            ["display_name"] = displayName,
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ResetAsync().ConfigureAwait(false);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async Task<string> ReplyAsync(string recipientAgentId, string provider, string body, string threadId, string? replyToMessageId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var result = await CallAsync("send_message", new Dictionary<string, object?>
        {
            ["sender_agent_id"] = _options.AgentId,
            ["recipient"] = recipientAgentId,
            ["provider"] = provider,
            ["body"] = body,
            ["thread_id"] = threadId,
            ["reply_to_message_id"] = replyToMessageId,
            ["idempotency_key"] = idempotencyKey,
        }, cancellationToken).ConfigureAwait(false);
        return Deserialize<SendMessageResult>(result).MessageId;
    }

    public async Task<bool> AcknowledgeAsync(string agentId, string messageId, string leaseId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseId);
        var result = await CallAsync("ack_message", new Dictionary<string, object?>
        {
            ["agent_id"] = agentId,
            ["consumer_agent_id"] = _options.AgentId,
            ["message_id"] = messageId,
            ["lease_id"] = leaseId,
        }, cancellationToken).ConfigureAwait(false);
        return Deserialize<AcknowledgeResult>(result).Acked;
    }

    public async Task<ItogurumaStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var result = await CallAsync("get_version", new Dictionary<string, object?>(), cancellationToken).ConfigureAwait(false);
        var version = Deserialize<VersionResult>(result);
        return new ItogurumaStatus(true, version.Name, version.Version);
    }

    public async ValueTask DisposeAsync()
    {
        await ResetAsync().ConfigureAwait(false);
        _connectLock.Dispose();
    }

    /// <summary>
    /// Hataori自身のProject Inboxを確保し、agentIdがInboxと異なる場合だけ通常エージェントとして登録します。
    /// Project Inboxはregister_agentで登録できないため、agentIdとProject IDが同じ場合はInboxを受信者・送信者として使います。
    /// </summary>
    private async Task RegisterSelfAsync(CancellationToken cancellationToken)
    {
        var projects = await ListProjectsAsync(cancellationToken).ConfigureAwait(false);
        if (!projects.Any(project => string.Equals(project.ProjectId, _options.ProjectId, StringComparison.OrdinalIgnoreCase)))
        {
            await RegisterProjectInboxAsync(_options.ProjectId, "Hataori", cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(_options.AgentId, _options.ProjectId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await CallAsync("register_agent", new Dictionary<string, object?>
        {
            ["agent_id"] = _options.AgentId,
            ["agent_type"] = _options.AgentType,
            ["project_id"] = _options.ProjectId,
            ["name"] = "Hataori",
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CallToolResult> CallAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        var client = _client ?? throw new InvalidOperationException("Itoguruma is not connected.");
        var result = await client.CallToolAsync(toolName, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.IsError == true)
        {
            throw new InvalidOperationException($"Itoguruma tool '{toolName}' returned an error.");
        }

        return result;
    }

    private static T Deserialize<T>(CallToolResult result) where T : class
    {
        var content = result.StructuredContent
            ?? throw new InvalidOperationException("Itoguruma returned no structured content.");
        return ItogurumaStructuredContentDeserializer.Deserialize<T>(content, JsonOptions);
    }

    private async ValueTask ResetAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }
    }

    private sealed record SendMessageResult(string MessageId);
    private sealed record AcknowledgeResult(bool Acked);
    private sealed record VersionResult(string Name, string Version);
}
