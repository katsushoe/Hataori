using System.Text.Json;
using Hataori.Server;

namespace Hataori.Server.Tests;

public sealed class HealthResponseTests
{
    [Fact]
    public void Health_response_reports_ok_with_server_version_in_camel_case_json()
    {
        var response = HealthResponse.Create();
        Assert.Equal("ok", response.Status);
        Assert.Equal("Hataori", response.Name);
        Assert.Equal(typeof(HealthResponse).Assembly.GetName().Version!.ToString(), response.Version);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("ok", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(response.Version, document.RootElement.GetProperty("version").GetString());
    }
}
