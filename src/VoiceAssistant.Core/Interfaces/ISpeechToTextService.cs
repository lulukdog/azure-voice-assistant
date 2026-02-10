using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Core.Interfaces;

/// <summary>
/// 语音转文字服务接口
/// </summary>
public interface ISpeechToTextService
{
    /// <summary>
    /// 从音频流识别语音并返回文字
    /// </summary>
    Task<SpeechRecognitionResult> RecognizeAsync(
        Stream audioStream,
        string language = "zh-CN",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 从音频字节数组识别语音
    /// </summary>
    Task<SpeechRecognitionResult> RecognizeAsync(
        byte[] audioData,
        string language = "zh-CN",
        CancellationToken cancellationToken = default);
}
