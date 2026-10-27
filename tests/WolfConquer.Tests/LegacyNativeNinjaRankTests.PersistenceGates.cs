using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using Ranking = WolfConquer.Core.Data.LegacyNativeNinjaRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaRankTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void HeldActualRegistryOrCategoryGateRefusesPersistenceReentryBeforeOutput(bool save, bool category) =>
        WithOwnedRankPersistence(files =>
        {
            files.Seed(Text(FullRow));
            var before = SeedPersistenceGraph();
            var gate = category ? Ranking.Ranks[Ranking.Type.Water].SynRoot : Ranking.Ranks.SyncRoot;
            Exception? error;
            lock (gate) error = Record.Exception(() => { if (save) Ranking.Save(); else Ranking.Load(); });
            before.RequireCurrent();
            files.Expect(Text(FullRow));
            Assert.IsType<InvalidOperationException>(error);
        });

    [Fact]
    public void ActualLoadWaitingOnCapturedRegistryGateCannotOverwriteANewerWholeRegistry() => WithOwnedRankPersistence(files =>
    {
        files.Seed(Text(FullRow));
        var old = SeedPersistenceGraph();
        var gate = Ranking.Ranks.SyncRoot;
        using var entered = new ManualResetEventSlim();
        Exception? workerFailure = null;
        Exception? phaseFailure = null;
        PersistenceGraphImage? newer = null;
        var worker = new Thread(() =>
        {
            entered.Set();
            workerFailure = Record.Exception(Ranking.Load);
        }) { IsBackground = true, Name = "owned-ninja-rank-publication" };
        var started = false;
        Monitor.Enter(gate);
        try
        {
            worker.Start(); started = true;
            if (!entered.Wait(TimeSpan.FromSeconds(10)) || !SpinWait.SpinUntil(() =>
                (worker.ThreadState & (ThreadState.WaitSleepJoin | ThreadState.Stopped)) != 0, TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Actual Load did not reach the held-registry observation phase.");
            Assert.False(worker.ThreadState.HasFlag(ThreadState.Stopped));
            // Real complete replacement owners, never an accepting publication callback.
            var replacement = new LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank>();
            for (uint index = 0; index < 6; index++)
            {
                var type = (Ranking.Type)index;
                var rank = new Ranking.Rank(type) { MaxItems = 10 };
                var row = Entry(800 + index, 1000 + index); row.Type = type;
                rank.RankingList.Add(row.UID, row);
                replacement.Add(type, rank);
            }
            _ = replacement.GetValues();
            Ranking.Ranks = replacement;
            newer = new PersistenceGraphImage();
        }
        catch (Exception error) { phaseFailure = error; }
        finally
        {
            Monitor.Exit(gate);
            // No packet/file/global fixture cleanup can race an unfinished Load.
            if (started) worker.Join();
        }
        if (phaseFailure is not null)
        {
            if (workerFailure is not null) throw new AggregateException(phaseFailure, workerFailure);
            ExceptionDispatchInfo.Capture(phaseFailure).Throw();
        }
        try
        {
            Assert.NotNull(newer);
            newer.RequireCurrent();
            old.RequireEscaped();
            files.Expect(Text(FullRow));
            Assert.IsType<InvalidOperationException>(workerFailure);
        }
        catch (Exception observation)
        {
            if (workerFailure is not null) throw new AggregateException(observation, workerFailure);
            throw;
        }
    });
}
