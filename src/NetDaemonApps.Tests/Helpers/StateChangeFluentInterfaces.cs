using NetDaemon.HassModel.Entities;

namespace NetDaemonApps.Tests.Helpers;

public interface IWithState : IFromState
{
    IWithState WithEntityState<T>(string entityId, T state);
    IWithState WithEntityState(string entityId, EntityState state);
    IFromState ChangeStateFor(string entityId);
}

public interface IFromState
{
    IToState FromState<T>(T state);
    IToState FromHassState(EntityState hassState);
}

public interface IToState
{
    void ToState<T>(T state);
    void ToHassState(EntityState hassState);
}