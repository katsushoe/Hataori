namespace Hataori.Infrastructure.Agents.ClaudeCode;

public static class ClaudeCodeCommandBuilder
{
    public static IReadOnlyList<string> BuildStart(ClaudeCodeDriverOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var arguments = BaseArguments(options);
        AddModel(arguments, options.Model);
        return arguments;
    }

    public static IReadOnlyList<string> BuildResume(ClaudeCodeDriverOptions options, string nativeSessionId)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeSessionId);
        var arguments = BaseArguments(options);
        arguments.Add("--resume");
        arguments.Add(nativeSessionId);
        AddModel(arguments, options.Model);
        return arguments;
    }

    private static List<string> BaseArguments(ClaudeCodeDriverOptions options) =>
        ["-p", "--output-format", "json", "--permission-mode", options.PermissionMode];

    private static void AddModel(List<string> arguments, string? model)
    {
        if (!string.IsNullOrWhiteSpace(model))
        {
            arguments.Add("--model");
            arguments.Add(model);
        }
    }
}
