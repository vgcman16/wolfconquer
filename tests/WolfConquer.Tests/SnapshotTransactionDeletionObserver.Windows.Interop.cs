using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WolfConquer.Tests;

internal sealed partial class WindowsSnapshotTransactionDeletionObserver
{
    private const uint FileListDirectory = 1;
    private const uint FileShareAll = 7;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOverlapped = 0x40000000;
    private const uint FileNotifyChangeFileName = 1;
    private const uint FileActionRemoved = 2;
    private const uint WaitObject0 = 0;
    private const uint WaitTimeout = 258;
    private const int ErrorIoPending = 997;
    private const int ErrorIoIncomplete = 996;
    private const int ErrorOperationAborted = 995;
    private const int ErrorNotFound = 1168;

    [StructLayout(LayoutKind.Sequential)]
    private struct DirectoryOverlapped
    {
        internal IntPtr Internal;
        internal IntPtr InternalHigh;
        internal uint Offset;
        internal uint OffsetHigh;
        internal IntPtr Event;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        var failures = new List<Exception>();
        if (_readPending)
        {
            // Both calls target ONLY this observer's retained directory handle
            // and exact OVERLAPPED address. Drain before freeing either buffer
            // or either handle, including timeout/assertion/registration paths.
            var cancellationAccepted = CancelIoEx(_directory!, _overlapped);
            if (!cancellationAccepted)
            {
                var error = Marshal.GetLastWin32Error();
                cancellationAccepted = error == ErrorNotFound;
                if (!cancellationAccepted)
                {
                    failures.Add(new Win32Exception(error, "Cancelling owned snapshot notification IO failed."));
                }
            }
            if (!GetOverlappedResult(_directory!, _overlapped, out _, cancellationAccepted))
            {
                var error = Marshal.GetLastWin32Error();
                if (error == ErrorIoIncomplete || error == ErrorIoPending)
                {
                    // Fail closed: an unproven pending operation must not lose
                    // its buffers/handles. Permit an explicit later Dispose
                    // retry; never free memory still reachable by native IO.
                    _disposed = false;
                    failures.Add(new Win32Exception(error, "Owned snapshot notification IO remains pending; its buffers and handles are retained."));
                    throw new AggregateException(failures);
                }
                if (error != ErrorOperationAborted)
                {
                    failures.Add(new Win32Exception(error, "Draining owned snapshot notification IO failed."));
                }
            }
            _readPending = false;
        }
        if (_overlapped != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_overlapped);
            _overlapped = IntPtr.Zero;
        }
        if (_buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_buffer);
            _buffer = IntPtr.Zero;
        }
        _directory?.Dispose();
        _event?.Dispose();
        if (failures.Count > 0)
        {
            throw new AggregateException(failures);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern SafeWaitHandle CreateEventW(IntPtr eventAttributes, [MarshalAs(UnmanagedType.Bool)] bool manualReset,
        [MarshalAs(UnmanagedType.Bool)] bool initialState, string? name);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadDirectoryChangesW(SafeFileHandle directory, IntPtr buffer, uint bufferLength,
        [MarshalAs(UnmanagedType.Bool)] bool watchSubtree, uint notifyFilter, IntPtr bytesReturned,
        IntPtr overlapped, IntPtr completionRoutine);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern uint WaitForSingleObject(SafeWaitHandle handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ResetEvent(SafeWaitHandle handle);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOverlappedResult(SafeFileHandle file, IntPtr overlapped, out uint bytesTransferred,
        [MarshalAs(UnmanagedType.Bool)] bool wait);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CancelIoEx(SafeFileHandle file, IntPtr overlapped);
}
