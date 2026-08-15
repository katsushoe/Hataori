using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hataori.Application.Control;

namespace Hataori.Monitor;

/// <summary>MonitorからローカルControl Pipeへ監視要求を送信します。</summary>
public sealed class MonitorControlClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public async Task<MonitorSnapshot> GetSnapshotAsync(string pipeName, TimeSpan timeout, CancellationToken cancellationToken)
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
            await writer.WriteLineAsync(JsonSerializer.Serialize(new ControlRequest("monitor"), JsonOptions)).ConfigureAwait(false);
            var line = await reader.ReadLineAsync(timeoutSource.Token).ConfigureAwait(false);
            var response = JsonSerializer.Deserialize<ControlResponse>(line ?? string.Empty, JsonOptions)
                ?? throw new IOException("Control Pipe returned an empty response.");
            return response.Monitor ?? throw new IOException("Control Pipe did not return monitor data.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Hataori Server did not respond before the timeout.");
        }
    }
}
