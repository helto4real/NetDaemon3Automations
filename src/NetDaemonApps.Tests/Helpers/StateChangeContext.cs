using System;
using System.Globalization;
using NetDaemon.HassModel.Entities;
using NSubstitute;

namespace NetDaemonApps.Tests.Helpers;

public class StateChangeContext : IFromState, IToState, IWithState
{
    private readonly AppTestContext _ctx;
    private readonly string _entityId;

    public StateChangeContext(AppTestContext ctx, string entityId)
    {
        _ctx = ctx;
        _entityId = entityId;
    }

    private EntityState? FromState { get; set; }
    private EntityState? ToState { get; set; }

    IToState IFromState.FromState<T>(T state)
    {
        ArgumentNullException.ThrowIfNull(state);
        FromState = new EntityState
        {
            EntityId = _entityId,
            State = Convert.ToString(state, CultureInfo.InvariantCulture)
        };
        return this;
    }

    IToState IFromState.FromHassState(EntityState hassState)
    {
        ArgumentNullException.ThrowIfNull(hassState);
        FromState = hassState;
        return this;
    }

    void IToState.ToState<T>(T state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ToState = new EntityState
        {
            EntityId = _entityId,
            State = state.ToString()
        };
        _ctx.HaContext.GetState(_entityId).Returns(ToState);
        _ctx.HaContextMock.StateChangeSubject.OnNext(
            new StateChange(
                new Entity(_ctx.HaContext, _entityId),
                FromState!,
                ToState!));
    }

    void IToState.ToHassState(EntityState hassState)
    {
        ArgumentNullException.ThrowIfNull(hassState);
        ToState = hassState;
        _ctx.HaContext.GetState(_entityId).Returns(FromState);
        _ctx.HaContextMock.StateChangeSubject.OnNext(
            new StateChange(
                new Entity(_ctx.HaContext, _entityId),
                FromState!,
                ToState!));
    }

    public IWithState WithEntityState<T>(string entityId, T state)
    {
        _ctx.HaContext.GetState(entityId).Returns(
            new EntityState
            {
                EntityId = _entityId,
                State = Convert.ToString(state, CultureInfo.InvariantCulture)
            }
        );
        return this;
    }

    public IWithState WithEntityState(string entityId, EntityState state)
    {
        _ctx.HaContext.GetState(entityId).Returns(
            state
        );
        return this;
    }

    public IFromState ChangeStateFor(string entityId)
    {
        return new StateChangeContext(_ctx, entityId);
    }
}