using System.Text.Json;
using FluentAssertions;

namespace Hataori.Cli.Tests;

public sealed class CliConfigurationManagerTests : IDisposable
{
    private readonly string _configPath = Path.Combine(Path.GetTempPath(), $"hataori-config-{Guid.NewGuid():N}.json");

    [Fact]
    public async Task ExecuteAsync_CheckValidConfiguration_ReturnsValid()
    {
        await File.WriteAllTextAsync(_configPath, ValidConfiguration);

        var result = await new CliConfigurationManager().ExecuteAsync("check", _configPath, CancellationToken.None);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result));
        document.RootElement.GetProperty("valid").GetBoolean().Should().BeTrue(document.RootElement.GetProperty("errors").ToString());
        document.RootElement.GetProperty("errors").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShowConfiguration_MasksAuthenticationToken()
    {
        await File.WriteAllTextAsync(_configPath, ValidConfiguration);

        var result = await new CliConfigurationManager().ExecuteAsync("show", _configPath, CancellationToken.None);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var values = document.RootElement.GetProperty("values");
        GetPropertyIgnoreCase(values, "itoguruma:authenticationToken").GetString().Should().Be("(redacted)");
        GetPropertyIgnoreCase(values, "server:mcpPort").GetString().Should().Be("45440");
    }

    [Fact]
    public async Task ExecuteAsync_PathForMissingConfiguration_ReturnsExistsFalse()
    {
        var result = await new CliConfigurationManager().ExecuteAsync("path", _configPath, CancellationToken.None);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result));
        document.RootElement.GetProperty("exists").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("path").GetString().Should().Be(Path.GetFullPath(_configPath));
    }

    public void Dispose()
    {
        if (File.Exists(_configPath))
        {
            File.Delete(_configPath);
        }
    }

    private static JsonElement GetPropertyIgnoreCase(JsonElement element, string name) =>
        element.EnumerateObject()
            .Single(property => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Value;

    private const string ValidConfiguration = """
        {
          "server": {
            "databasePath": "data/hataori.db",
            "controlPipeName": "hataori-control",
            "mcpHost": "127.0.0.1",
            "mcpPort": 45440,
            "mcpPath": "/mcp"
          },
          "itoguruma": {
            "endpoint": "http://127.0.0.1:47631/mcp",
            "authenticationToken": "sensitive-token",
            "agentId": "hataori",
            "agentType": "hataori",
            "monitoredAgentIds": [ "codex" ],
            "connectionTimeoutSeconds": 10,
            "pollIntervalSeconds": 5,
            "maxReconnectAttempts": 5,
            "receiveBatchSize": 50,
            "leaseSeconds": 300
          },
          "agents": {
            "codex": { "executablePath": "codex", "sandboxMode": "workspace-write", "maxCapturedCharacters": 4194304 },
            "claudeCode": { "executablePath": "claude", "permissionMode": "acceptEdits", "maxCapturedCharacters": 4194304 }
          },
          "activation": {
            "enabled": false,
            "pollIntervalMilliseconds": 1000,
            "maxConcurrentRuns": { "codex": 2 }
          },
          "replyRetry": {
            "maxAttempts": 5,
            "initialDelaySeconds": 5,
            "maximumDelaySeconds": 300,
            "batchSize": 20,
            "pollIntervalMilliseconds": 1000
          },
          "fileLogging": {
            "enabled": true,
            "directoryPath": "logs",
            "minimumLevel": "Information",
            "retentionDays": 30
          }
        }
        """;
}
