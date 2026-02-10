using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Core.Interfaces;

/// <summary>
/// 文字转语音服务接口
/// </summary>
public interface ITextToSpeechService
{
    /// <summary>
    /// 将文本合成为语音音频
    /// </summary>
    Task<AudioData> SynthesizeAsync(
        string text,
        string? voiceName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将文本合成为语音并写入流（流式）
    /// </summary>
    Task SynthesizeToStreamAsync(
        string text,
        Stream outputStream,
        string? voiceName = null,
        CancellationToken cancellationToken = default);
}
