namespace VoiceAssistant.Core.Models;

public class SpeechRecognitionResult
{
    /// <summary>
    /// 是否识别成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 识别出的文本
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 识别置信度 (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// 识别失败原因（如有）
    /// </summary>
    public string? ErrorMessage { get; set; }
}
