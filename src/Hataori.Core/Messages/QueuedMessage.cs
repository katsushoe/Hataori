namespace Hataori.Core.Messages;

public sealed record QueuedMessage(
    long QueueId,
    long Sequence,
    int Priority,
    IncomingMessage Message,
    DateTimeOffset EnqueuedAtUtc);
