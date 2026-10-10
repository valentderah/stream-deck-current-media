using BarRaider.SdTools;

namespace CurrentMedia.Actions;

[PluginActionId("ru.valentderah.current-media.media-backward")]
public class SeekBackwardAction : MediaCommandAction
{
    public SeekBackwardAction(ISDConnection connection, InitialPayload payload) : base(connection, payload)
    {
    }

    protected override ActionType Command => ActionType.Backward;

    protected override string FailureMessage => "Error seeking backward";
}
