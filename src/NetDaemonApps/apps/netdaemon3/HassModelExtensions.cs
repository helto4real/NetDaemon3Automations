namespace NetDaemonApps;

public static class HassModelExtensions
{
    internal static string GetDomainFromEntity(string entity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity);
        var entityParts = entity.Split('.', StringSplitOptions.TrimEntries);
        if (entityParts.Length != 2)
        {
            throw new ArgumentException($"Entity id is malformed: {entity}", nameof(entity));
        }

        return entityParts[0];
    }

    internal static string GetDomainForServiceCall(string[] entityIds)
    {
        ArgumentNullException.ThrowIfNull(entityIds);
        var domains = entityIds.Select(GetDomainFromEntity).Distinct(StringComparer.Ordinal).ToArray();
        return domains.Length == 1 ? domains[0] : "homeassistant";
    }

    public static void TurnOn(this IHaContext ha, params string[] entityIds)
    {
        if (entityIds.Length == 0)
        {
            throw new ArgumentException("At least one entity id is required.", nameof(entityIds));
        }

        ha.CallService(GetDomainForServiceCall(entityIds), "turn_on", new ServiceTarget { EntityIds = entityIds });
    }

    public static void TurnOff(this IHaContext ha, params string[] entityIds)
    {
        if (entityIds.Length == 0)
        {
            throw new ArgumentException("At least one entity id is required.", nameof(entityIds));
        }

        ha.CallService(GetDomainForServiceCall(entityIds), "turn_off", new ServiceTarget { EntityIds = entityIds });
    }

    public static void TurnOn(this IHaContext ha, string entityId, object attributes)
    {
        var entityIds = new[] { entityId };

        ha.CallService(GetDomainForServiceCall(entityIds), "turn_on", new ServiceTarget { EntityIds = entityIds },
            attributes);
    }

    public static void TurnOff(this IHaContext ha, string entityId, object attributes)
    {
        var entityIds = new[] { entityId };

        ha.CallService(GetDomainForServiceCall(entityIds), "turn_off", new ServiceTarget { EntityIds = entityIds },
            attributes);
    }

    public static void RunScript(this IHaContext ha, string script)
    {
        ha.CallService("script", script);
    }

    public static IEntities GetEntities(this IHaContext context)
    {
        return new HomeAssistantGenerated.Entities(context);
    }

    public static IServices GetServices(this IHaContext context)
    {
        return new Services(context);
    }
}
