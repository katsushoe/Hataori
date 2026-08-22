using FluentAssertions;

namespace Hataori.Server.Tests;

public sealed class ProviderMcpToolsTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"hataori-mcp-provider-{Guid.NewGuid():N}.json");

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsSamePriority()
    {
        await File.WriteAllTextAsync(_path, """{"activation":{"providerPriority":["codex"]}}""");
        var tools = new ProviderMcpTools(new ProviderPriorityService(_path));

        await tools.SetAsync(["claude-code", "codex"], CancellationToken.None);

        (await tools.GetAsync(CancellationToken.None)).Should().Equal("claude-code", "codex");
    }

    public void Dispose()
    {
        File.Delete(_path);
        GC.SuppressFinalize(this);
    }
}
