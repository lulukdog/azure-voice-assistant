using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Moq;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;
using VoiceAssistant.IntegrationTests.Fixtures;

namespace VoiceAssistant.IntegrationTests.Middleware;

public class ExceptionMiddlewareIntegrationTests : IClassFixture<VoiceAssistantWebApplicationFactory>
{
    private readonly VoiceAssistantWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ExceptionMiddlewareIntegrationTests(VoiceAssistantWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SttFailure_Returns502_WithSttFailedCode()
    {
        // Arrange
        var sessionId = await CreateSession();

        _factory.SttMock.Reset();
        _factory.SttMock
            .Setup(s => s.RecognizeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult { IsSuccess = false, ErrorMessage = "识别失败" });

        // Act
        var response = await PostSpeak(sessionId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var json = await DeserializeResponse(response);
        json.GetProperty("errorCode").GetString().Should().Be("STT_FAILED");
    }

    [Fact]
    public async Task LlmEmptyResponse_Returns502_WithLlmFailedCode()
    {
        // Arrange
        var sessionId = await CreateSession();

        _factory.ChatMock.Reset();
        _factory.ChatMock
            .Setup(s => s.ChatAsync(It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        // Act
        var response = await PostSpeak(sessionId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var json = await DeserializeResponse(response);
        json.GetProperty("errorCode").GetString().Should().Be("LLM_FAILED");
    }

    [Fact]
    public async Task TtsFailure_Returns502_WithTtsFailedCode()
    {
        // Arrange
        var sessionId = await CreateSession();

        _factory.TtsMock.Reset();
        _factory.TtsMock
            .Setup(s => s.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("TTS service error"));

        // Act
        var response = await PostSpeak(sessionId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var json = await DeserializeResponse(response);
        json.GetProperty("errorCode").GetString().Should().Be("TTS_FAILED");
    }

    [Fact]
    public async Task ErrorResponses_HaveJsonContentType()
    {
        // Arrange
        var sessionId = await CreateSession();

        _factory.SttMock.Reset();
        _factory.SttMock
            .Setup(s => s.RecognizeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult { IsSuccess = false, ErrorMessage = "测试" });

        // Act
        var response = await PostSpeak(sessionId);

        // Assert
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    #region Helpers

    private async Task<string> CreateSession()
    {
        var response = await _client.PostAsync("/api/conversations", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await DeserializeResponse(response);
        return json.GetProperty("sessionId").GetString()!;
    }

    private async Task<HttpResponseMessage> PostSpeak(string sessionId)
    {
        var audioBytes = new byte[] { 0x01, 0x02, 0x03 };
        var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(new MemoryStream(audioBytes));
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(streamContent, "audio", "test.wav");
        return await _client.PostAsync($"/api/conversations/{sessionId}/speak", content);
    }

    private static async Task<JsonElement> DeserializeResponse(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
    }

    #endregion
}
