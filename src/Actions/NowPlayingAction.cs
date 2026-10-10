using System;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using BarRaider.SdTools.Wrappers;
using CurrentMedia.Imaging;
using Newtonsoft.Json.Linq;

namespace CurrentMedia.Actions;

[PluginActionId("ru.valentderah.current-media.media-info")]
public class NowPlayingAction : KeyAndEncoderBase
{
    private const string EncoderController = "Encoder";
    private static readonly TimeSpan TrackStepInterval = TimeSpan.FromMilliseconds(250);

    private readonly NowPlayingSettings _settings;
    private readonly NowPlayingKeyView? _keyView;
    private readonly NowPlayingDialView? _dialView;
    private readonly SeekAccumulator? _seek;
    private readonly PluginStrings? _strings;
    private static readonly TimeSpan KeyHoldThreshold = TimeSpan.FromMilliseconds(500);

    private readonly DialPressTracker _press = new();
    private readonly KeyHoldTracker _hold = new();
    private readonly TrackStepThrottle _trackSteps = new(TrackStepInterval, TimeProvider.System);
    private readonly SeekSeries _seekSeries = new(TimelineTracker.HoldDuration, TimeProvider.System);
    private MediaState _currentMediaState = new();
    private Timer? _holdTimer;

    public NowPlayingAction(ISDConnection connection, InitialPayload payload) : base(connection, payload)
    {
        _settings = NowPlayingSettings.FromPayload(payload.Settings);

        if (payload.Controller == EncoderController)
        {
            _dialView = new NowPlayingDialView(
                feedback => Connection.SetFeedbackAsync(feedback),
                _settings,
                TimeProvider.System);
            _seek = new SeekAccumulator(
                FlushDialSeekAsync,
                onError: ex => Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error seeking from dial: {ex.Message}"));
            _strings = PluginStrings.Load(
                PluginStrings.FindPluginRoot(AppContext.BaseDirectory),
                payload.DeviceInfo?.Application?.Language);
            MediaManagerProvider.Instance.TimelineChanged += OnTimelineChanged;
            _dialView.Start();
            _ = SendTriggerDescriptionsAsync();
        }
        else
        {
            _keyView = new NowPlayingKeyView(
                image => Connection.SetImageAsync(image),
                title => Connection.SetTitleAsync(title),
                _settings);
            _keyView.Start();
        }

        MediaManagerProvider.Instance.MediaStateChanged += OnMediaStateChanged;
        Connection.OnPropertyInspectorDidAppear += OnPropertyInspectorDidAppear;
        Connection.OnSendToPlugin += OnSendToPlugin;
        _ = InitializeAndUpdateAsync();
    }

    private async Task InitializeAndUpdateAsync()
    {
        await ShowAsync(_currentMediaState);
        await MediaManagerProvider.Instance.InitializeAsync();
        await MediaManagerProvider.Instance.RequestUpdateAsync();
    }

    private async void OnPropertyInspectorDidAppear(object? sender, SDEventReceivedEventArgs<BarRaider.SdTools.Events.PropertyInspectorDidAppear> e)
    {
        await MediaManagerProvider.Instance.RequestUpdateAsync();
    }

    public override void Dispose()
    {
        MediaManagerProvider.Instance.MediaStateChanged -= OnMediaStateChanged;
        MediaManagerProvider.Instance.TimelineChanged -= OnTimelineChanged;
        Connection.OnPropertyInspectorDidAppear -= OnPropertyInspectorDidAppear;
        Connection.OnSendToPlugin -= OnSendToPlugin;
        Interlocked.Exchange(ref _holdTimer, null)?.Dispose();
        _hold.Up();
        _seek?.Dispose();
        _keyView?.Dispose();
        _dialView?.Dispose();
        Logger.Instance.LogMessage(TracingLevel.INFO, "NowPlayingAction disposed");
    }

    private async void OnMediaStateChanged(object? sender, MediaState state)
    {
        try
        {
            _currentMediaState = state;
            await ShowAsync(state);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error handling media state change: {ex.Message}");
        }
    }

    private async void OnTimelineChanged(object? sender, MediaTimeline timeline)
    {
        try
        {
            if (_dialView != null)
            {
                await _dialView.OnTimelineAsync(timeline);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error handling timeline change: {ex.Message}");
        }
    }

    public override async void KeyPressed(KeyPayload payload)
    {
        try
        {
            if (_settings.KeyLongPress == ActionType.None)
            {
                await RunKeyCommandAsync(_settings.Action);
                return;
            }

            var timer = new Timer(OnKeyHeld, _hold.Down(), KeyHoldThreshold, Timeout.InfiniteTimeSpan);
            Interlocked.Exchange(ref _holdTimer, timer)?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error on key press: {ex.Message}");
        }
    }

    public override async void KeyReleased(KeyPayload payload)
    {
        try
        {
            Interlocked.Exchange(ref _holdTimer, null)?.Dispose();
            if (_hold.Up())
            {
                await RunKeyCommandAsync(_settings.Action);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error on key release: {ex.Message}");
        }
    }

    private async void OnKeyHeld(object? press)
    {
        try
        {
            if (_hold.Hold((int)press!))
            {
                await RunKeyCommandAsync(_settings.KeyLongPress);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error on key long press: {ex.Message}");
        }
    }

    private Task RunKeyCommandAsync(ActionType action) =>
        MediaCommand.RunAsync(MediaManagerProvider.Instance, action, SeekSeconds.Normalize(_settings.Seconds));

    public override async void DialRotate(DialRotatePayload payload)
    {
        try
        {
            _press.Rotated();
            var mode = payload.IsDialPressed ? _settings.DialPressRotate : _settings.DialRotate;
            switch (mode)
            {
                case DialRotateMode.Seek:
                    SeekFromDial(payload.Ticks * DialSeekSeconds);
                    break;
                case DialRotateMode.Track:
                    var step = _trackSteps.Take(payload.Ticks);
                    if (step > 0)
                    {
                        await MediaManagerProvider.Instance.NextAsync();
                    }
                    else if (step < 0)
                    {
                        await MediaManagerProvider.Instance.PreviousAsync();
                    }

                    break;
                case DialRotateMode.None:
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error on dial rotate: {ex.Message}");
        }
    }

    public override void DialDown(DialPayload payload)
    {
        _press.Down();
    }

    public override async void DialUp(DialPayload payload)
    {
        try
        {
            if (_press.Up())
            {
                await RunDialCommandAsync(_settings.DialPress);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error on dial press: {ex.Message}");
        }
    }

    public override async void TouchPress(TouchpadPressPayload payload)
    {
        try
        {
            if (payload.IsLongPress)
            {
                await RunDialCommandAsync(_settings.TouchLongTap);
                return;
            }

            var target = TouchZones.SeekTarget(payload.TapPosition, _settings.TapSeek, _dialView?.Duration ?? 0);
            if (target == null)
            {
                await RunDialCommandAsync(_settings.TouchTap);
                return;
            }

            if (_dialView != null)
            {
                _ = _dialView.JumpAsync(target.Value);
            }

            _seekSeries.Next();
            await MediaManagerProvider.Instance.SeekToAsync(target.Value);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error on touch: {ex.Message}");
        }
    }

    public override void OnTick() { }

    public override void ReceivedSettings(ReceivedSettingsPayload payload)
    {
        Tools.AutoPopulateSettings(_settings, payload.Settings);
        _ = ApplySettingsAsync();
    }

    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

    private int DialSeekSeconds => SeekSeconds.Normalize(_settings.DialSeekSeconds, SeekSeconds.DialDefault);

    private Task RunDialCommandAsync(ActionType action)
    {
        switch (action)
        {
            case ActionType.Forward:
                SeekFromDial(DialSeekSeconds);
                return Task.CompletedTask;
            case ActionType.Backward:
                SeekFromDial(-DialSeekSeconds);
                return Task.CompletedTask;
            default:
                return MediaCommand.RunAsync(MediaManagerProvider.Instance, action, DialSeekSeconds);
        }
    }

    private void SeekFromDial(int seconds)
    {
        if (_dialView == null || _seek == null || seconds == 0 || _dialView.Duration <= 0)
        {
            return;
        }

        _ = _dialView.ShiftAsync(seconds);
        _seek.Add(seconds);
    }

    private Task FlushDialSeekAsync(int seconds)
    {
        if (_dialView != null && _seekSeries.Next())
        {
            return MediaManagerProvider.Instance.SeekToAsync(_dialView.Position);
        }

        return MediaManagerProvider.Instance.SeekByAsync(seconds);
    }

    private Task ShowAsync(MediaState state)
    {
        if (_dialView != null)
        {
            return _dialView.ShowAsync(state);
        }

        return _keyView?.ShowAsync(state) ?? Task.CompletedTask;
    }

    private async Task ApplySettingsAsync()
    {
        try
        {
            if (_dialView != null)
            {
                await _dialView.ApplySettingsAsync();
                await SendTriggerDescriptionsAsync();
            }
            else if (_keyView != null)
            {
                await _keyView.ApplySettingsAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error applying settings: {ex.Message}");
        }
    }

    private async Task SendTriggerDescriptionsAsync()
    {
        if (_strings == null)
        {
            return;
        }

        try
        {
            await TriggerDescriptions.SendAsync(Connection, TriggerDescriptions.Build(_settings, _strings));
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error sending trigger descriptions: {ex.Message}");
        }
    }

    private void OnSendToPlugin(object? sender, SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
    {
        if (e.Event.Payload?["event"]?.ToString() == "pickIdleImage")
        {
            _ = PickIdleImageAsync();
        }
    }

    private async Task PickIdleImageAsync()
    {
        try
        {
            var path = await Task.Run(() => NativeIdleImagePicker.Pick());
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var png = await Task.Run(() => IdleImageHelper.TryLoadFromFile(path));
            if (png == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Could not load idle image from {path}");
                return;
            }

            _settings.IdleImage = png;
            _settings.IdleImageName = Path.GetFileName(path);
            await Connection.SetSettingsAsync(JObject.FromObject(_settings));
            await ApplySettingsAsync();
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error picking idle image: {ex.Message}");
        }
    }
}
