namespace Hataori.Core.Messages;

public sealed record PendingReply(
    string MessageId,
    string ConversationId,
    string RecipientAgentId,
    string FinalMessage,
    int AttemptCount,
    DateTimeOffset NextAttemptAtUtc);
