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
    public Task MissingLateRuneRetainsRealCounterAndDamagePrefixRatherThanReportingSuccess() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); s.Client.Rune = null;
        LegacyNativeMsgSpellAnimation.SpellObj obj = null!;
        Assert.Throws<NullReferenceException>(() => LegacyNativeCalculatePhysical.OnMonster(s.Player, monster,
            new LegacyNativeMagicType.Magic { ID = 16330, Damage = 100 }, out obj));
        Assert.Equal(1u, s.Player.KylinSigil); Assert.Equal(1800u, obj.Damage); Assert.Equal(Effect.None, obj.Effect); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task MissingSigilOwnerFaultsAfterDamageAndLuckyRollBeforeCriticalRoll() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); s.Client.MyNinja = null;
        LegacyNativeMsgSpellAnimation.SpellObj obj = null!;
        Assert.Throws<NullReferenceException>(() => LegacyNativeCalculatePhysical.OnMonster(s.Player, monster,
            new LegacyNativeMagicType.Magic { ID = 16420, Damage = 100 }, out obj));
        Assert.Equal(1200u, obj.Damage); var random = new Random(1); _ = random.Next(); _ = random.Next();
        Assert.Equal(random.Next(), LegacyNativeAttackBase.MyRandom.Next()); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task LuckyRecycleFaultPrecedesEffectCommitAndNullNameFaultIsLate() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); s.Client.PerfectionStatus.LuckyStrike = 100; s.NullBin();
        LegacyNativeMsgSpellAnimation.SpellObj obj = null!;
        Assert.Throws<NullReferenceException>(() => LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, null, out obj));
        Assert.Equal(1200u, obj.Damage); Assert.Equal(Effect.None, obj.Effect); Assert.Empty(s.Queue);
        s.Client.PerfectionStatus.LuckyStrike = 0; monster.Name = null;
        Assert.Throws<NullReferenceException>(() => LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, null, out obj));
        Assert.Equal(1200u, obj.Damage); Assert.Empty(s.Queue);
    });
}
