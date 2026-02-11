using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using VoiceAssistant.Api.Middleware;
using VoiceAssistant.Core.Exceptions;

namespace VoiceAssistant.Api.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _loggerMock;

    public ExceptionHandlingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<(int StatusCode, JsonDocument Body)> InvokeMiddlewareWithException(
        ExceptionHandlingMiddleware middleware,
        DefaultHttpContext context)
    {
        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        var json = JsonDocument.Parse(body);

        return (context.Response.StatusCode, json);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsCorrectStatusCode_ForSessionNotFoundException()
    {
        // Arrange
        var context = CreateHttpContext();
        RequestDelegate next = _ => throw new SessionNotFoundException("test-session");
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        var (statusCode, json) = await InvokeMiddlewareWithException(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status404NotFound);
        json.RootElement.GetProperty("code").GetString().Should().Be("SESSION_NOT_FOUND");
    }

    [Fact]
    public async Task InvokeAsync_ReturnsCorrectStatusCode_ForAudioTooLongException()
    {
        // Arrange
        var context = CreateHttpContext();
        RequestDelegate next = _ => throw new AudioTooLongException(90.0, 60.0);
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        var (statusCode, json) = await InvokeMiddlewareWithException(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status400BadRequest);
        json.RootElement.GetProperty("code").GetString().Should().Be("AUDIO_TOO_LONG");
    }

    [Fact]
    public async Task InvokeAsync_ReturnsCorrectStatusCode_ForSpeechRecognitionException()
    {
        // Arrange
        var context = CreateHttpContext();
        RequestDelegate next = _ => throw new SpeechRecognitionException("test error");
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        var (statusCode, json) = await InvokeMiddlewareWithException(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status502BadGateway);
        json.RootElement.GetProperty("code").GetString().Should().Be("STT_FAILED");
    }

    [Fact]
    public async Task InvokeAsync_ReturnsCorrectStatusCode_ForChatServiceException()
    {
        // Arrange
        var context = CreateHttpContext();
        RequestDelegate next = _ => throw new ChatServiceException("test error");
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        var (statusCode, json) = await InvokeMiddlewareWithException(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status502BadGateway);
        json.RootElement.GetProperty("code").GetString().Should().Be("LLM_FAILED");
    }

    [Fact]
    public async Task InvokeAsync_Returns500_ForGenericException()
    {
        // Arrange
        var context = CreateHttpContext();
        RequestDelegate next = _ => throw new InvalidOperationException("something went wrong");
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        var (statusCode, json) = await InvokeMiddlewareWithException(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status500InternalServerError);
        json.RootElement.GetProperty("code").GetString().Should().Be("INTERNAL_ERROR");
        json.RootElement.GetProperty("message").GetString().Should().Be("An unexpected error occurred.");
    }

    [Fact]
    public async Task InvokeAsync_ReturnsJsonResponse_WithErrorCodeAndMessage()
    {
        // Arrange
        var context = CreateHttpContext();
        RequestDelegate next = _ => throw new SpeechSynthesisException("test error");
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.ContentType.Should().Be("application/json");
        context.Response.StatusCode.Should().Be(StatusCodes.Status502BadGateway);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        var json = JsonDocument.Parse(body);

        json.RootElement.GetProperty("code").GetString().Should().Be("TTS_FAILED");
        json.RootElement.GetProperty("message").GetString().Should().Contain("test error");
    }

    [Fact]
    public async Task InvokeAsync_PassesThrough_WhenNoException()
    {
        // Arrange
        var context = CreateHttpContext();
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }
}
