using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VoiceAssistant.Core.Exceptions;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;
using VoiceAssistant.Core.Options;

namespace VoiceAssistant.Infrastructure.Azure;

/// <summary>
/// Azure Speech Service TTS 实现
/// </summary>
public class AzureTextToSpeechService(
    IOptions<AzureSpeechOptions> options,
    ILogger<AzureTextToSpeechService> logger) : ITextToSpeechService
{
    private readonly AzureSpeechOptions _options = options.Value;

    public async Task<AudioData> SynthesizeAsync(
        string text,
        string? voiceName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(text, nameof(text));

        if (text.Length > 5000)
            throw new ArgumentException(
                $"Text length ({text.Length}) exceeds the maximum allowed length of 5000 characters.", nameof(text));

        var voice = voiceName ?? _options.SynthesisVoiceName;
        logger.LogInformation("TTS: Synthesizing text ({Length} chars) with voice {VoiceName}",
            text.Length, voice);

        try
        {
            var speechConfig = SpeechConfig.FromSubscription(_options.SubscriptionKey, _options.Region);
            speechConfig.SpeechSynthesisVoiceName = voice;
            speechConfig.SetSpeechSynthesisOutputFormat(ParseOutputFormat(_options.SynthesisOutputFormat));

            // 使用 null AudioConfig 将输出到内存（不播放到扬声器）
            using var synthesizer = new SpeechSynthesizer(speechConfig, null);

            var result = await synthesizer.SpeakTextAsync(text);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                logger.LogInformation("TTS: Synthesis completed, audio size: {Size} bytes", result.AudioData.Length);

                return new AudioData
                {
                    Data = result.AudioData,
                    ContentType = GetContentType(_options.SynthesisOutputFormat),
                    DurationSeconds = result.AudioDuration.TotalSeconds
                };
            }

            if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                logger.LogError("TTS cancellation: {Reason}, ErrorCode: {ErrorCode}, Details: {Details}",
                    cancellation.Reason, cancellation.ErrorCode, cancellation.ErrorDetails);

                throw new SpeechSynthesisException(
                    $"语音合成失败: {cancellation.ErrorCode} - {cancellation.ErrorDetails}");
            }

            throw new SpeechSynthesisException($"语音合成返回未知结果: {result.Reason}");
        }
        catch (SpeechSynthesisException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TTS: Unexpected error during synthesis");
            throw new SpeechSynthesisException("语音合成过程中发生意外错误", ex);
        }
    }

    public async Task SynthesizeToStreamAsync(
        string text,
        Stream outputStream,
        string? voiceName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(text, nameof(text));

        if (text.Length > 5000)
            throw new ArgumentException(
                $"Text length ({text.Length}) exceeds the maximum allowed length of 5000 characters.", nameof(text));

        var voice = voiceName ?? _options.SynthesisVoiceName;
        logger.LogInformation("TTS: Streaming synthesis of text ({Length} chars) with voice {VoiceName}",
            text.Length, voice);

        try
        {
            var speechConfig = SpeechConfig.FromSubscription(_options.SubscriptionKey, _options.Region);
            speechConfig.SpeechSynthesisVoiceName = voice;
            speechConfig.SetSpeechSynthesisOutputFormat(ParseOutputFormat(_options.SynthesisOutputFormat));

            // 使用 null AudioConfig 将输出到内存（不播放到扬声器）
            using var synthesizer = new SpeechSynthesizer(speechConfig, null);

            // 订阅 Synthesizing 事件，将音频数据块实时写入输出流
            synthesizer.Synthesizing += (s, e) =>
            {
                if (e.Result.AudioData.Length > 0)
                {
                    outputStream.Write(e.Result.AudioData, 0, e.Result.AudioData.Length);
                }
            };

            var result = await synthesizer.SpeakTextAsync(text);

            if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                logger.LogError("TTS streaming cancellation: {Reason}, ErrorCode: {ErrorCode}, Details: {Details}",
                    cancellation.Reason, cancellation.ErrorCode, cancellation.ErrorDetails);

                throw new SpeechSynthesisException(
                    $"流式语音合成失败: {cancellation.ErrorCode} - {cancellation.ErrorDetails}");
            }

            logger.LogInformation("TTS: Streaming synthesis completed");
        }
        catch (SpeechSynthesisException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TTS: Unexpected error during streaming synthesis");
            throw new SpeechSynthesisException("流式语音合成过程中发生意外错误", ex);
        }
    }

    private SpeechSynthesisOutputFormat ParseOutputFormat(string format)
    {
        return format switch
        {
            "Audio16Khz32KBitRateMonoMp3" => SpeechSynthesisOutputFormat.Audio16Khz32KBitRateMonoMp3,
            "Audio16Khz64KBitRateMonoMp3" => SpeechSynthesisOutputFormat.Audio16Khz64KBitRateMonoMp3,
            "Audio16Khz128KBitRateMonoMp3" => SpeechSynthesisOutputFormat.Audio16Khz128KBitRateMonoMp3,
            "Audio24Khz48KBitRateMonoMp3" => SpeechSynthesisOutputFormat.Audio24Khz48KBitRateMonoMp3,
            "Audio24Khz96KBitRateMonoMp3" => SpeechSynthesisOutputFormat.Audio24Khz96KBitRateMonoMp3,
            "Riff16Khz16BitMonoPcm" => SpeechSynthesisOutputFormat.Riff16Khz16BitMonoPcm,
            "Riff24Khz16BitMonoPcm" => SpeechSynthesisOutputFormat.Riff24Khz16BitMonoPcm,
            "Ogg16Khz16BitMonoOpus" => SpeechSynthesisOutputFormat.Ogg16Khz16BitMonoOpus,
            "Ogg24Khz16BitMonoOpus" => SpeechSynthesisOutputFormat.Ogg24Khz16BitMonoOpus,
            _ => LogAndReturnDefaultFormat(format)
        };
    }

    private SpeechSynthesisOutputFormat LogAndReturnDefaultFormat(string format)
    {
        logger.LogWarning("TTS: Unknown output format '{Format}', falling back to Audio16Khz32KBitRateMonoMp3",
            format);
        return SpeechSynthesisOutputFormat.Audio16Khz32KBitRateMonoMp3;
    }

    private static string GetContentType(string format)
    {
        if (format.Contains("Mp3", StringComparison.OrdinalIgnoreCase))
            return "audio/mpeg";
        if (format.Contains("Pcm", StringComparison.OrdinalIgnoreCase) ||
            format.Contains("Riff", StringComparison.OrdinalIgnoreCase))
            return "audio/wav";
        if (format.Contains("Opus", StringComparison.OrdinalIgnoreCase) ||
            format.Contains("Ogg", StringComparison.OrdinalIgnoreCase))
            return "audio/ogg";
        return "audio/mpeg";
    }
}
