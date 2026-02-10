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
        services.Configure<AzureSpeechOptions>(
            configuration.GetSection(AzureSpeechOptions.SectionName));
        services.Configure<AzureOpenAIOptions>(
            configuration.GetSection(AzureOpenAIOptions.SectionName));

        services.AddScoped<ISpeechToTextService, AzureSpeechToTextService>();
        services.AddScoped<ITextToSpeechService, AzureTextToSpeechService>();
        services.AddScoped<IChatService, AzureOpenAIChatService>();

        return services;
    }
}
