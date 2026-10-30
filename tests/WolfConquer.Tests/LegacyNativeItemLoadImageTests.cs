using System.Buffers.Binary;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeItemLoadImageTests
{
    [Fact]
    public void CompleteEmptyEnvelopeHasNoRowsAndDoesNotNeedRuntimeOwners()
    {
        var bytes = new byte[8];
        var image = LegacyNativeItemLoadImage.Parse(bytes);

        Assert.Empty(image.Roots);
        Assert.Empty(image.Perfections);
        Assert.Equal(new byte[8], bytes);
    }

    [Fact]
    public void MixedImageRetainsRootRoutingSeparatelyFromEveryImmediateChild()
    {
        var root = Row(11, 410009, 2, 4, 100);
        var first = Row(12, 750000, 0, 0, 0);
        var second = Row(13, 600000, 0, 212, 999);
        var last = Row(21, 420009, 0, 5, 0);
        var perfection = Perfection(13, 7);
        var otherPerfection = Perfection(21, 23);
        var bytes = Image([(root, [first, second]), (last, [])], perfection, otherPerfection);

        var image = LegacyNativeItemLoadImage.Parse(bytes);

        Assert.Equal(2, image.Roots.Count);
        Assert.Equal(root, Raw(image.Roots[0].Record));
        Assert.Equal(first, Raw(image.Roots[0].Deposits[0]));
        Assert.Equal(second, Raw(image.Roots[0].Deposits[1]));
        Assert.Equal(last, Raw(image.Roots[1].Record));
        Assert.Empty(image.Roots[1].Deposits);
        Assert.Equal(new uint[] { 13, 21 }, image.Perfections.Select(row => row.ItemUID));
        Assert.Equal(perfection, Raw(image.Perfections[0]));
        Assert.Equal(otherPerfection, Raw(image.Perfections[1]));
    }

    [Fact]
    public void CompleteStorageAndPaddingRemainRawWithoutStatOrDateConversion()
    {
        var row = Row(uint.MaxValue, 203009, 0, ushort.MaxValue, uint.MaxValue);
        row[28] = 255; // Plus: no loader normalization here.
        row[29] = 255; // Bless.
        row[31] = 255; // Enchant.
        BinaryPrimitives.WriteInt64LittleEndian(row.AsSpan(128), long.MinValue);
        BinaryPrimitives.WriteInt64LittleEndian(row.AsSpan(168), long.MaxValue);
        var perfection = Perfection(uint.MaxValue, 41); // Arbitrary fixed buffers, no string getters.

        var image = LegacyNativeItemLoadImage.Parse(Image([(row, [])], perfection));

        Assert.Equal(row, Raw(image.Roots[0].Record));
        Assert.Equal(perfection, Raw(image.Perfections[0]));
        Assert.Equal(255, image.Roots[0].Record.Plus);
        Assert.Equal(long.MinValue, image.Roots[0].Record.EffectAddedOn);
        Assert.Equal(long.MaxValue, image.Roots[0].Record.Expiration);
    }

    [Fact]
    public void Filtered205901RootStillConsumesAndRetainsItsChildrenAndLateRows()
    {
        var filtered = Row(31, 205901, 1, 0, 0);
        var child = Row(32, 750000, 0, 0, 0);
        var last = Row(33, 410009, 0, 6, 0);
        var perfection = Perfection(32, 11);

        var image = LegacyNativeItemLoadImage.Parse(Image([(filtered, [child]), (last, [])], perfection));

        Assert.Equal(205901u, image.Roots[0].Record.ITEM_ID);
        Assert.Equal(child, Raw(Assert.Single(image.Roots[0].Deposits)));
        Assert.Equal(last, Raw(image.Roots[1].Record));
        Assert.Equal(perfection, Raw(Assert.Single(image.Perfections)));
    }

    [Fact]
    public void PerfectionRowsCanReferenceRealRootsAndImmediateChildrenInAnyPostludeOrder()
    {
        var first = Row(41, 410009, 1);
        var child = Row(42, 420009, 0);
        var other = Row(43, 430009, 0);
        var childPerfection = Perfection(42, 17);
        var laterRootPerfection = Perfection(43, 19);
        var rootPerfection = Perfection(41, 23);

        var image = LegacyNativeItemLoadImage.Parse(Image([(first, [child]), (other, [])],
            childPerfection, laterRootPerfection, rootPerfection));

        Assert.Equal(41u, image.Roots[0].Record.UID);
        Assert.Equal(42u, image.Roots[0].Deposits[0].UID);
        Assert.Equal(43u, image.Roots[1].Record.UID);
        Assert.Equal(childPerfection, Raw(image.Perfections[0]));
        Assert.Equal(laterRootPerfection, Raw(image.Perfections[1]));
        Assert.Equal(rootPerfection, Raw(image.Perfections[2]));
    }

    [Fact]
    public void PerfectionOnlyImageRefusesAnOrphanWithoutRepairOrInputMutation()
    {
        var row = Perfection(77, 59);
        var bytes = Image([], row);
        var before = bytes.ToArray();

        Assert.Throws<InvalidDataException>(() => LegacyNativeItemLoadImage.Parse(bytes));
        Assert.Equal(before, bytes);
    }

    [Fact]
    public void ImageOwnsValueCopiesWithoutChangingOrRetainingTheInput()
    {
        var root = Row(51, 410009, 1);
        var child = Row(52, 420009, 0);
        var perfection = Perfection(52, 71);
        var bytes = Image([(root, [child])], perfection);
        var before = bytes.ToArray();

        var image = LegacyNativeItemLoadImage.Parse(bytes);
        Assert.Equal(before, bytes);
        Array.Fill(bytes, (byte)0);

        Assert.Equal(root, Raw(image.Roots[0].Record));
        Assert.Equal(child, Raw(image.Roots[0].Deposits[0]));
        Assert.Equal(perfection, Raw(image.Perfections[0]));
        var rootCopy = image.Roots[0].Record;
        rootCopy.UID = 0;
        var childCopy = image.Roots[0].Deposits[0];
        childCopy.UID = 0;
        var perfectionCopy = image.Perfections[0];
        perfectionCopy.ItemUID = 0;
        Assert.Equal(51u, image.Roots[0].Record.UID);
        Assert.Equal(52u, image.Roots[0].Deposits[0].UID);
        Assert.Equal(52u, image.Perfections[0].ItemUID);
    }

    [Fact]
    public void ReadOnlyCollectionsDoNotExposeMutableBackingArrays()
    {
        var image = LegacyNativeItemLoadImage.Parse(
            Image([(Row(61, 410009, 1), [Row(62, 420009, 0)])], Perfection(62, 83)));
        var roots = Assert.IsAssignableFrom<IList<LegacyNativeItemLoadImage.Root>>(image.Roots);
        var deposits = Assert.IsAssignableFrom<IList<LegacyNativeClientItemRecord>>(image.Roots[0].Deposits);
        var perfections = Assert.IsAssignableFrom<IList<LegacyNativeClientItemPerfectionRecord>>(image.Perfections);

        Assert.True(roots.IsReadOnly);
        Assert.True(deposits.IsReadOnly);
        Assert.True(perfections.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => roots[0] = roots[0]);
        Assert.Throws<NotSupportedException>(() => deposits[0] = default);
        Assert.Throws<NotSupportedException>(() => perfections[0] = default);
        Assert.Equal(62u, image.Roots[0].Deposits[0].UID);
        Assert.Equal(62u, image.Perfections[0].ItemUID);
    }

    [Fact]
    public void ParserUsesOnlyTheSuppliedSliceInsideLargerOwnedStorage()
    {
        var row = Row(71, 410009, 0);
        var bytes = Image([(row, [])]);
        var storage = Enumerable.Repeat((byte)0xCC, bytes.Length + 15).ToArray();
        bytes.CopyTo(storage, 7);
        var before = storage.ToArray();

        var image = LegacyNativeItemLoadImage.Parse(storage.AsSpan(7, bytes.Length));

        Assert.Equal(row, Raw(Assert.Single(image.Roots).Record));
        Assert.Equal(before, storage);
    }

    private static byte[] Row(uint uid, uint itemId, uint deposits,
        ushort position = 0, uint warehouse = 0)
    {
        var row = Enumerable.Range(0, 184).Select(index => unchecked((byte)(index * 37 + 11))).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(row, uid);
        BinaryPrimitives.WriteUInt32LittleEndian(row.AsSpan(4), itemId);
        BinaryPrimitives.WriteUInt16LittleEndian(row.AsSpan(12), position);
        BinaryPrimitives.WriteUInt32LittleEndian(row.AsSpan(56), warehouse);
        BinaryPrimitives.WriteUInt32LittleEndian(row.AsSpan(136), deposits);
        return row;
    }

    private static byte[] Perfection(uint uid, int seed)
    {
        var row = Enumerable.Range(0, 80).Select(index => unchecked((byte)(index * 11 + seed))).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(row, uid);
        return row;
    }

    private static byte[] Image((byte[] Record, byte[][] Deposits)[] roots, params byte[][] perfections)
    {
        using var image = new MemoryStream();
        WriteCount(image, roots.Length);
        foreach (var root in roots)
        {
            image.Write(root.Record);
            foreach (var child in root.Deposits) image.Write(child);
        }
        WriteCount(image, perfections.Length);
        foreach (var perfection in perfections) image.Write(perfection);
        return image.ToArray();
    }

    private static void WriteCount(Stream image, int count)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, count);
        image.Write(bytes);
    }

    private static byte[] Raw<T>(T record) where T : unmanaged =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref record, 1)).ToArray();
}
