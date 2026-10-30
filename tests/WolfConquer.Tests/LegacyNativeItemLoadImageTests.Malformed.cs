using System.Buffers.Binary;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeItemLoadImageTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(999u)]
    public void LatePerfectionMustReferenceANonzeroSerializedItemUid(uint invalidUid)
    {
        var bytes = Image([(Row(181, 410009, 1), [Row(182, 420009, 0)])],
            Perfection(181, 109), Perfection(invalidUid, 113));
        var before = bytes.ToArray();

        Assert.Throws<InvalidDataException>(() => LegacyNativeItemLoadImage.Parse(bytes));
        Assert.Equal(before, bytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LateDuplicatePerfectionCannotOverrideEitherARootOrChildRow(bool repeatChild)
    {
        var bytes = Image([(Row(191, 410009, 1), [Row(192, 420009, 0)])],
            Perfection(191, 127), Perfection(192, 131), Perfection(repeatChild ? 192u : 191u, 137));
        var before = bytes.ToArray();

        Assert.Throws<InvalidDataException>(() => LegacyNativeItemLoadImage.Parse(bytes));
        Assert.Equal(before, bytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroUidIsRejectedInEitherARootOrItsImmediateChild(bool zeroChild)
    {
        var bytes = Image([(Row(zeroChild ? 161u : 0u, 410009, 1),
            [Row(zeroChild ? 0u : 162u, 420009, 0)])]);
        var before = bytes.ToArray();

        Assert.Throws<InvalidDataException>(() => LegacyNativeItemLoadImage.Parse(bytes));
        Assert.Equal(before, bytes);
    }

    [Theory]
    [InlineData(0)] // Root/root.
    [InlineData(1)] // Root/child.
    [InlineData(2)] // Child/child.
    [InlineData(3)] // Child/later root.
    public void DuplicateItemUidsAreRejectedAcrossTheCompleteSerializedNamespace(int shape)
    {
        var root = Row(171, 410009, 2);
        var first = Row(shape == 1 ? 171u : 172u, 420009, 0);
        var second = Row(shape == 2 ? 172u : 173u, 430009, 0);
        var later = Row(shape == 0 ? 171u : shape == 3 ? 172u : 174u, 440009, 0);
        var bytes = Image([(root, [first, second]), (later, [])]);
        var before = bytes.ToArray();

        Assert.Throws<InvalidDataException>(() => LegacyNativeItemLoadImage.Parse(bytes));
        Assert.Equal(before, bytes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NegativeRootCountIsNotAcceptedAsAnEmptyImage(int count)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, count);
        var before = bytes.ToArray();

        Assert.Throws<InvalidDataException>(() => LegacyNativeItemLoadImage.Parse(bytes));
        Assert.Equal(before, bytes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NegativeLatePerfectionCountRefusesTheWholeImage(int count)
    {
        var bytes = Image([(Row(81, 410009, 1), [Row(82, 420009, 0)])]);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(bytes.Length - 4), count);
        var before = bytes.ToArray();

        Assert.Throws<InvalidDataException>(() => LegacyNativeItemLoadImage.Parse(bytes));
        Assert.Equal(before, bytes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(11671107)] // A naive count * 184 would overflow Int32.
    [InlineData(int.MaxValue)]
    public void ImpossibleRootCountIsBoundedBeforeAnyRowArrayAllocation(int count)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, count);
        var before = bytes.ToArray();

        Assert.Throws<EndOfStreamException>(() => LegacyNativeItemLoadImage.Parse(bytes));
        Assert.Equal(before, bytes);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(11671107u)]
    [InlineData(uint.MaxValue)]
    public void ImpossibleUnsignedDepositCountIsBoundedBeforeAllocation(uint count)
    {
        var bytes = Image([(Row(91, 410009, count), [])]);
        var before = bytes.ToArray();

        Assert.Throws<EndOfStreamException>(() => LegacyNativeItemLoadImage.Parse(bytes));
        Assert.Equal(before, bytes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(26843546)] // A naive count * 80 would overflow Int32.
    [InlineData(int.MaxValue)]
    public void ImpossibleLatePerfectionCountIsBoundedBeforeAllocation(int count)
    {
        var bytes = Image([(Row(101, 410009, 1), [Row(102, 420009, 0)])]);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(bytes.Length - 4), count);
        var before = bytes.ToArray();

        Assert.Throws<EndOfStreamException>(() => LegacyNativeItemLoadImage.Parse(bytes));
        Assert.Equal(before, bytes);
    }

    [Fact]
    public void EveryTruncatedPrefixOfACompleteMixedImageRefusesWithoutReusingRows()
    {
        var bytes = Image([(Row(111, 410009, 2), [Row(112, 420009, 0), Row(113, 430009, 0)]),
            (Row(114, 440009, 0), [])], Perfection(112, 89), Perfection(114, 97));
        var before = bytes.ToArray();

        for (int length = 0; length < bytes.Length; length++)
        {
            int suppliedLength = length;
            Assert.Throws<EndOfStreamException>(() =>
                LegacyNativeItemLoadImage.Parse(bytes.AsSpan(0, suppliedLength)));
        }

        Assert.Equal(before, bytes);
        Assert.Equal(2, LegacyNativeItemLoadImage.Parse(bytes).Roots.Count);
    }

    [Fact]
    public void DepositCountCannotConsumeAReservedLaterRootOrPostludeCount()
    {
        // Root one claims a child, but only root two and the mandatory count remain.
        var bytes = Image([(Row(121, 410009, 1), []), (Row(122, 420009, 0), [])]);
        var before = bytes.ToArray();

        Assert.Throws<EndOfStreamException>(() => LegacyNativeItemLoadImage.Parse(bytes));
        Assert.Equal(before, bytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChildDeclaringDepositsIsRejectedWithOrWithoutGuessedGrandchildBytes(bool appendGrandchild)
    {
        var children = appendGrandchild
            ? new[] { Row(132, 420009, 1), Row(133, 430009, 0) }
            : new[] { Row(132, 420009, 1) };
        var bytes = Image([(Row(131, 410009, 1), children)]);
        var before = bytes.ToArray();

        Assert.Throws<InvalidDataException>(() => LegacyNativeItemLoadImage.Parse(bytes));
        Assert.Equal(before, bytes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(80)]
    [InlineData(184)]
    public void CompleteEnvelopeDoesNotSilentlyAcceptTrailingBytes(int extra)
    {
        var complete = Image([(Row(141, 410009, 0), [])], Perfection(141, 103));
        var bytes = new byte[complete.Length + extra];
        complete.CopyTo(bytes, 0);
        var before = bytes.ToArray();

        Assert.Throws<InvalidDataException>(() => LegacyNativeItemLoadImage.Parse(bytes));
        Assert.Equal(before, bytes);
    }

    [Fact]
    public void BytesOutsideTheSuppliedSpanCannotRepairATruncatedTail()
    {
        var bytes = Image([(Row(151, 410009, 0), [])], Perfection(151, 107));
        var before = bytes.ToArray();

        Assert.Throws<EndOfStreamException>(() =>
            LegacyNativeItemLoadImage.Parse(bytes.AsSpan(0, bytes.Length - 1)));
        Assert.Equal(before, bytes);
    }
}
