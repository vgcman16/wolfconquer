namespace WolfConquer.Core.World;

public sealed partial record WorldPacketHandlerContext
{
    [System.Text.Json.Serialization.JsonIgnore]
    internal WorldSessionStore.OwnedHawksOutputReceipt? OwnedHawksOutput { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    internal WorldSessionStore.OwnedHawksOutputPhase? OwnedHawksPhase { get; init; }
}
