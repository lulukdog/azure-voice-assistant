using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Core.Interfaces;

/// <summary>
/// 对话处理管道：STT → LLM → TTS
/// </summary>
public interface IConversationPipeline
{
    /// <summary>
    /// 处理一轮完整对话：语音输入 → 文字 → AI 回复 → 语音输出
    /// </summary>
    Task<ConversationTurnResult> ProcessAsync(
        string sessionId,
        Stream audioInput,
        CancellationToken cancellationToken = default);
}
