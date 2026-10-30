using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedItemRepairTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualConcurrentRequestsCommitOneRepairAndOneCost(bool meteor)
    {
        await WithRepair(meteor, false, async f =>
        {
            await f.Login(); var before = f.Current; int first = f.Sent.Count;
            var expected = PlayerItemSnapshotLoader.ToLegacyBytes(ExpectedItems(before, meteor));
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var held = await f.Operations.EnterExclusiveAsync();
            Task one = f.Handler.HandleFrameAsync(f.Connection, Frame(Request()), cancellation.Token).AsTask();
            Task two = f.Handler.HandleFrameAsync(f.Connection, Frame(Request()), cancellation.Token).AsTask();
            Exception? primary = null;
            try
            {
                Assert.False(one.IsCompleted); Assert.False(two.IsCompleted);
                Assert.Same(before, f.Current);
            }
            catch (Exception error) { primary = error; }
            // Release and drain both actual producers before fixture disposal,
            // preserving the original assertion and every secondary failure.
            await CompleteRepairRequestsAsync(held, primary, one, two);

            AssertGraph(f); Assert.Equal(meteor ? 1000 : 943, f.Current.Character.Money);
            Assert.Equal((ushort)10, RepairedTarget(f.Current).Durability);
            Assert.Equal(expected, File.ReadAllBytes(f.ItemsPath));
            Assert.Equal(first + (meteor ? 6 : 3), f.Sent.Count);
            Assert.False(LegacyTradeJournal.HasPending(f.Users)); Assert.False(f.Recovery.IsBlocked);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FirstTransportFailureAfterCommitNeverRefundsOrChargesAgain(bool meteor)
    {
        await WithRepair(meteor, false, async f =>
        {
            await f.Login(); var before = f.Current;
            byte[] character = ExpectedCharacter(File.ReadAllBytes(f.CharacterPath), meteor);
            byte[] items = PlayerItemSnapshotLoader.ToLegacyBytes(ExpectedItems(before, meteor));
            int first = f.Sent.Count, attempted = 0;
            Exception? observationFailure = null;
            f.OnSend = _ =>
            {
                attempted++;
                try
                {
                    AssertGraph(f); Assert.NotSame(before, f.Current);
                    Assert.Equal(character, File.ReadAllBytes(f.CharacterPath));
                    Assert.Equal(items, File.ReadAllBytes(f.ItemsPath));
                }
                catch (Exception error) { observationFailure = error; throw; }
                throw new RepairNotificationFailure();
            };

            // The public Frame owns its existing exception logging boundary.
            // Its return cannot turn the already durable repair into a rejection.
            try { await f.Send(Request()); }
            finally { f.OnSend = null; }
            if (observationFailure is not null) ExceptionDispatchInfo.Capture(observationFailure).Throw();

            Assert.Equal(1, attempted); Assert.Equal(first, f.Sent.Count);
            Assert.Equal(meteor ? 1000 : 943, f.Current.Character.Money);
            Assert.Equal((ushort)10, RepairedTarget(f.Current).Durability);
            Assert.Equal(character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(items, File.ReadAllBytes(f.ItemsPath));
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
            var durable = f.Files(); var committed = f.Current;
            await f.Send(Request());
            Assert.Same(committed, f.Current); Assert.Equal(first, f.Sent.Count); f.CheckFiles(durable);
        });
    }

    private static async Task CompleteRepairRequestsAsync(IDisposable held, Exception? primary, params Task[] requests)
    {
        var errors = new List<Exception>();
        if (primary is not null) errors.Add(primary);
        try { held.Dispose(); } catch (Exception error) { errors.Add(error); }
        foreach (var request in requests)
            try { await request; } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Repair assertion and actual producer drain failed.", errors);
    }

    private sealed class RepairNotificationFailure : Exception { }
}
