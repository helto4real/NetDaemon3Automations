using NetDaemon.Extensions.Tts;

/// <summary>
///     Pushes important calendar events to TTS.
/// </summary>
[NetDaemonApp]
public class GoogleCalendarManager
{
    public GoogleCalendarManager(IHaContext ha, ITextToSpeechService tts)
    {
        var entities = new Entities(ha);
        entities.Calendar.TaUtSopor
            .WhenTurnsOn(s =>
                {
                    tts.Speak("media_player.huset", "Viktigt meddelande", "google_cloud_say"); // Important message
                    if (s.New?.Attributes?.Message is not null)
                        tts.Speak("media_player.huset", s.New.Attributes.Message, "google_cloud_say");
                    if (s.New?.Attributes?.Description is not null)
                        tts.Speak("media_player.huset", s.New.Attributes.Description, "google_cloud_say");
                }
            );
    }
}