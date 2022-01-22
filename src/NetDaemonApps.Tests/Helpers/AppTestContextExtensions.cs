using System.Linq;
using HomeAssistantGenerated;
using NetDaemon.HassModel.Entities;
using NetDaemon.HassModel.Integration;
using NSubstitute;

namespace NetDaemonApps.Tests.Helpers;

public static class AppTestContextExtensions
{
    public static void VerityInputSelect_SelectOption(this AppTestContext ctx, string entityId, string option)
    {
        ctx.HaContext.Received(1).CallService("input_select", "select_option",
            Arg.Is<ServiceTarget>(x
                => x.EntityIds != null && x.EntityIds.First() == entityId),
            Arg.Is<InputSelectSelectOptionParameters>(x
                => x.Option == option));
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