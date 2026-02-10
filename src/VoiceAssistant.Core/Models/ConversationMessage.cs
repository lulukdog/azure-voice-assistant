namespace VoiceAssistant.Core.Models;

public class ConversationMessage
{
    /// <summary>
    /// 角色: "system", "user", "assistant"
    /// </summary>
    public required string Role { get; set; }

    /// <summary>
    /// 消息内容
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// 消息时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
