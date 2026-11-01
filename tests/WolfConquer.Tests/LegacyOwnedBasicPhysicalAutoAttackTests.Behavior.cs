using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    [Fact]
    public Task RealPhysicalReceiveArmsOneRepeatAtItsActualOrdinaryDeadlineWithoutCatchUpReplay() => Run(async f =>
    {
        await f.SitAsync();
        await f.HitAsync(2000, 1700, repeat: false);
        var first = f.Files(); int outputs = f.Order.Count;
        f.Clock.Advance(OrdinaryDelay - 1); await f.TickAsync();
        f.AssertFiles(first); f.AssertCoherent(1700); Assert.Equal(outputs, f.Order.Count);
        f.Clock.Advance(1); await f.HitAsync(1700, 1400, repeat: true);
        // One late worker observation is one real request, not a replay loop
        // spending each missed historical deadline.
        f.Clock.Advance(OrdinaryDelay * 5); await f.HitAsync(1400, 1100, repeat: true);
        var late = f.Files(); outputs = f.Order.Count;
        await f.TickAsync(); f.AssertFiles(late); f.AssertCoherent(1100); Assert.Equal(outputs, f.Order.Count);
    });

    [Fact]
    public Task TargetOnlyActualHundredPercentHawksCastProtectsAgainstNeverCastAttackerAndArmsRepeat() => Run(async f =>
    {
        await f.MintHawksAsync();
        await f.SitAsync();
        var before = f.Files();
        // Only the TARGET casts real15920. Its actual protection100 consumes
        // once:300-floor(300*100/100)=0. The never-cast attacker's lack of any
        // power claim catches the inherited wrong-owner read of attacker.Power.
        await f.HitAsync(2000, 2000, repeat: false); f.AssertFiles(before);
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(f.Current(Target).Character.Attributes, 256));
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(f.Current(Actor).Character.Attributes, 256));
        Assert.False(f.Current(Actor).Character.Attributes.ContainsKey("HawksAmbitionPower"));
        f.Clock.Advance(OrdinaryDelay); await f.HitAsync(2000, 1700, repeat: true);
    }, hawks: true);

    [Fact]
    public Task RealLethalPhysicalReceiveFinishesNormalDeathAndNeverRepeats() => Run(async f =>
    {
        await f.SitAsync();
        var before = f.Files(); var after = HpImage(f, before, 200, 0);
        var stamp = f.Clock.GetLocalNow().DateTime;
        int hpMessages = 0;
        f.BeforeSend = (_, bytes) =>
        {
            if (IsPhysical(bytes))
            {
                f.AssertFiles(after); f.AssertCoherent(0); AssertCommittedActor(f, stamp);
                Assert.Equal(InteractionGolden(), bytes);
            }
            if (IsHpPair(bytes, Target))
            {
                // Preserve source query's three200 pairs and final0 pair on
                // the wire. The owned outcome is already durably committed0
                // before any successful notification, including Interaction.
                Assert.InRange(hpMessages, 0, 7); long hp = hpMessages < 6 ? 200 : 0;
                f.AssertFiles(after); f.AssertCoherent(0); AssertCommittedActor(f, stamp);
                Assert.Equal(hpMessages % 2 == 0 ? MaximumGolden(Target) : HpGolden(Target, hp), bytes);
                hpMessages++;
            }
        };
        await f.AttackAsync(); f.BeforeSend = null;
        f.AssertFiles(after); f.AssertCoherent(0); AssertCommittedActor(f, stamp);
        Assert.False(f.Binding().Client.OnAutoAttack);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(f.Current(Target).Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.True(f.Current(Target).Character.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
        Assert.Equal(Actor.ToString(System.Globalization.CultureInfo.InvariantCulture),
            f.Current(Target).Character.Attributes[LegacyPlayerDeathState.DeadKillerUidAttribute]);
        var incoming = f.Order.Where(row => IsPhysical(row.Bytes)).ToArray();
        Assert.Equal(new[] { Actor, Target }, incoming.Select(row => row.Recipient));
        Assert.All(incoming, row => Assert.Equal(InteractionGolden(), row.Bytes));
        Assert.Equal(8, hpMessages);
        var pair = f.Order.Where(row => IsHpPair(row.Bytes, Target)).ToArray(); Assert.Equal(8, pair.Length);
        Assert.All(pair, row => Assert.Equal(Target, row.Recipient));
        for (int index = 0; index < pair.Length; index++)
            Assert.Equal(index % 2 == 0 ? MaximumGolden(Target) : HpGolden(Target, index < 6 ? 200 : 0), pair[index].Bytes);
        Assert.True(f.Order.FindIndex(row => IsHpPair(row.Bytes, Target)) > f.Order.FindLastIndex(row => IsPhysical(row.Bytes)));
        int outputs = f.Order.Count; var committed = f.Files();
        f.Clock.Advance(OrdinaryDelay * 5); await f.TickAsync(); await f.TickAsync();
        f.AssertFiles(committed); f.AssertCoherent(0); Assert.Equal(outputs, f.Order.Count);
    }, targetHp: 200);
}
