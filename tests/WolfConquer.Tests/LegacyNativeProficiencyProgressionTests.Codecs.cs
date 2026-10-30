using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeProficiencyProgressionTests
{
    [Fact]
    public void ThresholdLookupMatchesEveryNativeRowAndClampsByteInputsAboveTwenty()
    {
        Assert.Equal(21, Thresholds.Length);
        for (int level = 0; level < Thresholds.Length; level++)
            Assert.Equal(Thresholds[level], LegacyNativeBaseFunc.ProficiencyLevelExperience((byte)level));
        Assert.Equal(3200000000u, LegacyNativeBaseFunc.ProficiencyLevelExperience(21));
        Assert.Equal(3200000000u, LegacyNativeBaseFunc.ProficiencyLevelExperience(byte.MaxValue));
    }

    [Fact]
    public Task ProficiencyCodecUsesLiteralFieldOrderRecomputesNeededAndReaderStartsAtCallerCursor() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Assert.Same(scope.Packet, scope.Packet.ProficiencyCreate(0x01020304, 6, 0x11121314, uint.MaxValue));
            byte[] expected = Convert.FromHexString("1400F90704030201060000001413121100093D00");
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position);
            Assert.All(scope.Memory(36).Skip(20), value => Assert.Equal((byte)0xA5, value));
            scope.Packet.GetProficiency(out var read);
            AssertRecord(read, 0x01020304, 6, 0x11121314, 4000000);
            Assert.Equal(20, scope.Packet.Position);
        });

    [Theory]
    [InlineData(256u, 0u)]
    [InlineData(uint.MaxValue, 3200000000u)]
    public Task ProficiencyCodecNarrowsOnlyThresholdLookupNotTheLevelWord(uint level, uint needed) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Packet.ProficiencyCreate(410, level, 123, 17);
            Assert.Equal(Proficiency(410, level, 123, needed), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position);
        });

    [Fact]
    public Task ExperienceCodecUsesSuppliedNeededWordZeroesEightBytesAndReturnsOriginalStream() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Assert.Same(scope.Packet, scope.Packet.UpdateProfExperienceCreate(0x01020304, 0x11121314, 0x21222324));
            Assert.Equal(Convert.FromHexString("180097080403020114131211242322210000000000000000"),
                NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position);
            Assert.All(scope.Memory(40).Skip(24), value => Assert.Equal((byte)0xA5, value));
        });

    [Fact]
    public void MissingProficiencyPacketPreservesTheCallersExistingMessage()
    {
        var old = new LegacyNativeMsgProficiency { ID = 17, PreviouseLevel = 99 };
        var result = old;
        Assert.Throws<ArgumentNullException>(() => LegacyNativeProficiencyPacket.GetProficiency(null!, out result));
        Assert.Same(old, result);
        AssertRecord(result, 17, 0, 0, 0, 99);
    }

    [Fact]
    public Task IncompleteProficiencyRecordPreservesTheWholeCallerMessageAndInput() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            byte[] fragment = Convert.FromHexString("04030201");
            scope.Place(16378, fragment); scope.Packet.Size = 16378 + fragment.Length;
            scope.Packet.Seek(16378);
            byte[] before = scope.Memory(scope.Packet.Capacity);
            var old = new LegacyNativeMsgProficiency { ID = 17, PreviouseLevel = 99 }; var read = old;
            Assert.Throws<EndOfStreamException>(() => scope.Packet.GetProficiency(out read));
            Assert.Same(old, read); AssertRecord(read, 17, 0, 0, 0, 99);
            Assert.Equal(16378, scope.Packet.Position); Assert.Equal(16382, scope.Packet.Size);
            Assert.Equal(before, scope.Memory(scope.Packet.Capacity));
        });

    [Fact]
    public Task NullNativeThresholdTableFaultsBeforeCodecResetsWriterOrTouchesCallerBuffer() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Scope.Table.SetValue(null, null);
            Assert.Throws<NullReferenceException>(() => scope.Packet.ProficiencyCreate(410, 1, 2, 3));
            AssertUntouched(scope);
        });
}
