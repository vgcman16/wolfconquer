using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Effect = WolfConquer.Core.World.LegacyNativeMsgAttackPacket.AttackEffect;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Fact]
    public Task FloorBranchPreservesRealEarlyReturnBeforePlayerOrRngWithoutStandingInForWholeCaller() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); monster.IsFloor = true;
        LegacyNativeCalculatePhysical.OnMonster(null!, monster, null, out var obj); Assert.Equal(2u, obj.Damage);
        Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next()); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task MissingRequiredRuneRejectsBeforeCounterDamageAndRng() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); s.Client.Rune = null;
        var sentinel = PhysicalAdmissionSentinel(); var obj = sentinel;
        var before = PhysicalAdmissionResultValues(obj);
        var prefix = new PhysicalAdmissionPrefixSnapshot(s, monster);
        Assert.Throws<InvalidDataException>(() => LegacyNativeCalculatePhysical.OnMonster(s.Player, monster,
            new LegacyNativeMagicType.Magic { ID = 16330, Damage = 100 }, out obj));
        Assert.Same(sentinel, obj); Assert.Equal(before, PhysicalAdmissionResultValues(obj));
        prefix.AssertUnchanged(s); Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next());
    });

    [Fact]
    public Task MissingRequiredSigilOwnerRejectsBeforeDamageAndLuckyRoll() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); s.Client.MyNinja = null;
        var sentinel = PhysicalAdmissionSentinel(); var obj = sentinel;
        var before = PhysicalAdmissionResultValues(obj);
        var prefix = new PhysicalAdmissionPrefixSnapshot(s, monster);
        Assert.Throws<InvalidDataException>(() => LegacyNativeCalculatePhysical.OnMonster(s.Player, monster,
            new LegacyNativeMagicType.Magic { ID = 16420, Damage = 100 }, out obj));
        Assert.Same(sentinel, obj); Assert.Equal(before, PhysicalAdmissionResultValues(obj));
        prefix.AssertUnchanged(s); Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next());
    });

    [Fact]
    public Task LuckyRecycleFaultRemainsSeparateFromRequiredOwnerAdmission() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); s.Client.PerfectionStatus.LuckyStrike = 100; s.NullBin();
        LegacyNativeMsgSpellAnimation.SpellObj obj = null!;
        Assert.Throws<NullReferenceException>(() => LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, null, out obj));
        Assert.Equal(1200u, obj.Damage); Assert.Equal(Effect.None, obj.Effect); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task MissingMonsterNameRejectsBeforeOutputRngOrLuckySend() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); monster.Name = null;
        s.Client.PerfectionStatus.LuckyStrike = 100;
        var sentinel = PhysicalAdmissionSentinel(); var obj = sentinel;
        var before = PhysicalAdmissionResultValues(obj);
        var prefix = new PhysicalAdmissionPrefixSnapshot(s, monster);
        Assert.Throws<InvalidDataException>(() => LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, null, out obj));
        Assert.Same(sentinel, obj); Assert.Equal(before, PhysicalAdmissionResultValues(obj));
        prefix.AssertUnchanged(s); Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next());
    });
}
