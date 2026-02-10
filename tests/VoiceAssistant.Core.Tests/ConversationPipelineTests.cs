using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VoiceAssistant.Core.Exceptions;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;
using VoiceAssistant.Core.Options;
using VoiceAssistant.Core.Pipeline;
using VoiceAssistant.Core.Services;

namespace VoiceAssistant.Core.Tests;

public class ConversationPipelineTests
{
    private readonly Mock<ISpeechToTextService> _sttMock = new();
    private readonly Mock<IChatService> _chatMock = new();
    private readonly Mock<ITextToSpeechService> _ttsMock = new();
    private readonly InMemorySessionManager _sessionManager = new();
    private readonly ConversationPipeline _sut;

    public ConversationPipelineTests()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AzureOpenAIOptions
        {
            Endpoint = "https://test.openai.azure.com/",
            ApiKey = "test-key",
            DeploymentName = "gpt-4o",
            SystemPrompt = "你是测试助手"
        });

        _sut = new ConversationPipeline(
            _sttMock.Object,
            _chatMock.Object,
            _ttsMock.Object,
            _sessionManager,
            options,
            NullLogger<ConversationPipeline>.Instance);
    }

    [Fact]
    public async Task ProcessAsync_FullPipeline_ReturnsResult()
    {
        // Arrange
        var session = _sessionManager.CreateSession("你是测试助手");
        using var audioStream = new MemoryStream([1, 2, 3]);

        _sttMock.Setup(s => s.RecognizeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult { IsSuccess = true, Text = "你好", Confidence = 0.95 });

        _chatMock.Setup(s => s.ChatAsync(It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("你好！有什么可以帮你的？");

        _ttsMock.Setup(s => s.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioData { Data = [10, 20, 30], ContentType = "audio/mp3" });

        // Act
        var result = await _sut.ProcessAsync(session.SessionId, audioStream);

        // Assert
        Assert.Equal("你好", result.UserText);
        Assert.Equal("你好！有什么可以帮你的？", result.AssistantText);
        Assert.Equal("audio/mp3", result.Audio.ContentType);

        // 验证会话历史
        var updatedSession = _sessionManager.GetSession(session.SessionId)!;
        // system + user + assistant = 3
        Assert.Equal(3, updatedSession.Messages.Count);
        Assert.Equal("user", updatedSession.Messages[1].Role);
        Assert.Equal("assistant", updatedSession.Messages[2].Role);
    }

    [Fact]
    public async Task ProcessAsync_SttFails_ThrowsSpeechRecognitionException()
    {
        var session = _sessionManager.CreateSession();
        using var audioStream = new MemoryStream([1, 2, 3]);

        _sttMock.Setup(s => s.RecognizeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult { IsSuccess = false, ErrorMessage = "无法识别语音" });

        var ex = await Assert.ThrowsAsync<SpeechRecognitionException>(
            () => _sut.ProcessAsync(session.SessionId, audioStream));

        Assert.Equal("STT_FAILED", ex.ErrorCode);
    }

    [Fact]
    public async Task ProcessAsync_SttThrows_WrapsAsSpeechRecognitionException()
    {
        var session = _sessionManager.CreateSession();
        using var audioStream = new MemoryStream([1, 2, 3]);

        _sttMock.Setup(s => s.RecognizeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("网络错误"));

        var ex = await Assert.ThrowsAsync<SpeechRecognitionException>(
            () => _sut.ProcessAsync(session.SessionId, audioStream));

        Assert.Equal("STT_FAILED", ex.ErrorCode);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task ProcessAsync_LlmReturnsEmpty_ThrowsChatServiceException()
    {
        var session = _sessionManager.CreateSession();
        using var audioStream = new MemoryStream([1, 2, 3]);

        _sttMock.Setup(s => s.RecognizeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult { IsSuccess = true, Text = "你好" });

        _chatMock.Setup(s => s.ChatAsync(It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("");

        var ex = await Assert.ThrowsAsync<ChatServiceException>(
            () => _sut.ProcessAsync(session.SessionId, audioStream));

        Assert.Equal("LLM_FAILED", ex.ErrorCode);
    }

    [Fact]
    public async Task ProcessAsync_LlmThrows_WrapsAsChatServiceException()
    {
        var session = _sessionManager.CreateSession();
        using var audioStream = new MemoryStream([1, 2, 3]);

        _sttMock.Setup(s => s.RecognizeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult { IsSuccess = true, Text = "你好" });

        _chatMock.Setup(s => s.ChatAsync(It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API 超时"));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(
            () => _sut.ProcessAsync(session.SessionId, audioStream));

        Assert.Equal("LLM_FAILED", ex.ErrorCode);
    }

    [Fact]
    public async Task ProcessAsync_TtsThrows_WrapsAsSpeechSynthesisException()
    {
        var session = _sessionManager.CreateSession();
        using var audioStream = new MemoryStream([1, 2, 3]);

        _sttMock.Setup(s => s.RecognizeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult { IsSuccess = true, Text = "你好" });

        _chatMock.Setup(s => s.ChatAsync(It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("回复");

        _ttsMock.Setup(s => s.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("TTS 配额用尽"));

        var ex = await Assert.ThrowsAsync<SpeechSynthesisException>(
            () => _sut.ProcessAsync(session.SessionId, audioStream));

        Assert.Equal("TTS_FAILED", ex.ErrorCode);
    }

    [Fact]
    public async Task ProcessAsync_InvalidSession_ThrowsSessionNotFoundException()
    {
        using var audioStream = new MemoryStream([1, 2, 3]);

        var ex = await Assert.ThrowsAsync<SessionNotFoundException>(
            () => _sut.ProcessAsync("non-existing-session", audioStream));

        Assert.Equal("SESSION_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public async Task ProcessAsync_PassesMessagesToChat()
    {
        var session = _sessionManager.CreateSession("系统提示");
        using var audioStream = new MemoryStream([1, 2, 3]);

        _sttMock.Setup(s => s.RecognizeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult { IsSuccess = true, Text = "问题" });

        IReadOnlyList<ConversationMessage>? capturedMessages = null;
        _chatMock.Setup(s => s.ChatAsync(It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<ConversationMessage>, CancellationToken>((msgs, _) => capturedMessages = msgs.ToList())
            .ReturnsAsync("回答");

        _ttsMock.Setup(s => s.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioData { Data = [1], ContentType = "audio/mp3" });

        await _sut.ProcessAsync(session.SessionId, audioStream);

        // Chat 应该收到 system + user = 2 条消息
        Assert.NotNull(capturedMessages);
        Assert.Equal(2, capturedMessages.Count);
        Assert.Equal("system", capturedMessages[0].Role);
        Assert.Equal("系统提示", capturedMessages[0].Content);
        Assert.Equal("user", capturedMessages[1].Role);
        Assert.Equal("问题", capturedMessages[1].Content);
    }

    [Fact]
    public async Task ProcessAsync_MultiTurn_AccumulatesHistory()
    {
        var session = _sessionManager.CreateSession();

        _sttMock.Setup(s => s.RecognizeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult { IsSuccess = true, Text = "问题", Confidence = 0.9 });

        _chatMock.Setup(s => s.ChatAsync(It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("回答");

        _ttsMock.Setup(s => s.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioData { Data = [1], ContentType = "audio/mp3" });

        // 第一轮
        using (var s1 = new MemoryStream([1]))
            await _sut.ProcessAsync(session.SessionId, s1);

        // 第二轮
        using (var s2 = new MemoryStream([2]))
            await _sut.ProcessAsync(session.SessionId, s2);

        var updated = _sessionManager.GetSession(session.SessionId)!;
        // user + assistant + user + assistant = 4
        Assert.Equal(4, updated.Messages.Count);
    }
}
