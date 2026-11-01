using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// DATA-only feasibility controls. No Frame, binding, query epoch or gameplay
// permission is constructed by these tests.
public sealed class LegacyInitialWorldEntryQueryDataTests
{
    [Fact]
    public void EmptyEquipmentDataPinsExactCandidateAndPreservesOpaqueCaseVariants()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal) { ["Other"] = "UP", ["other"] = "low" };
        var candidate = Candidate(fields); var catalog = OwnedEquipmentBehaviorFixture.Catalog();
        Assert.True(WorldPacketHandlers.InitialWorldEntryQueryData.TryPrepare(candidate, catalog, new Clock(20_000), out var prepared));
        var data = Assert.IsType<WorldPacketHandlers.InitialWorldEntryQueryData>(prepared);
        Assert.True(data.IsCurrentFor(candidate, catalog));
        Assert.False(data.IsCurrentFor(candidate with { }, catalog));
        Assert.Null(candidate.NativeGameClient);
        Assert.Equal("UP", fields["Other"]); Assert.Equal("low", fields["other"]);
        fields["Other"] = "changed";
        Assert.False(data.IsCurrentFor(candidate, catalog));
        Assert.Null(candidate.EquipmentHitpointsStatus);
    }

    [Fact]
    public void ActualInterserverHistoryDependencyAndUnrepresentableStatsRefuseDataPreparation()
    {
        var catalog = OwnedEquipmentBehaviorFixture.Catalog(); var clock = new Clock(20_000);
        var interserver = Candidate(new Dictionary<string, string> { ["OnInterServer"] = "1" });
        Assert.False(WorldPacketHandlers.InitialWorldEntryQueryData.TryPrepare(interserver, catalog, clock, out _));
        var normal = Candidate(new Dictionary<string, string>());
        var overflow = normal with { Character = normal.Character with { Spirit = long.MaxValue } };
        Assert.False(WorldPacketHandlers.InitialWorldEntryQueryData.TryPrepare(overflow, catalog, clock, out _));
        Assert.Null(interserver.NativeGameClient); Assert.Null(overflow.NativeGameClient);
    }

    [Fact]
    public void CurrentnessRepeatsFineRainSignedClockDecisionWithoutPublishingAStamp()
    {
        var candidate = Candidate(new Dictionary<string, string> { ["StatusFlags"] = "0/0/0/0/0/0/1048576" });
        candidate = candidate with { FineRainEquipmentAdmission = new(candidate.Character.Uid, 10_000) };
        var catalog = OwnedEquipmentBehaviorFixture.Catalog(); var clock = new Clock(20_000);
        Assert.True(WorldPacketHandlers.InitialWorldEntryQueryData.TryPrepare(candidate, catalog, clock, out var prepared));
        var data = Assert.IsType<WorldPacketHandlers.InitialWorldEntryQueryData>(prepared);
        Assert.True(data.IsCurrentFor(candidate, catalog));
        clock.Now = 12_000;
        Assert.False(data.IsCurrentFor(candidate, catalog));
        Assert.Equal(10_000, candidate.FineRainEquipmentAdmission!.StampMilliseconds);
        Assert.Null(candidate.EquipmentHitpointsStatus);
    }

    private static WorldSession Candidate(Dictionary<string, string> fields) => new(77, "data-only",
        OwnedEquipmentBehaviorFixture.Character(classCode: 1000, level: 100, minHitPoints: 300) with { Attributes = fields },
        PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty);

    private sealed class Clock(int now) : ILegacySignedMillisecondClock
    {
        internal int Now = now;
        public int GetMilliseconds() => Now;
    }
}
