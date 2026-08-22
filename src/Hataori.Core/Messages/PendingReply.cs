namespace Hataori.Core.Messages;

public sealed record PendingReply(
    string MessageId,
    string ConversationId,
    string RecipientAgentId,
    string Provider,
    string FinalMessage,
    int AttemptCount,
    DateTimeOffset NextAttemptAtUtc);
