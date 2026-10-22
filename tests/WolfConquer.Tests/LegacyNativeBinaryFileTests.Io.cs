using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeBinaryFileTests
{
    [Fact]
    public unsafe void ActualReadAdvancesKernelPointerAndPreservesUnrequestedBufferTail()
    {
        using var owned = new OwnedFiles();
        var file = owned.Open(owned.Seed([0x11, 0x22, 0x33, 0x44]));
        var handle = Stream(file).SafeFileHandle; // Observe actual OS pointer without a later stream getter.
        byte* buffer = stackalloc byte[8];
        new Span<byte>(buffer, 8).Fill(0xA5);
        Assert.True(file.Read(buffer, 3));
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5 }, new Span<byte>(buffer, 8).ToArray());
        Assert.Equal(3, LegacyNativeKernel32File.SetFilePointer(handle, 0, null, SeekOrigin.Current));
    }

    [Fact]
    public unsafe void ShortReadReturnsFalseButPublishesAvailablePrefixWithoutClearingTail()
    {
        using var owned = new OwnedFiles();
        var file = owned.Open(owned.Seed([0x11, 0x22]));
        var handle = Stream(file).SafeFileHandle;
        byte* buffer = stackalloc byte[8];
        new Span<byte>(buffer, 8).Fill(0xA5);
        Assert.False(file.Read(buffer, 5));
        Assert.Equal(new byte[] { 0x11, 0x22, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5 }, new Span<byte>(buffer, 8).ToArray());
        Assert.Equal(2, LegacyNativeKernel32File.SetFilePointer(handle, 0, null, SeekOrigin.Current));
    }

    [Fact]
    public unsafe void EmptyReadReturnsFalseForPositiveCountAndKeepsEntireCallerBuffer()
    {
        using var owned = new OwnedFiles();
        var file = owned.Open(owned.Seed([]));
        byte* buffer = stackalloc byte[8];
        new Span<byte>(buffer, 8).Fill(0xA5);
        Assert.False(file.Read(buffer, 8));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 8).ToArray(), new Span<byte>(buffer, 8).ToArray());
        Assert.True(file.Read(buffer, 0));
    }

    [Fact]
    public unsafe void ReadFromActualAppendOnlyHandleIgnoresBoolAndReturnsCountMismatch()
    {
        using var owned = new OwnedFiles();
        var file = owned.Open(owned.Seed([1, 2]), FileMode.Append);
        Assert.False(Stream(file).CanRead);
        byte* buffer = stackalloc byte[2];
        buffer[0] = 0xA5;
        buffer[1] = 0x5A;
        Assert.False(file.Read(buffer, 1));
        Assert.Equal((byte)0xA5, buffer[0]);
        Assert.Equal((byte)0x5A, buffer[1]);
    }

    [Fact]
    public unsafe void SingleWritePublishesExactlyRequestedBytesThenActualReopenReadsThem()
    {
        using var owned = new OwnedFiles();
        var path = owned.PathFor("first.bin");
        var file = owned.Open(path, FileMode.CreateNew);
        var handle = Stream(file).SafeFileHandle;
        byte* buffer = stackalloc byte[8];
        for (var index = 0; index < 8; index++) buffer[index] = (byte)(index + 1);
        Assert.True(file.Write(buffer, 2, 3));
        Assert.Equal(6, LegacyNativeKernel32File.SetFilePointer(handle, 0, null, SeekOrigin.Current));
        Assert.True(file.Reopen(path, FileMode.Open));
        owned.Capture(file);
        new Span<byte>(buffer, 8).Fill(0xA5);
        Assert.True(file.Read(buffer, 2, 3));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 0xA5, 0xA5 }, new Span<byte>(buffer, 8).ToArray());
        file.Close();
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NegativeAmountsFailBeforeColdOwnerAndLeaveStateUntouched(int amount)
    {
        using var owned = new OwnedFiles();
        var file = owned.Track(new LegacyNativeBinaryFile());
        Assert.Equal("AmountOfBytes", Assert.Throws<ArgumentException>(() => ReadBytes(file, amount)).Message);
        Assert.Equal("AmountOfBytes", Assert.Throws<ArgumentException>(() => WriteBytes(file, amount)).Message);
        Assert.False(file.Success);
        Assert.Null(StreamField.GetValue(file));
    }

    [Theory]
    [InlineData(65536, 65536, 0)]
    [InlineData(int.MinValue, 2, 0)]
    [InlineData(int.MaxValue, int.MaxValue, 1)]
    [InlineData(-1, -1, 1)]
    [InlineData(2, 2, 4)]
    public void CountTimesSizeUsesUncheckedSignedProductWithoutIndependentOperandGuards(int count, int size, int expectedBytes)
    {
        using var owned = new OwnedFiles();
        Assert.Equal(expectedBytes, unchecked(count * size));
        var first = owned.Seed([1, 2, 3, 4]);
        var input = owned.Open(first);
        Assert.True(SizedRead(input, count, size));
        var output = owned.Open(owned.PathFor("second.bin"), FileMode.CreateNew);
        Assert.True(SizedWrite(output, count, size));
        output.Close();
        Assert.Equal(Enumerable.Repeat((byte)0x5A, expectedBytes).ToArray(), File.ReadAllBytes(owned.PathFor("second.bin")));
    }

    [Theory]
    [InlineData(int.MaxValue, 2)]
    [InlineData(int.MinValue, 1)]
    public void NegativeWrappedProductsReachSameAmountValidationBeforeOwnerCheck(int count, int size)
    {
        using var owned = new OwnedFiles();
        var file = owned.Track(new LegacyNativeBinaryFile());
        Assert.True(unchecked(count * size) < 0);
        Assert.Equal("AmountOfBytes", Assert.Throws<ArgumentException>(() => SizedRead(file, count, size)).Message);
        Assert.Equal("AmountOfBytes", Assert.Throws<ArgumentException>(() => SizedWrite(file, count, size)).Message);
    }
}
