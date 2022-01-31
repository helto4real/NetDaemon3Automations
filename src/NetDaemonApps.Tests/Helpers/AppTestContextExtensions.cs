using System;
using System.Linq;
using HomeAssistantGenerated;
using NetDaemon.HassModel.Entities;
using NSubstitute;

namespace NetDaemonApps.Tests.Helpers;

public static class AppTestContextExtensions
{
    public static void VerifyInputSelect_SelectOption(this AppTestContext ctx, string entityId, string option)
    {
        ctx.HaContext.Received(1).CallService("input_select", "select_option",
            Arg.Is<ServiceTarget>(x
                => x.EntityIds != null && x.EntityIds.First() == entityId),
            Arg.Is<InputSelectSelectOptionParameters>(x
                => x.Option == option));
    }

    public static void VerifyCallService(this AppTestContext ctx, string serviceCall, int times = 1)
    {
        var domain = serviceCall[..serviceCall.IndexOf(".", StringComparison.InvariantCultureIgnoreCase)];
        var service = serviceCall[(serviceCall.IndexOf(".", StringComparison.InvariantCultureIgnoreCase) + 1)..];
        
        ctx.HaContext.Received(times)
            .CallService(domain, service, Arg.Any<ServiceTarget>(), Arg.Any<object?>());
    }

    public static T? GetEntity<T>(this AppTestContext ctx, string entityId) where T : Entity 
        
    {
        return  Activator.CreateInstance(typeof(T), ctx.HaContext, entityId) as T;
    }    
    
    public static T? GetEntity<T>(this AppTestContext ctx, string entityId, string state) where T : Entity 
        
    {
        ctx.HaContext.GetState(entityId).Returns(
            new EntityState
            {
                EntityId = entityId,
                State = state
            }
        );
        return  Activator.CreateInstance(typeof(T), ctx.HaContext, entityId) as T;
    }

    public static void ActivateScene(this AppTestContext ctx, string sceneName)
    {
        ctx.ChangeStateFor($"scene.{sceneName}")
            .FromState("off")
            .ToState("on");
    }

    public static IFromState ChangeStateFor(this AppTestContext ctx, string entityId)
    {
        return new StateChangeContext(ctx, entityId);
    }

    public static IWithState WithEntityState<T>(this AppTestContext ctx, string entityId, T state)
    {
        var stateChangeContext = new StateChangeContext(ctx, entityId);
        stateChangeContext.WithEntityState(entityId, state);
        return stateChangeContext;
    }
}
