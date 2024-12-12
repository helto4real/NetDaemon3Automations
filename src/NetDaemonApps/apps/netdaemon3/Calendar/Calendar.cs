/// <summary>
///     Pushes important calendar events to TTS.
/// </summary>
[NetDaemonApp]
public class GoogleCalendarManager
{
    public GoogleCalendarManager(Entities entities, ITextToSpeechService tts, ILogger<GoogleCalendarManager> logger)
    {
        entities.Calendar.FamiljenHellstrom
            .WhenTurnsOn(s =>
                {
                    logger.LogInformation("Calendar event: {Event}", s.New?.Attributes?.Message);               
                    if (s.New?.Attributes?.Message is not null && s.New.Attributes.Message.Contains("sopor"))
                    {
                        tts.Speak("media_player.huset", "Viktigt meddelande", "cloud_say"); // Important message
                        if (s.New?.Attributes?.Message is not null)
                            tts.Speak("media_player.huset", s.New.Attributes.Message, "cloud_say");
                        if (s.New?.Attributes?.Description is not null)
                            tts.Speak("media_player.huset", s.New.Attributes.Description, "cloud_say");
                    }
                }
            );
    }
}
