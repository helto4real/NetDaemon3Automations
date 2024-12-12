using System.Text.Json;

[NetDaemonApp]
// [Focus]
public class ElinsOfficeApp
{
    private readonly Entities _entities;

    public ElinsOfficeApp(Entities entities)
    {
        _entities = entities;
        HandlebBlinds();
    }

    private void HandlebBlinds()
    {
        _entities.Button.ElinsRumKnappRullgardin.StateAllChanges()
            .Where(n => n.New?.Attributes?.Action is not null)
            .Subscribe(s =>
        {
            var action = ((JsonElement)s.New!.Attributes!.Action!).GetString();
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
