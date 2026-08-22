using System.Text.Json;
using FluentAssertions;
using Hataori.Application.Itoguruma;
using Hataori.Infrastructure.Itoguruma;

namespace Hataori.Infrastructure.Tests.Itoguruma;

public sealed class ItogurumaStructuredContentDeserializerTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void Deserialize_DataWrapper_ReturnsInnerObject()
    {
        using var document = JsonDocument.Parse("""{"data":{"name":"itoguruma","version":"0.3.4"}}""");

        var result = ItogurumaStructuredContentDeserializer.Deserialize<VersionData>(document.RootElement, Options);

        result.Should().Be(new VersionData("itoguruma", "0.3.4"));
    }

    [Fact]
    public void Deserialize_DataWrapper_ReturnsInnerArray()
    {
        using var document = JsonDocument.Parse("""{"data":[{"messageId":"message-1"}]}""");

        var result = ItogurumaStructuredContentDeserializer.Deserialize<List<MessageData>>(document.RootElement, Options);

        result.Should().ContainSingle().Which.MessageId.Should().Be("message-1");
    }

    [Fact]
    public void Deserialize_DataWrapper_MapsItogurumaCamelCaseMessage()
    {
        using var document = JsonDocument.Parse("""
            {"data":[{"messageId":"message-1","threadId":"thread-1","senderAgentId":"hataori","replyToMessageId":null,"messageType":"message","body":"work","payloadJson":null,"provider":"codex","createdAt":"2026-08-16T12:19:05+00:00","deliveryStatus":"leased","leaseUntil":"2026-08-16T12:20:05+00:00"}]}
            """);

        var result = ItogurumaStructuredContentDeserializer.Deserialize<List<ItogurumaMessage>>(document.RootElement, Options);

        result.Should().ContainSingle().Which.Should().Be(new ItogurumaMessage(
            "message-1", "thread-1", "hataori", null, "message", "work", null, "codex",
            DateTimeOffset.Parse("2026-08-16T12:19:05+00:00"), "leased",
            DateTimeOffset.Parse("2026-08-16T12:20:05+00:00")));
    }

    [Fact]
    public void Deserialize_DirectLegacyObject_RemainsSupported()
    {
        using var document = JsonDocument.Parse("""{"name":"itoguruma","version":"0.3.2"}""");

        var result = ItogurumaStructuredContentDeserializer.Deserialize<VersionData>(document.RootElement, Options);

        result.Should().Be(new VersionData("itoguruma", "0.3.2"));
    }

    private sealed record VersionData(string Name, string Version);
    private sealed record MessageData(string MessageId);
}
