using BarRaider.SdTools;

namespace CurrentMedia.Actions;

[PluginActionId("ru.valentderah.current-media.media-forward")]
public class SeekForwardAction : MediaCommandAction
{
    public SeekForwardAction(ISDConnection connection, InitialPayload payload) : base(connection, payload)
    {
    }

    protected override ActionType Command => ActionType.Forward;

    protected override string FailureMessage => "Error seeking forward";
}
