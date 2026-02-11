using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Infrastructure.Azure;

namespace VoiceAssistant.Infrastructure.Tests;

public class DependencyInjectionTests : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _provider;

    public DependencyInjectionTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureSpeech:SubscriptionKey"] = "test-key",
                ["AzureSpeech:Region"] = "eastus",
                ["AzureOpenAI:Endpoint"] = "https://test.openai.azure.com/",
                ["AzureOpenAI:ApiKey"] = "test-api-key",
                ["AzureOpenAI:DeploymentName"] = "gpt-4"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddInfrastructure(configuration);

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
    }

    public void Dispose()
    {
        _scope.Dispose();
        (_provider as IDisposable)?.Dispose();
    }

    [Fact]
    public void AddInfrastructure_RegistersISpeechToTextService()
    {
        // Act
        var service = _scope.ServiceProvider.GetService<ISpeechToTextService>();

        // Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<AzureSpeechToTextService>();
    }

    [Fact]
    public void AddInfrastructure_RegistersITextToSpeechService()
    {
        // Act
        var service = _scope.ServiceProvider.GetService<ITextToSpeechService>();

        // Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<AzureTextToSpeechService>();
    }

    [Fact]
    public void AddInfrastructure_RegistersIChatService()
    {
        // Act
        var service = _scope.ServiceProvider.GetService<IChatService>();

        // Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<AzureOpenAIChatService>();
    }

    [Fact]
    public void AddInfrastructure_RegistersHealthChecks()
    {
        // Act
        var healthCheckService = _provider.GetService(typeof(HealthCheckService));

        // Assert
        healthCheckService.Should().NotBeNull();
    }
}
