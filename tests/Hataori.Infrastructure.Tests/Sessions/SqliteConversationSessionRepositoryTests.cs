using FluentAssertions;
using Hataori.Core.Sessions;
using Hataori.Infrastructure.Sessions;
using Microsoft.Data.Sqlite;

namespace Hataori.Infrastructure.Tests.Sessions;

public sealed class SqliteConversationSessionRepositoryTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"hataori-session-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task SaveAsync_ExistingLogicalKey_UpdatesSessionState()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync(CancellationToken.None);
        var session = ConversationSession.Create("conversation-1", "codex", "native-1", DateTimeOffset.UtcNow);
        await repository.SaveAsync(session, CancellationToken.None);

        session.StartRun(DateTimeOffset.UtcNow);
        session.CompleteRun("native-2", DateTimeOffset.UtcNow);
        await repository.SaveAsync(session, CancellationToken.None);
        var restored = await repository.GetAsync("conversation-1", "codex", CancellationToken.None);

        restored.Should().NotBeNull();
        restored!.NativeSessionId.Should().Be("native-2");
        restored.Status.Should().Be(ConversationSessionStatus.Idle);
        (await repository.ListAsync(ConversationSessionStatus.Idle, "codex", CancellationToken.None)).Should().ContainSingle();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private SqliteConversationSessionRepository CreateRepository()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        return new SqliteConversationSessionRepository(connectionString);
    }
}
