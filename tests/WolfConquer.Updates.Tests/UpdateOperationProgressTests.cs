using WolfConquer.Updates.Install;
using WolfConquer.Updates.Progress;
using Xunit;

namespace WolfConquer.Updates.Tests;

public sealed partial class UpdateOperationProgressTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateByteMeasurementsAreCoalescedButFirstProgressAndFinalCountsAreRetained(bool throws)
    {
        var observer = new Observer(throws);
        var reporter = new UpdateProgressReporter(observer);
        for (var index = 0; index < 10000; index++)
            reporter.Report(UpdateOperationPhase.Downloading, "Synthetic bytes.", 0, 100, UpdateProgressUnit.Bytes);
        reporter.Report(UpdateOperationPhase.Downloading, "Synthetic bytes.", 50, 100, UpdateProgressUnit.Bytes);
        for (var index = 0; index < 10000; index++)
            reporter.Report(UpdateOperationPhase.Downloading, "Synthetic bytes.", 100, 100, UpdateProgressUnit.Bytes);
        Assert.Equal(new long?[] { 0, 50, 100 }, observer.Events.Select(p => p.Completed));
    }

    [WindowsFact]
    public async Task PatchCountsOnlyChangedDownloadedBytesAndActualBackupAndAppliedFiles()
    {
        using var f = new InstallFixture();
        f.Put("first", [1]); f.Put("unchanged", [2, 3]); f.Put("third", [4]);
        var first = f.Entry("first", Enumerable.Range(0, 300001).Select(i => (byte)i).ToArray(), [1]);
        var unchanged = f.Entry("unchanged", [2, 3]);
        var third = f.Entry("third", [5, 6, 7], [4]);
        var progress = new Observer();
        var result = await f.Installer.InstallAsync(f.Release([first, unchanged, third]), f.Options(progress));
        Assert.Equal(2, result.InstalledFiles);
        Assert.Equal(1, result.SkippedFiles);
        AssertFinal(progress, UpdateOperationPhase.Preflight, 3, UpdateProgressUnit.Files);
        AssertFinal(progress, UpdateOperationPhase.Downloading, 300004, UpdateProgressUnit.Bytes);
        AssertFinal(progress, UpdateOperationPhase.Verifying, 2, UpdateProgressUnit.Files);
        AssertFinal(progress, UpdateOperationPhase.BackingUp, 2, UpdateProgressUnit.Files);
        AssertFinal(progress, UpdateOperationPhase.Applying, 2, UpdateProgressUnit.Files);
        AssertFinal(progress, UpdateOperationPhase.Completed, 2, UpdateProgressUnit.Files);
        var downloaded = progress.Events.Where(p => p.Phase == UpdateOperationPhase.Downloading).ToArray();
        Assert.Contains(downloaded, p => p.Completed > 0 && p.Completed < p.Total);
        Assert.Equal(downloaded.Select(p => p.Completed).OrderBy(n => n), downloaded.Select(p => p.Completed));
        AssertSafeMessages(progress, f.Root);
    }

    [WindowsFact]
    public async Task BundleCountsCompressedTransportBytesSeparatelyFromVerifiedEntryFiles()
    {
        using var f = new InstallFixture();
        var files = new[] { f.Entry("first", new byte[20000]), f.Entry("dir/second", [1, 2, 3]) };
        var bundle = f.Bundle(files);
        var progress = new Observer();
        await f.Installer.InstallAsync(f.Release(files, bundle), f.Options(progress, fresh: true));
        Assert.NotEqual(files.Sum(file => file.Length), bundle.Length);
        AssertFinal(progress, UpdateOperationPhase.Downloading, bundle.Length, UpdateProgressUnit.Bytes);
        AssertFinal(progress, UpdateOperationPhase.Verifying, 2, UpdateProgressUnit.Files);
        AssertFinal(progress, UpdateOperationPhase.BackingUp, 0, UpdateProgressUnit.Files);
        AssertFinal(progress, UpdateOperationPhase.Applying, 2, UpdateProgressUnit.Files);
        Assert.Single(f.Handler.Requested);
        Assert.Equal(bundle.Sha256, f.Handler.Requested[0], ignoreCase: true);
    }

    [WindowsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThrowingObserverDoesNotChangeSuccessfulInstallOrCurrentNoDownloadResult(bool throws)
    {
        using var f = new InstallFixture();
        var release = f.Release([f.Entry("owned", [9, 8, 7])]);
        var progress = new Observer(throws);
        var result = await f.Installer.InstallAsync(release, f.Options(progress, fresh: true));
        Assert.Equal(1, result.InstalledFiles);
        Assert.Equal(new byte[] { 9, 8, 7 }, File.ReadAllBytes(f.PathOf("owned")));
        Assert.Equal(UpdateOperationPhase.Completed, progress.Events[^1].Phase);
        var requests = f.Handler.Requested.Count;
        progress.Events.Clear();
        var again = await f.Installer.InstallAsync(release, f.Options(progress));
        Assert.Equal(0, again.InstalledFiles);
        Assert.Equal(requests, f.Handler.Requested.Count);
        Assert.Equal(UpdateOperationPhase.Current, progress.Events[^1].Phase);
        Assert.DoesNotContain(progress.Events, p => p.Phase == UpdateOperationPhase.Downloading);
    }

    [WindowsFact]
    public async Task ThrowingObserverCannotInterruptAutomaticRollbackOrTurnFailedInstallIntoComplete()
    {
        using var f = new InstallFixture();
        f.Put("first", [1]); f.Put("second", [2]);
        var progress = new Observer(throws: true);
        File.SetAttributes(f.PathOf("second"), FileAttributes.ReadOnly);
        try
        {
            await Assert.ThrowsAsync<IOException>(() => f.Installer.InstallAsync(f.Release([
                f.Entry("first", [3], [1]), f.Entry("second", [4], [2])]), f.Options(progress)));
            Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(f.PathOf("first")));
            Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(f.PathOf("second")));
            AssertFinal(progress, UpdateOperationPhase.RollingBack, 2, UpdateProgressUnit.Files);
            Assert.Equal(UpdateOperationPhase.Failed, progress.Events[^1].Phase);
            Assert.DoesNotContain(progress.Events, p => p.Phase == UpdateOperationPhase.Completed);
            Assert.Contains("rolledback", File.ReadAllText(Path.Combine(f.Transaction(), "journal.json")));
        }
        finally { File.SetAttributes(f.PathOf("second"), FileAttributes.Normal); }
    }

    [WindowsFact]
    public async Task CancelDuringFirstRecoveryFileFinishesCriticalRestorationAndJournal()
    {
        using var f = new InstallFixture();
        f.Put("first", [1]); f.Put("second", [2]);
        var release = f.Release([f.Entry("first", [3], [1]), f.Entry("second", [4], [2])]);
        var transaction = await f.PreparePartialApply(release);
        using var cancellation = new CancellationTokenSource();
        var progress = new Observer(onReport: p =>
        {
            if (p.Phase == UpdateOperationPhase.RollingBack && p.Completed == 1) cancellation.Cancel();
        });
        Assert.Equal(1, await f.Installer.RecoverAsync(f.Options(progress), cancellation.Token));
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(f.PathOf("first")));
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(f.PathOf("second")));
        AssertFinal(progress, UpdateOperationPhase.RollingBack, 2, UpdateProgressUnit.Files);
        Assert.Contains("rolledback", File.ReadAllText(Path.Combine(transaction, "journal.json")));
    }

    [WindowsFact]
    public async Task AlreadyCanceledRecoveryDoesNotBeginRestoration()
    {
        using var f = new InstallFixture();
        f.Put("first", [1]);
        var transaction = await f.PreparePartialApply(f.Release([f.Entry("first", [3], [1])]));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var progress = new Observer();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Installer.RecoverAsync(f.Options(progress), cancellation.Token));
        Assert.Equal(new byte[] { 3 }, File.ReadAllBytes(f.PathOf("first")));
        Assert.Contains("prepared", File.ReadAllText(Path.Combine(transaction, "journal.json")));
        Assert.DoesNotContain(progress.Events, p => p.Phase == UpdateOperationPhase.RollingBack);
    }

    [WindowsFact]
    public async Task BadBytesMayFinishDownloadingButNeverReportVerificationOrApplyComplete()
    {
        using var f = new InstallFixture(); f.Put("owned", [1]);
        var entry = f.Entry("owned", [2, 3], [1]);
        f.Handler.Blobs[entry.Sha256] = [8, 9];
        var progress = new Observer(throws: true);
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Installer.InstallAsync(f.Release([entry]), f.Options(progress)));
        AssertFinal(progress, UpdateOperationPhase.Downloading, 2, UpdateProgressUnit.Bytes);
        Assert.DoesNotContain(progress.Events, p => p.Phase is UpdateOperationPhase.Verifying or UpdateOperationPhase.Applying or UpdateOperationPhase.Completed);
        Assert.Equal(UpdateOperationPhase.Failed, progress.Events[^1].Phase);
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(f.PathOf("owned")));
    }

    private static void AssertFinal(Observer observer, UpdateOperationPhase phase, long count, UpdateProgressUnit unit)
    {
        var last = observer.Events.Last(p => p.Phase == phase);
        Assert.Equal(count, last.Completed); Assert.Equal(count, last.Total); Assert.Equal(unit, last.Unit);
    }

    private static void AssertSafeMessages(Observer observer, string root)
    {
        Assert.All(observer.Events, p =>
        {
            Assert.DoesNotContain(root, p.Message); Assert.DoesNotContain("https://", p.Message);
            Assert.DoesNotContain("secret", p.Message, StringComparison.OrdinalIgnoreCase);
            if (p.RelativePath is not null) Assert.False(Path.IsPathRooted(p.RelativePath));
        });
    }
}
