namespace VoiceAssistant.Core.Models;

public class AudioData
{
    /// <summary>
    /// 音频二进制数据
    /// </summary>
    public required byte[] Data { get; set; }

    /// <summary>
    /// 音频格式 MIME 类型，如 "audio/mp3", "audio/wav"
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// 音频时长（秒）
    /// </summary>
    public double? DurationSeconds { get; set; }
}
