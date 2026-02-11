using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using VoiceAssistant.Api.Controllers;
using VoiceAssistant.Core.Exceptions;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Api.Tests.Controllers;

public class ConversationsControllerTests
{
    private readonly Mock<IConversationPipeline> _pipelineMock;
    private readonly Mock<ISessionManager> _sessionManagerMock;
    private readonly Mock<ILogger<ConversationsController>> _loggerMock;
    private readonly ConversationsController _controller;

    public ConversationsControllerTests()
    {
        _pipelineMock = new Mock<IConversationPipeline>();
        _sessionManagerMock = new Mock<ISessionManager>();
        _loggerMock = new Mock<ILogger<ConversationsController>>();
        _controller = new ConversationsController(
            _pipelineMock.Object,
            _sessionManagerMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void CreateSession_ReturnsOk_WithSessionId()
    {
        // Arrange
        var session = new ConversationSession
        {
            SessionId = "test-session-123",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _sessionManagerMock
            .Setup(m => m.CreateSession(null))
            .Returns(session);

        // Act
        var result = _controller.CreateSession();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var value = okResult.Value!;
        var sessionIdProp = value.GetType().GetProperty("SessionId");
        var createdAtProp = value.GetType().GetProperty("CreatedAt");

        sessionIdProp.Should().NotBeNull();
        createdAtProp.Should().NotBeNull();
        sessionIdProp!.GetValue(value).Should().Be("test-session-123");
        createdAtProp!.GetValue(value).Should().Be(session.CreatedAt);

        _sessionManagerMock.Verify(m => m.CreateSession(null), Times.Once);
    }

    [Fact]
    public void GetSession_ReturnsOk_WhenSessionExists()
    {
        // Arrange
        var sessionId = "existing-session";
        var session = new ConversationSession
        {
            SessionId = sessionId,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow,
            Messages = []
        };
        _sessionManagerMock
            .Setup(m => m.GetSession(sessionId))
            .Returns(session);

        // Act
        var result = _controller.GetSession(sessionId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var value = okResult.Value!;
        var sessionIdProp = value.GetType().GetProperty("SessionId");
        var messageCountProp = value.GetType().GetProperty("MessageCount");

        sessionIdProp.Should().NotBeNull();
        messageCountProp.Should().NotBeNull();
        sessionIdProp!.GetValue(value).Should().Be(sessionId);
        messageCountProp!.GetValue(value).Should().Be(0);

        _sessionManagerMock.Verify(m => m.GetSession(sessionId), Times.Once);
    }

    [Fact]
    public void GetSession_ReturnsNotFound_WhenSessionDoesNotExist()
    {
        // Arrange
        var sessionId = "non-existent-session";
        _sessionManagerMock
            .Setup(m => m.GetSession(sessionId))
            .Returns((ConversationSession?)null);

        // Act
        var result = _controller.GetSession(sessionId);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);

        _sessionManagerMock.Verify(m => m.GetSession(sessionId), Times.Once);
    }

    [Fact]
    public void DeleteSession_ReturnsNoContent_WhenSessionExists()
    {
        // Arrange
        var sessionId = "session-to-delete";
        _sessionManagerMock
            .Setup(m => m.RemoveSession(sessionId))
            .Returns(true);

        // Act
        var result = _controller.DeleteSession(sessionId);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        _sessionManagerMock.Verify(m => m.RemoveSession(sessionId), Times.Once);
    }

    [Fact]
    public void DeleteSession_ReturnsNotFound_WhenSessionDoesNotExist()
    {
        // Arrange
        var sessionId = "non-existent-session";
        _sessionManagerMock
            .Setup(m => m.RemoveSession(sessionId))
            .Returns(false);

        // Act
        var result = _controller.DeleteSession(sessionId);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);

        _sessionManagerMock.Verify(m => m.RemoveSession(sessionId), Times.Once);
    }

    [Fact]
    public async Task Speak_ReturnsBadRequest_WhenNoAudioFile()
    {
        // Arrange
        var sessionId = "test-session";

        // Act
        var result = await _controller.Speak(sessionId, null!, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }
}
