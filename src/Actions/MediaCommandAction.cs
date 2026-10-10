using System;
using BarRaider.SdTools;

namespace CurrentMedia.Actions;

public abstract class MediaCommandAction : KeypadBase
{
    private readonly SeekActionSettings _settings;

    protected MediaCommandAction(ISDConnection connection, InitialPayload payload) : base(connection, payload)
    {
        _settings = SeekActionSettings.FromPayload(payload.Settings);

        _ = MediaManagerProvider.Instance.InitializeAsync();
    }

    protected abstract ActionType Command { get; }

    protected abstract string FailureMessage { get; }

    public override void Dispose()
    {
        Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType().Name} disposed");
    }

    public override async void KeyPressed(KeyPayload payload)
    {
        try
        {
            await MediaCommand.RunAsync(
                MediaManagerProvider.Instance,
                Command,
                SeekSeconds.Normalize(_settings.Seconds));
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"{FailureMessage}: {ex.Message}");
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
