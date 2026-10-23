using System.Runtime.InteropServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClientItemPerfectionRecordTests
{
    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 31)] [InlineData(false, 32)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 31)] [InlineData(true, 32)]
    public void BoundedFixedTextWritesOnlyItsOwnLowByteFieldAndClearsUnusedTail(bool signature, int length)
    {
        var raw = Enumerable.Repeat((byte)0x5A, 80).ToArray();
        var record = MemoryMarshal.Read<LegacyNativeClientItemPerfectionRecord>(raw);
        var text = new string('\u0141', length);
        if (signature) record.SpecialText = text; else record.OwnerName = text;
        var after = Bytes(ref record); int offset = signature ? 48 : 16;
        Assert.Equal(raw.AsSpan(0, offset).ToArray(), after.AsSpan(0, offset).ToArray());
        Assert.Equal(raw.AsSpan(offset + 32).ToArray(), after.AsSpan(offset + 32).ToArray());
        Assert.Equal(Enumerable.Repeat((byte)0x41, length), after.AsSpan(offset, length).ToArray());
        Assert.All(after.AsSpan(offset + length, 32 - length).ToArray(), value => Assert.Equal((byte)0, value));
        Assert.Equal(new string('A', length), signature ? record.SpecialText : record.OwnerName);
    }

    [Theory]
    [InlineData(false, 33)] [InlineData(false, 1024)] [InlineData(true, 33)] [InlineData(true, 1024)]
    public void OversizedFixedTextRejectsBeforeAnyRecordByteChanges(bool signature, int length)
    {
        var record = MemoryMarshal.Read<LegacyNativeClientItemPerfectionRecord>(
            Enumerable.Repeat((byte)0x5A, 80).ToArray());
        var before = Bytes(ref record); var text = new string('X', length);
        if (signature) Assert.Throws<ArgumentOutOfRangeException>(() => record.SpecialText = text);
        else Assert.Throws<ArgumentOutOfRangeException>(() => record.OwnerName = text);
        Assert.Equal(before, Bytes(ref record));
    }

    [Fact]
    public void UnterminatedAdjacentFieldsAreReadWithinSeparateThirtyTwoByteBounds()
    {
        var raw = new byte[80]; Array.Fill(raw, (byte)'A', 16, 32); Array.Fill(raw, (byte)'B', 48, 32);
        var record = MemoryMarshal.Read<LegacyNativeClientItemPerfectionRecord>(raw);
        Assert.Equal(new string('A', 32), record.OwnerName); Assert.Equal(new string('B', 32), record.SpecialText);
        Assert.Equal(raw, Bytes(ref record));
    }
}
