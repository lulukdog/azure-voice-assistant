using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;
using VoiceAssistant.Core.Options;

namespace VoiceAssistant.Infrastructure.Azure;

/// <summary>
/// Azure Speech Service STT 实现
/// </summary>
public class AzureSpeechToTextService(
    IOptions<AzureSpeechOptions> options,
    ILogger<AzureSpeechToTextService> logger) : ISpeechToTextService
{
    private readonly AzureSpeechOptions _options = options.Value;

    public async Task<SpeechRecognitionResult> RecognizeAsync(
        Stream audioStream,
        string language = "zh-CN",
        CancellationToken cancellationToken = default)
    {
        // TODO: 实现 Azure Speech SDK 调用
        // var speechConfig = SpeechConfig.FromSubscription(_options.SubscriptionKey, _options.Region);
        // speechConfig.SpeechRecognitionLanguage = language;
        logger.LogInformation("STT: Recognizing speech with language {Language}", language);

        await Task.CompletedTask;
        throw new NotImplementedException("Azure Speech STT 尚未实现，请集成 Microsoft.CognitiveServices.Speech SDK");
    }

    public async Task<SpeechRecognitionResult> RecognizeAsync(
        byte[] audioData,
        string language = "zh-CN",
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(audioData);
        return await RecognizeAsync(stream, language, cancellationToken);
    }
}
