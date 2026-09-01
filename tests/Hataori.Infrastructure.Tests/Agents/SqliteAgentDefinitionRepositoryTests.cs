using FluentAssertions;
using Hataori.Application.Agents;
using Hataori.Infrastructure.Agents;
using Microsoft.Data.Sqlite;

namespace Hataori.Infrastructure.Tests.Agents;

public sealed class SqliteAgentDefinitionRepositoryTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"hataori-agent-definition-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task SetAsync_WhenCreatedAndUpdated_PersistsWorkspaceDefinitionAndHistory()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync(CancellationToken.None);
        var service = new AgentDefinitionService(repository, TimeProvider.System);

        await service.SetAsync("alpha", "Claude-Code", true, 2, CancellationToken.None);
        await service.SetAsync("alpha", "claude-code", false, 3, CancellationToken.None);
        await service.SetAsync("beta", "codex", true, 0, CancellationToken.None);

        var definitions = await service.ListAsync("alpha", CancellationToken.None);
        definitions.Should().ContainSingle();
        definitions[0].AgentId.Should().Be("claude-code");
        definitions[0].Enabled.Should().BeFalse();
        definitions[0].MaxConcurrentRuns.Should().Be(3);
        (await service.HistoryAsync("alpha", "claude-code", CancellationToken.None))
            .Select(item => item.EventType).Should().Equal("created", "updated");
        (await service.ListAsync(null, CancellationToken.None)).Should().HaveCount(2);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65)]
    public async Task SetAsync_WhenConcurrencyIsInvalid_RejectsValue(int maxConcurrentRuns)
    {
        var repository = CreateRepository();
        await repository.InitializeAsync(CancellationToken.None);
        var service = new AgentDefinitionService(repository, TimeProvider.System);

        var action = () => service.SetAsync("default", "codex", true, maxConcurrentRuns, CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private SqliteAgentDefinitionRepository CreateRepository()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        return new SqliteAgentDefinitionRepository(connectionString);
    }
}
