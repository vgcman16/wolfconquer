namespace WolfConquer.Core.World;

public sealed partial record WorldPacketHandlerContext
{
    // A copied context cannot register a request or a prepared execution.
    [System.Text.Json.Serialization.JsonIgnore]
    internal WorldServerFrameHandler.OwnedDragonhowlRequest? OwnedDragonhowl { get; init; }
}
