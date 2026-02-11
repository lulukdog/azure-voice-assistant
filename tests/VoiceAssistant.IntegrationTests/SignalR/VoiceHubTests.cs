using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Moq;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;
using VoiceAssistant.IntegrationTests.Fixtures;

namespace VoiceAssistant.IntegrationTests.SignalR;

public class VoiceHubTests : IClassFixture<VoiceAssistantWebApplicationFactory>, IAsyncLifetime
{
    private readonly VoiceAssistantWebApplicationFactory _factory;
    private HubConnection _hubConnection = null!;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public VoiceHubTests(VoiceAssistantWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
    }

    public async Task InitializeAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/voice", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        await _hubConnection.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_hubConnection.State != HubConnectionState.Disconnected)
        {
            await _hubConnection.DisposeAsync();
        }
    }

    [Fact]
    public async Task StartSession_ReturnsSessionStarted_WithSessionId()
    {
        // Arrange
        var tcs = new TaskCompletionSource<JsonElement>();
        _hubConnection.On<JsonElement>("SessionStarted", msg => tcs.SetResult(msg));

        // Act
        await _hubConnection.InvokeAsync("StartSession", "zh-CN");

        // Assert — SignalR uses camelCase JSON serialization by default
        var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.GetProperty("sessionId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SendAudio_FullPipeline_ReceivesAllMessages()
    {
        // Arrange
        var sessionStartedTcs = new TaskCompletionSource<JsonElement>();
        var recognitionTcs = new TaskCompletionSource<JsonElement>();
        var textChunkTcs = new TaskCompletionSource<JsonElement>();
        var audioChunkTcs = new TaskCompletionSource<JsonElement>();

        _hubConnection.On<JsonElement>("SessionStarted", msg => sessionStartedTcs.TrySetResult(msg));
        _hubConnection.On<JsonElement>("RecognitionResult", msg => recognitionTcs.TrySetResult(msg));
        _hubConnection.On<JsonElement>("AssistantTextChunk", msg => textChunkTcs.TrySetResult(msg));
        _hubConnection.On<JsonElement>("AudioChunk", msg => audioChunkTcs.TrySetResult(msg));

        await _hubConnection.InvokeAsync("StartSession", "zh-CN");
        var sessionMsg = await sessionStartedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var sessionId = sessionMsg.GetProperty("sessionId").GetString()!;

        var audioBase64 = Convert.ToBase64String(new byte[] { 0x01, 0x02, 0x03 });

        // Act
        await _hubConnection.InvokeAsync("SendAudio", sessionId, audioBase64);

        // Assert — SignalR uses camelCase JSON serialization by default
        var recognition = await recognitionTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        recognition.GetProperty("text").GetString().Should().Be("你好");
        recognition.GetProperty("isFinal").GetBoolean().Should().BeTrue();

        var textChunk = await textChunkTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        textChunk.GetProperty("textChunk").GetString().Should().Be("你好！有什么可以帮您？");
        textChunk.GetProperty("isComplete").GetBoolean().Should().BeTrue();

        var audioChunk = await audioChunkTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        audioChunk.GetProperty("audioChunk").GetString().Should().NotBeNullOrEmpty();
        audioChunk.GetProperty("contentType").GetString().Should().Be("audio/mp3");
    }

    [Fact]
    public async Task SendAudio_WhenSttFails_ReceivesError()
    {
        // Arrange
        _factory.SttMock.Reset();
        _factory.SttMock
            .Setup(s => s.RecognizeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult { IsSuccess = false, ErrorMessage = "无法识别" });

        var sessionStartedTcs = new TaskCompletionSource<JsonElement>();
        var errorTcs = new TaskCompletionSource<JsonElement>();

        _hubConnection.On<JsonElement>("SessionStarted", msg => sessionStartedTcs.TrySetResult(msg));
        _hubConnection.On<JsonElement>("Error", msg => errorTcs.TrySetResult(msg));

        await _hubConnection.InvokeAsync("StartSession", "zh-CN");
        var sessionMsg = await sessionStartedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var sessionId = sessionMsg.GetProperty("sessionId").GetString()!;

        var audioBase64 = Convert.ToBase64String(new byte[] { 0x01, 0x02 });

        // Act
        await _hubConnection.InvokeAsync("SendAudio", sessionId, audioBase64);

        // Assert
        var error = await errorTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        error.GetProperty("code").GetString().Should().Be("STT_FAILED");
    }

    [Fact]
    public async Task EndSession_ReturnsSessionEnded_WithMatchingSessionId()
    {
        // Arrange
        var sessionStartedTcs = new TaskCompletionSource<JsonElement>();
        var sessionEndedTcs = new TaskCompletionSource<JsonElement>();

        _hubConnection.On<JsonElement>("SessionStarted", msg => sessionStartedTcs.TrySetResult(msg));
        _hubConnection.On<JsonElement>("SessionEnded", msg => sessionEndedTcs.TrySetResult(msg));

        await _hubConnection.InvokeAsync("StartSession", "zh-CN");
        var sessionMsg = await sessionStartedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var sessionId = sessionMsg.GetProperty("sessionId").GetString()!;

        // Act
        await _hubConnection.InvokeAsync("EndSession", sessionId);

        // Assert
        var ended = await sessionEndedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        ended.GetProperty("sessionId").GetString().Should().Be(sessionId);
    }

    [Fact]
    public async Task Disconnect_RemovesConnectionMapping()
    {
        // Arrange
        var sessionStartedTcs = new TaskCompletionSource<JsonElement>();
        _hubConnection.On<JsonElement>("SessionStarted", msg => sessionStartedTcs.TrySetResult(msg));

        await _hubConnection.InvokeAsync("StartSession", "zh-CN");
        var sessionMsg = await sessionStartedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var sessionId = sessionMsg.GetProperty("sessionId").GetString()!;

        // Act — disconnect
        await _hubConnection.DisposeAsync();

        // Assert — session still exists in session manager (OnDisconnectedAsync only removes
        // connection mapping, not the session itself)
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/conversations/{sessionId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CrossProtocol_StartSessionViaSignalR_GetSessionViaRest()
    {
        // Arrange
        var sessionStartedTcs = new TaskCompletionSource<JsonElement>();
        _hubConnection.On<JsonElement>("SessionStarted", msg => sessionStartedTcs.TrySetResult(msg));

        // Act — create session via SignalR
        await _hubConnection.InvokeAsync("StartSession", "zh-CN");
        var sessionMsg = await sessionStartedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var sessionId = sessionMsg.GetProperty("sessionId").GetString()!;

        // Assert — retrieve via REST
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/conversations/{sessionId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        json.GetProperty("sessionId").GetString().Should().Be(sessionId);
    }
}
