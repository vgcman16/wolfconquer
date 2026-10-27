using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// New preparation API: corrected-source only, entirely managed byte arrays.
public sealed class LegacyNativePreparedNinjaFrameTests
{
    [Theory]
    [InlineData(1028, null, 1032)]
    [InlineData(1020, "TQServer", 1024)]
    public void NativeSendLimitIncludesTheOptionalSeal(int bytes, string? seal, int declared)
    {
        var frame = LegacyNativePreparedPacketFrames.Frame(2395, new byte[bytes], seal);
        Assert.Equal(1032, frame.Length);
        Assert.Equal(declared, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(frame));
        Assert.Equal((ushort)2395, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
    }

    [Theory]
    [InlineData(1029, null)]
    [InlineData(1021, "TQServer")]
    public void UnsendablePreparedFramesRefuseBeforeNativeStorage(int bytes, string? seal) =>
        Assert.Throws<InvalidDataException>(() => LegacyNativePreparedPacketFrames.Frame(2395, new byte[bytes], seal));

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("123456789")]
    public void InvalidSealLengthIsNeverRepaired(string seal) =>
        Assert.Throws<InvalidDataException>(() => LegacyNativePreparedPacketFrames.Frame(2101, [], seal));

    [Theory]
    [InlineData(0, "0A00800808111202087B")]
    [InlineData(1, "0C00800808111204087B1001")]
    [InlineData(255, "0D00800808111205087B10FF01")]
    public void BadgePreparationRetainsTypedDefaultOmission(int badge, string hex) =>
        Assert.Equal(Convert.FromHexString(hex), LegacyNativeMsgUpdate.PrepareNinjaBadgeFrame(17, (byte)badge, null));

    [Fact]
    public void RemovalPreparationRetainsTheTypedAction109Contract() =>
        Assert.Equal(Convert.FromHexString("1400FE07081118A4A8014800606D680070007800"),
            LegacyNativeActionPacket.PrepareSpellRemovalFrame(17, 21540, null));

    [Fact]
    public void ExplicitSealUsesItsCapturedUtf8PrefixWithoutGlobalReads() =>
        Assert.Equal(Convert.FromHexString("04003508E9BE994142434445"),
            LegacyNativePreparedPacketFrames.Frame(2101, [], "龙ABCDEFG"));

    [Fact]
    public void MissingPayloadRefusesWithoutProducingAFrame() =>
        Assert.Throws<ArgumentNullException>(() => LegacyNativePreparedPacketFrames.Frame(2101, null!, null));
}
