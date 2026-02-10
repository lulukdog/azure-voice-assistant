namespace VoiceAssistant.Core.Exceptions;

/// <summary>
/// 语音助手基础异常
/// </summary>
public class VoiceAssistantException : Exception
{
    /// <summary>
    /// 错误码，对应 API_SPEC 中定义的错误码
    /// </summary>
    public string ErrorCode { get; }

    public VoiceAssistantException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public VoiceAssistantException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// 语音识别失败异常
/// </summary>
public class SpeechRecognitionException(string message, Exception? innerException = null)
    : VoiceAssistantException("STT_FAILED", message, innerException ?? new Exception(message));

/// <summary>
/// LLM 对话服务失败异常
/// </summary>
public class ChatServiceException(string message, Exception? innerException = null)
    : VoiceAssistantException("LLM_FAILED", message, innerException ?? new Exception(message));

/// <summary>
/// 语音合成失败异常
/// </summary>
public class SpeechSynthesisException(string message, Exception? innerException = null)
    : VoiceAssistantException("TTS_FAILED", message, innerException ?? new Exception(message));

/// <summary>
/// 会话不存在异常
/// </summary>
public class SessionNotFoundException(string sessionId)
    : VoiceAssistantException("SESSION_NOT_FOUND", $"会话 {sessionId} 不存在");

/// <summary>
/// 音频超时异常
/// </summary>
public class AudioTooLongException(double durationSeconds, double maxSeconds)
    : VoiceAssistantException("AUDIO_TOO_LONG", $"音频时长 {durationSeconds:F1}s 超过最大限制 {maxSeconds:F0}s");
