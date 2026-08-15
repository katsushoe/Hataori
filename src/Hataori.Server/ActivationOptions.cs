namespace Hataori.Server;

public sealed class ActivationOptions
{
    public const string SectionName = "activation";

    public bool Enabled { get; init; }
    public string WorkingDirectory { get; init; } = string.Empty;
    public int PollIntervalMilliseconds { get; init; } = 1000;
}
