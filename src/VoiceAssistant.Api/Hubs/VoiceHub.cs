using Microsoft.AspNetCore.SignalR;
using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Api.Hubs;

/// <summary>
/// 语音对话 WebSocket Hub
/// </summary>
public class VoiceHub(
    IConversationPipeline pipeline,
    ILogger<VoiceHub> logger) : Hub
{
    /// <summary>
    /// 客户端请求开始新会话
    /// </summary>
    public async Task StartSession(string language = "zh-CN")
    {
        var sessionId = Guid.NewGuid().ToString("N");
        logger.LogInformation("New session started: {SessionId}, language: {Language}", sessionId, language);

        await Clients.Caller.SendAsync("SessionStarted", new { SessionId = sessionId });
    }

    /// <summary>
    /// 接收完整音频并处理对话
    /// </summary>
    public async Task SendAudio(string sessionId, string audioChunkBase64)
    {
        try
        {
            var audioBytes = Convert.FromBase64String(audioChunkBase64);
            using var audioStream = new MemoryStream(audioBytes);

            // 通知客户端开始处理
            await Clients.Caller.SendAsync("RecognitionResult", new
            {
                SessionId = sessionId,
                Text = "",
                Confidence = 0.0,
                IsFinal = false
            });

            var result = await pipeline.ProcessAsync(sessionId, audioStream);

            // 发送识别结果
            await Clients.Caller.SendAsync("RecognitionResult", new
            {
                SessionId = sessionId,
                Text = result.UserText,
                Confidence = 1.0,
                IsFinal = true
            });

            // 发送 AI 文本回复
            await Clients.Caller.SendAsync("AssistantTextChunk", new
            {
                SessionId = sessionId,
                TextChunk = result.AssistantText,
                IsComplete = true
            });

            // 发送音频数据
            await Clients.Caller.SendAsync("AudioChunk", new
            {
                SessionId = sessionId,
                AudioChunk = Convert.ToBase64String(result.Audio.Data),
                ContentType = result.Audio.ContentType,
                IsComplete = true
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing audio for session {SessionId}", sessionId);
            await Clients.Caller.SendAsync("Error", new
            {
                SessionId = sessionId,
                Code = "INTERNAL_ERROR",
                Message = ex.Message
            });
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
