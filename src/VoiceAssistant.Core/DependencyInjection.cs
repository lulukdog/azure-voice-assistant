using Microsoft.Extensions.DependencyInjection;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Pipeline;
using VoiceAssistant.Core.Services;

namespace VoiceAssistant.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton<ISessionManager, InMemorySessionManager>();
        services.AddScoped<IConversationPipeline, ConversationPipeline>();
        return services;
    }
}
