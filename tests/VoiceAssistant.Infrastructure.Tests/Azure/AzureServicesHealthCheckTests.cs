using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using VoiceAssistant.Core.Options;
using VoiceAssistant.Infrastructure.Azure;

namespace VoiceAssistant.Infrastructure.Tests.Azure;

public class AzureServicesHealthCheckTests
{
    private static AzureSpeechOptions CreateValidSpeechOptions() => new()
    {
        SubscriptionKey = "test-subscription-key",
        Region = "eastus"
    };

    private static AzureOpenAIOptions CreateValidOpenAIOptions() => new()
    {
        Endpoint = "https://test.openai.azure.com/",
        ApiKey = "test-api-key",
        DeploymentName = "gpt-4"
    };

    private AzureServicesHealthCheck CreateHealthCheck(
        AzureSpeechOptions? speechOptions = null,
        AzureOpenAIOptions? openAIOptions = null)
    {
        return new AzureServicesHealthCheck(
            Options.Create(speechOptions ?? CreateValidSpeechOptions()),
            Options.Create(openAIOptions ?? CreateValidOpenAIOptions()));
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenAllConfigValuesAreProvided()
    {
        // Arrange
        var healthCheck = CreateHealthCheck();

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("All Azure service configurations are present.");
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenSubscriptionKeyIsMissing()
    {
        // Arrange
        var speechOptions = CreateValidSpeechOptions();
        speechOptions.SubscriptionKey = "";
        var healthCheck = CreateHealthCheck(speechOptions: speechOptions);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        var issues = result.Data["issues"] as List<string>;
        issues.Should().Contain("AzureSpeech:SubscriptionKey is missing");
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenRegionIsMissing()
    {
        // Arrange
        var speechOptions = CreateValidSpeechOptions();
        speechOptions.Region = "";
        var healthCheck = CreateHealthCheck(speechOptions: speechOptions);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        var issues = result.Data["issues"] as List<string>;
        issues.Should().Contain("AzureSpeech:Region is missing");
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenEndpointIsMissing()
    {
        // Arrange
        var openAIOptions = CreateValidOpenAIOptions();
        openAIOptions.Endpoint = "";
        var healthCheck = CreateHealthCheck(openAIOptions: openAIOptions);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        var issues = result.Data["issues"] as List<string>;
        issues.Should().Contain("AzureOpenAI:Endpoint is missing");
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenApiKeyIsMissing()
    {
        // Arrange
        var openAIOptions = CreateValidOpenAIOptions();
        openAIOptions.ApiKey = "";
        var healthCheck = CreateHealthCheck(openAIOptions: openAIOptions);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        var issues = result.Data["issues"] as List<string>;
        issues.Should().Contain("AzureOpenAI:ApiKey is missing");
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenDeploymentNameIsMissing()
    {
        // Arrange
        var openAIOptions = CreateValidOpenAIOptions();
        openAIOptions.DeploymentName = "";
        var healthCheck = CreateHealthCheck(openAIOptions: openAIOptions);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        var issues = result.Data["issues"] as List<string>;
        issues.Should().Contain("AzureOpenAI:DeploymentName is missing");
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WithMultipleIssues_WhenMultipleValuesAreMissing()
    {
        // Arrange
        var speechOptions = CreateValidSpeechOptions();
        speechOptions.SubscriptionKey = "";
        speechOptions.Region = "";

        var openAIOptions = CreateValidOpenAIOptions();
        openAIOptions.Endpoint = "";

        var healthCheck = CreateHealthCheck(speechOptions, openAIOptions);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("One or more Azure service configurations are missing.");

        var issues = result.Data["issues"] as List<string>;
        issues.Should().NotBeNull();
        issues.Should().HaveCount(3);
        issues.Should().Contain("AzureSpeech:SubscriptionKey is missing");
        issues.Should().Contain("AzureSpeech:Region is missing");
        issues.Should().Contain("AzureOpenAI:Endpoint is missing");
    }

    [Fact]
    public async Task CheckHealthAsync_UnhealthyResultData_ContainsCorrectIssuesList()
    {
        // Arrange
        var speechOptions = CreateValidSpeechOptions();
        speechOptions.SubscriptionKey = "  ";
        speechOptions.Region = "";

        var openAIOptions = CreateValidOpenAIOptions();
        openAIOptions.Endpoint = "";
        openAIOptions.ApiKey = "  ";
        openAIOptions.DeploymentName = "";

        var healthCheck = CreateHealthCheck(speechOptions, openAIOptions);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Data.Should().ContainKey("issues");

        var issues = result.Data["issues"] as List<string>;
        issues.Should().NotBeNull();
        issues.Should().HaveCount(5);
        issues.Should().BeEquivalentTo(new List<string>
        {
            "AzureSpeech:SubscriptionKey is missing",
            "AzureSpeech:Region is missing",
            "AzureOpenAI:Endpoint is missing",
            "AzureOpenAI:ApiKey is missing",
            "AzureOpenAI:DeploymentName is missing"
        });
    }
}
