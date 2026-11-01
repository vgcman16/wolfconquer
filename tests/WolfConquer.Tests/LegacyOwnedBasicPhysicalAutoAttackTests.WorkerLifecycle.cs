using System.Diagnostics;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    [Fact]
    public Task EnabledWorkerRepeatsActualHitThenDrainsOnOwnedRuntimeShutdown() => Run(async f =>
    {
        var original = f.Files(); var first = HpImage(f, original, 2000, 1700);
        await f.HitAsync(2000, 1700, repeat: false);
        f.AssertFiles(first); var actor = f.Binding(Actor); var target = f.Binding(Target);
        Assert.True(actor.Client.OnAutoAttack);
        var stamp = actor.Player.AttackStamp; int initialOutputs = f.Order.Count;
        var second = HpImage(f, first, 1700, 1400);
        // This advances only the configured source clock. The enabled Frame's
        // real worker, not a manual TickAsync call, must execute the repeat.
        f.Clock.Advance(OrdinaryDelay);
        var deadline = Stopwatch.StartNew();
        while ((actor.Player.AttackStamp == stamp || !actor.Client.OnAutoAttack) && deadline.Elapsed < TimeSpan.FromSeconds(10))
            await Task.Delay(20);
        Assert.NotEqual(stamp, actor.Player.AttackStamp); Assert.True(actor.Client.OnAutoAttack);
        f.AssertFiles(second); f.AssertCoherent(1400);
        Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(actor)); Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(target));
        var repeatOutputs = f.Order.Skip(initialOutputs).ToArray();
        int interaction = Array.FindIndex(repeatOutputs, row => row.Recipient == Actor && IsPhysical(row.Bytes));
        int hp = Array.FindIndex(repeatOutputs, row => row.Recipient == Target && IsHpPair(row.Bytes, Target));
        Assert.True(interaction >= 0 && hp > interaction);
        Assert.Equal(InteractionGolden(repeat: true), repeatOutputs[interaction].Bytes);
        int outputCount = f.Order.Count;

        // Real namespace shutdown cancels/drains its registered worker.
        await f.Owner.Runtime.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(f.Owner.Runtime.IsStopping); Assert.True(f.Owner.Recovery.IsBlocked);
        f.Clock.Advance(OrdinaryDelay * 5);
        await Task.Delay(800); // Exceeds two real worker delay periods.
        f.AssertFiles(second); Assert.Equal(outputCount, f.Order.Count);
        Assert.Equal(1400, target.Player.HitPoints);
        Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
    }, enabledScheduling: true);
}
