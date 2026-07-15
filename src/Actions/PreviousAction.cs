using System;
using BarRaider.SdTools;

namespace CurrentMedia.Actions;

[PluginActionId("ru.valentderah.current-media.media-previous")]
public class PreviousAction : KeypadBase
{
    public PreviousAction(ISDConnection connection, InitialPayload payload) : base(connection, payload)
    {
        _ = MediaManagerProvider.Instance.InitializeAsync();
    }

    public override void Dispose()
    {
        Logger.Instance.LogMessage(TracingLevel.INFO, "PreviousAction disposed");
    }

    public override async void KeyPressed(KeyPayload payload)
    {
        try
        {
            await MediaManagerProvider.Instance.PreviousAsync();
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error skipping to previous track: {ex.Message}");
        }
    }

    public override void KeyReleased(KeyPayload payload) { }

    public override void OnTick() { }

    public override void ReceivedSettings(ReceivedSettingsPayload payload) { }

    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }
}
