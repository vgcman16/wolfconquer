using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using Ranking = WolfConquer.Core.Data.LegacyNativeNinjaRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaRankTests
{
    [Theory]
    [InlineData(true, "registry")]
    [InlineData(true, "category")]
    [InlineData(false, "file")]
    [InlineData(true, "file")]
    public void ActualPersistenceCannotRetargetANewerRegistryAcrossRealGateWaits(bool save, string phase) =>
        WithOwnedRankPersistence(files =>
    {
        files.Seed(Text(FullRow)); byte[] savedBefore = File.ReadAllBytes(files.PathFor());
        var previous = SeedPersistenceGraph(); var registry = Ranking.Ranks; object registryGate = registry.SyncRoot;
        using var entered = new ManualResetEventSlim();
        Exception? workerFailure = null, phaseFailure = null; PersistenceGraphImage? newer = null;
        var worker = new Thread(() =>
        {
            entered.Set();
            workerFailure = Record.Exception(() => { if (save) Ranking.Save(); else Ranking.Load(); });
        }) { IsBackground = true, Name = "owned-ninja-persistence-witness" };
        bool started = false;
        try
        {
            if (phase == "file")
            {
                // The existing coordinator owns this actual saved path. This is
                // a real lock scope, not an injected saver or publication hook.
                SnapshotFileLockCoordinator.ExecuteLocked(files.PathFor(), () =>
                {
                    worker.Start(); started = true;
                    WaitForPersistenceOwnerBlock(worker, entered, null);
                    newer = PublishNewPersistenceTestRegistry();
                    return true;
                });
            }
            else
            {
                object gate = phase == "registry" ? registryGate : registry[Ranking.Type.Water].SynRoot;
                Monitor.Enter(gate);
                try
                {
                    worker.Start(); started = true;
                    // In the category case the actual save must also own its
                    // captured registry gate before the category wait is accepted.
                    WaitForPersistenceOwnerBlock(worker, entered, phase == "category" ? registryGate : null);
                    Assert.True(Monitor.IsEntered(gate));
                    newer = PublishNewPersistenceTestRegistry();
                }
                finally { Monitor.Exit(gate); }
            }
        }
        catch (Exception error) { phaseFailure = error; }
        finally
        {
            // Every actual gate held above has been released before waiting for
            // completion; there is no test barrier left holding the operation.
            if (started && !worker.Join(TimeSpan.FromSeconds(10)))
            {
                var timeout = new TimeoutException("Owned Ninja persistence worker did not finish after gate release.");
                phaseFailure = phaseFailure is null ? timeout : new AggregateException(phaseFailure, timeout);
            }
        }
        if (phaseFailure is not null)
        {
            if (workerFailure is not null) throw new AggregateException(phaseFailure, workerFailure);
            ExceptionDispatchInfo.Capture(phaseFailure).Throw();
        }
        try
        {
            // ThreadState only located the blocked phase. These real owner,
            // complete-row, byte, stage and completed-outcome checks are the oracle.
            Assert.NotNull(newer); newer.RequireCurrent(); previous.RequireEscaped();
            Assert.Equal(savedBefore, File.ReadAllBytes(files.PathFor())); files.Expect(Text(FullRow));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(files.PathFor())!, "*.ninja-stage"));
            var error = Assert.IsType<InvalidOperationException>(workerFailure);
            Assert.Equal("Ninja rank persistence requires a complete current registry owner.", error.Message);
            Assert.False(worker.IsAlive); Assert.Same(registryGate, registry.SyncRoot);
        }
        catch (Exception observation)
        {
            if (workerFailure is not null) throw new AggregateException(observation, workerFailure);
            throw;
        }
    });

    private static void WaitForPersistenceOwnerBlock(Thread worker, ManualResetEventSlim entered, object? capturedGate)
    {
        if (!entered.Wait(TimeSpan.FromSeconds(10)) || !SpinWait.SpinUntil(() =>
            (worker.ThreadState & ThreadState.Stopped) != 0 ||
            ((worker.ThreadState & ThreadState.WaitSleepJoin) != 0 &&
             (capturedGate is null || PersistenceOwnerGateHeldElsewhere(capturedGate))), TimeSpan.FromSeconds(10)))
            throw new TimeoutException("Actual persistence did not reach the real held-gate phase.");
        Assert.False(worker.ThreadState.HasFlag(ThreadState.Stopped));
    }

    private static bool PersistenceOwnerGateHeldElsewhere(object gate)
    {
        if (!Monitor.TryEnter(gate)) return true;
        Monitor.Exit(gate);
        return false;
    }

    private static PersistenceGraphImage PublishNewPersistenceTestRegistry()
    {
        var replacement = new LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank>();
        for (uint index = 0; index < 6; index++)
        {
            var type = (Ranking.Type)index; var rank = new Ranking.Rank(type) { MaxItems = 10 };
            var row = Entry(900 + index, 3000 + index); row.Type = type;
            rank.RankingList.Add(row.UID, row); replacement.Add(type, rank);
        }
        replacement.GetValues(); Ranking.Ranks = replacement;
        return new PersistenceGraphImage();
    }
}