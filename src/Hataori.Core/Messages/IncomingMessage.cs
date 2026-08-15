namespace Hataori.Core.Messages;

public sealed record IncomingMessage(
    string MessageId,
    string ConversationId,
    string AgentId,
    string SenderAgentId,
    string? ReplyToMessageId,
    string MessageType,
    string Body,
    string? PayloadJson,
    DateTimeOffset ReceivedAtUtc);
