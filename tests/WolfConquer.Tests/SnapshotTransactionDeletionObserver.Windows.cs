using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WolfConquer.Tests;

// An explicitly registered, owned Windows directory read, not an asynchronous
// FileSystemWatcher callback whose dispatch can trail all five real retries.
internal sealed partial class WindowsSnapshotTransactionDeletionObserver : ISnapshotTransactionDeletionObserver
{
    private const int BufferSize = 64 * 1024;
    private readonly string _tempPrefix;
    private SafeFileHandle? _directory;
    private SafeWaitHandle? _event;
    private IntPtr _buffer;
    private IntPtr _overlapped;
    private bool _readPending;
    private bool _disposed;

    internal WindowsSnapshotTransactionDeletionObserver(string path, string transactionMarker)
    {
        _tempPrefix = Path.GetFileName(path) + transactionMarker;
        try
        {
            _directory = CreateFileW(Path.GetDirectoryName(Path.GetFullPath(path))!,
                FileListDirectory, FileShareAll, IntPtr.Zero, OpenExisting,
                FileFlagBackupSemantics | FileFlagOverlapped, IntPtr.Zero);
            if (_directory.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Opening the owned snapshot directory observer failed.");
            }
            _event = CreateEventW(IntPtr.Zero, true, false, null);
            if (_event.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Creating the owned snapshot observer event failed.");
            }
            _buffer = Marshal.AllocHGlobal(BufferSize);
            _overlapped = Marshal.AllocHGlobal(Marshal.SizeOf<DirectoryOverlapped>());
            RegisterRead();
        }
        catch (Exception creationFailure)
        {
            try
            {
                Dispose();
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(creationFailure, cleanupFailure);
            }
            throw;
        }
    }

    public bool WaitForDeletion(TimeSpan timeout)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < timeout)
        {
            var remaining = timeout - elapsed.Elapsed;
            var milliseconds = (uint)Math.Clamp(Math.Ceiling(remaining.TotalMilliseconds), 1, uint.MaxValue - 1);
            var wait = WaitForSingleObject(_event!, milliseconds);
            if (wait == WaitTimeout)
            {
                return false;
            }
            if (wait != WaitObject0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Waiting for owned snapshot cleanup notification failed.");
            }
            var succeeded = GetOverlappedResult(_directory!, _overlapped, out var count, false);
            var error = succeeded ? 0 : Marshal.GetLastWin32Error();
            _readPending = error == ErrorIoIncomplete || error == ErrorIoPending;
            if (!succeeded)
            {
                throw new Win32Exception(error, "Reading the owned snapshot cleanup notification failed.");
            }
            if (count == 0 || count > BufferSize)
            {
                throw new IOException("Snapshot directory notification overflow cannot prove a failed replace cleanup.");
            }
            var bytes = new byte[count];
            Marshal.Copy(_buffer, bytes, 0, bytes.Length);
            if (ContainsMatchingDeletion(bytes))
            {
                return true;
            }
            RegisterRead();
        }
        return false;
    }

    private void RegisterRead()
    {
        if (!ResetEvent(_event!))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Resetting the owned snapshot observer event failed.");
        }
        Marshal.StructureToPtr(new DirectoryOverlapped { Event = _event!.DangerousGetHandle() }, _overlapped, false);
        var accepted = ReadDirectoryChangesW(_directory!, _buffer, BufferSize, false,
            FileNotifyChangeFileName, IntPtr.Zero, _overlapped, IntPtr.Zero);
        var error = accepted ? 0 : Marshal.GetLastWin32Error();
        if (!accepted && error != ErrorIoPending)
        {
            throw new Win32Exception(error, "Registering the owned snapshot directory read failed.");
        }
        // Set only after successful registration; callers may now start writer.
        _readPending = true;
    }

    private bool ContainsMatchingDeletion(byte[] bytes)
    {
        var offset = 0;
        while (true)
        {
            if (bytes.Length - offset < 12)
            {
                throw new IOException("Snapshot directory notification header is truncated.");
            }
            var next = BitConverter.ToUInt32(bytes, offset);
            var action = BitConverter.ToUInt32(bytes, offset + 4);
            var length = BitConverter.ToUInt32(bytes, offset + 8);
            if ((length & 1) != 0 || length > bytes.Length - offset - 12)
            {
                throw new IOException("Snapshot directory notification name is truncated.");
            }
            var name = Encoding.Unicode.GetString(bytes, offset + 12, (int)length);
            if (action == FileActionRemoved && name.StartsWith(_tempPrefix, StringComparison.OrdinalIgnoreCase) &&
                name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (next == 0)
            {
                return false;
            }
            if (next < 12 || next > bytes.Length - offset)
            {
                throw new IOException("Snapshot directory notification offset is invalid.");
            }
            offset += (int)next;
        }
    }
}
