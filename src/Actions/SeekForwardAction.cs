using System;
using BarRaider.SdTools;

namespace CurrentMedia.Actions;

[PluginActionId("ru.valentderah.current-media.media-forward")]
public class SeekForwardAction : KeypadBase
{
    private readonly SeekActionSettings _settings;

    public SeekForwardAction(ISDConnection connection, InitialPayload payload) : base(connection, payload)
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
        Logger.Instance.LogMessage(TracingLevel.INFO, "SeekForwardAction disposed");
    }

    public override async void KeyPressed(KeyPayload payload)
    {
        try
        {
            await MediaManagerProvider.Instance.SeekByAsync(SeekSeconds.Normalize(_settings.Seconds));
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error seeking forward: {ex.Message}");
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
