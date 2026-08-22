namespace Hataori.Server;

public static class ActivationLanePlan
{
    private const string CodexDesktopProvider = "codex";

    public static IReadOnlyList<string> Create(IReadOnlyDictionary<string, int> maximumConcurrentRuns)
    {
        ArgumentNullException.ThrowIfNull(maximumConcurrentRuns);
        return maximumConcurrentRuns
            .Where(pair => !pair.Key.Equals(CodexDesktopProvider, StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .SelectMany(pair => Enumerable.Repeat(pair.Key, pair.Value))
            .ToArray();
    }
}
