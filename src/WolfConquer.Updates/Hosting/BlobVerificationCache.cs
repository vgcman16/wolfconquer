using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace WolfConquer.Updates.Hosting;

/// <summary>
/// Avoids rehashing unchanged Windows blobs. This is not a signed-release check:
/// clients must independently hash every download against their trusted manifest.
/// </summary>
internal sealed class BlobVerificationCache : IDisposable
{
    private const int Capacity = 4096;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<Identity> _verified = [];
    private readonly Queue<Identity> _order = [];
    private long _hashPassCount;
    internal long HashPassCount => Interlocked.Read(ref _hashPassCount);

    internal async Task<bool> VerifyAsync(FeedFile file, CancellationToken token)
    {
        // Serialize cache misses as well as updates, so simultaneous range requests
        // do not each hash the same large bundle. HEAD never enters this gate.
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var before = GetIdentity(file.Stream);
            if (before is { } known && _verified.Contains(known)) return true;
            file.Stream.Position = 0;
            Interlocked.Increment(ref _hashPassCount);
            var hash = await SHA256.HashDataAsync(file.Stream, token).ConfigureAwait(false);
            file.Stream.Position = 0;
            if (!Convert.ToHexString(hash).Equals(file.Hash, StringComparison.OrdinalIgnoreCase)) return false;
            var after = GetIdentity(file.Stream);
            if (before != after) return false;
            if (after is { } identity)
            {
                if (_verified.Count == Capacity) _verified.Remove(_order.Dequeue());
                if (_verified.Add(identity)) _order.Enqueue(identity);
            }
            return true;
        }
        finally { _gate.Release(); }
    }

    private static Identity? GetIdentity(FileStream stream)
    {
        // FileShare.Read holds the exact opened object against writers/deletion on
        // Windows. Non-Windows platforms conservatively rehash each GET instead.
        if (!OperatingSystem.IsWindows()) return null;
        if (!GetFileInformationByHandle(stream.SafeFileHandle, out var info)) throw new IOException();
        var size = ((ulong)info.FileSizeHigh << 32) | info.FileSizeLow;
        if (size > long.MaxValue || (long)size != stream.Length) throw new IOException();
        return new Identity(stream.Name, (long)size,
            ((ulong)info.LastWriteTimeHigh << 32) | info.LastWriteTimeLow,
            info.VolumeSerialNumber, ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow);
    }

    private readonly record struct Identity(string Path, long Length, ulong LastWriteTime, uint Volume, ulong FileIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        internal uint Attributes;
        internal uint CreationTimeLow, CreationTimeHigh;
        internal uint LastAccessTimeLow, LastAccessTimeHigh;
        internal uint LastWriteTimeLow, LastWriteTimeHigh;
        internal uint VolumeSerialNumber;
        internal uint FileSizeHigh, FileSizeLow;
        internal uint NumberOfLinks;
        internal uint FileIndexHigh, FileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);

    public void Dispose() => _gate.Dispose();
}
