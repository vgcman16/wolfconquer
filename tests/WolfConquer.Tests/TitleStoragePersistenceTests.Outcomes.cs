using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class TitleStoragePersistenceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RealFailedWriteEvidenceDistinguishesExactBytesFromChangedMissingOrOversize(int kind)
    {
        using var fixture = new TitlePersistenceFileFixture();
        fixture.Seed();
        var before = File.ReadAllBytes(fixture.PathName);
        if (kind == 1) File.AppendAllText(fixture.PathName, "\r\n; changed unrelated bytes\r\n");
        if (kind == 2) File.Delete(fixture.PathName);
        if (kind == 3)
        {
            using var file = new FileStream(fixture.PathName, FileMode.Open, FileAccess.Write, FileShare.None);
            file.SetLength(CharacterSnapshotFileBaseline.MaximumBytes + 1L);
        }
        var args = new object?[] { fixture.PathName, before, "synthetic-observed-failure", null };
        // Invoke only the actual evidence classifier, not an injected writer or accepting save callback.
        var method = typeof(LegacyTitleStoragePersistence).GetMethod("ClassifyFailedWrite",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.False((bool)method.Invoke(null, args)!);
        Assert.StartsWith(kind == 0 ? "titleStorageWriteFailed:" : "titleStorageRecoveryRequired:",
            Assert.IsType<string>(args[3]));
    }

    [Theory]
    [InlineData("SpecialTitleID", "1")]
    [InlineData("SpecialWingID", "1")]
    [InlineData("SpecialTitleScore", "1")]
    [InlineData("SpecialTitles", "0,")]
    [InlineData("UID", "1")]
    [InlineData("Money", "999999")]
    public void ActualPostconditionReaderRequiresCandidateUidAllTitlesAndUnrelatedFields(string key, string value)
    {
        using var fixture = new TitlePersistenceFileFixture();
        var original = fixture.Seed();
        Assert.True(CharacterSnapshotFileBaseline.TryRead(fixture.PathName, "test", out var before, out var reason), reason);
        var updated = TitlePersistenceFileFixture.Updated(original);
        var candidate = TitlePersistenceFileFixture.Values(updated);
        Assert.True(LegacyTitleStoragePersistence.TryPersist(original, updated, candidate, out reason), reason);
        File.WriteAllLines(fixture.PathName, File.ReadAllLines(fixture.PathName)
            .Select(line => line.StartsWith(key + "=", StringComparison.Ordinal) ? key + "=" + value : line));
        var args = new object?[] { fixture.PathName, TitlePersistenceFileFixture.Uid, candidate, before, null };
        var method = typeof(LegacyTitleStoragePersistence).GetMethod("TryValidateWrittenState",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.False((bool)method.Invoke(null, args)!);
        Assert.Equal("titleStorageRecoveryRequired:postWriteStateUnproven", args[4]);
    }

    [Fact]
    public async Task SameRealFileGateBlocksTitleWriteUntilItsCurrentOwnerReleases()
    {
        using var fixture = new TitlePersistenceFileFixture();
        var original = fixture.Seed();
        var updated = TitlePersistenceFileFixture.Updated(original);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var attempted = new ManualResetEventSlim();
        var owner = Task.Factory.StartNew(() => SnapshotFileLockCoordinator.ExecuteLocked(fixture.PathName, () =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Owned file gate was not released.");
            return true;
        }), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        var writer = Task.Factory.StartNew(() =>
        {
            attempted.Set();
            return LegacyTitleStoragePersistence.TryPersist(original, updated,
                TitlePersistenceFileFixture.Values(updated), out _);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            Assert.True(attempted.Wait(TimeSpan.FromSeconds(5)));
            Assert.NotSame(writer, await Task.WhenAny(writer, Task.Delay(150)));
        }
        finally
        {
            release.Set();
            await Task.WhenAll(owner, writer).WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.True(owner.Result);
        Assert.True(writer.Result);
    }
}
