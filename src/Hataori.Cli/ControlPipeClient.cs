using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hataori.Application.Control;

namespace Hataori.Cli;

/// <summary>
/// Hataori ServerのローカルControl Pipeへ管理要求を送信します。
/// </summary>
public sealed class ControlPipeClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public async Task<ControlResponse> SendAsync(string pipeName, string command, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeoutSource.Token).ConfigureAwait(false);
            await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, false, leaveOpen: true);
            await writer.WriteLineAsync(JsonSerializer.Serialize(new ControlRequest(command), JsonOptions)).ConfigureAwait(false);
            var line = await reader.ReadLineAsync(timeoutSource.Token).ConfigureAwait(false);
            return JsonSerializer.Deserialize<ControlResponse>(line ?? string.Empty, JsonOptions)
                ?? throw new IOException("Control Pipe returned an empty response.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Hataori Server did not respond before the timeout.");
        }
    }
}
