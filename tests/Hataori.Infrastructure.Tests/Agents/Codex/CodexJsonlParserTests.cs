using FluentAssertions;
using Hataori.Infrastructure.Agents.Codex;

namespace Hataori.Infrastructure.Tests.Agents.Codex;

public sealed class CodexJsonlParserTests
{
    [Fact]
    public void Parse_CompletedTurn_ReturnsThreadAndLastAgentMessage()
    {
        const string jsonl = """
            {"type":"thread.started","thread_id":"thread-1"}
            {"type":"item.completed","item":{"type":"agent_message","text":"first"}}
            {"type":"item.completed","item":{"type":"agent_message","text":"final"}}
            {"type":"turn.completed","usage":{"input_tokens":10}}
            """;

        var result = CodexJsonlParser.Parse(jsonl);

        result.NativeSessionId.Should().Be("thread-1");
        result.FinalMessage.Should().Be("final");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Parse_FailedTurn_ReturnsNestedError()
    {
        const string jsonl = """
            {"type":"thread.started","thread_id":"thread-1"}
            {"type":"turn.failed","error":{"message":"failure"}}
            """;

        CodexJsonlParser.Parse(jsonl).Error.Should().Be("failure");
    }

    [Fact]
    public void Parse_InvalidLine_ThrowsWithLineNumber()
    {
        var action = () => CodexJsonlParser.Parse("{not-json}");

        action.Should().Throw<FormatException>().WithMessage("*line 1*");
    }
}
