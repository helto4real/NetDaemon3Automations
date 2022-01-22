[NetDaemonApp]
public class NotifyOnFail
{
    private readonly IHaContext _haContext;

    public NotifyOnFail(
        IHaContext haContext
    )
    {
        _haContext = haContext;
        _haContext.StateAllChanges()
            .Timeout(TimeSpan.FromMinutes(5))
            .Subscribe(_ => { },
                e =>
                {
                    _haContext.CallService("notify", "persistent_notification",
                        data: new {message = "Slutade att få events", title = "Slutade att få events tyvärr.!"});
                });
    }
    
}