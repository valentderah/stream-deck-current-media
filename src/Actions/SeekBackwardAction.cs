using System;
using BarRaider.SdTools;

namespace CurrentMedia.Actions;

[PluginActionId("ru.valentderah.current-media.media-backward")]
public class SeekBackwardAction : KeypadBase
{
    private readonly SeekActionSettings _settings;

    public SeekBackwardAction(ISDConnection connection, InitialPayload payload) : base(connection, payload)
    {
        if (payload.Settings == null || payload.Settings.Count == 0)
        {
            _settings = SeekActionSettings.CreateDefault();
        }
        else
        {
            _settings = payload.Settings.ToObject<SeekActionSettings>() ?? SeekActionSettings.CreateDefault();
        }

        _ = MediaManagerProvider.Instance.InitializeAsync();
    }

    public override void Dispose()
    {
        Logger.Instance.LogMessage(TracingLevel.INFO, "SeekBackwardAction disposed");
    }

    public override async void KeyPressed(KeyPayload payload)
    {
        try
        {
            await MediaManagerProvider.Instance.SeekByAsync(-SeekSeconds.Normalize(_settings.Seconds));
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error seeking backward: {ex.Message}");
        }
    }

    public override void KeyReleased(KeyPayload payload) { }

    public override void OnTick() { }

    public override void ReceivedSettings(ReceivedSettingsPayload payload)
    {
        Tools.AutoPopulateSettings(_settings, payload.Settings);
    }

    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }
}
