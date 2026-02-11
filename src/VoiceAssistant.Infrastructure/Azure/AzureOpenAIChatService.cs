using System.ClientModel;
using System.Runtime.CompilerServices;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using VoiceAssistant.Core.Exceptions;
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
        logger.LogInformation("LLM: Processing chat with {MessageCount} messages", messages.Count);

        try
        {
            var chatClient = CreateChatClient();
            var chatMessages = ConvertMessages(messages);

            var completionOptions = new ChatCompletionOptions
            {
                MaxOutputTokenCount = _options.MaxTokens,
                Temperature = (float)_options.Temperature,
            };

            ChatCompletion completion = await chatClient.CompleteChatAsync(
                chatMessages, completionOptions, cancellationToken);

            if (completion.Content is null || completion.Content.Count == 0)
            {
                throw new ChatServiceException("LLM returned empty response content");
            }

            var responseText = completion.Content[0].Text;

            logger.LogInformation("LLM: Chat completed, response length: {Length} chars, tokens: {Usage}",
                responseText.Length, $"in={completion.Usage.InputTokenCount}/out={completion.Usage.OutputTokenCount}");

            return responseText;
        }
        catch (ChatServiceException)
        {
            throw;
        }
        catch (ClientResultException ex)
        {
            logger.LogError(ex, "LLM: Azure OpenAI API call failed with status {Status}", ex.Status);
            throw new ChatServiceException($"Azure OpenAI API call failed: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM: Unexpected error during chat completion");
            throw new ChatServiceException($"Unexpected error during chat completion: {ex.Message}", ex);
        }
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        IReadOnlyList<ConversationMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        logger.LogInformation("LLM: Processing streaming chat with {MessageCount} messages", messages.Count);

        ChatClient chatClient;
        List<ChatMessage> chatMessages;
        ChatCompletionOptions completionOptions;

        try
        {
            chatClient = CreateChatClient();
            chatMessages = ConvertMessages(messages);

            completionOptions = new ChatCompletionOptions
            {
                MaxOutputTokenCount = _options.MaxTokens,
                Temperature = (float)_options.Temperature,
            };
        }
        catch (ChatServiceException)
        {
            throw;
        }
        catch (ClientResultException ex)
        {
            logger.LogError(ex, "LLM: Azure OpenAI API call failed with status {Status}", ex.Status);
            throw new ChatServiceException($"Azure OpenAI API call failed: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM: Unexpected error during streaming chat setup");
            throw new ChatServiceException($"Unexpected error during streaming chat setup: {ex.Message}", ex);
        }

        AsyncCollectionResult<StreamingChatCompletionUpdate> updates;

        try
        {
            updates = chatClient.CompleteChatStreamingAsync(chatMessages, completionOptions, cancellationToken);
        }
        catch (ClientResultException ex)
        {
            logger.LogError(ex, "LLM: Azure OpenAI streaming API call failed with status {Status}", ex.Status);
            throw new ChatServiceException($"Azure OpenAI streaming API call failed: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM: Unexpected error initiating streaming chat");
            throw new ChatServiceException($"Unexpected error initiating streaming chat: {ex.Message}", ex);
        }

        await foreach (var update in updates.WithCancellation(cancellationToken))
        {
            if (update.ContentUpdate.Count > 0)
            {
                var text = update.ContentUpdate[0].Text;
                if (!string.IsNullOrEmpty(text))
                {
                    yield return text;
                }
            }
        }

        logger.LogInformation("LLM: Streaming chat completed");
    }

    private ChatClient CreateChatClient()
    {
        var azureClient = new AzureOpenAIClient(
            new Uri(_options.Endpoint),
            new ApiKeyCredential(_options.ApiKey));

        return azureClient.GetChatClient(_options.DeploymentName);
    }

    internal List<ChatMessage> ConvertMessages(IReadOnlyList<ConversationMessage> messages)
    {
        var chatMessages = new List<ChatMessage>(messages.Count + 1);

        // 如果对话中没有系统消息，则自动注入系统提示词
        var hasSystemMessage = messages.Any(m => m.Role == "system");
        if (!hasSystemMessage && !string.IsNullOrWhiteSpace(_options.SystemPrompt))
        {
            chatMessages.Add(new SystemChatMessage(_options.SystemPrompt));
        }

        foreach (var msg in messages)
        {
            ChatMessage chatMessage = msg.Role switch
            {
                "system" => new SystemChatMessage(msg.Content),
                "user" => new UserChatMessage(msg.Content),
                "assistant" => new AssistantChatMessage(msg.Content),
                _ => LogAndCreateUserMessage(msg)
            };
            chatMessages.Add(chatMessage);
        }

        return chatMessages;
    }

    private UserChatMessage LogAndCreateUserMessage(ConversationMessage msg)
    {
        logger.LogWarning("LLM: Unknown message role '{Role}', falling back to user role", msg.Role);
        return new UserChatMessage(msg.Content);
    }
}
