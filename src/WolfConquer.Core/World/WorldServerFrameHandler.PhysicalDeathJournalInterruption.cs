namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal LegacyJournalInterruption? PhysicalDeathJournalInterruption { get; set; }
}

public sealed partial record WorldPacketHandlerContext
{
    [System.Text.Json.Serialization.JsonIgnore]
    internal LegacyJournalInterruption? PhysicalDeathJournalInterruption { get; init; }
}
