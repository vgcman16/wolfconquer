using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

public sealed class LegacyNativeCastPackTests
{
    [Fact]
    public void UInt32BigEndianOverloadsKeepExactBytesAndOffsets()
    {
        var first = new byte[4];
        LegacyNativeCastPack.UInt32_To_BE(0x89ABCDEF, first);
        Assert.Equal(new byte[] { 0x89, 0xAB, 0xCD, 0xEF }, first);
        Assert.Equal(0x89ABCDEFu, LegacyNativeCastPack.BE_To_UInt32(first));
        var offset = Enumerable.Repeat((byte)0xCC, 8).ToArray();
        LegacyNativeCastPack.UInt32_To_BE(0x89ABCDEF, offset, 2);
        Assert.Equal(new byte[] { 0xCC, 0xCC, 0x89, 0xAB, 0xCD, 0xEF, 0xCC, 0xCC }, offset);
        Assert.Equal(0x89ABCDEFu, LegacyNativeCastPack.BE_To_UInt32(offset, 2));
    }

    [Fact]
    public void UInt32LittleEndianOverloadsKeepExactBytesAndOffsets()
    {
        var first = new byte[4];
        LegacyNativeCastPack.UInt32_To_LE(0x89ABCDEF, first);
        Assert.Equal(new byte[] { 0xEF, 0xCD, 0xAB, 0x89 }, first);
        Assert.Equal(0x89ABCDEFu, LegacyNativeCastPack.LE_To_UInt32(first));
        var offset = Enumerable.Repeat((byte)0xCC, 8).ToArray();
        LegacyNativeCastPack.UInt32_To_LE(0x89ABCDEF, offset, 2);
        Assert.Equal(new byte[] { 0xCC, 0xCC, 0xEF, 0xCD, 0xAB, 0x89, 0xCC, 0xCC }, offset);
        Assert.Equal(0x89ABCDEFu, LegacyNativeCastPack.LE_To_UInt32(offset, 2));
    }

    [Fact]
    public void UInt64BigEndianOverloadsComposeHighWordBeforeLowWord()
    {
        var first = new byte[8];
        LegacyNativeCastPack.UInt64_To_BE(0x0123456789ABCDEFul, first);
        Assert.Equal(Convert.FromHexString("0123456789ABCDEF"), first);
        Assert.Equal(0x0123456789ABCDEFul, LegacyNativeCastPack.BE_To_UInt64(first));
        var offset = Enumerable.Repeat((byte)0xCC, 12).ToArray();
        LegacyNativeCastPack.UInt64_To_BE(0x0123456789ABCDEFul, offset, 2);
        Assert.Equal(Convert.FromHexString("CCCC0123456789ABCDEFCCCC"), offset);
        Assert.Equal(0x0123456789ABCDEFul, LegacyNativeCastPack.BE_To_UInt64(offset, 2));
    }

    [Fact]
    public void UInt64LittleEndianOverloadsComposeLowWordBeforeHighWord()
    {
        var first = new byte[8];
        LegacyNativeCastPack.UInt64_To_LE(0x0123456789ABCDEFul, first);
        Assert.Equal(Convert.FromHexString("EFCDAB8967452301"), first);
        Assert.Equal(0x0123456789ABCDEFul, LegacyNativeCastPack.LE_To_UInt64(first));
        var offset = Enumerable.Repeat((byte)0xCC, 12).ToArray();
        LegacyNativeCastPack.UInt64_To_LE(0x0123456789ABCDEFul, offset, 2);
        Assert.Equal(Convert.FromHexString("CCCCEFCDAB8967452301CCCC"), offset);
        Assert.Equal(0x0123456789ABCDEFul, LegacyNativeCastPack.LE_To_UInt64(offset, 2));
    }

    [Fact]
    public void NullArraysKeepNativeDereferenceFaultsWithoutValidation()
    {
        Action[] actions =
        {
            () => LegacyNativeCastPack.UInt32_To_BE(1, null!),
            () => LegacyNativeCastPack.UInt32_To_BE(1, null!, 0),
            () => LegacyNativeCastPack.BE_To_UInt32(null!),
            () => LegacyNativeCastPack.BE_To_UInt32(null!, 0),
            () => LegacyNativeCastPack.BE_To_UInt64(null!),
            () => LegacyNativeCastPack.BE_To_UInt64(null!, 0),
            () => LegacyNativeCastPack.UInt64_To_BE(1, null!),
            () => LegacyNativeCastPack.UInt64_To_BE(1, null!, 0),
            () => LegacyNativeCastPack.UInt32_To_LE(1, null!),
            () => LegacyNativeCastPack.UInt32_To_LE(1, null!, 0),
            () => LegacyNativeCastPack.LE_To_UInt32(null!),
            () => LegacyNativeCastPack.LE_To_UInt32(null!, 0),
            () => LegacyNativeCastPack.LE_To_UInt64(null!),
            () => LegacyNativeCastPack.LE_To_UInt64(null!, 0),
            () => LegacyNativeCastPack.UInt64_To_LE(1, null!),
            () => LegacyNativeCastPack.UInt64_To_LE(1, null!, 0),
        };
        foreach (var action in actions) Assert.Throws<NullReferenceException>(action);
    }

    [Fact]
    public void ShortUInt32WritesKeepEarlierByteCommits()
    {
        var big = new byte[3];
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeCastPack.UInt32_To_BE(0x11223344, big));
        Assert.Equal(Convert.FromHexString("112233"), big);
        var little = new byte[3];
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeCastPack.UInt32_To_LE(0x11223344, little));
        Assert.Equal(Convert.FromHexString("443322"), little);
        var offset = new byte[3];
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeCastPack.UInt32_To_BE(0x11223344, offset, 1));
        Assert.Equal(new byte[] { 0, 0x11, 0x22 }, offset);
    }

    [Fact]
    public void ShortUInt64WritesRetainTheCompleteFirstWordAndSecondWordPrefix()
    {
        var big = new byte[6];
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeCastPack.UInt64_To_BE(0x1122334455667788ul, big));
        Assert.Equal(Convert.FromHexString("112233445566"), big);
        var little = new byte[6];
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeCastPack.UInt64_To_LE(0x1122334455667788ul, little));
        Assert.Equal(Convert.FromHexString("887766554433"), little);
    }

    [Fact]
    public void NegativeAndShortReadsKeepManagedIndexFaultsAndDoNotMutateInput()
    {
        var input = new byte[] { 1, 2, 3 };
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeCastPack.BE_To_UInt32(input));
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeCastPack.LE_To_UInt32(input, -1));
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeCastPack.BE_To_UInt64(input, int.MaxValue));
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeCastPack.LE_To_UInt64(input));
        Assert.Equal(new byte[] { 1, 2, 3 }, input);
    }
}
