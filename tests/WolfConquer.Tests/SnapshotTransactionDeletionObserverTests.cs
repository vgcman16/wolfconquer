using System.Reflection;
using Microsoft.Win32.SafeHandles;

namespace WolfConquer.Tests;

public sealed class SnapshotTransactionDeletionObserverTests
{
    private const string Marker = ".observer-control-";

    [WindowsSnapshotObserverFact]
    public void CreationRenameAndOtherBasenamesDoNotStandInForRealTemporaryRemoval()
    {
        WithOwnedDirectory(root =>
        {
            var destination = Path.Combine(root, "123.bin");
            File.WriteAllBytes(destination, [1, 2, 3]);
            using var observer = SnapshotTransactionDeletionObserver.Create(destination, Marker);
            var temporary = destination + Marker + "created.tmp";
            File.WriteAllBytes(temporary, [4]);
            Assert.False(observer.WaitForDeletion(TimeSpan.FromMilliseconds(100)));
            var renamed = temporary + ".renamed";
            File.Move(temporary, renamed);
            File.Delete(renamed);
            var other = Path.Combine(root, "456.bin" + Marker + "other.tmp");
            File.WriteAllBytes(other, [5]);
            File.Delete(other);
            Assert.False(observer.WaitForDeletion(TimeSpan.FromMilliseconds(100)));
            File.WriteAllBytes(temporary, [6]);
            File.Delete(temporary);
            Assert.True(observer.WaitForDeletion(TimeSpan.FromSeconds(5)));
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(destination));
            Assert.Equal(destination, Assert.Single(Directory.GetFiles(root)));
        });
    }

    [WindowsSnapshotObserverFact]
    public void TimeoutCancelsAndDrainsTheOwnedReadBeforeBuffersAndHandlesAreReleased()
    {
        WithOwnedDirectory(root =>
        {
            var observer = new WindowsSnapshotTransactionDeletionObserver(Path.Combine(root, "123.bin"), Marker);
            try
            {
                Assert.True(Field<bool>(observer, "_readPending"));
                Assert.False(observer.WaitForDeletion(TimeSpan.FromMilliseconds(100)));
                Assert.True(Field<bool>(observer, "_readPending"));
            }
            finally
            {
                observer.Dispose();
            }
            Assert.False(Field<bool>(observer, "_readPending"));
            Assert.Equal(IntPtr.Zero, Field<IntPtr>(observer, "_buffer"));
            Assert.Equal(IntPtr.Zero, Field<IntPtr>(observer, "_overlapped"));
            Assert.True(Field<SafeFileHandle>(observer, "_directory").IsClosed);
            Assert.True(Field<SafeWaitHandle>(observer, "_event").IsClosed);
            observer.Dispose();
            Assert.Empty(Directory.GetFiles(root));
        });
    }

    [WindowsSnapshotObserverFact]
    public void CancellingOneObserverDoesNotCancelAnotherObserversPendingDirectoryRead()
    {
        WithOwnedDirectory(root =>
        {
            var destination = Path.Combine(root, "123.bin");
            using var first = new WindowsSnapshotTransactionDeletionObserver(destination, Marker);
            using var second = new WindowsSnapshotTransactionDeletionObserver(destination, Marker);
            first.Dispose();
            Assert.True(Field<bool>(second, "_readPending"));
            var temporary = destination + Marker + "second.tmp";
            File.WriteAllBytes(temporary, [7]);
            File.Delete(temporary);
            Assert.True(second.WaitForDeletion(TimeSpan.FromSeconds(5)));
            Assert.False(Field<bool>(second, "_readPending"));
            Assert.Empty(Directory.GetFiles(root));
        });
    }

    private static T Field<T>(WindowsSnapshotTransactionDeletionObserver observer, string name) =>
        (T)typeof(WindowsSnapshotTransactionDeletionObserver)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(observer)!;

    private sealed class WindowsSnapshotObserverFactAttribute : FactAttribute
    {
        public WindowsSnapshotObserverFactAttribute()
        {
            if (!OperatingSystem.IsWindows())
                Skip = "Requires actual Windows directory notification and cancellation APIs.";
        }
    }

    private static void WithOwnedDirectory(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-snapshot-notify-control-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            action(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
