using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeBinaryFileTests
{
    [Fact]
    public void ColdOwnerAndMissingOpenPreserveNativeFailureContract()
    {
        using var owned = new OwnedFiles();
        var file = owned.Track(new LegacyNativeBinaryFile());
        Assert.False(file.Success);
        Assert.False(file.Open(owned.PathFor("missing.bin"), FileMode.Open));
        Assert.False(file.Success);
        Assert.Null(StreamField.GetValue(file));
        Assert.Equal("File isn't open/failed to open previously", Assert.Throws<IOException>(() => file.Close()).Message);
        Assert.Throws<IOException>(() => _ = file.Position);
        Assert.Throws<IOException>(() => file.Position = 0);
        Assert.Throws<IOException>(() => ReadBytes(file, 0));
        Assert.Throws<IOException>(() => WriteBytes(file, 0));
    }

    [Fact]
    public void FilenameConstructorRunsActualOpenAndRetainsDefaultReadWriteExclusiveStream()
    {
        using var owned = new OwnedFiles();
        var path = owned.PathFor("first.bin");
        var file = owned.Track(new LegacyNativeBinaryFile(path, FileMode.CreateNew));
        Assert.True(file.Success);
        Assert.True(Stream(file).CanRead);
        Assert.True(Stream(file).CanWrite);
        StillLocked(path);
        file.Close();
        Assert.True(file.Success);
        Exclusive(path);
    }

    [Fact]
    public void CloseIsIdempotentButSuccessAndDisposedStreamRemainStale()
    {
        using var owned = new OwnedFiles();
        var file = owned.Open(owned.Seed([1]));
        var stream = Stream(file);
        file.Close();
        file.Close();
        Assert.True(file.Success);
        Assert.Same(stream, StreamField.GetValue(file));
        Assert.False(stream.CanRead);
        Assert.Throws<ObjectDisposedException>(() => _ = file.Position);
        Assert.Throws<ObjectDisposedException>(() => file.Position = 0);
        Assert.Throws<ObjectDisposedException>(() => ReadBytes(file, 0));
        Assert.Throws<ObjectDisposedException>(() => WriteBytes(file, 0));
        Assert.Equal("AmountOfBytes", Assert.Throws<ArgumentException>(() => ReadBytes(file, -1)).Message);
        Assert.Equal("AmountOfBytes", Assert.Throws<ArgumentException>(() => WriteBytes(file, -1)).Message);
    }

    [Fact]
    public void ReopenOnColdOwnerFailsAtCloseWithoutCreatingDestination()
    {
        using var owned = new OwnedFiles();
        var path = owned.PathFor("first.bin");
        var file = owned.Track(new LegacyNativeBinaryFile());
        Assert.Throws<IOException>(() => file.Reopen(path, FileMode.CreateNew));
        Assert.False(File.Exists(path));
        Assert.False(file.Success);
    }

    [Fact]
    public void ReopenClosesPreviousStreamBeforeOpeningDestination()
    {
        using var owned = new OwnedFiles();
        var first = owned.Seed([1]);
        var second = owned.Seed([2], "second.bin");
        var file = owned.Open(first);
        var previous = Stream(file);
        Assert.True(file.Reopen(second, FileMode.Open));
        owned.Capture(file);
        Assert.False(previous.CanRead);
        Assert.NotSame(previous, Stream(file));
        Exclusive(first);
        StillLocked(second);
    }

    [Fact]
    public void FailedReopenClosesOldStreamAndClearsSuccess()
    {
        using var owned = new OwnedFiles();
        var path = owned.Seed([1]);
        var file = owned.Open(path);
        var previous = Stream(file);
        Assert.False(file.Reopen(owned.PathFor("missing.bin"), FileMode.Open));
        Assert.False(file.Success);
        Assert.Null(StreamField.GetValue(file));
        Assert.False(previous.CanRead);
        Exclusive(path);
    }

    [Fact]
    public void SuccessfulOpenWithoutCloseAbandonsButDoesNotClosePreviousStream()
    {
        using var owned = new OwnedFiles();
        var first = owned.Seed([1]);
        var second = owned.Seed([2], "second.bin");
        var file = owned.Open(first);
        var previous = Stream(file);
        Assert.True(file.Open(second, FileMode.Open));
        owned.Capture(file);
        Assert.NotSame(previous, Stream(file));
        Assert.True(previous.CanRead);
        file.Close();
        StillLocked(first);
        Exclusive(second);
        // OwnedFiles retains the real abandoned stream for explicit cleanup.
    }

    [Fact]
    public void FailedOpenWithoutCloseAbandonsPreviousStreamThenCloseCannotReachIt()
    {
        using var owned = new OwnedFiles();
        var path = owned.Seed([1]);
        var file = owned.Open(path);
        var previous = Stream(file);
        Assert.False(file.Open(path, FileMode.Open)); // Native exclusive share conflict.
        Assert.False(file.Success);
        Assert.Null(StreamField.GetValue(file));
        Assert.True(previous.CanRead);
        StillLocked(path);
        Assert.Throws<IOException>(() => file.Close());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(42)]
    public void InvalidFileModesAreCaughtByOpenRatherThanEscaping(int mode)
    {
        using var owned = new OwnedFiles();
        var file = owned.Track(new LegacyNativeBinaryFile(owned.PathFor("first.bin"), (FileMode)mode));
        Assert.False(file.Success);
        Assert.Null(StreamField.GetValue(file));
    }

    [Fact]
    public unsafe void KernelImportsRetainExactNativeSignaturesAndErrorMetadata()
    {
        var type = typeof(LegacyNativeKernel32File);
        var pointer = type.GetMethod(nameof(LegacyNativeKernel32File.SetFilePointer))!;
        Assert.Equal(typeof(int), pointer.ReturnType);
        Assert.Equal(new[] { typeof(SafeFileHandle), typeof(int), typeof(int*), typeof(SeekOrigin) },
            pointer.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        var pointerImport = pointer.GetCustomAttribute<DllImportAttribute>()!;
        Assert.Equal("kernel32.dll", pointerImport.Value);
        Assert.Equal("SetFilePointer", pointerImport.EntryPoint);
        Assert.False(pointerImport.SetLastError);
        foreach (var name in new[] { "ReadFile", "WriteFile" })
        {
            var method = type.GetMethod(name)!;
            Assert.Equal(typeof(int), method.ReturnType);
            Assert.Equal(new[] { typeof(SafeFileHandle), typeof(byte*), typeof(int), typeof(int*), typeof(IntPtr) },
                method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
            var import = method.GetCustomAttribute<DllImportAttribute>()!;
            Assert.Equal("kernel32.dll", import.Value);
            Assert.Equal(name, import.EntryPoint);
            Assert.True(import.SetLastError);
        }
    }
}
