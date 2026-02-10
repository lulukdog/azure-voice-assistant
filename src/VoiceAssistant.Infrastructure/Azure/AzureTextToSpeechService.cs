using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        // TODO: 实现 Azure Speech SDK TTS 调用
        // var speechConfig = SpeechConfig.FromSubscription(_options.SubscriptionKey, _options.Region);
        // speechConfig.SpeechSynthesisVoiceName = voiceName ?? _options.SynthesisVoiceName;
        logger.LogInformation("TTS: Synthesizing text with voice {VoiceName}", voiceName ?? _options.SynthesisVoiceName);

        await Task.CompletedTask;
        throw new NotImplementedException("Azure Speech TTS 尚未实现，请集成 Microsoft.CognitiveServices.Speech SDK");
    }

    public async Task SynthesizeToStreamAsync(
        string text,
        Stream outputStream,
        string? voiceName = null,
        CancellationToken cancellationToken = default)
    {
        var audioData = await SynthesizeAsync(text, voiceName, cancellationToken);
        await outputStream.WriteAsync(audioData.Data, cancellationToken);
    }
}
