namespace WolfConquer.Core.World;

public sealed partial record WorldPacketHandlerContext
{
    [System.Text.Json.Serialization.JsonIgnore]
    internal LegacyRuneRankingPublicationOwner? RuneRankingPublicationOwner { get; init; }
}
