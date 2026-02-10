using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VoiceAssistant.Core.Exceptions;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;
using VoiceAssistant.Core.Options;

namespace VoiceAssistant.Core.Pipeline;

/// <summary>
/// 对话处理管道：STT → LLM → TTS
/// </summary>
public class ConversationPipeline(
    ISpeechToTextService sttService,
    IChatService chatService,
    ITextToSpeechService ttsService,
    ISessionManager sessionManager,
    IOptions<AzureOpenAIOptions> openAIOptions,
    ILogger<ConversationPipeline> logger) : IConversationPipeline
{
    private readonly AzureOpenAIOptions _openAIOptions = openAIOptions.Value;

    public async Task<ConversationTurnResult> ProcessAsync(
        string sessionId,
        Stream audioInput,
        CancellationToken cancellationToken = default)
    {
        var session = sessionManager.GetSessionOrThrow(sessionId);

        // Step 1: STT - 语音转文字
        logger.LogInformation("Pipeline STT started for session {SessionId}", sessionId);
        SpeechRecognitionResult sttResult;
        try
        {
            sttResult = await sttService.RecognizeAsync(audioInput, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not VoiceAssistantException)
        {
            throw new SpeechRecognitionException("语音识别服务调用失败", ex);
        }

        if (!sttResult.IsSuccess)
        {
            throw new SpeechRecognitionException(sttResult.ErrorMessage ?? "语音识别失败，未返回有效文本");
        }

        logger.LogInformation("Pipeline STT completed for session {SessionId}: {Text}", sessionId, sttResult.Text);

        // 将用户消息加入会话历史
        sessionManager.AddMessage(sessionId, new ConversationMessage
        {
            Role = "user",
            Content = sttResult.Text
        });

        // Step 2: LLM - AI 对话
        logger.LogInformation("Pipeline LLM started for session {SessionId}", sessionId);
        string assistantText;
        try
        {
            assistantText = await chatService.ChatAsync(session.Messages, cancellationToken);
        }
        catch (Exception ex) when (ex is not VoiceAssistantException)
        {
            throw new ChatServiceException("AI 对话服务调用失败", ex);
        }

        if (string.IsNullOrWhiteSpace(assistantText))
        {
            throw new ChatServiceException("AI 返回了空回复");
        }

        logger.LogInformation("Pipeline LLM completed for session {SessionId}: {Text}", sessionId, assistantText);

        // 将 AI 回复加入会话历史
        sessionManager.AddMessage(sessionId, new ConversationMessage
        {
            Role = "assistant",
            Content = assistantText
        });

        // Step 3: TTS - 文字转语音
        logger.LogInformation("Pipeline TTS started for session {SessionId}", sessionId);
        AudioData audioData;
        try
        {
            audioData = await ttsService.SynthesizeAsync(assistantText, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not VoiceAssistantException)
        {
            throw new SpeechSynthesisException("语音合成服务调用失败", ex);
        }

        logger.LogInformation("Pipeline TTS completed for session {SessionId}", sessionId);

        return new ConversationTurnResult
        {
            UserText = sttResult.Text,
            AssistantText = assistantText,
            Audio = audioData
        };
    }
}
