using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Core.Interfaces;

/// <summary>
/// AI 对话服务接口
/// </summary>
public interface IChatService
{
    /// <summary>
    /// 发送消息并获取 AI 回复
    /// </summary>
    Task<string> ChatAsync(
        IReadOnlyList<ConversationMessage> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送消息并获取流式 AI 回复
    /// </summary>
    IAsyncEnumerable<string> ChatStreamAsync(
        IReadOnlyList<ConversationMessage> messages,
        CancellationToken cancellationToken = default);
}
