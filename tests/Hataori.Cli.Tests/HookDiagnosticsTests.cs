using FluentAssertions;

namespace Hataori.Cli.Tests;

public sealed class HookDiagnosticsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hataori-hooks-{Guid.NewGuid():N}");

    [Fact]
    public async Task CheckAsync_RequiredEventsPresent_Succeeds()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "hooks.json");
        await File.WriteAllTextAsync(path, """{"hooks":{"SessionStart":[{}],"UserPromptSubmit":[{}],"PreToolUse":[{}],"Stop":[{}]}}""");

        var action = () => HookDiagnostics.CheckAsync(["hooks.json"], _directory, CancellationToken.None);

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CheckAsync_MissingEvent_Fails()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "hooks.json");
        await File.WriteAllTextAsync(path, """{"hooks":{"SessionStart":[{}]}}""");

        var action = () => HookDiagnostics.CheckAsync(["hooks.json"], _directory, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*UserPromptSubmit*");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
