using BarRaider.SdTools;

namespace CurrentMedia.Actions;

[PluginActionId("ru.valentderah.current-media.media-previous")]
public class PreviousAction : MediaCommandAction
{
    public PreviousAction(ISDConnection connection, InitialPayload payload) : base(connection, payload)
    {
    }

    protected override ActionType Command => ActionType.Previous;

    protected override string FailureMessage => "Error skipping to previous track";
}
