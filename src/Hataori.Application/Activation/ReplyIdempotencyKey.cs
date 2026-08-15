namespace Hataori.Application.Activation;

public static class ReplyIdempotencyKey
{
    public static string Create(string messageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        return $"hataori-reply:{messageId}";
    }
}
