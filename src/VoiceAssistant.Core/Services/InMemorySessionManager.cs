using System.Collections.Concurrent;
using VoiceAssistant.Core.Exceptions;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Core.Services;

/// <summary>
/// 基于内存的会话管理实现（线程安全）
/// 后续可替换为 Redis 实现
/// </summary>
public class InMemorySessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, ConversationSession> _sessions = new();

    public ConversationSession CreateSession(string? systemPrompt = null)
    {
        var session = new ConversationSession
        {
            SessionId = Guid.NewGuid().ToString("N")
        };

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            session.Messages.Add(new ConversationMessage
            {
                Role = "system",
                Content = systemPrompt
            });
        }

        _sessions[session.SessionId] = session;
        return session;
    }

    public ConversationSession? GetSession(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    public ConversationSession GetSessionOrThrow(string sessionId)
    {
        return GetSession(sessionId)
            ?? throw new SessionNotFoundException(sessionId);
    }

    public void AddMessage(string sessionId, ConversationMessage message)
    {
        var session = GetSessionOrThrow(sessionId);
        session.Messages.Add(message);
        session.LastActiveAt = DateTimeOffset.UtcNow;
    }

    public bool RemoveSession(string sessionId)
    {
        return _sessions.TryRemove(sessionId, out _);
    }

    public IReadOnlyList<string> GetActiveSessionIds()
    {
        return _sessions.Keys.ToList().AsReadOnly();
    }
}
