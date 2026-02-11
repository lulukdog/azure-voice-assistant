using System.Text.Json;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VoiceAssistant.Core.Exceptions;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Options;
using SpeechRecognitionResult = VoiceAssistant.Core.Models.SpeechRecognitionResult;

namespace VoiceAssistant.Infrastructure.Azure;

/// <summary>
/// Azure Speech Service STT 实现
/// </summary>
public class AzureSpeechToTextService(
    IOptions<AzureSpeechOptions> options,
    ILogger<AzureSpeechToTextService> logger) : ISpeechToTextService
{
    private readonly AzureSpeechOptions _options = options.Value;

    /// <summary>
    /// 最大音频时长（秒）
    /// </summary>
    private const double MaxAudioDurationSeconds = 60;

    /// <summary>
    /// 最大音频字节数: 60s * 16000Hz * 2 bytes (16-bit mono)
    /// </summary>
    private const int MaxAudioBytes = (int)(MaxAudioDurationSeconds * 16000 * 2);

    public async Task<SpeechRecognitionResult> RecognizeAsync(
        Stream audioStream,
        string language = "zh-CN",
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("STT: Recognizing speech with language {Language}", language);

        try
        {
            var speechConfig = SpeechConfig.FromSubscription(_options.SubscriptionKey, _options.Region);
            speechConfig.SpeechRecognitionLanguage = language;

            // 使用 PushAudioInputStream 将音频流推送给 SDK
            using var pushStream = AudioInputStream.CreatePushStream(
                AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));

            // 将音频数据写入 push stream，同时跟踪总字节数
            var buffer = new byte[4096];
            int bytesRead;
            long totalBytesRead = 0;
            while ((bytesRead = await audioStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                totalBytesRead += bytesRead;
                if (totalBytesRead > MaxAudioBytes)
                {
                    var durationSeconds = (double)totalBytesRead / (16000 * 2);
                    throw new AudioTooLongException(durationSeconds, MaxAudioDurationSeconds);
                }

                pushStream.Write(buffer, bytesRead);
            }
            pushStream.Close();

            using var audioConfig = AudioConfig.FromStreamInput(pushStream);
            using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

            var result = await recognizer.RecognizeOnceAsync();

            return result.Reason switch
            {
                ResultReason.RecognizedSpeech => new SpeechRecognitionResult
                {
                    IsSuccess = true,
                    Text = result.Text,
                    Confidence = ExtractConfidence(result)
                },
                ResultReason.NoMatch => new SpeechRecognitionResult
                {
                    IsSuccess = false,
                    ErrorMessage = "未能识别语音内容，请确保音频清晰并重试"
                },
                ResultReason.Canceled => HandleCancellation(result),
                _ => new SpeechRecognitionResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"未知识别结果: {result.Reason}"
                }
            };
        }
        catch (AudioTooLongException)
        {
            throw;
        }
        catch (SpeechRecognitionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "STT: Unexpected error during speech recognition");
            throw new SpeechRecognitionException("语音识别过程中发生意外错误", ex);
        }
    }

    public async Task<SpeechRecognitionResult> RecognizeAsync(
        byte[] audioData,
        string language = "zh-CN",
        CancellationToken cancellationToken = default)
    {
        // 在创建流之前先验证音频长度
        if (audioData.Length > MaxAudioBytes)
        {
            var durationSeconds = (double)audioData.Length / (16000 * 2);
            throw new AudioTooLongException(durationSeconds, MaxAudioDurationSeconds);
        }

        using var stream = new MemoryStream(audioData);
        return await RecognizeAsync(stream, language, cancellationToken);
    }

    /// <summary>
    /// 尝试从 Azure SDK JSON 结果中提取置信度
    /// </summary>
    private double ExtractConfidence(Microsoft.CognitiveServices.Speech.SpeechRecognitionResult result)
    {
        try
        {
            var json = result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult);
            if (string.IsNullOrEmpty(json))
            {
                return 1.0;
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("NBest", out var nBest) &&
                nBest.GetArrayLength() > 0)
            {
                var firstResult = nBest[0];
                if (firstResult.TryGetProperty("Confidence", out var confidence))
                {
                    return confidence.GetDouble();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "STT: Failed to extract confidence from JSON result, falling back to 1.0");
        }

        // Azure SDK 单次识别不直接返回置信度，解析失败时回退到 1.0
        return 1.0;
    }

    private SpeechRecognitionResult HandleCancellation(
        Microsoft.CognitiveServices.Speech.SpeechRecognitionResult result)
    {
        var cancellation = CancellationDetails.FromResult(result);
        var errorMessage = cancellation.Reason == CancellationReason.Error
            ? $"语音识别错误: {cancellation.ErrorCode} - {cancellation.ErrorDetails}"
            : $"语音识别被取消: {cancellation.Reason}";

        logger.LogError("STT cancellation: {Reason}, ErrorCode: {ErrorCode}, Details: {Details}",
            cancellation.Reason, cancellation.ErrorCode, cancellation.ErrorDetails);

        return new SpeechRecognitionResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
