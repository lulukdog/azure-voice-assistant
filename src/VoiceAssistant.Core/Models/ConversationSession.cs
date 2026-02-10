namespace VoiceAssistant.Core.Models;

public class ConversationSession
{
    public required string SessionId { get; set; }
    public List<ConversationMessage> Messages { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActiveAt { get; set; } = DateTimeOffset.UtcNow;
}
