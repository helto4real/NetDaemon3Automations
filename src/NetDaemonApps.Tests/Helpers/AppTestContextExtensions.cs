using System;
using System.Linq;
using FluentAssertions;
using HomeAssistantGenerated;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using NSubstitute.Core.Arguments;

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

    public static void VerifyInputSelect_SelectOption_NotChanged(this AppTestContext ctx, string entityId)
    {
        ctx.HaContext.DidNotReceive().CallService("input_select", "select_option",
            Arg.Is<ServiceTarget>(x
                => x.EntityIds != null && x.EntityIds.First() == entityId),
            Arg.Any<InputSelectSelectOptionParameters>());
    }

    public static void VerifyCallService(this AppTestContext ctx, string serviceCall, int times = 1)
    {
        var domain = serviceCall[..serviceCall.IndexOf(".", StringComparison.InvariantCultureIgnoreCase)];
        var service = serviceCall[(serviceCall.IndexOf(".", StringComparison.InvariantCultureIgnoreCase) + 1)..];
        
        ctx.HaContext.Received(times)
            .CallService(domain, service, Arg.Any<ServiceTarget>(), Arg.Any<object?>());
    }

    public static void VerifyNotCallService(this AppTestContext ctx, string serviceCall)
    {
        var domain = serviceCall[..serviceCall.IndexOf(".", StringComparison.InvariantCultureIgnoreCase)];
        var service = serviceCall[(serviceCall.IndexOf(".", StringComparison.InvariantCultureIgnoreCase) + 1)..];
        
        ctx.HaContext.Received(0)
            .CallService(domain, service, Arg.Any<ServiceTarget?>(), Arg.Any<object?>());
    }

    public static void VerifyCallServiceWithData<T>(this AppTestContext ctx, string serviceCall, ServiceTarget? target, T? data, int times = 1) where T : class
    {
        var domain = serviceCall[..serviceCall.IndexOf(".", StringComparison.InvariantCultureIgnoreCase)];
        var service = serviceCall[(serviceCall.IndexOf(".", StringComparison.InvariantCultureIgnoreCase) + 1)..];
        T? calledData = null;

        ctx.HaContext.Received(times).CallService(domain, service, target, Arg.Any<T>());
        var sp = ctx.HaContext.ReceivedCalls().Where(x => x.GetMethodInfo().Name == "CallService").ToList();
        foreach (var s in sp)
        {
            if (s.GetArguments()[3] is T arg)
            {
                calledData = arg;
                break;
            }
        }
        calledData.Should().BeEquivalentTo(data);
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
