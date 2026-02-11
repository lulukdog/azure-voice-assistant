using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using VoiceAssistant.Core.Options;

namespace VoiceAssistant.Infrastructure.Azure;

public class AzureServicesHealthCheck : IHealthCheck
{
    private readonly AzureSpeechOptions _speechOptions;
    private readonly AzureOpenAIOptions _openAIOptions;

    public AzureServicesHealthCheck(
        IOptions<AzureSpeechOptions> speechOptions,
        IOptions<AzureOpenAIOptions> openAIOptions)
    {
        _speechOptions = speechOptions.Value;
        _openAIOptions = openAIOptions.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(_speechOptions.SubscriptionKey))
            issues.Add("AzureSpeech:SubscriptionKey is missing");

        if (string.IsNullOrWhiteSpace(_speechOptions.Region))
            issues.Add("AzureSpeech:Region is missing");

        if (string.IsNullOrWhiteSpace(_openAIOptions.Endpoint))
            issues.Add("AzureOpenAI:Endpoint is missing");

        if (string.IsNullOrWhiteSpace(_openAIOptions.ApiKey))
            issues.Add("AzureOpenAI:ApiKey is missing");

        if (string.IsNullOrWhiteSpace(_openAIOptions.DeploymentName))
            issues.Add("AzureOpenAI:DeploymentName is missing");

        if (issues.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "One or more Azure service configurations are missing.",
                data: new Dictionary<string, object> { ["issues"] = issues }));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "All Azure service configurations are present."));
    }
}
