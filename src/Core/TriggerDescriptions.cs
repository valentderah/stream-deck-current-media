using System.Reflection;
using BarRaider.SdTools;
using BarRaider.SdTools.Communication;
using CurrentMedia.Actions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CurrentMedia;

internal static class TriggerDescriptions
{
    private static readonly MethodInfo? RawSend = typeof(StreamDeckConnection).GetMethod(
        "SendAsync",
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        new[] { typeof(string) },
        null);

    private static int _warned;

    public static bool IsAvailable => RawSend != null;

    public static JObject Build(NowPlayingSettings settings, PluginStrings strings) => new()
    {
        ["rotate"] = Describe(RotateKey(settings.DialRotate), strings),
        ["push"] = Describe(ActionKey(settings.DialPress), strings),
        ["touch"] = Describe(ActionKey(settings.TouchTap), strings),
        ["longTouch"] = Describe(ActionKey(settings.TouchLongTap), strings)
    };

    public static Task SendAsync(ISDConnection connection, JObject descriptions)
    {
        if (RawSend == null)
        {
            if (Interlocked.Exchange(ref _warned, 1) == 0)
            {
                Logger.Instance.LogMessage(
                    TracingLevel.WARN,
                    "StreamDeckConnection.SendAsync(string) not found, trigger descriptions stay static");
            }

            return Task.CompletedTask;
        }

        var message = new JObject
        {
            ["event"] = "setTriggerDescription",
            ["context"] = connection.ContextId,
            ["payload"] = descriptions
        };

        return RawSend.Invoke(connection.StreamDeckConnection, new object[] { message.ToString(Formatting.None) }) as Task
               ?? Task.CompletedTask;
    }

    private static string Describe(string key, PluginStrings strings) => key.Length == 0 ? "" : strings.Get(key);

    private static string RotateKey(DialRotateMode mode) => mode switch
    {
        DialRotateMode.Seek => "SeekInTrack",
        DialRotateMode.Track => "SwitchTrack",
        _ => ""
    };

    private static string ActionKey(ActionType action) => action switch
    {
        ActionType.Toggle => "PlayPause",
        ActionType.Next => "NextTrack",
        ActionType.Previous => "PreviousTrack",
        ActionType.Forward => "SeekForward",
        ActionType.Backward => "SeekBackward",
        _ => ""
    };
}
