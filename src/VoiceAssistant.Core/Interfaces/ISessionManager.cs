using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Core.Interfaces;

/// <summary>
/// 会话管理服务接口
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// 创建新会话
    /// </summary>
    ConversationSession CreateSession(string? systemPrompt = null);

    /// <summary>
    /// 获取会话，不存在返回 null
    /// </summary>
    ConversationSession? GetSession(string sessionId);

    /// <summary>
    /// 获取会话，不存在则抛出异常
    /// </summary>
    ConversationSession GetSessionOrThrow(string sessionId);

    /// <summary>
    /// 添加消息到会话
    /// </summary>
    void AddMessage(string sessionId, ConversationMessage message);

    /// <summary>
    /// 删除会话
    /// </summary>
    bool RemoveSession(string sessionId);

    /// <summary>
    /// 获取所有活跃会话 ID
    /// </summary>
    IReadOnlyList<string> GetActiveSessionIds();
}
