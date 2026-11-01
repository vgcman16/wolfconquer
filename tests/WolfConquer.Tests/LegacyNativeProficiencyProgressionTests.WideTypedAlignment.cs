using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Actual constructor-owned native component/control alignment; this is not a
// Frame admission, saved-file transaction, live socket or gameplay acceptance.
public sealed partial class LegacyNativeProficiencyProgressionTests
{
    [Theory]
    [InlineData(0u, 0u, uint.MaxValue, (ushort)0, 20u, 0u, 3_200_000_000u)]
    [InlineData(4u, 123u, 42_949_673u, (ushort)0, 14u, 239_503_583u, 320_625_000u)]
    [InlineData(5u, uint.MaxValue - 3, 60u, (ushort)0, 18u, 1_071_039_177u, 1_623_164_062u)]
    [InlineData(5u, 4_261_412_963u, 201_326_598u, (ushort)128, 18u, 1_113_988_945u, 1_623_164_062u)]
    [InlineData(4u, 0u, uint.MaxValue, ushort.MaxValue, 20u, 0u, 3_200_000_000u)]
    public Task TypedAndActualNativeSubtractThresholdsFromTheSameWidenedGain(uint level, uint initial,
        uint raw, ushort violet, uint expectedLevel, uint expectedExperience, uint expectedNeeded) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var original = scope.Record(level: level, experience: initial, needed: 991);
            scope.Client.Gems[5] = violet;
            var typedBefore = new PlayerProficiencySnapshotEntry(410, level, 0, initial, 991);
            var typed = LegacyProficiencyPacket.ApplyExperience(typedBefore, raw, violet);
            scope.Storage.CheckUpdate(410, raw, scope.Packet);
            AssertRecord(original, 410, level, initial, 991);
            AssertRecord(scope.Storage.ClientProf[410], 410, expectedLevel, expectedExperience, 991);
            Assert.Equal(new PlayerProficiencySnapshotEntry(410, expectedLevel, 0, expectedExperience, expectedNeeded), typed.Proficiency);
            Assert.Equal(new PlayerProficiencySnapshotEntry(410, level, 0, initial, 991), typedBefore);
            Assert.True(typed.Updated); Assert.True(typed.LevelChanged);
            var nativeFrame = Assert.Single(scope.Queue, bytes => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)) == 2041);
            Assert.Equal(Proficiency(410, expectedLevel, expectedExperience, expectedNeeded), nativeFrame);
            Assert.Equal(nativeFrame, typed.Packet.ToFrameBytes());
            Assert.Equal(Experience(expectedExperience, expectedNeeded, 410), scope.Queue.Last());
        });

    [Fact]
    public void ThePublicUintSummaryDoesNotClipActualProgressionWithMaximalTypedBonus()
    {
        var before = new PlayerProficiencySnapshotEntry(410, 0, 0, 0, 0);
        var result = LegacyProficiencyPacket.ApplyExperience(before, uint.MaxValue, uint.MaxValue);
        Assert.Equal(uint.MaxValue, result.AppliedExperience); // Existing bounded diagnostic API only.
        Assert.Equal(new PlayerProficiencySnapshotEntry(410, 20, 0, 0, 3_200_000_000), result.Proficiency);
        Assert.Equal(Proficiency(410, 20, 0, 3_200_000_000), result.Packet.ToFrameBytes());
        Assert.Equal(new PlayerProficiencySnapshotEntry(410, 0, 0, 0, 0), before);
    }
}
