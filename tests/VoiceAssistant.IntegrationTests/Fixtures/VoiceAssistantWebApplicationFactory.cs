using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.IntegrationTests.Fixtures;

public class VoiceAssistantWebApplicationFactory : WebApplicationFactory<Program>
{
    public Mock<ISpeechToTextService> SttMock { get; } = new();
    public Mock<IChatService> ChatMock { get; } = new();
    public Mock<ITextToSpeechService> TtsMock { get; } = new();

    public VoiceAssistantWebApplicationFactory()
    {
        MockServiceDefaults.SetupHappyPath(SttMock, ChatMock, TtsMock);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureSpeech:SubscriptionKey"] = "test-subscription-key",
                ["AzureSpeech:Region"] = "eastus",
                ["AzureOpenAI:Endpoint"] = "https://test.openai.azure.com/",
                ["AzureOpenAI:ApiKey"] = "test-api-key",
                ["AzureOpenAI:DeploymentName"] = "gpt-4o"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove real Azure service registrations
            RemoveService<ISpeechToTextService>(services);
            RemoveService<IChatService>(services);
            RemoveService<ITextToSpeechService>(services);

            // Register mocks — scoped, so each request scope gets the same mock instance
            services.AddScoped<ISpeechToTextService>(_ => SttMock.Object);
            services.AddScoped<IChatService>(_ => ChatMock.Object);
            services.AddScoped<ITextToSpeechService>(_ => TtsMock.Object);
        });

        builder.UseEnvironment("Development");
    }

    /// <summary>
    /// Resets all mocks to their happy-path defaults for test isolation.
    /// </summary>
    public void ResetMocks()
    {
        SttMock.Reset();
        ChatMock.Reset();
        TtsMock.Reset();
        MockServiceDefaults.SetupHappyPath(SttMock, ChatMock, TtsMock);
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }
}
