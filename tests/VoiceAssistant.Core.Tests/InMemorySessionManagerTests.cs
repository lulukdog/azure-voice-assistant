using VoiceAssistant.Core.Exceptions;
using VoiceAssistant.Core.Models;
using VoiceAssistant.Core.Services;

namespace VoiceAssistant.Core.Tests;

public class InMemorySessionManagerTests
{
    private readonly InMemorySessionManager _sut = new();

    [Fact]
    public void CreateSession_ReturnsNewSession()
    {
        var session = _sut.CreateSession();

        Assert.NotNull(session);
        Assert.NotEmpty(session.SessionId);
        Assert.Empty(session.Messages);
    }

    [Fact]
    public void CreateSession_WithSystemPrompt_AddsSystemMessage()
    {
        var session = _sut.CreateSession("你是一个助手");

        Assert.Single(session.Messages);
        Assert.Equal("system", session.Messages[0].Role);
        Assert.Equal("你是一个助手", session.Messages[0].Content);
    }

    [Fact]
    public void CreateSession_WithEmptySystemPrompt_NoSystemMessage()
    {
        var session = _sut.CreateSession("");

        Assert.Empty(session.Messages);
    }

    [Fact]
    public void GetSession_ExistingSession_ReturnsSession()
    {
        var created = _sut.CreateSession();

        var retrieved = _sut.GetSession(created.SessionId);

        Assert.NotNull(retrieved);
        Assert.Equal(created.SessionId, retrieved.SessionId);
    }

    [Fact]
    public void GetSession_NonExistingSession_ReturnsNull()
    {
        var result = _sut.GetSession("non-existing-id");

        Assert.Null(result);
    }

    [Fact]
    public void GetSessionOrThrow_NonExistingSession_ThrowsSessionNotFoundException()
    {
        var ex = Assert.Throws<SessionNotFoundException>(() =>
            _sut.GetSessionOrThrow("non-existing-id"));

        Assert.Equal("SESSION_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public void AddMessage_AddsMessageToSession()
    {
        var session = _sut.CreateSession();
        var message = new ConversationMessage { Role = "user", Content = "你好" };

        _sut.AddMessage(session.SessionId, message);

        var retrieved = _sut.GetSession(session.SessionId)!;
        Assert.Single(retrieved.Messages);
        Assert.Equal("你好", retrieved.Messages[0].Content);
    }

    [Fact]
    public void AddMessage_UpdatesLastActiveAt()
    {
        var session = _sut.CreateSession();
        var originalTime = session.LastActiveAt;

        // 确保时间有差异
        Thread.Sleep(10);

        _sut.AddMessage(session.SessionId, new ConversationMessage { Role = "user", Content = "test" });

        var retrieved = _sut.GetSession(session.SessionId)!;
        Assert.True(retrieved.LastActiveAt >= originalTime);
    }

    [Fact]
    public void AddMessage_NonExistingSession_Throws()
    {
        Assert.Throws<SessionNotFoundException>(() =>
            _sut.AddMessage("non-existing", new ConversationMessage { Role = "user", Content = "test" }));
    }

    [Fact]
    public void RemoveSession_ExistingSession_ReturnsTrue()
    {
        var session = _sut.CreateSession();

        var result = _sut.RemoveSession(session.SessionId);

        Assert.True(result);
        Assert.Null(_sut.GetSession(session.SessionId));
    }

    [Fact]
    public void RemoveSession_NonExistingSession_ReturnsFalse()
    {
        var result = _sut.RemoveSession("non-existing");

        Assert.False(result);
    }

    [Fact]
    public void GetActiveSessionIds_ReturnsAllSessionIds()
    {
        var s1 = _sut.CreateSession();
        var s2 = _sut.CreateSession();

        var ids = _sut.GetActiveSessionIds();

        Assert.Contains(s1.SessionId, ids);
        Assert.Contains(s2.SessionId, ids);
    }

    [Fact]
    public async Task ConcurrentAccess_IsThreadSafe()
    {
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            var session = _sut.CreateSession();
            _sut.AddMessage(session.SessionId, new ConversationMessage { Role = "user", Content = "test" });
            _sut.GetSession(session.SessionId);
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(100, _sut.GetActiveSessionIds().Count);
    }
}
