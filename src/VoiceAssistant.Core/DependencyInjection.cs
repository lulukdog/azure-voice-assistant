using Microsoft.Extensions.DependencyInjection;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Pipeline;

namespace VoiceAssistant.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddScoped<IConversationPipeline, ConversationPipeline>();
        return services;
    }
}
