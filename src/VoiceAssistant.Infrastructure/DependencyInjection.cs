using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Options;
using VoiceAssistant.Infrastructure.Azure;

namespace VoiceAssistant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptionsWithValidateOnStart<AzureSpeechOptions>()
            .BindConfiguration(AzureSpeechOptions.SectionName)
            .ValidateDataAnnotations();
        services.AddOptionsWithValidateOnStart<AzureOpenAIOptions>()
            .BindConfiguration(AzureOpenAIOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddScoped<ISpeechToTextService, AzureSpeechToTextService>();
        services.AddScoped<ITextToSpeechService, AzureTextToSpeechService>();
        services.AddScoped<IChatService, AzureOpenAIChatService>();

        services.AddHealthChecks()
            .AddCheck<AzureServicesHealthCheck>("azure-services");

        return services;
    }
}
