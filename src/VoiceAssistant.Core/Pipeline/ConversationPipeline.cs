using Microsoft.Extensions.Logging;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Core.Pipeline;

/// <summary>
/// 对话处理管道：STT → LLM → TTS
/// </summary>
public class ConversationPipeline(
    ISpeechToTextService sttService,
    IChatService chatService,
    ITextToSpeechService ttsService,
    ILogger<ConversationPipeline> logger) : IConversationPipeline
{
    // 简单的内存会话存储，后续可替换为 Redis
    private static readonly Dictionary<string, ConversationSession> Sessions = new();

    public async Task<ConversationTurnResult> ProcessAsync(
        string sessionId,
        Stream audioInput,
        CancellationToken cancellationToken = default)
    {
        // 获取或创建会话
        if (!Sessions.TryGetValue(sessionId, out var session))
        {
            session = new ConversationSession { SessionId = sessionId };
            Sessions[sessionId] = session;
        }

        // Step 1: STT - 语音转文字
        logger.LogInformation("Processing STT for session {SessionId}", sessionId);
        var sttResult = await sttService.RecognizeAsync(audioInput, cancellationToken: cancellationToken);

        if (!sttResult.IsSuccess)
        {
            throw new InvalidOperationException($"语音识别失败: {sttResult.ErrorMessage}");
        }

        logger.LogInformation("STT result for session {SessionId}: {Text}", sessionId, sttResult.Text);

        // 将用户消息加入会话历史
        session.Messages.Add(new ConversationMessage { Role = "user", Content = sttResult.Text });
        session.LastActiveAt = DateTimeOffset.UtcNow;

        // Step 2: LLM - AI 对话
        logger.LogInformation("Processing LLM for session {SessionId}", sessionId);
        var assistantText = await chatService.ChatAsync(session.Messages, cancellationToken);

        logger.LogInformation("LLM result for session {SessionId}: {Text}", sessionId, assistantText);

        // 将 AI 回复加入会话历史
        session.Messages.Add(new ConversationMessage { Role = "assistant", Content = assistantText });

        // Step 3: TTS - 文字转语音
        logger.LogInformation("Processing TTS for session {SessionId}", sessionId);
        var audioData = await ttsService.SynthesizeAsync(assistantText, cancellationToken: cancellationToken);

        logger.LogInformation("TTS completed for session {SessionId}", sessionId);

        return new ConversationTurnResult
        {
            UserText = sttResult.Text,
            AssistantText = assistantText,
            Audio = audioData
        };
    }
}
