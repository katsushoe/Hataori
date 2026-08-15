using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Hataori.Server.Tests;

public sealed class FileLoggerProviderTests : IDisposable
{
    private readonly string _directoryPath = Path.Combine(Path.GetTempPath(), $"hataori-logs-{Guid.NewGuid():N}");

    [Fact]
    public void Log_Information_WritesStructuredRedactedLine()
    {
        var provider = new FileLoggerProvider(new FileLogOptions { DirectoryPath = _directoryPath }, AppContext.BaseDirectory);
        var logger = provider.CreateLogger("Hataori.Server.ActivationWorker");

        logger.LogInformation("Agent {AgentId} authenticated with {AuthenticationToken}", "codex", "secret-value");
        provider.Dispose();

        var path = Directory.GetFiles(_directoryPath, "hataori-*.log").Should().ContainSingle().Subject;
        var content = File.ReadAllText(path);
        content.Should().Contain("[I] [ActivationWorker]");
        content.Should().Contain("AgentId=codex");
        content.Should().Contain("AuthenticationToken=(redacted)");
        content.Should().NotContain("secret-value");
    }

    [Fact]
    public void Validate_InvalidRetention_ReturnsFailure()
    {
        var result = new FileLogOptionsValidator().Validate(null, new FileLogOptions { RetentionDays = 0 });

        result.Failed.Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, true);
        }
    }
}
