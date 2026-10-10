using BarRaider.SdTools;

namespace CurrentMedia.Actions;

[PluginActionId("ru.valentderah.current-media.media-next")]
public class NextAction : MediaCommandAction
{
    public NextAction(ISDConnection connection, InitialPayload payload) : base(connection, payload)
    {
    }

    protected override ActionType Command => ActionType.Next;

    protected override string FailureMessage => "Error skipping to next track";
}
