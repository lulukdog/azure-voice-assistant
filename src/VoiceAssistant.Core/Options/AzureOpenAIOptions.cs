namespace VoiceAssistant.Core.Options;

public class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    public required string Endpoint { get; set; }
    public required string ApiKey { get; set; }
    public required string DeploymentName { get; set; }
    public int MaxTokens { get; set; } = 800;
    public double Temperature { get; set; } = 0.7;
    public string SystemPrompt { get; set; } = "你是一个友好的 AI 语音助手，请用简洁的中文回答问题。";
}
