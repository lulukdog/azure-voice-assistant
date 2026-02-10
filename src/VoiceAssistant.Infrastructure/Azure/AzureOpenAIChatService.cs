using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;
using VoiceAssistant.Core.Options;

namespace VoiceAssistant.Infrastructure.Azure;

/// <summary>
/// Azure OpenAI Service 对话实现
/// </summary>
public class AzureOpenAIChatService(
    IOptions<AzureOpenAIOptions> options,
    ILogger<AzureOpenAIChatService> logger) : IChatService
{
    private readonly AzureOpenAIOptions _options = options.Value;

    public async Task<string> ChatAsync(
        IReadOnlyList<ConversationMessage> messages,
        CancellationToken cancellationToken = default)
    {
        // TODO: 实现 Azure OpenAI SDK 调用
        // var client = new AzureOpenAIClient(new Uri(_options.Endpoint), new ApiKeyCredential(_options.ApiKey));
        // var chatClient = client.GetChatClient(_options.DeploymentName);
        logger.LogInformation("LLM: Processing chat with {MessageCount} messages", messages.Count);

        await Task.CompletedTask;
        throw new NotImplementedException("Azure OpenAI Chat 尚未实现，请集成 Azure.AI.OpenAI SDK");
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        IReadOnlyList<ConversationMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // TODO: 实现流式调用
        logger.LogInformation("LLM: Processing streaming chat with {MessageCount} messages", messages.Count);

        await Task.CompletedTask;
        throw new NotImplementedException("Azure OpenAI Chat Stream 尚未实现，请集成 Azure.AI.OpenAI SDK");

        // 以下代码不会执行，仅为满足 IAsyncEnumerable 返回类型
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}
