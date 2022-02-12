using System.Reactive.Concurrency;
namespace NetDaemon.Extensions.Observables;

public static class ObservableExtensions
{
    // IObservable<StateChange<TEntity,TEntityState>>
    // public static IObservable<StateChange> SameStateFor(this IObservable<StateChange> observable, Func<EntityState?, bool> predicate, TimeSpan timeSpan)
    // {
    //     return observable
    //         .Where(e => predicate(e.New)!=predicate(e.Old))
    //         .Throttle(timeSpan).Where(e=>predicate(e.New));
    // }
    public static IObservable<NumericStateChange> SameStateForNumeric(this IObservable<NumericStateChange> observable, Func<NumericEntityState?, bool> predicate, TimeSpan timeSpan)
    {
        return observable
            .Where(e => predicate(e.New)!=predicate(e.Old))
            .Throttle(timeSpan).Where(e=>predicate(e.New));
    }

    public static IObservable<StateChange<TEntity, TEntityState>> SameStateFor<TEntity, TEntityState>(this IObservable<StateChange<TEntity, TEntityState>> observable, Func<TEntityState?, bool> predicate, TimeSpan timeSpan, IScheduler? scheduler = null)
        where TEntity : Entity
        where TEntityState : EntityState =>
        observable
            .Where(e => predicate(e.Old) != predicate(e.New))
            .Throttle(timeSpan, scheduler ?? DefaultScheduler.Instance)
            .Where(e => predicate(e.New));
}
