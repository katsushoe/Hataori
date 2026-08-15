namespace Hataori.Server;

public static class ActivationLanePlan
{
    public static IReadOnlyList<string> Create(IReadOnlyDictionary<string, int> maximumConcurrentRuns)
    {
        ArgumentNullException.ThrowIfNull(maximumConcurrentRuns);
        return maximumConcurrentRuns
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .SelectMany(pair => Enumerable.Repeat(pair.Key, pair.Value))
            .ToArray();
    }
}
