using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualGuildWarPrimaryUsesCurrentRealProcessForCostAndHit(bool active) =>
        RunOwnedSobPrimaryAsync(1, "guild-war", async (fixture, sobs, states) =>
    {
        var context = fixture.Handler.CreateMonsterRuntimeContext(new(fixture.Connection(PlayerUid), fixture.Current(PlayerUid)));
        var manager = Assert.IsType<LegacyGuildWarRuntimeStateStore>(context.GuildWarStates);
        manager.SetActive(active);
        var before = fixture.Current(PlayerUid); var native = before.NativePlayer!;
        var saved = ProcFiles(fixture); var life = states.GetVisible(sobs[1]).Life;
        await fixture.SendAsync(PlayerUid, OwnedSobQuery(sobs[1], 1));
        var after = fixture.Current(PlayerUid);
        if (!active)
        {
            Assert.Same(before, after); Assert.Equal(500U, native.Mana);
            Assert.Equal((ushort)100, native.Stamina); Assert.Equal(life, states.GetVisible(sobs[1]).Life);
            Assert.Empty(fixture.ActorFrames); ProcFilesUnchanged(fixture, saved);
        }
        else
        {
            Assert.NotSame(before, after); Assert.Equal(493L, after.Character.MinMana);
            Assert.Equal(493U, native.Mana); Assert.Equal((ushort)95, native.Stamina);
            Assert.True(states.GetVisible(sobs[1]).Life < life);
            Assert.Single(ParseSpellAnimations(fixture.ActorFrames));
            Assert.True(fixture.Store.IsBoundSpellOwnerCurrent(after.NativeGameClient!.OrdinarySpellBinding!));
        }
    });

    [Fact]
    public Task HeldRealGuildWarOwnerRefusesActualPrimaryBeforeCost() =>
        RunOwnedSobPrimaryAsync(1, "guild-war", async (fixture, sobs, states) =>
    {
        var context = fixture.Handler.CreateMonsterRuntimeContext(new(fixture.Connection(PlayerUid), fixture.Current(PlayerUid)));
        var manager = Assert.IsType<LegacyGuildWarRuntimeStateStore>(context.GuildWarStates);
        manager.Start();
        var before = fixture.Current(PlayerUid); var native = before.NativePlayer!;
        var saved = ProcFiles(fixture); var life = states.GetVisible(sobs[1]).Life;
        Assert.True(manager.TryCapturePermission(out var observation));
        using var release = new ManualResetEventSlim(false);
        var held = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = Task.Run(() =>
        {
            if (!observation!.TryEnter(out var lease) || lease is null) { held.TrySetResult(false); return; }
            using (lease) { held.TrySetResult(true); release.Wait(); }
        });
        try
        {
            Assert.True(await held.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            await fixture.SendAsync(PlayerUid, OwnedSobQuery(sobs[1], 1));
            Assert.Same(before, fixture.Current(PlayerUid));
            Assert.Equal(500U, native.Mana); Assert.Equal((ushort)100, native.Stamina);
            Assert.Equal(life, states.GetVisible(sobs[1]).Life);
            Assert.Empty(fixture.ActorFrames); ProcFilesUnchanged(fixture, saved);
        }
        finally { release.Set(); await worker.WaitAsync(TimeSpan.FromSeconds(5)); }
        await fixture.SendAsync(PlayerUid, OwnedSobQuery(sobs[1], 1));
        Assert.Equal(493U, native.Mana); Assert.Equal((ushort)95, native.Stamina);
        Assert.True(states.GetVisible(sobs[1]).Life < life);
        Assert.Single(ParseSpellAnimations(fixture.ActorFrames));
    });

    [Fact]
    public Task RealPostCaptureHeldGuildOwnerRefusesAtCharacterCostBoundary() =>
        RunOwnedSobPrimaryAsync(1, "guild-war", async (fixture, sobs, states) =>
    {
        var context = fixture.Handler.CreateMonsterRuntimeContext(new(fixture.Connection(PlayerUid), fixture.Current(PlayerUid)));
        var manager = Assert.IsType<LegacyGuildWarRuntimeStateStore>(context.GuildWarStates);
        manager.Start();
        var before = fixture.Current(PlayerUid); var native = before.NativePlayer!;
        var saved = ProcFiles(fixture); var life = states.GetVisible(sobs[1]).Life;
        long prepared = manager.PreparedAdmissionCount;
        var file = Path.Combine(fixture.Data.Users, PlayerUid.ToString(CultureInfo.InvariantCulture) + ".ini");
        using var releaseFile = new ManualResetEventSlim(false);
        using var releaseGuild = new ManualResetEventSlim(false);
        var fileHeld = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var guildHeld = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fileWorker = Task.Run(() => SnapshotFileLockCoordinator.ExecuteLocked(file, () =>
        {
            fileHeld.TrySetResult(true); releaseFile.Wait(); return 0;
        }));
        Task? cast = null, guildWorker = null;
        try
        {
            Assert.True(await fileHeld.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            cast = Task.Run(() => fixture.SendAsync(PlayerUid, OwnedSobQuery(sobs[1], 1)));
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (manager.PreparedAdmissionCount == prepared && DateTime.UtcNow < deadline)
                await Task.Delay(10);
            Assert.True(manager.PreparedAdmissionCount > prepared);
            guildWorker = Task.Run(() =>
            {
                var until = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < until)
                {
                    if (manager.TryCapturePermission(out var observation) && observation!.TryEnter(out var lease) && lease is not null)
                    {
                        using (lease) { guildHeld.TrySetResult(true); releaseGuild.Wait(); }
                        return;
                    }
                    Thread.Yield();
                }
                guildHeld.TrySetResult(false);
            });
            Assert.True(await guildHeld.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            releaseFile.Set();
            await cast.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(before, fixture.Current(PlayerUid));
            Assert.Equal(500U, native.Mana); Assert.Equal((ushort)100, native.Stamina);
            Assert.Equal(life, states.GetVisible(sobs[1]).Life);
            Assert.Empty(fixture.ActorFrames); ProcFilesUnchanged(fixture, saved);
        }
        finally
        {
            releaseFile.Set(); releaseGuild.Set();
            await fileWorker.WaitAsync(TimeSpan.FromSeconds(5));
            if (guildWorker is not null) await guildWorker.WaitAsync(TimeSpan.FromSeconds(5));
            if (cast is not null) await cast.WaitAsync(TimeSpan.FromSeconds(5));
        }
        await fixture.SendAsync(PlayerUid, OwnedSobQuery(sobs[1], 1));
        Assert.Equal(493U, native.Mana); Assert.Equal((ushort)95, native.Stamina);
        Assert.True(states.GetVisible(sobs[1]).Life < life);
        Assert.Single(ParseSpellAnimations(fixture.ActorFrames));
    });
    [Fact]
    public void ActualGuildWarProducersInvalidatePriorPermissionEvenAfterEqualVisibleState()
    {
        var manager = new LegacyGuildWarRuntimeStateStore();
        manager.Start();
        Assert.True(manager.TryCapturePermission(out var before));
        manager.Stop(); manager.Start();
        Assert.True(manager.IsActive);
        Assert.False(before!.TryEnter(out var stale)); Assert.Null(stale);
        Assert.True(manager.TryCapturePermission(out var current));
        Assert.True(current!.TryEnter(out var held));
        using (held)
        {
            Assert.True(held!.IsCurrentPlain());
            Assert.Throws<InvalidOperationException>(() => manager.Start());
        }
        manager.SetActive(true);
        Assert.False(current.TryEnter(out var equalWrite)); Assert.Null(equalWrite);
    }
}
