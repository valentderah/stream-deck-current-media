using BarRaider.SdTools;

namespace CurrentMedia.Actions;

[PluginActionId("ru.valentderah.current-media.media-play-pause")]
public class PlayPauseAction : MediaCommandAction
{
    public PlayPauseAction(ISDConnection connection, InitialPayload payload) : base(connection, payload)
    {
    }

    protected override ActionType Command => ActionType.Toggle;

    protected override string FailureMessage => "Error toggling play/pause";
}
