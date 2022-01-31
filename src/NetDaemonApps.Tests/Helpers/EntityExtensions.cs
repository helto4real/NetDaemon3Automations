using System;
using NetDaemon.HassModel.Entities;
using NSubstitute;

namespace NetDaemonApps.Tests.Helpers;

public static class EntityExtensions
{
    public static void VerifyCallService(this Entity entity, string serviceCall, int times = 1)
    {
        var domain = entity.EntityId[..entity.EntityId.IndexOf(".", StringComparison.InvariantCultureIgnoreCase)];
        entity.HaContext
            .Received(times)
            .CallService(
                domain,
                serviceCall,
                Arg.Any<ServiceTarget>(),
                Arg.Any<object?>()
                );
    }
}
