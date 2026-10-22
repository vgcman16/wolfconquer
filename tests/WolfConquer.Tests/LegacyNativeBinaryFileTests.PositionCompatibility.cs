using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeBinaryFileTests
{
    // These source-shaped advancement controls intentionally expose a possible
    // modern FileStream.SafeFileHandle getter resynchronization. They are not
    // weakened to accept repeated first-record reads or overwritten writes.
    [Fact]
    public unsafe void SequentialReadsAdvanceThroughActualFileRatherThanRestartingAtZero()
    {
        using var owned = new OwnedFiles();
        var file = owned.Open(owned.Seed([1, 2, 3, 4]));
        byte* buffer = stackalloc byte[2];
        Assert.True(file.Read(buffer, 2));
        Assert.Equal(new byte[] { 1, 2 }, new Span<byte>(buffer, 2).ToArray());
        Assert.True(file.Read(buffer, 2));
        Assert.Equal(new byte[] { 3, 4 }, new Span<byte>(buffer, 2).ToArray());
        buffer[0] = 0xA5;
        Assert.False(file.Read(buffer, 1));
        Assert.Equal((byte)0xA5, buffer[0]);
    }

    [Fact]
    public unsafe void SequentialWritesAdvanceThroughActualFileRatherThanOverwritingFirstBytes()
    {
        using var owned = new OwnedFiles();
        var path = owned.PathFor("first.bin");
        var file = owned.Open(path, FileMode.CreateNew);
        byte* buffer = stackalloc byte[2];
        buffer[0] = 1; buffer[1] = 2;
        Assert.True(file.Write(buffer, 2));
        buffer[0] = 3; buffer[1] = 4;
        Assert.True(file.Write(buffer, 2));
        file.Close();
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(path));
    }

    [Fact]
    public unsafe void PositionGetterReportsAdvancedKernelPositionAndSetterSeeksAbsolutely()
    {
        using var owned = new OwnedFiles();
        var file = owned.Open(owned.Seed([1, 2, 3, 4]));
        var handle = Stream(file).SafeFileHandle;
        byte* buffer = stackalloc byte[2];
        Assert.True(file.Read(buffer, 2));
        Assert.Equal(2, LegacyNativeKernel32File.SetFilePointer(handle, 0, null, SeekOrigin.Current));
        Assert.Equal(2, file.Position);
        file.Position = 1;
        Assert.Equal(1, file.Position);
        Assert.True(file.Read(buffer, 1));
        Assert.Equal((byte)2, buffer[0]);
    }

    [Fact]
    public unsafe void PositionBeyondEndDoesNotExtendUntilActualWriteThenLeavesZeroGap()
    {
        using var owned = new OwnedFiles();
        var path = owned.Seed([0x11, 0x22]);
        var file = owned.Open(path);
        file.Position = 5;
        Assert.Equal(5, file.Position);
        Assert.Equal(2, Stream(file).Length);
        byte* buffer = stackalloc byte[1];
        buffer[0] = 0x77;
        Assert.True(file.Write(buffer, 1));
        file.Close();
        Assert.Equal(new byte[] { 0x11, 0x22, 0, 0, 0, 0x77 }, File.ReadAllBytes(path));
    }

    [Fact]
    public unsafe void InvalidNegativeSeekResultIsIgnoredRatherThanThrownOrPublishedAsPosition()
    {
        using var owned = new OwnedFiles();
        var file = owned.Open(owned.Seed([1]));
        var handle = Stream(file).SafeFileHandle;
        file.Position = -1;
        Assert.Equal(0, LegacyNativeKernel32File.SetFilePointer(handle, 0, null, SeekOrigin.Current));
        Assert.Equal(0, file.Position);
        Assert.True(file.Success);
    }
}
