using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hataori.Application.Control;

namespace Hataori.Monitor;

/// <summary>MonitorからローカルControl Pipeへ監視・管理要求を送信します。</summary>
public sealed class MonitorControlClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    /// <summary>最新のMonitorスナップショットを取得します。</summary>
    public async Task<MonitorSnapshot> GetSnapshotAsync(string pipeName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var response = await SendAsync(pipeName, new ControlRequest("monitor"), timeout, cancellationToken).ConfigureAwait(false);
        return response.Monitor ?? throw new IOException("Control Pipe did not return monitor data.");
    }

    /// <summary>指定したTaskをキャンセルします。</summary>
    public Task<ControlResponse> CancelTaskAsync(string pipeName, string taskId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        return SendAsync(pipeName, new ControlRequest("task-cancel", taskId), timeout, cancellationToken);
    }

    /// <summary>指定したAgent Runをキャンセルします。</summary>
    public Task<ControlResponse> CancelRunAsync(string pipeName, string runId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        return SendAsync(pipeName, new ControlRequest("agent-cancel", runId), timeout, cancellationToken);
    }

    private static async Task<ControlResponse> SendAsync(string pipeName, ControlRequest request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeoutSource.Token).ConfigureAwait(false);
            await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, false, leaveOpen: true);
            await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions)).ConfigureAwait(false);
            var line = await reader.ReadLineAsync(timeoutSource.Token).ConfigureAwait(false);
            var response = JsonSerializer.Deserialize<ControlResponse>(line ?? string.Empty, JsonOptions)
                ?? throw new IOException("Control Pipe returned an empty response.");
            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Hataori Server did not respond before the timeout.");
        }
    }
}
