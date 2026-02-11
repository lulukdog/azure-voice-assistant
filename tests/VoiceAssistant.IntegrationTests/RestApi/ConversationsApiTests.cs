using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Moq;
using VoiceAssistant.Core.Exceptions;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;
using VoiceAssistant.IntegrationTests.Fixtures;

namespace VoiceAssistant.IntegrationTests.RestApi;

public class ConversationsApiTests : IClassFixture<VoiceAssistantWebApplicationFactory>
{
    private readonly VoiceAssistantWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ConversationsApiTests(VoiceAssistantWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
        _client = factory.CreateClient();
    }

    #region CreateSession

    [Fact]
    public async Task CreateSession_Returns200_WithSessionId()
    {
        // Act
        var response = await _client.PostAsync("/api/conversations", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await DeserializeResponse(response);
        json.GetProperty("sessionId").GetString().Should().NotBeNullOrEmpty();
    }

    #endregion

    #region GetSession

    [Fact]
    public async Task GetSession_Returns200_WithSessionDetails()
    {
        // Arrange — create a session first
        var createResponse = await _client.PostAsync("/api/conversations", null);
        var createJson = await DeserializeResponse(createResponse);
        var sessionId = createJson.GetProperty("sessionId").GetString()!;

        // Act
        var response = await _client.GetAsync($"/api/conversations/{sessionId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await DeserializeResponse(response);
        json.GetProperty("sessionId").GetString().Should().Be(sessionId);
        json.GetProperty("messageCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task GetSession_Returns404_WhenSessionDoesNotExist()
    {
        // Act
        var response = await _client.GetAsync("/api/conversations/non-existent-session");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var json = await DeserializeResponse(response);
        json.GetProperty("code").GetString().Should().Be("SESSION_NOT_FOUND");
    }

    #endregion

    #region DeleteSession

    [Fact]
    public async Task DeleteSession_Returns204_WhenSessionExists()
    {
        // Arrange
        var createResponse = await _client.PostAsync("/api/conversations", null);
        var createJson = await DeserializeResponse(createResponse);
        var sessionId = createJson.GetProperty("sessionId").GetString()!;

        // Act
        var response = await _client.DeleteAsync($"/api/conversations/{sessionId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteSession_Returns404_WhenSessionDoesNotExist()
    {
        // Act
        var response = await _client.DeleteAsync("/api/conversations/non-existent-session");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Speak

    [Fact]
    public async Task Speak_Returns200_WithFullPipelineResult()
    {
        // Arrange
        var createResponse = await _client.PostAsync("/api/conversations", null);
        var createJson = await DeserializeResponse(createResponse);
        var sessionId = createJson.GetProperty("sessionId").GetString()!;

        var content = CreateAudioMultipartContent();

        // Act
        var response = await _client.PostAsync($"/api/conversations/{sessionId}/speak", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await DeserializeResponse(response);
        json.GetProperty("userText").GetString().Should().Be("你好");
        json.GetProperty("assistantText").GetString().Should().Be("你好！有什么可以帮您？");
        json.GetProperty("audioBase64").GetString().Should().NotBeNullOrEmpty();
        json.GetProperty("contentType").GetString().Should().Be("audio/mp3");
    }

    [Fact]
    public async Task Speak_Returns400_WhenNoAudioFile()
    {
        // Arrange
        var createResponse = await _client.PostAsync("/api/conversations", null);
        var createJson = await DeserializeResponse(createResponse);
        var sessionId = createJson.GetProperty("sessionId").GetString()!;

        // Act — send empty multipart form without audio field
        var content = new MultipartFormDataContent();
        var response = await _client.PostAsync($"/api/conversations/{sessionId}/speak", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Speak_Returns404_WhenSessionDoesNotExist()
    {
        // Arrange
        var content = CreateAudioMultipartContent();

        // Act
        var response = await _client.PostAsync("/api/conversations/non-existent-session/speak", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var json = await DeserializeResponse(response);
        json.GetProperty("errorCode").GetString().Should().Be("SESSION_NOT_FOUND");
    }

    [Fact]
    public async Task Speak_Returns502_WhenSttFails()
    {
        // Arrange
        var createResponse = await _client.PostAsync("/api/conversations", null);
        var createJson = await DeserializeResponse(createResponse);
        var sessionId = createJson.GetProperty("sessionId").GetString()!;

        _factory.SttMock.Reset();
        _factory.SttMock
            .Setup(s => s.RecognizeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult { IsSuccess = false, ErrorMessage = "无法识别语音" });

        var content = CreateAudioMultipartContent();

        // Act
        var response = await _client.PostAsync($"/api/conversations/{sessionId}/speak", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var json = await DeserializeResponse(response);
        json.GetProperty("errorCode").GetString().Should().Be("STT_FAILED");
    }

    [Fact]
    public async Task Speak_Returns502_WhenLlmFails()
    {
        // Arrange
        var createResponse = await _client.PostAsync("/api/conversations", null);
        var createJson = await DeserializeResponse(createResponse);
        var sessionId = createJson.GetProperty("sessionId").GetString()!;

        _factory.ChatMock.Reset();
        _factory.ChatMock
            .Setup(s => s.ChatAsync(It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM service unavailable"));

        var content = CreateAudioMultipartContent();

        // Act
        var response = await _client.PostAsync($"/api/conversations/{sessionId}/speak", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var json = await DeserializeResponse(response);
        json.GetProperty("errorCode").GetString().Should().Be("LLM_FAILED");
    }

    [Fact]
    public async Task Speak_Returns502_WhenTtsFails()
    {
        // Arrange
        var createResponse = await _client.PostAsync("/api/conversations", null);
        var createJson = await DeserializeResponse(createResponse);
        var sessionId = createJson.GetProperty("sessionId").GetString()!;

        _factory.TtsMock.Reset();
        _factory.TtsMock
            .Setup(s => s.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("TTS service unavailable"));

        var content = CreateAudioMultipartContent();

        // Act
        var response = await _client.PostAsync($"/api/conversations/{sessionId}/speak", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var json = await DeserializeResponse(response);
        json.GetProperty("errorCode").GetString().Should().Be("TTS_FAILED");
    }

    #endregion

    #region Full Lifecycle

    [Fact]
    public async Task FullLifecycle_CreateGetSpeakGetDeleteGet()
    {
        // Step 1: Create session
        var createResponse = await _client.PostAsync("/api/conversations", null);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createJson = await DeserializeResponse(createResponse);
        var sessionId = createJson.GetProperty("sessionId").GetString()!;

        // Step 2: Get session — messageCount = 0
        var getResponse1 = await _client.GetAsync($"/api/conversations/{sessionId}");
        getResponse1.StatusCode.Should().Be(HttpStatusCode.OK);
        var getJson1 = await DeserializeResponse(getResponse1);
        getJson1.GetProperty("messageCount").GetInt32().Should().Be(0);

        // Step 3: Speak
        var speakContent = CreateAudioMultipartContent();
        var speakResponse = await _client.PostAsync($"/api/conversations/{sessionId}/speak", speakContent);
        speakResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 4: Get session — messageCount = 2 (user + assistant)
        var getResponse2 = await _client.GetAsync($"/api/conversations/{sessionId}");
        getResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        var getJson2 = await DeserializeResponse(getResponse2);
        getJson2.GetProperty("messageCount").GetInt32().Should().Be(2);

        // Step 5: Delete session
        var deleteResponse = await _client.DeleteAsync($"/api/conversations/{sessionId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Step 6: Get session — 404
        var getResponse3 = await _client.GetAsync($"/api/conversations/{sessionId}");
        getResponse3.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Helpers

    private static MultipartFormDataContent CreateAudioMultipartContent()
    {
        var audioBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(new MemoryStream(audioBytes));
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(streamContent, "audio", "test.wav");
        return content;
    }

    private static async Task<JsonElement> DeserializeResponse(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
    }

    #endregion
}
