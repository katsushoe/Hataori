namespace Hataori.Infrastructure.Agents.Codex;

public static class CodexCommandBuilder
{
    public static IReadOnlyList<string> BuildStart(CodexDriverOptions options, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        var arguments = new List<string> { "exec", "--json", "--color", "never" };
        if (options.ApproveForMe)
        {
            arguments.Add("--approve-for-me");
        }
        else
        {
            arguments.Add("--sandbox");
            arguments.Add(options.SandboxMode);
        }

        AddModel(arguments, options.Model);
        arguments.Add("--cd");
        arguments.Add(workingDirectory);
        arguments.Add("-");
        return arguments;
    }

    public static IReadOnlyList<string> BuildResume(CodexDriverOptions options, string nativeSessionId)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeSessionId);
        var arguments = new List<string> { "exec", "resume", "--json" };
        AddModel(arguments, options.Model);
        arguments.Add(nativeSessionId);
        arguments.Add("-");
        return arguments;
    }

    private static void AddModel(List<string> arguments, string? model)
    {
        if (!string.IsNullOrWhiteSpace(model))
        {
            arguments.Add("--model");
            arguments.Add(model);
        }
    }
}
