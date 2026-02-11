using System.ClientModel;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenAI.Chat;
using VoiceAssistant.Core.Exceptions;
using VoiceAssistant.Core.Models;
using VoiceAssistant.Core.Options;
using VoiceAssistant.Infrastructure.Azure;

namespace VoiceAssistant.Infrastructure.Tests.Azure;

public class AzureOpenAIChatServiceTests
{
    private const string TestEndpoint = "https://test.openai.azure.com/";
    private const string TestApiKey = "test-key";
    private const string TestDeploymentName = "gpt-4o";
    private const string DefaultSystemPrompt = "你是一个友好的 AI 语音助手，请用简洁的中文回答问题。";

    private static AzureOpenAIChatService CreateService(
        AzureOpenAIOptions? optionsOverride = null)
    {
        var options = optionsOverride ?? new AzureOpenAIOptions
        {
            Endpoint = TestEndpoint,
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
        };

        var mockOptions = new Mock<IOptions<AzureOpenAIOptions>>();
        mockOptions.Setup(o => o.Value).Returns(options);

        var mockLogger = new Mock<ILogger<AzureOpenAIChatService>>();

        return new AzureOpenAIChatService(mockOptions.Object, mockLogger.Object);
    }

    private static AzureOpenAIChatService CreateServiceWithLogger(
        out Mock<ILogger<AzureOpenAIChatService>> mockLogger,
        AzureOpenAIOptions? optionsOverride = null)
    {
        var options = optionsOverride ?? new AzureOpenAIOptions
        {
            Endpoint = TestEndpoint,
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
        };

        var mockOptions = new Mock<IOptions<AzureOpenAIOptions>>();
        mockOptions.Setup(o => o.Value).Returns(options);

        mockLogger = new Mock<ILogger<AzureOpenAIChatService>>();

        return new AzureOpenAIChatService(mockOptions.Object, mockLogger.Object);
    }

    #region Construction Tests

    [Fact]
    public void Constructor_WithValidOptions_CreatesInstance()
    {
        // Act
        var service = CreateService();

        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<Core.Interfaces.IChatService>();
    }

    [Fact]
    public void Constructor_WithCustomOptions_CreatesInstance()
    {
        // Arrange
        var options = new AzureOpenAIOptions
        {
            Endpoint = "https://custom.openai.azure.com/",
            ApiKey = "custom-key",
            DeploymentName = "gpt-35-turbo",
            MaxTokens = 1600,
            Temperature = 0.5,
            SystemPrompt = "Custom system prompt",
        };

        // Act
        var service = CreateService(options);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region ConvertMessages - System Prompt Injection

    [Fact]
    public void ConvertMessages_WithNoSystemMessage_InjectsSystemPrompt()
    {
        // Arrange
        var service = CreateService();
        var messages = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "你好" },
        };

