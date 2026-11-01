using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    private const long VipLoadedHitpoints = 3_200;
    private const long VipHistoricalHitpoints = 6_200;
    private const ulong VipMaximum = TargetMaximum + 3_000;

    [Fact]
    public Task LethalVipQueryRetainsHistoricalHpFramesWhileEveryObserverSeesCommittedDeath() => RunVipDeath(async f =>
    {
        await f.SitAsync();
        var before = f.Files();
        var after = HpImage(f, before, VipLoadedHitpoints, 0);
        var stamp = f.Clock.GetLocalNow().DateTime;
        int observedPairs = 0;
        f.BeforeSend = (_, bytes) =>
        {
            if (IsPhysical(bytes) || IsHpPair(bytes, Target))
            {
                f.AssertFiles(after); f.AssertCoherent(0); AssertCommittedActor(f, stamp);
            }
            if (!IsHpPair(bytes, Target)) return;
            Assert.InRange(observedPairs, 0, 9);
            // VIP's HP setter precedes its maximum addition. The later blood0,
            // clamp and explicit query pair retain the completed VIP maximum.
            ulong maximum = observedPairs < 2 ? TargetMaximum : VipMaximum;
            long hp = observedPairs < 8 ? VipHistoricalHitpoints : 0;
            Assert.Equal(DeathQueryHpGolden(maximum, observedPairs % 2 == 0 ? null : hp), bytes);
            observedPairs++;
        };
        try { await f.AttackAsync(); }
        finally { f.BeforeSend = null; }
        Assert.Equal(10, observedPairs);
        f.AssertFiles(after); f.AssertCoherent(0); AssertCommittedActor(f, stamp);
        Assert.Equal(VipMaximum, f.Current(Target).EquipmentHitpointsStatus!.MaximumHitpoints);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(f.Current(Target).Character.Attributes,
            LegacyPlayerDeathState.DeadStatusFlag));
        Assert.False(f.Binding().Client.OnAutoAttack);
        var physical = f.Order.Where(row => IsPhysical(row.Bytes)).ToArray();
        Assert.Equal(new[] { Actor, Target }, physical.Select(row => row.Recipient));
        Assert.True(f.Order.FindIndex(row => IsHpPair(row.Bytes, Target)) >
            f.Order.FindLastIndex(row => IsPhysical(row.Bytes)));
        int count = f.Order.Count;
        f.Clock.Advance(OrdinaryDelay * 5); await f.TickAsync();
        f.AssertFiles(after); f.AssertCoherent(0); Assert.Equal(count, f.Order.Count);
    });

    [Fact]
    public Task LethalQueryWriterRefusalCannotPublishVipHpOrReplaceItsNativeCaches() => RunVipDeath(async f =>
    {
        await f.SitAsync();
        var before = f.Files(); var actor = f.Current(); var target = f.Current(Target);
        var stamp = f.Binding().Player.AttackStamp;
        var binding = f.Binding(Target);
        Assert.True(LegacyNativeMagicQueryClient.TryRead(f.Owner.Store, binding.Connection,
            Target, out var magic, binding.NativeIdentity));
        var perfection = LegacyNativePerfectionClient.Read(f.Owner.Store, binding.Connection, Target, binding.NativeIdentity);
        Assert.Equal(3d, perfection.LuckyStrike);
        using (var lease = new FileStream(f.Owner.CharacterPath(Target), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.True(lease.CanRead);
            await f.AttackAsync();
            f.AssertFiles(before); f.AssertCoherent(VipLoadedHitpoints); Assert.Empty(f.Order);
            Assert.Same(actor, f.Current()); Assert.Same(target, f.Current(Target));
            Assert.Equal(stamp, f.Binding().Player.AttackStamp);
            Assert.Equal(250U, (uint)f.Binding().Player.Action);
            Assert.False(f.Binding().Client.OnAutoAttack);
            Assert.True(LegacyNativeMagicQueryClient.TryRead(f.Owner.Store, binding.Connection,
                Target, out var retained, binding.NativeIdentity));
            Assert.Equal(magic, retained);
            Assert.Same(perfection, LegacyNativePerfectionClient.Read(f.Owner.Store, binding.Connection, Target, binding.NativeIdentity));
            Assert.False(LegacyPlayerDeathState.HasStatusFlag(f.Current(Target).Character.Attributes,
                LegacyPlayerDeathState.DeadStatusFlag));
        }
        // The same real request/clock can be retried after the actual OS lease
        // is released; the failed attempt did not debit its cadence or owner.
        await f.AttackAsync();
        f.AssertFiles(HpImage(f, before, VipLoadedHitpoints, 0)); f.AssertCoherent(0);
        AssertCommittedActor(f, f.Clock.GetLocalNow().DateTime);
        Assert.False(f.Binding().Client.OnAutoAttack);
    });

    private static Task RunVipDeath(Func<AttackFixture, Task> body)
    {
        var clock = new AttackClock();
        var actor = Saved(Actor, "DeathOwner", 830, 5000, 10_000, false);
        var target = Saved(Target, "DeathTarget", 831, 200, 0, false);
        var attributes = new Dictionary<string, string>(target.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["VipLevel"] = "7" };
        target = target with { Attributes = attributes };
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000));
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(new(actor, PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty),
            OwnedEquipmentBehaviorFixture.Catalog(), options, async owner =>
            {
                var fixture = new AttackFixture(owner, clock);
                Assert.Equal(VipLoadedHitpoints, fixture.Current(Target).Character.MinHitPoints);
                Assert.Equal(VipMaximum, fixture.Current(Target).EquipmentHitpointsStatus!.MaximumHitpoints);
                fixture.AssertCoherent(VipLoadedHitpoints);
                fixture.Clear(); await body(fixture);
            }, new OwnedEquipmentBehaviorInput(target, PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty));
    }

    // Independent2176 tag/varint expected bytes, with explicit declared phase
    // numbers. No production packet writer supplies this expected payload.
    private static byte[] DeathQueryHpGolden(ulong maximum, long? hp)
    {
        var body = new List<byte>(); Varint(body, 8); Varint(body, Target);
        var max = new List<byte>(); Varint(max, 8); Varint(max, 1); Varint(max, 16); Varint(max, maximum);
        Varint(body, 18); Varint(body, checked((ulong)max.Count)); body.AddRange(max);
        if (hp is { } value)
        {
            var current = new List<byte>();
            if (value != 0) { Varint(current, 16); Varint(current, checked((ulong)value)); }
            Varint(body, 18); Varint(body, checked((ulong)current.Count)); body.AddRange(current);
        }
        return Frame(2176, body);
    }
}
