namespace VoiceAssistant.Core.Models;

public class ConversationTurnResult
{
    /// <summary>
    /// 用户语音识别后的文本
    /// </summary>
    public required string UserText { get; set; }

    /// <summary>
    /// AI 回复的文本
    /// </summary>
    public required string AssistantText { get; set; }

    /// <summary>
    /// AI 回复的语音音频
    /// </summary>
    public required AudioData Audio { get; set; }
}