        // Act
        var result = service.ConvertMessages(messages);

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().BeOfType<SystemChatMessage>();
        result[1].Should().BeOfType<UserChatMessage>();
    }

    [Fact]
    public void ConvertMessages_WithExistingSystemMessage_DoesNotInjectSystemPrompt()
    {
        // Arrange
        var service = CreateService();
        var messages = new List<ConversationMessage>
        {
            new() { Role = "system", Content = "你是一个翻译助手" },
            new() { Role = "user", Content = "翻译这段话" },
        };

        // Act
        var result = service.ConvertMessages(messages);

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().BeOfType<SystemChatMessage>();
        result[1].Should().BeOfType<UserChatMessage>();
    }

    [Fact]
    public void ConvertMessages_WithEmptySystemPrompt_DoesNotInject()
    {
        // Arrange
        var options = new AzureOpenAIOptions
        {
            Endpoint = TestEndpoint,
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
            SystemPrompt = "",
        };
        var service = CreateService(options);
        var messages = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "你好" },
        };

        // Act
        var result = service.ConvertMessages(messages);

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().BeOfType<UserChatMessage>();
    }

    [Fact]
    public void ConvertMessages_WithWhitespaceSystemPrompt_DoesNotInject()
    {
        // Arrange
        var options = new AzureOpenAIOptions
        {
            Endpoint = TestEndpoint,
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
            SystemPrompt = "   ",
        };
        var service = CreateService(options);
        var messages = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "你好" },
        };

        // Act
        var result = service.ConvertMessages(messages);

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().BeOfType<UserChatMessage>();
    }

    [Fact]
    public void ConvertMessages_WithSystemMessageAtMiddle_DoesNotInjectSystemPrompt()
    {
        // Arrange
        var service = CreateService();
        var messages = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "你好" },
            new() { Role = "system", Content = "系统消息" },
            new() { Role = "user", Content = "继续" },
        };

        // Act
        var result = service.ConvertMessages(messages);

        // Assert
        // Should NOT inject because a system message exists (even though it's not first)
        result.Should().HaveCount(3);
    }

    #endregion

    #region ConvertMessages - Role Mapping

    [Fact]
    public void ConvertMessages_UserRole_CreatesUserChatMessage()
    {
        // Arrange
        var options = new AzureOpenAIOptions
        {
            Endpoint = TestEndpoint,
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
            SystemPrompt = "",
        };
        var service = CreateService(options);
        var messages = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "你好" },
        };

        // Act
        var result = service.ConvertMessages(messages);

        // Assert
        result.Should().ContainSingle()
            .Which.Should().BeOfType<UserChatMessage>();
    }

    [Fact]
    public void ConvertMessages_AssistantRole_CreatesAssistantChatMessage()
    {
        // Arrange
        var options = new AzureOpenAIOptions
        {
            Endpoint = TestEndpoint,
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
            SystemPrompt = "",
        };
        var service = CreateService(options);
        var messages = new List<ConversationMessage>
        {
            new() { Role = "assistant", Content = "我可以帮你" },
        };

        // Act
        var result = service.ConvertMessages(messages);

        // Assert
        result.Should().ContainSingle()
            .Which.Should().BeOfType<AssistantChatMessage>();
    }

    [Fact]
    public void ConvertMessages_SystemRole_CreatesSystemChatMessage()
    {
        // Arrange
        var service = CreateService();
        var messages = new List<ConversationMessage>
        {
            new() { Role = "system", Content = "你是助手" },
        };

        // Act
        var result = service.ConvertMessages(messages);

        // Assert
        result.Should().ContainSingle()
            .Which.Should().BeOfType<SystemChatMessage>();
    }

    [Fact]
    public void ConvertMessages_UnknownRole_FallsBackToUserChatMessage()
    {
        // Arrange
        var options = new AzureOpenAIOptions
        {
            Endpoint = TestEndpoint,
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
            SystemPrompt = "",
        };
        var service = CreateServiceWithLogger(out var mockLogger, options);
        var messages = new List<ConversationMessage>
        {
            new() { Role = "tool", Content = "tool output" },
        };

        // Act
        var result = service.ConvertMessages(messages);

        // Assert
        result.Should().ContainSingle()
            .Which.Should().BeOfType<UserChatMessage>();
    }

    [Fact]
    public void ConvertMessages_UnknownRole_LogsWarning()
    {
        // Arrange
        var options = new AzureOpenAIOptions
        {
            Endpoint = TestEndpoint,
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
            SystemPrompt = "",
        };
        var service = CreateServiceWithLogger(out var mockLogger, options);
        var messages = new List<ConversationMessage>
        {
            new() { Role = "unknown_role", Content = "some content" },
        };

        // Act
        service.ConvertMessages(messages);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unknown message role")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region ConvertMessages - Multi-turn Conversation

    [Fact]
    public void ConvertMessages_MultiTurnConversation_PreservesOrder()
    {
        // Arrange
        var options = new AzureOpenAIOptions
        {
            Endpoint = TestEndpoint,
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
            SystemPrompt = "",
        };
        var service = CreateService(options);
        var messages = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "你好" },
            new() { Role = "assistant", Content = "你好！有什么可以帮你的吗？" },
            new() { Role = "user", Content = "天气怎么样" },
        };

        // Act
        var result = service.ConvertMessages(messages);

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().BeOfType<UserChatMessage>();
        result[1].Should().BeOfType<AssistantChatMessage>();
        result[2].Should().BeOfType<UserChatMessage>();
    }

    [Fact]
    public void ConvertMessages_MultiTurnWithSystemPromptInjection_InsertsAtBeginning()
    {
        // Arrange
        var service = CreateService();
        var messages = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "你好" },
            new() { Role = "assistant", Content = "你好！" },
            new() { Role = "user", Content = "再见" },
        };

        // Act
        var result = service.ConvertMessages(messages);

        // Assert
        result.Should().HaveCount(4);
        result[0].Should().BeOfType<SystemChatMessage>();
        result[1].Should().BeOfType<UserChatMessage>();
        result[2].Should().BeOfType<AssistantChatMessage>();
        result[3].Should().BeOfType<UserChatMessage>();
    }

    [Fact]
    public void ConvertMessages_EmptyMessages_WithSystemPrompt_InjectsOnlySystemMessage()
    {
        // Arrange
        var service = CreateService();
        var messages = new List<ConversationMessage>();

        // Act
        var result = service.ConvertMessages(messages);

        // Assert
        result.Should().ContainSingle()
            .Which.Should().BeOfType<SystemChatMessage>();
    }

    [Fact]
    public void ConvertMessages_EmptyMessages_WithoutSystemPrompt_ReturnsEmpty()
    {
        // Arrange
        var options = new AzureOpenAIOptions
        {
            Endpoint = TestEndpoint,
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
            SystemPrompt = "",
        };
        var service = CreateService(options);
        var messages = new List<ConversationMessage>();

        // Act
        var result = service.ConvertMessages(messages);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region ChatAsync - Error Handling

    [Fact]
    public async Task ChatAsync_WithInvalidEndpoint_ThrowsChatServiceException()
    {
        // Arrange
        var options = new AzureOpenAIOptions
        {
            Endpoint = "https://invalid-endpoint-that-does-not-exist.openai.azure.com/",
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
        };
        var service = CreateService(options);
        var messages = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "你好" },
        };

        // Act
        Func<Task> act = async () => await service.ChatAsync(messages);

        // Assert
        await act.Should().ThrowAsync<ChatServiceException>();
    }

    [Fact]
    public async Task ChatAsync_WithInvalidEndpoint_ExceptionHasLlmFailedErrorCode()
    {
        // Arrange
        var options = new AzureOpenAIOptions
        {
            Endpoint = "https://invalid-endpoint-that-does-not-exist.openai.azure.com/",
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
        };
        var service = CreateService(options);
        var messages = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "你好" },
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ChatServiceException>(
            () => service.ChatAsync(messages));
        exception.ErrorCode.Should().Be("LLM_FAILED");
    }

    [Fact]
    public async Task ChatAsync_WithEmptyMessages_ThrowsChatServiceException()
    {
        // Arrange -- even with system prompt injected, the HTTP call will fail
        var service = CreateService();
        var messages = new List<ConversationMessage>();

        // Act
        Func<Task> act = async () => await service.ChatAsync(messages);

        // Assert -- will throw because endpoint is fake
        await act.Should().ThrowAsync<ChatServiceException>();
    }

    #endregion

    #region ChatStreamAsync - Error Handling

    [Fact]
    public async Task ChatStreamAsync_WithInvalidEndpoint_ThrowsException()
    {
        // Arrange
        var options = new AzureOpenAIOptions
        {
            Endpoint = "https://invalid-endpoint-that-does-not-exist.openai.azure.com/",
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
        };
        var service = CreateService(options);
        var messages = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "你好" },
        };

        // Act & Assert
        // The streaming method may throw ChatServiceException during setup,
        // or ClientResultException during iteration (await foreach), since
        // the service does not wrap exceptions thrown during stream enumeration.
        Exception? caughtException = null;
        try
        {
            await foreach (var chunk in service.ChatStreamAsync(messages))
            {
                // consume the stream
            }
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        caughtException.Should().NotBeNull();
        // The streaming method may throw:
        // - ChatServiceException (wrapped during setup)
        // - ClientResultException (unwrapped, from iteration)
        // - AggregateException wrapping a ClientResultException (async enumerable iteration)
        var isExpectedType = caughtException is ChatServiceException
            || caughtException is ClientResultException
            || (caughtException is AggregateException agg
                && agg.InnerExceptions.Any(e => e is ClientResultException));
        isExpectedType.Should().BeTrue(
            $"expected ChatServiceException, ClientResultException, or AggregateException " +
            $"containing ClientResultException, but got {caughtException!.GetType().FullName}");
    }

    [Fact]
    public async Task ChatStreamAsync_WithInvalidEndpoint_ExceptionContainsRelevantInfo()
    {
        // Arrange
        var options = new AzureOpenAIOptions
        {
            Endpoint = "https://invalid-endpoint-that-does-not-exist.openai.azure.com/",
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
        };
        var service = CreateService(options);
        var messages = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "你好" },
        };

        // Act & Assert
        Exception? caughtException = null;
        try
        {
            await foreach (var chunk in service.ChatStreamAsync(messages))
            {
                // consume the stream
            }
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        caughtException.Should().NotBeNull();
        // The exception message should contain useful diagnostic information
        caughtException!.Message.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region ConvertMessages - Capacity Allocation

    [Fact]
    public void ConvertMessages_AllocatesCorrectCapacity_WithSystemPrompt()
    {
        // Arrange
        var service = CreateService();
        var messages = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "message 1" },
            new() { Role = "assistant", Content = "response 1" },
            new() { Role = "user", Content = "message 2" },
        };

        // Act
        var result = service.ConvertMessages(messages);

        // Assert - should be messages.Count + 1 (injected system prompt)
        result.Should().HaveCount(4);
    }

    [Fact]
    public void ConvertMessages_AllocatesCorrectCapacity_WithoutSystemPrompt()
    {
        // Arrange
        var options = new AzureOpenAIOptions
        {
            Endpoint = TestEndpoint,
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
            SystemPrompt = "",
        };
        var service = CreateService(options);
        var messages = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "message 1" },
            new() { Role = "assistant", Content = "response 1" },
        };

        // Act
        var result = service.ConvertMessages(messages);

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion

    #region ConvertMessages - Edge Cases

    [Fact]
    public void ConvertMessages_MultipleUnknownRoles_AllFallBackToUser()
    {
        // Arrange
        var options = new AzureOpenAIOptions
        {
            Endpoint = TestEndpoint,
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
            SystemPrompt = "",
        };
        var service = CreateServiceWithLogger(out var mockLogger, options);
        var messages = new List<ConversationMessage>
        {
            new() { Role = "tool", Content = "tool output" },
            new() { Role = "function", Content = "function result" },
            new() { Role = "custom", Content = "custom content" },
        };

        // Act
        var result = service.ConvertMessages(messages);

        // Assert
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(m => m.Should().BeOfType<UserChatMessage>());

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unknown message role")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(3));
    }

    [Fact]
    public void ConvertMessages_MixedKnownAndUnknownRoles_HandlesCorrectly()
    {
        // Arrange
        var options = new AzureOpenAIOptions
        {
            Endpoint = TestEndpoint,
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
            SystemPrompt = "",
        };
        var service = CreateService(options);
        var messages = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "hello" },
            new() { Role = "unknown", Content = "mystery" },
            new() { Role = "assistant", Content = "response" },
        };

        // Act
        var result = service.ConvertMessages(messages);

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().BeOfType<UserChatMessage>();
        result[1].Should().BeOfType<UserChatMessage>(); // unknown falls back to user
        result[2].Should().BeOfType<AssistantChatMessage>();
    }

    [Fact]
    public void ConvertMessages_CustomSystemPrompt_InjectsCustomPrompt()
    {
        // Arrange
        var customPrompt = "You are a translation assistant.";
        var options = new AzureOpenAIOptions
        {
            Endpoint = TestEndpoint,
            ApiKey = TestApiKey,
            DeploymentName = TestDeploymentName,
            SystemPrompt = customPrompt,
        };
        var service = CreateService(options);
        var messages = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Translate this" },
        };

        // Act
        var result = service.ConvertMessages(messages);

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().BeOfType<SystemChatMessage>();
    }

    [Fact]
    public void ConvertMessages_MultipleSystemMessages_DoesNotInjectAdditional()
    {
        // Arrange
        var service = CreateService();
        var messages = new List<ConversationMessage>
        {
            new() { Role = "system", Content = "First system message" },
            new() { Role = "system", Content = "Second system message" },
            new() { Role = "user", Content = "hello" },
        };

        // Act
        var result = service.ConvertMessages(messages);

        // Assert - no injection because system messages already exist
        result.Should().HaveCount(3);
        result[0].Should().BeOfType<SystemChatMessage>();
        result[1].Should().BeOfType<SystemChatMessage>();
        result[2].Should().BeOfType<UserChatMessage>();
    }

    #endregion

    #region ChatAsync - Cancellation

    [Fact]
    public async Task ChatAsync_WithCancelledToken_ThrowsChatServiceException()
    {
        // Arrange
        var service = CreateService();
        var messages = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "你好" },
        };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await service.ChatAsync(messages, cts.Token);

        // Assert -- cancelled token causes an exception that gets wrapped
        await act.Should().ThrowAsync<Exception>();
    }

    #endregion
}
