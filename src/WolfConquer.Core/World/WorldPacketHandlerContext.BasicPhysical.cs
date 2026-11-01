namespace WolfConquer.Core.World;

public sealed partial record WorldPacketHandlerContext
{
    // Privately registered by the actual Frame dispatch. Constructing an
    // empty token or copying a context cannot create an accepted attempt.
    [System.Text.Json.Serialization.JsonIgnore]
    internal WorldServerFrameHandler.BasicPhysicalRequest? BasicPhysicalRequest { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    internal WorldServerFrameHandler.BasicPhysicalAttempt? BasicPhysicalAttempt { get; init; }
}
