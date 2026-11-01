using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFirstUseProficiencyCalculationTests
{
    [Fact]
    public void ZeroThresholdPromotesTheRealNativeLevelZeroCaseWithoutConsumingItsExperience()
    {
        // Same pinned level/XP and notification threshold as actual native
        // CheckUpdate: one raw point scales to100, promotes to1 and subtracts
        // zero. The typed snapshot retains its existing current-needed-field
        // contract rather than the native component's stale saved777 field.
        var original = new PlayerProficiencySnapshotEntry(410, 0, 0, 0, 777);
        var result = LegacyProficiencyPacket.ApplyExperience(original, rawExperience: 1);
        Assert.True(result.Updated); Assert.True(result.LevelChanged); Assert.Equal(100U, result.AppliedExperience);
        Assert.Equal(new PlayerProficiencySnapshotEntry(410, 1, 0, 100, 1200), result.Proficiency);
        Assert.Equal(Wire(1, 100, 1200), result.Packet.ToFrameBytes());
        Assert.Equal(new PlayerProficiencySnapshotEntry(410, 0, 0, 0, 777), original);
    }

    [Fact]
    public void ActualBasicRateAndNativeThresholdsProduceTheSecondHitLevelEightGolden()
    {
        var before = new PlayerProficiencySnapshot([new(410, 0, 0, 0, 0)]);
        var calculation = new WorldPacketHandlers.BasicPhysicalProficiencyCalculation(before, new uint[] { 410 }, 300);
        Assert.True(calculation.HasChanges); Assert.Equal(300_000U, calculation.RatedExperience);
        Assert.Equal(new PlayerProficiencySnapshotEntry(410, 8, 0, 13_440_800, 22_000_000),
            Assert.Single(calculation.After.Proficiencies));
        Assert.Equal(Wire(8, 13_440_800, 22_000_000), Assert.Single(calculation.Frames).ToArray());
        Assert.Equal(new PlayerProficiencySnapshotEntry(410, 0, 0, 0, 0), Assert.Single(before.Proficiencies));
    }

    [Fact]
    public void NativePreviousLevelBeyondTheCapRefusesAtItsActualJumpInsteadOfPublishingLevelTwentyOne()
    {
        var before = new PlayerProficiencySnapshotEntry(410, 0, 21, 0, 0);
        Assert.Throws<InvalidDataException>(() => LegacyProficiencyPacket.ApplyExperience(before, 1_000_000));
        Assert.Equal(new PlayerProficiencySnapshotEntry(410, 0, 21, 0, 0), before);
    }

    [Fact]
    public void SaturatedExperienceStillStopsAtTheRealMaximumLevel()
    {
        var before = new PlayerProficiencySnapshotEntry(410, 19, 0, uint.MaxValue, 2_100_000_000);
        var result = LegacyProficiencyPacket.ApplyExperience(before, uint.MaxValue);
        Assert.True(result.Updated); Assert.True(result.LevelChanged);
        Assert.Equal(new PlayerProficiencySnapshotEntry(410, 20, 0, 0, 3_200_000_000), result.Proficiency);
        Assert.Equal(Wire(20, 0, 3_200_000_000), result.Packet.ToFrameBytes());
        Assert.Equal(uint.MaxValue, before.Experience);
    }

    private static byte[] Wire(uint level, uint experience, uint needed)
    {
        var bytes = new byte[20]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 20);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2041);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 410);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), level);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), experience);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), needed);
        return bytes;
    }
}
