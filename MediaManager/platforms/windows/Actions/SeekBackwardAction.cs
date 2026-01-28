using System;
using BarRaider.SdTools;
using Newtonsoft.Json.Linq;

namespace CurrentMedia.Actions;

[PluginActionId("ru.valentderah.current-media.media-backward")]
public class SeekBackwardAction : KeypadBase
{
    public SeekBackwardAction(ISDConnection connection, InitialPayload payload) : base(connection, payload)
    {
        _ = MediaSessionManager.Instance.InitializeAsync();
    }

    public override void Dispose()
    {
        Logger.Instance.LogMessage(TracingLevel.INFO, "SeekBackwardAction disposed");
    }

    public override async void KeyPressed(KeyPayload payload)
    {
        try
        {
            await MediaSessionManager.Instance.SeekBackwardAsync();
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error seeking backward: {ex.Message}");
        }
    }

    public override void KeyReleased(KeyPayload payload) { }

    public override void OnTick() { }

    public override void ReceivedSettings(ReceivedSettingsPayload payload) { }

    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }
}
