using FluentAssertions;
using Hataori.Infrastructure.Agents.ClaudeCode;

namespace Hataori.Infrastructure.Tests.Agents.ClaudeCode;

public sealed class ClaudeCodeJsonParserTests
{
    [Fact]
    public void Parse_Success_ReturnsSessionAndFinalMessage()
    {
        const string json = """
            {"type":"result","subtype":"success","is_error":false,"result":"done","session_id":"session-1"}
            """;

        var result = ClaudeCodeJsonParser.Parse(json);

        result.NativeSessionId.Should().Be("session-1");
        result.FinalMessage.Should().Be("done");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Parse_ErrorResult_ReturnsError()
    {
        const string json = """
            {"type":"result","subtype":"error_max_turns","is_error":true,"result":"limit reached","session_id":"session-1"}
            """;

        ClaudeCodeJsonParser.Parse(json).Error.Should().Be("limit reached");
    }

    [Fact]
    public void Parse_InvalidJson_Throws()
    {
        var action = () => ClaudeCodeJsonParser.Parse("not-json");

        action.Should().Throw<FormatException>();
    }
}
