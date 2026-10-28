using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public async Task RealAdmittedPublicationUsesConnectionRouteInOrderOutsideOwnerGates()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var current = f.Current; var storage = RequireCoherent(f);
            Assert.True(LegacySpellPublicationPlan.TryPrepare(current.PlayerSpells, null, out var plan));
            Assert.NotNull(plan);
            Assert.True(LegacySpellPublicationOwner.TryBind(f.Store, f.Trades, f.Connection, current, plan!, out var owner));
            Assert.NotNull(owner);
            var frames = new List<byte[]>();
            f.ObserveSend = (frame, _) =>
            {
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                Assert.False(Monitor.IsEntered(f.Native.SpellAdmissionSyncRoot));
                Assert.False(Monitor.IsEntered(f.Player.SpellAdmissionSyncRoot));
                Assert.False(Monitor.IsEntered(storage.MutationGate));
                frames.Add(frame); return ValueTask.CompletedTask;
            };
            byte[] trailing = [8, 0, 0xA5, 0x5A, 1, 2, 3, 4];
            Assert.True(await owner!.PublishAsync(trailing, CancellationToken.None));
            Assert.Equal(plan!.Frames.Length + 1, frames.Count);
            for (int i = 0; i < plan.Frames.Length; i++) Assert.Equal(plan.Frames[i], frames[i]);
            Assert.Equal(trailing, frames[^1]); Assert.Same(current, f.Current); RequireCoherent(f);
        });
    }

    [Fact]
    public async Task RetirementDuringFirstRealConnectionSendSuppressesRemainingPreparedFrames()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var current = f.Current;
            Assert.True(LegacySpellPublicationPlan.TryPrepare(current.PlayerSpells, null, out var plan)); Assert.NotNull(plan);
            Assert.True(plan!.Frames.Length >= 2);
            Assert.True(LegacySpellPublicationOwner.TryBind(f.Store, f.Trades, f.Connection, current, plan, out var owner));
            var seen = new List<byte[]>();
            f.ObserveSend = (frame, _) =>
            {
                seen.Add(frame);
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                Assert.True(f.Store.TryRemove(f.Connection, out var removed)); Assert.Same(current, removed);
                Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection, current));
                return ValueTask.CompletedTask;
            };
            Assert.False(await owner!.PublishAsync([8, 0, 1, 2, 3, 4, 5, 6], CancellationToken.None));
            Assert.Single(seen); Assert.Equal(plan.Frames[0], seen[0]); Assert.Null(f.CurrentOrNull);
        });
    }

    [Fact]
    public async Task CooperativeConnectionCancellationDoesNotUndoCommittedOrdinaryAdmission()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var current = f.Current; var storage = RequireCoherent(f); var files = f.Files();
            Assert.True(LegacySpellPublicationPlan.TryPrepare(current.PlayerSpells, null, out var plan));
            Assert.True(LegacySpellPublicationOwner.TryBind(f.Store, f.Trades, f.Connection, current, plan!, out var owner));
            using var cancel = new CancellationTokenSource(); cancel.Cancel(); int calls = 0;
            f.ObserveSend = (_, token) => { calls++; token.ThrowIfCancellationRequested(); return ValueTask.CompletedTask; };
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await owner!.PublishAsync(null, cancel.Token));
            Assert.Equal(1, calls); Assert.Same(current, f.Current); Assert.Same(storage, f.Native.MySpells);
            RequireCoherent(f); f.RequireFiles(files);
        });
    }
}
