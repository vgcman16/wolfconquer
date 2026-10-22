using System.Reflection;
using Microsoft.Win32.SafeHandles;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeBinaryFileTests
{
    private static readonly FieldInfo CachedHandleField = typeof(LegacyNativeBinaryFile)
        .GetField("fileHandle", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static SafeFileHandle? CachedHandle(LegacyNativeBinaryFile file) =>
        (SafeFileHandle?)CachedHandleField.GetValue(file);

    [Fact]
    public void HandleRemainsLazyThroughOpenSuccessAndNegativeAmountValidation()
    {
        using var owned = new OwnedFiles();
        var file = owned.Open(owned.Seed([1, 2]));
        Assert.True(file.Success);
        Assert.Null(CachedHandle(file));
        Assert.Throws<ArgumentException>(() => ReadBytes(file, -1));
        Assert.Throws<ArgumentException>(() => WriteBytes(file, -1));
        Assert.Throws<ArgumentException>(() => SizedRead(file, int.MaxValue, 2));
        Assert.Throws<ArgumentException>(() => SizedWrite(file, int.MaxValue, 2));
        Assert.Null(CachedHandle(file));
        Assert.Equal(0, file.Position);
        var handle = Assert.IsType<SafeFileHandle>(CachedHandle(file));
        Assert.False(handle.IsClosed);
        Assert.Same(Stream(file).SafeFileHandle, handle); // Both pointers are still zero here.
        Assert.Same(handle, CachedHandle(file));
    }

    [Fact]
    public unsafe void PositionReadAndWriteShareOneActualAdvancingHandle()
    {
        using var owned = new OwnedFiles();
        var path = owned.Seed([1, 2, 3, 4]);
        var file = owned.Open(path);
        file.Position = 1;
        var handle = Assert.IsType<SafeFileHandle>(CachedHandle(file));
        byte* buffer = stackalloc byte[4];
        Assert.True(file.Read(buffer, 1));
        Assert.Equal((byte)2, buffer[0]);
        Assert.Equal(2, file.Position);
        buffer[0] = 9;
        Assert.True(file.Write(buffer, 1));
        Assert.Equal(3, file.Position);
        Assert.Equal(3, LegacyNativeKernel32File.SetFilePointer(handle, 0, null, SeekOrigin.Current));
        file.Position = 0;
        Assert.True(file.Read(buffer, 4));
        Assert.Equal(new byte[] { 1, 2, 9, 4 }, new Span<byte>(buffer, 4).ToArray());
        Assert.Same(handle, CachedHandle(file));
        file.Close();
        Assert.True(handle.IsClosed);
        Assert.Equal(new byte[] { 1, 2, 9, 4 }, File.ReadAllBytes(path));
    }

    [Fact]
    public void ZeroByteIoStillAcquiresHandleAndClosedCacheReentersDisposedStreamGetter()
    {
        using var owned = new OwnedFiles();
        var file = owned.Open(owned.Seed([]));
        Assert.True(ReadBytes(file, 0));
        var handle = Assert.IsType<SafeFileHandle>(CachedHandle(file));
        Assert.True(WriteBytes(file, 0));
        Assert.Same(handle, CachedHandle(file));
        file.Close();
        Assert.True(file.Success);
        Assert.True(handle.IsClosed);
        Assert.Throws<ArgumentException>(() => ReadBytes(file, -1));
        Assert.Throws<ArgumentException>(() => WriteBytes(file, -1));
        Assert.Throws<ObjectDisposedException>(() => ReadBytes(file, 0));
        Assert.Throws<ObjectDisposedException>(() => WriteBytes(file, 0));
        Assert.Throws<ObjectDisposedException>(() => _ = file.Position);
        Assert.Throws<ObjectDisposedException>(() => file.Position = 0);
        Assert.Same(handle, CachedHandle(file));
    }

    [Fact]
    public void CloseBeforeFirstIoStaysLazyUntilFirstDisposedHandleFault()
    {
        using var owned = new OwnedFiles();
        var file = owned.Open(owned.Seed([]));
        var stream = Stream(file);
        file.Close();
        Assert.True(file.Success);
        Assert.Null(CachedHandle(file));
        Assert.Throws<ObjectDisposedException>(() => ReadBytes(file, 0));
        Assert.Throws<ObjectDisposedException>(() => WriteBytes(file, 0));
        Assert.Throws<ObjectDisposedException>(() => _ = file.Position);
        // Actual .NET9 getter returns its closed handle; the native P/Invoke
        // raises the disposed fault. Native BinaryFile has no cache field.
        var closed = Assert.IsType<SafeFileHandle>(CachedHandle(file));
        Assert.True(closed.IsClosed);
        Assert.Same(stream.SafeFileHandle, closed);
        Assert.Same(stream, StreamField.GetValue(file));
    }

    [Fact]
    public unsafe void ReopenSuccessClosesOldHandleAndDefersAcquiringNewStreamHandle()
    {
        using var owned = new OwnedFiles();
        var first = owned.Seed([1, 2]);
        var second = owned.Seed([7, 8], "second.bin");
        var file = owned.Open(first);
        Assert.True(ReadBytes(file, 1));
        var old = Assert.IsType<SafeFileHandle>(CachedHandle(file));
        Assert.True(file.Reopen(second, FileMode.Open));
        owned.Capture(file);
        Assert.True(old.IsClosed);
        Assert.Null(CachedHandle(file));
        byte* buffer = stackalloc byte[2];
        Assert.True(file.Read(buffer, 2));
        Assert.Equal(new byte[] { 7, 8 }, new Span<byte>(buffer, 2).ToArray());
        Assert.NotSame(old, CachedHandle(file));
        Assert.False(CachedHandle(file)!.IsClosed);
        Exclusive(first);
    }

    [Fact]
    public unsafe void OpenSuccessResetsCacheWithoutClosingAbandonedStreamOrOldHandle()
    {
        using var owned = new OwnedFiles();
        var first = owned.Seed([1, 2]);
        var second = owned.Seed([7, 8], "second.bin");
        var file = owned.Open(first);
        Assert.True(ReadBytes(file, 1));
        var old = Assert.IsType<SafeFileHandle>(CachedHandle(file));
        var abandoned = Stream(file);
        Assert.True(file.Open(second, FileMode.Open));
        owned.Capture(file);
        Assert.Null(CachedHandle(file));
        Assert.False(old.IsClosed);
        Assert.True(abandoned.CanRead);
        byte* buffer = stackalloc byte[2];
        Assert.True(file.Read(buffer, 2));
        Assert.Equal(new byte[] { 7, 8 }, new Span<byte>(buffer, 2).ToArray());
        Assert.NotSame(old, CachedHandle(file));
        file.Close();
        Assert.False(old.IsClosed);
        StillLocked(first);
        Exclusive(second);
    }

    [Fact]
    public unsafe void FailedOpenResetsCacheLosesOwnerAndNeverFallsBackToAbandonedHandle()
    {
        using var owned = new OwnedFiles();
        var first = owned.Seed([1, 2]);
        var second = owned.Seed([7, 8], "second.bin");
        var file = owned.Open(first);
        Assert.True(ReadBytes(file, 1));
        var old = Assert.IsType<SafeFileHandle>(CachedHandle(file));
        Assert.False(file.Open(owned.PathFor("missing.bin"), FileMode.Open));
        Assert.Null(CachedHandle(file));
        Assert.Null(StreamField.GetValue(file));
        Assert.False(file.Success);
        Assert.False(old.IsClosed);
        Assert.Throws<IOException>(() => ReadBytes(file, 0));
        Assert.Throws<IOException>(() => WriteBytes(file, 0));
        Assert.Throws<IOException>(() => _ = file.Position);
        Assert.True(file.Open(second, FileMode.Open));
        owned.Capture(file);
        Assert.Null(CachedHandle(file));
        byte* buffer = stackalloc byte[2];
        Assert.True(file.Read(buffer, 2));
        Assert.Equal(new byte[] { 7, 8 }, new Span<byte>(buffer, 2).ToArray());
        Assert.NotSame(old, CachedHandle(file));
        Assert.False(old.IsClosed);
        StillLocked(first);
    }

    [Fact]
    public void FailedReopenClearsCacheAfterClosingOldHandleAndKeepsColdOwnerIoFault()
    {
        using var owned = new OwnedFiles();
        var path = owned.Seed([1]);
        var file = owned.Open(path);
        Assert.True(ReadBytes(file, 1));
        var old = Assert.IsType<SafeFileHandle>(CachedHandle(file));
        Assert.False(file.Reopen(owned.PathFor("missing.bin"), FileMode.Open));
        Assert.True(old.IsClosed);
        Assert.False(file.Success);
        Assert.Null(CachedHandle(file));
        Assert.Throws<IOException>(() => ReadBytes(file, 0));
        Assert.Throws<IOException>(() => WriteBytes(file, 0));
        Assert.Throws<IOException>(() => _ = file.Position);
        Assert.Null(CachedHandle(file));
        Exclusive(path);
    }
}
