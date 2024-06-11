
using NetDaemon.Extensions.Observables;

[NetDaemonApp]
// [Focus]
public class VaccumCleanerManager
{
    public VaccumCleanerManager(
        Entities entities,
        ITextToSpeechService textToSpeechService)
    {
        // entities.Vacuum.RoborockS7.StateChanges()
        //     .WhenStateIsFor(n => n?.State == "cleaning", TimeSpan.FromMinutes(15))
        //     .Subscribe(s =>
        //     {
        //         entities.Vacuum.RoborockS7.Pause();
        //         textToSpeechService.Speak("media_player.allmanna_ytor", "Pausat dammsugaren. Töm dammsugaren på damm.", "google_cloud_say");
        //     });

        // entities.Vacuum.RoborockS7.StateChanges()
        //         .Where(n => n.New?.State == "cleaning")
        //         .Subscribe(_ =>
        //         {
        //             textToSpeechService.Speak("media_player.allmanna_ytor", "Startar dammsugning, kommer att pausa om 15 minuter för att tömma damm.", "google_cloud_say");
        //         });
    }
}
