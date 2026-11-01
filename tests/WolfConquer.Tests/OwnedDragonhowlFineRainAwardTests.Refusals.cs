using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlFineRainAwardTests
{
    [Theory]
    [InlineData((ushort)17_490)]
    [InlineData((ushort)17_650)]
    public Task EarnedFineRainStillDeniesARequiredLevelQueryBeforeAnyCastCost(ushort spell) =>
        Run(spell, threshold: true, async c =>
    {
        var f = c.Owner; var actor = f.Current(); var peer = f.Current(Peer);
        var a = Binding(f, Actor); var p = Binding(f, Peer);
        var beforeA = Native(a); var beforeP = Native(p); var files = f.Data.Files();
        AssertFineRain(peer, 10_000);
        Assert.False(LegacyEquipmentRebuildAdmission.ApplyCurrent(peer, LegacyEquipmentRebuildReason.LevelUp, c.LegacyClock).Admitted);
        await f.SendAsync(Actor, c.Request());
        Assert.Same(actor, f.Current()); Assert.Same(peer, f.Current(Peer));
        Assert.Equal(beforeA, Native(a)); Assert.Equal(beforeP, Native(p));
        Assert.Same(c.InitialQuery.Epoch, ReadQuery(p).Epoch); Assert.Same(c.InitialQuery.Completion, ReadQuery(p).Completion);
        Assert.True(c.Monsters.TryGetVisibleInstance(c.Monster, c.Clock.GetUtcNow(), out var monster));
        Assert.Equal(500U, monster.Hitpoints);
        Assert.Empty(f.ActorFrames); Assert.Empty(f.Frames(Peer)); f.Data.AssertFiles(files);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(a)); Assert.True(f.Store.IsBoundSpellOwnerCurrent(p));
        Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }, early: true);

    [Theory]
    [InlineData((ushort)17_490, false)]
    [InlineData((ushort)17_650, false)]
    [InlineData((ushort)17_490, true)]
    [InlineData((ushort)17_650, true)]
    public Task ChangedCapturedFineRainPowerAfterRealAwardFencesBeforePlayerDeath(ushort spell, bool threshold) =>
        Run(spell, threshold, async c =>
    {
        var f = c.Owner; var a = Binding(f, Actor); var p = Binding(f, Peer);
        var expected = ExpectedFiles(c, dead: false);
        Exception? assertionFailure = null; int changed = 0;
        f.BeforeSend = (recipient, bytes) =>
        {
            if (recipient != Peer || !bytes.AsSpan().SequenceEqual(threshold ? Exp9 : Exp10)) return;
            try
            {
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                Assert.Equal(0, changed++); AssertAward(c, p); f.Data.AssertFiles(expected);
                // This actual public DATA alias exists on the captured holder.
                // Never replace its owner, install a status or alter a private
                // registry. If it becomes immutable, the setup must fail.
                var fields = Assert.IsType<Dictionary<string, string>>(f.Current(Peer).Character.Attributes);
                Assert.Equal("100", fields["FineRainPower"]); fields["FineRainPower"] = "101";
            }
            catch (Exception error) { assertionFailure = error; throw; }
        };
        try { await f.Handler.HandleFrameAsync(f.Connection(Actor), OwnedEquipmentBehaviorFixture.ReadFrame(c.Request())); }
        finally { f.BeforeSend = null; }
        if (assertionFailure is not null) ExceptionDispatchInfo.Capture(assertionFailure).Throw();
        Assert.Equal(1, changed);
        Assert.True(f.Recovery.IsBlocked); Assert.True(a.IsRetired); Assert.True(p.IsRetired);
        Assert.False(f.Store.TryGet(f.Connection(Actor), out _)); Assert.False(f.Store.TryGet(f.Connection(Peer), out _));
        Assert.False(a.Client.OnAutoAttack); Assert.False(p.Client.OnAutoAttack);
        Assert.Equal(493U, a.Player.Mana); Assert.Equal(95, (int)a.Player.Stamina); Assert.Equal(639_778_909U, a.Player.ConquerPoints);
        Assert.Equal(c.BeforeDeathHp, p.Player.HitPoints); Assert.Equal((uint)c.BeforeDeathHp, p.Client.Status.MaxHitpoints);
        Assert.Equal(threshold ? 11 : 10, (int)p.Player.Level); Assert.Equal(threshold ? 9UL : 10UL, p.Player.Experience);
        Assert.False(c.Monsters.TryGetVisibleInstance(c.Monster, c.Clock.GetUtcNow(), out _));
        Assert.DoesNotContain(f.ActorFrames, bytes => Animation(bytes, spell));
        Assert.DoesNotContain(f.Frames(Peer), bytes => bytes.AsSpan().SequenceEqual(Dead30) || bytes.AsSpan().SequenceEqual(Dead540));
        Assert.DoesNotContain(f.Frames(Peer), bytes => StatusVector(bytes, out var words) && Flag(words, LegacyPlayerDeathState.DeadStatusFlag));
        f.Data.AssertFiles(expected); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        int count = f.ActorFrames.Count + f.Frames(Peer).Count; var stamp = a.Player.AttackStamp;
        c.Clock.Advance(700); await f.Handler.SendDueBasicPhysicalAttacksAsync(CancellationToken.None);
        await f.Handler.HandleFrameAsync(f.Connection(Actor), OwnedEquipmentBehaviorFixture.ReadFrame(c.Request()));
        Assert.Equal(count, f.ActorFrames.Count + f.Frames(Peer).Count); Assert.Equal(stamp, a.Player.AttackStamp);
        Assert.Equal(639_778_909U, a.Player.ConquerPoints); Assert.Equal(c.BeforeDeathHp, p.Player.HitPoints);
        f.Data.AssertFiles(expected);
    });

    private static object Native(LegacyOrdinarySpellBinding binding) => new
    {
        Stamp = Unsafe.BitCast<DateTime, long>(binding.Player.AttackStamp), binding.Player.HitPoints,
        binding.Player.Mana, binding.Player.Stamina, binding.Player.Level, binding.Player.Experience,
        binding.Player.ConquerPoints, binding.Player.Strength, binding.Player.Agility, binding.Player.Vitality,
        binding.Player.Spirit, binding.Player.Atributes, binding.Client.OnAutoAttack, binding.Client.AutoAttack,
    };
}
