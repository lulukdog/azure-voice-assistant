namespace VoiceAssistant.Core.Options;

public class AzureSpeechOptions
{
    public const string SectionName = "AzureSpeech";

    public required string SubscriptionKey { get; set; }
    public required string Region { get; set; }
    public string RecognitionLanguage { get; set; } = "zh-CN";
    public string SynthesisVoiceName { get; set; } = "zh-CN-XiaoxiaoNeural";
    public string SynthesisOutputFormat { get; set; } = "Audio16Khz32KBitRateMonoMp3";
}
