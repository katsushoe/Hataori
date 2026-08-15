using Microsoft.Data.Sqlite;

namespace Hataori.Cli;

/// <summary>
/// SQLiteデータベースの読み取り専用診断を実行します。
/// </summary>
public static class CliDatabaseDiagnostics
{
    /// <summary>
    /// DB診断コマンドを実行します。
    /// </summary>
    public static async Task<object> ExecuteAsync(string command, string databasePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException("Hataori database was not found.", databasePath);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            ForeignKeys = true,
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        return command.ToLowerInvariant() switch
        {
            "status" => await GetStatusAsync(connection, databasePath, cancellationToken).ConfigureAwait(false),
            "integrity" => await GetIntegrityAsync(connection, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentException($"Unknown db command '{command}'."),
        };
    }

    private static async Task<object> GetStatusAsync(SqliteConnection connection, string databasePath, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
        var tableCount = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
        return new { path = databasePath, exists = true, table_count = tableCount, size_bytes = new FileInfo(databasePath).Length };
    }

    private static async Task<object> GetIntegrityAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        return new { ok = result.Equals("ok", StringComparison.OrdinalIgnoreCase), result };
    }
}
