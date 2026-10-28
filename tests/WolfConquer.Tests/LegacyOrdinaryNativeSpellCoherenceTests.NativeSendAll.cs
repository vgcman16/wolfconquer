using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public async Task BoundNativeSendAllDrainsInFlightConnectionWriteAndStopsAfterRetirement()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var before = f.Current; var storage = RequireCoherent(f);
            var profs = Assert.IsType<LegacyNativeProficiencyStorage>(f.Native.MyProfs);
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var frames = new List<byte[]>();
            f.ObserveSend = (frame, _) =>
            {
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                Assert.False(Monitor.IsEntered(f.Native.SpellAdmissionSyncRoot));
                Assert.False(Monitor.IsEntered(f.Player.SpellAdmissionSyncRoot));
                Assert.False(Monitor.IsEntered(storage.MutationGate));
                Assert.False(Monitor.IsEntered(profs.MutationGate));
                lock (frames) frames.Add(frame);
                entered.TrySetResult(true);
                return new ValueTask(release.Task); // Actual connection route; deliberately remains pending.
            };
            Task<Exception?> send = Task.Factory.StartNew(() =>
            {
                using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                return Record.Exception(() => storage.SendAll(packet));
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(send.IsCompleted);
                Assert.True(f.Store.TryRemove(f.Connection, out var removed)); Assert.Same(before, removed);
                Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection, before));
                release.TrySetResult(true);
                await send.WaitAsync(TimeSpan.FromSeconds(10));
                lock (frames) Assert.Single(frames);
                Assert.Null(f.CurrentOrNull);
            }
            finally { release.TrySetResult(true); await send.WaitAsync(TimeSpan.FromSeconds(20)); }
        });
    }

    [Fact]
    public async Task BoundNativeBatchCancelsDisconnectsAndBoundsAnIgnoredInFlightWrite()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var current = f.Current; var storage = RequireCoherent(f);
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken sentToken = default; int sends = 0;
            f.ObserveSend = (_, token) =>
            {
                sentToken = token; Interlocked.Increment(ref sends); entered.TrySetResult(true);
                return new ValueTask(release.Task); // Deliberately ignores cancellation until the test releases it.
            };
            Task<Exception?> send = Task.Factory.StartNew(() =>
            {
                using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                return Record.Exception(() => storage.SendAll(packet));
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Exception? failure = await send.WaitAsync(TimeSpan.FromSeconds(25));
                Assert.IsType<LegacyCommittedSpellNotificationException>(failure);
                Assert.True(sentToken.IsCancellationRequested); Assert.True(f.Disconnected);
                Assert.Equal(1, Volatile.Read(ref sends)); Assert.False(release.Task.IsCompleted);
                Assert.Same(current, f.Current); RequireCoherent(f);
            }
            finally { release.TrySetResult(true); await send.WaitAsync(TimeSpan.FromSeconds(25)); }
        });
    }
}
