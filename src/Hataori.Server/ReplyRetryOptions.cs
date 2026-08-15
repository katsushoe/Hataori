namespace Hataori.Server;

public sealed class ReplyRetryOptions
{
    public const string SectionName = "replyRetry";

    public bool Enabled { get; init; } = true;
    public int MaxAttempts { get; init; } = 5;
    public int InitialDelaySeconds { get; init; } = 5;
    public int MaximumDelaySeconds { get; init; } = 300;
    public int BatchSize { get; init; } = 20;
    public int PollIntervalMilliseconds { get; init; } = 1000;
}
