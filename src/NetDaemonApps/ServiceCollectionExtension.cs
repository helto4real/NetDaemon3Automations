using Microsoft.Extensions.DependencyInjection;

namespace HomeAssistantGenerated;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddHeltoServices(this IServiceCollection services)
    {
        services.AddScoped<Services>();
        services.AddScoped<IServices>(n => n.GetRequiredService<Services>());
        services.AddScoped<Entities>();
        services.AddScoped<IEntities>(n => n.GetRequiredService<Entities>());
        return services;
    }
}
