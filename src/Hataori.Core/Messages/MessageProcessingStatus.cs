namespace Hataori.Core.Messages;

public enum MessageProcessingStatus
{
    Received,
    Queued,
    Starting,
    Running,
    Responded,
    Failed,
    Cancelled,
}
