using System;
using BarRaider.SdTools;
using Newtonsoft.Json.Linq;

namespace CurrentMedia.Actions;

[PluginActionId("ru.valentderah.current-media.media-play-pause")]
public class PlayPauseAction : KeypadBase
{
    public PlayPauseAction(ISDConnection connection, InitialPayload payload) : base(connection, payload)
    {
        _ = MediaSessionManager.Instance.InitializeAsync();
    }

    public override void Dispose()
    {
        Logger.Instance.LogMessage(TracingLevel.INFO, "PlayPauseAction disposed");
    }

    public override async void KeyPressed(KeyPayload payload)
    {
        try
        {
            await MediaSessionManager.Instance.TogglePlayPauseAsync();
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error toggling play/pause: {ex.Message}");
        }
    }

    public override void KeyReleased(KeyPayload payload) { }

    public override void OnTick() { }

    public override void ReceivedSettings(ReceivedSettingsPayload payload) { }

    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }
}
