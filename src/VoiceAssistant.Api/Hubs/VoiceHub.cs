using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using VoiceAssistant.Core.Exceptions;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Api.Hubs;

/// <summary>
/// 语音对话 WebSocket Hub
/// </summary>
public class VoiceHub(
    IConversationPipeline pipeline,
    ISessionManager sessionManager,
    ILogger<VoiceHub> logger) : Hub
{
    /// <summary>
    /// ConnectionId → SessionId 的映射
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> ConnectionSessionMap = new();

    /// <summary>
    /// 客户端请求开始新会话
    /// </summary>
    public async Task StartSession(string language = "zh-CN")
    {
        var session = sessionManager.CreateSession();
        ConnectionSessionMap[Context.ConnectionId] = session.SessionId;

        logger.LogInformation("New session started: {SessionId}, language: {Language}", session.SessionId, language);

        await Clients.Caller.SendAsync("SessionStarted", new { SessionId = session.SessionId });
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

            var result = await pipeline.ProcessAsync(sessionId, audioStream, Context.ConnectionAborted);

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
        catch (VoiceAssistantException ex)
        {
            logger.LogWarning(ex, "Voice assistant error for session {SessionId}: {ErrorCode}", sessionId, ex.ErrorCode);
            await Clients.Caller.SendAsync("Error", new
            {
                SessionId = sessionId,
                Code = ex.ErrorCode,
                Message = ex.Message
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

    /// <summary>
    /// 客户端请求结束会话
    /// </summary>
    public async Task EndSession(string sessionId)
    {
        sessionManager.RemoveSession(sessionId);
        ConnectionSessionMap.TryRemove(Context.ConnectionId, out _);

        logger.LogInformation("Session ended: {SessionId}", sessionId);

        await Clients.Caller.SendAsync("SessionEnded", new { SessionId = sessionId });
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionSessionMap.TryRemove(Context.ConnectionId, out var sessionId))
        {
            logger.LogInformation("Client disconnected: {ConnectionId}, removed mapping for session: {SessionId}", Context.ConnectionId, sessionId);
        }
        else
        {
            logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }

        return base.OnDisconnectedAsync(exception);
    }
}
