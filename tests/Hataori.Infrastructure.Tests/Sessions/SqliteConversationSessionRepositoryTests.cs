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

    [Fact]
    public async Task SaveAsync_SameConversationInDifferentWorkspaces_PersistsSeparately()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync(CancellationToken.None);
        await repository.SaveAsync(ConversationSession.Create("alpha", "conversation-1", "codex", "native-a", DateTimeOffset.UtcNow), CancellationToken.None);
        await repository.SaveAsync(ConversationSession.Create("beta", "conversation-1", "codex", "native-b", DateTimeOffset.UtcNow), CancellationToken.None);

        (await repository.GetAsync("alpha", "conversation-1", "codex", CancellationToken.None))!.NativeSessionId.Should().Be("native-a");
        (await repository.GetAsync("beta", "conversation-1", "codex", CancellationToken.None))!.NativeSessionId.Should().Be("native-b");
    }

    [Fact]
    public async Task InitializeAsync_PreWorkspaceSchema_MigratesSessionToDefault()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        await using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE conversation_sessions (
                    conversation_id TEXT NOT NULL, agent_id TEXT NOT NULL, native_session_id TEXT NOT NULL,
                    status TEXT NOT NULL, created_at_utc TEXT NOT NULL, last_used_at_utc TEXT NOT NULL,
                    invalidated_at_utc TEXT NULL, PRIMARY KEY(conversation_id, agent_id));
                INSERT INTO conversation_sessions VALUES ('legacy', 'codex', 'native', 'idle',
                    '2026-01-01T00:00:00.0000000+00:00', '2026-01-01T00:00:00.0000000+00:00', NULL);
                """;
            await command.ExecuteNonQueryAsync();
        }

        var repository = new SqliteConversationSessionRepository(connectionString);
        await repository.InitializeAsync(CancellationToken.None);

        (await repository.GetAsync("legacy", "codex", CancellationToken.None))!.WorkspaceId.Should().Be("default");
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
