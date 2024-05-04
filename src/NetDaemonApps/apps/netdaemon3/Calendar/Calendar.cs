/// <summary>
///     Pushes important calendar events to TTS.
/// </summary>
[NetDaemonApp]
public class GoogleCalendarManager
{
    public GoogleCalendarManager(Entities entities, ITextToSpeechService tts, ILogger<GoogleCalendarManager> logger)
    {
        // entities.InputSelect.HouseModeSelect.StateChanges()
        //     .Where(e => e.New?.State == "Natt")
        //     .Subscribe(_ => tts.Speak("media_player.huset", "Tomas, kom ihåg att ta din medicin!", "google_cloud_say"));
        entities.Calendar.FamiljenHellstrom
            .WhenTurnsOn(s =>
                {
                    logger.LogInformation("Calendar event: {Event}", s.New?.Attributes?.Message);               
                    if (s.New?.Attributes?.Message is not null && s.New.Attributes.Message.Contains("sopor"))
                    {
                        tts.Speak("media_player.huset", "Viktigt meddelande", "google_translate_say"); // Important message
                        if (s.New?.Attributes?.Message is not null)
                            tts.Speak("media_player.huset", s.New.Attributes.Message, "google_translate_say");
                        if (s.New?.Attributes?.Description is not null)
                            tts.Speak("media_player.huset", s.New.Attributes.Description, "google_translate_say");
                    }
                }
            );
    }
}
