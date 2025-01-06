using System.Text.Json;

[NetDaemonApp]
// [Focus]
public class ElinsOfficeApp
{
    private readonly Entities _entities;
    private readonly ITriggerManager _triggerManager;

    public ElinsOfficeApp(Entities entities, ITriggerManager triggerManager)
    {
        _entities = entities;
        _triggerManager = triggerManager;
        HandlebBlinds();
    }

    private void HandlebBlinds()
    {
        _triggerManager.RegisterMqttActionTrigger("elins_rum_knapp_rullgardin")
            .Subscribe(e =>
            {
                var action = e;
                switch (action)
                {
                    case "open":
                        _entities.Cover.ElinsRumRullgardin.OpenCover();
                        break;
                    case "close":
                        _entities.Cover.ElinsRumRullgardin.CloseCover();
                        break;
                }
            });
    }
}
