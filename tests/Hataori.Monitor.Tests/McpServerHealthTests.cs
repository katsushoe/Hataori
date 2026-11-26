using System.Net;
using Hataori.Monitor;

namespace Hataori.Monitor.Tests;

public sealed class McpServerHealthTests
{
    [Fact]
    public void Health_uri_uses_configured_loopback_host_and_port()
    {
        var uri = McpServerHealth.ResolveHealthUri("""{ "server": { "mcpHost": "127.0.0.1", "mcpPort": 45999, "mcpPath": "/mcp" } }""");
        Assert.Equal(new Uri("http://127.0.0.1:45999/health"), uri);
        Assert.Equal(new Uri("http://localhost:45441/health"), McpServerHealth.ResolveHealthUri("""{ "server": { "mcpHost": "localhost", "mcpPort": 45441 } }"""));
        Assert.Equal(new Uri("http://[::1]:45442/health"), McpServerHealth.ResolveHealthUri("""{ "server": { "mcpHost": "::1", "mcpPort": 45442 } }"""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("""{ "server": {} }""")]
    public void Health_uri_falls_back_to_default_when_setting_is_missing(string? json)
    {
        var uri = McpServerHealth.ResolveHealthUri(json);
        Assert.Equal(McpServerHealth.DefaultHealthUri, uri);
        Assert.Equal(new Uri("http://127.0.0.1:45440/health"), uri);
    }

    [Theory]
    [InlineData("""{ "server": { "mcpHost": "192.168.1.10", "mcpPort": 45440 } }""")]
    [InlineData("""{ "server": { "mcpHost": "0.0.0.0", "mcpPort": 45440 } }""")]
    [InlineData("""{ "server": { "mcpHost": "example.com", "mcpPort": 45440 } }""")]
    public void Health_uri_ignores_non_loopback_hosts(string json) =>
        Assert.Equal(McpServerHealth.DefaultHealthUri, McpServerHealth.ResolveHealthUri(json));

    [Theory]
    [InlineData("{ \"server\": ")]
    [InlineData("not json")]
    [InlineData("""{ "server": { "mcpHost": "127.0.0.1", "mcpPort": 70000 } }""")]
    public void Health_uri_falls_back_to_default_for_broken_or_invalid_json(string json) =>
        Assert.Equal(McpServerHealth.DefaultHealthUri, McpServerHealth.ResolveHealthUri(json));

    [Fact]
    public void Health_uri_from_missing_file_uses_default() =>
        Assert.Equal(McpServerHealth.DefaultHealthUri, McpServerHealth.ResolveHealthUriFromFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "hataori.json")));

    [Fact]
    public void Ok_response_is_running_with_version()
    {
        var status = McpServerHealth.Classify(HttpStatusCode.OK, """{"status":"ok","name":"Hataori","version":"3.2.5.0"}""");
        Assert.Equal(McpServerState.Running, status.State);
        Assert.Equal("3.2.5.0", status.Version);
        Assert.Equal(McpServerState.Running, McpServerHealth.Classify(HttpStatusCode.Accepted, """{"status":"ok"}""").State);
        Assert.Null(McpServerHealth.Classify(HttpStatusCode.OK, """{"status":"ok"}""").Version);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"status":"degraded"}""")]
    [InlineData(HttpStatusCode.OK, """{"version":"1.0"}""")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, "")]
    [InlineData(HttpStatusCode.InternalServerError, """{"status":"ok"}""")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "")]
    [InlineData(HttpStatusCode.NotFound, "")]
    public void Non_ok_or_http_error_response_is_error(HttpStatusCode code, string body) =>
        Assert.Equal(McpServerState.Error, McpServerHealth.Classify(code, body).State);
}

public sealed class McpStatusLedTests
{
    [Fact]
    public void Lamp_colors_follow_the_shared_specification()
    {
        Assert.Equal(Color.FromArgb(40, 190, 70), McpStatusLed.ColorFor(McpServerState.Running));
        Assert.Equal(Color.FromArgb(240, 170, 30), McpStatusLed.ColorFor(McpServerState.NotResponding));
        Assert.Equal(Color.FromArgb(215, 50, 45), McpStatusLed.ColorFor(McpServerState.Error));
        Assert.Equal(Color.FromArgb(150, 150, 150), McpStatusLed.ColorFor(McpServerState.Stopped));
        Assert.Equal(Color.FromArgb(150, 150, 150), McpStatusLed.ColorFor(McpServerState.Unknown));
    }

    [Theory]
    [InlineData(McpServerState.Running, "running")]
    [InlineData(McpServerState.NotResponding, "not-responding")]
    [InlineData(McpServerState.Error, "error")]
    [InlineData(McpServerState.Stopped, "stopped")]
    public void Lamp_is_a_transparent_16px_pill_and_exports_png(McpServerState state, string name)
    {
        using var led = new McpStatusLed();
        var image = led.ImageFor(state);
        Assert.Equal(new Size(16, 16), image.Size);
        // 角と上下の余白は透明（ピル型で正方形全体を塗らない）。
        Assert.Equal(0, image.GetPixel(0, 0).A);
        Assert.Equal(0, image.GetPixel(15, 15).A);
        Assert.Equal(0, image.GetPixel(8, 2).A);
        Assert.Equal(0, image.GetPixel(8, 13).A);
        // 本体中央と左右端付近は塗られている（横長）。
        Assert.Equal(255, image.GetPixel(8, 8).A);
        Assert.True(image.GetPixel(2, 8).A > 0);
        Assert.True(image.GetPixel(13, 8).A > 0);
        // 縦グラデーション：上側（光沢より下）が下側より明るい。
        var upper = image.GetPixel(8, 7).GetBrightness();
        var lower = image.GetPixel(8, 10).GetBrightness();
        Assert.True(upper > lower, $"upper {upper} should be brighter than lower {lower}");
        Assert.Same(image, led.ImageFor(state));

        var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/validation"));
        Directory.CreateDirectory(directory);
        using var enlarged = new Bitmap(128, 128);
        using (var graphics = Graphics.FromImage(enlarged))
        {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            graphics.DrawImage(image, new Rectangle(0, 0, 128, 128));
        }
        image.Save(Path.Combine(directory, $"mcp-led-{name}.png"));
        enlarged.Save(Path.Combine(directory, $"mcp-led-{name}-x8.png"));
    }
}
