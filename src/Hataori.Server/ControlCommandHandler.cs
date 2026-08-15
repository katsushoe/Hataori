using Hataori.Application.Control;
using Microsoft.Extensions.Hosting;

namespace Hataori.Server;

/// <summary>
/// ローカルControl Pipeから受け取った管理コマンドを処理します。
/// </summary>
public sealed class ControlCommandHandler
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly TimeProvider _timeProvider;

    public ControlCommandHandler(IHostApplicationLifetime lifetime, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _lifetime = lifetime;
        _timeProvider = timeProvider;
    }

    public ControlResponse Handle(ControlRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.Equals(request.Command, "status", StringComparison.OrdinalIgnoreCase))
        {
            return new ControlResponse(true, "running", _timeProvider.GetUtcNow());
        }

        if (string.Equals(request.Command, "stop", StringComparison.OrdinalIgnoreCase))
        {
            _lifetime.StopApplication();
            return new ControlResponse(true, "stopping", _timeProvider.GetUtcNow());
        }

        return new ControlResponse(false, "unknown_command", _timeProvider.GetUtcNow());
    }
}
