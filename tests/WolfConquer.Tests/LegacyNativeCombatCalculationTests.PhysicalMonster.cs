using ProtoBuf;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Effect = WolfConquer.Core.World.LegacyNativeMsgAttackPacket.AttackEffect;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    private static LegacyNativeMonsterRole CombatMonster(Scope s, int seed = 1)
    {
        Set(s.Player, "_level", (ushort)100); Set(s.Player, "_class", 2000u);
        s.Client.Status.MaxAttack = s.Client.Status.MinAttack = 1000;
        LegacyNativeAttackBase.MyRandom = new LegacyNativeSafeRandom(seed);
        var monster = s.Monster(100); monster.Level = 100; monster.Family.Level = 100;
        monster.Family.Defense2 = 10000; return monster;
    }

    [Fact]
    public Task WholePhysicalCallerUsesRealOwnersAndFourDrawBaselineWithoutReceivingOrKilling() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); uint hp = monster.HitPoints;
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, null, out var obj);
        Assert.Equal(monster.UID, obj.UID); Assert.Equal(1200u, obj.Damage); Assert.Equal(Effect.None, obj.Effect);
        Assert.Equal(1u, obj.Hit); Assert.Equal(hp, monster.HitPoints); Assert.Equal(0u, s.Player.Kills); Assert.Empty(s.Queue);
        var random = new Random(1); for (int i = 0; i < 4; i++) _ = random.Next();
        Assert.Equal(random.Next(), LegacyNativeAttackBase.MyRandom.Next());
    });

    [Theory]
    [InlineData(0, 1200u)] [InlineData(1, 3600u)] [InlineData(2, 6000u)]
    [InlineData(3, 4200u)] [InlineData(4, 4800u)] [InlineData(5, 6000u)]
    public Task WholePhysicalCallerPreservesSourceAttackMultiplierPriority(int mode, uint expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s);
        var spell = new LegacyNativeMagicType.Magic { ID = 500, Damage = 100 };
        byte multiple = 0;
        if (mode == 1) s.Flag(Flag.Superman);
        if (mode == 2) { s.Flag(Flag.FatalStrike); spell.ID = 12860; multiple = 4; }
        if (mode == 3) { spell.ID = 12860; multiple = 4; }
        if (mode == 4) { multiple = 4; spell.ID = 12770; }
        if (mode == 5) spell.ID = 12770;
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, spell, out var obj, multiple);
        Assert.Equal(expected, obj.Damage); Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(false, 6000u)] [InlineData(true, 4200u)]
    public Task Special12770KeepsBossSpecificMultiplier(bool boss, uint expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); monster.Boss = boss ? (byte)1 : (byte)0;
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, new LegacyNativeMagicType.Magic { ID = 12770, Damage = 100 }, out var obj);
        Assert.Equal(expected, obj.Damage);
    });

    [Theory]
    [InlineData(false, 12000u)] [InlineData(true, 3600u)]
    public Task ManiacDanceUsesWholeBossBranchAndOblivionFollowsIt(bool boss, uint expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); monster.Boss = boss ? (byte)1 : (byte)0;
        s.Flag(Flag.ManiacDance);
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, null, out var obj); Assert.Equal(expected, obj.Damage);
        LegacyNativeAttackBase.MyRandom = new LegacyNativeSafeRandom(1); s.Flag(Flag.Oblivion);
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, null, out obj); Assert.Equal(expected * 2, obj.Damage);
    });

    [Theory]
    [InlineData(0.0f, 120u)] [InlineData(9.9f, 120u)] [InlineData(float.NaN, 13u)]
    [InlineData(float.PositiveInfinity, 13u)] [InlineData(float.MaxValue, 13u)]
    public Task SpellDamageFloorMutationAndExceptionalFactorStayInWholeCaller(float factor, uint expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); var spell = new LegacyNativeMagicType.Magic { ID = 500, Damage = factor };
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, spell, out var obj); Assert.Equal(expected, obj.Damage);
        if (!float.IsNaN(factor) && factor < 10) Assert.Equal(10f, spell.Damage); else Assert.Equal(factor, spell.Damage);
    });

    [Fact]
    public Task RealCombatGearSpellBranchUsesAllFiveNativeDamageFieldsRatherThanSpellFloat() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s);
        var spell = new LegacyNativeMagicType.Magic { ID = 17460, Damage = 0, GDamage = 30010,
            DamageOnHuman = 30020, Damage2 = 30050, Damage3 = 30030, DamageOnMonster = 30040 };
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, spell, out var obj);
        Assert.Equal(1800u, obj.Damage); Assert.Equal(10f, spell.Damage);
    });

    [Fact]
    public Task DefenseIsSubtractedBeforeAndInsideLevelFormulaAndNonpositiveDefenseTwoLeavesZero() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); monster.Family.Defense = 100;
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, null, out var obj); Assert.Equal(980u, obj.Damage);
        monster.Family.Defense2 = 0; LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, null, out obj);
        Assert.Equal(0u, obj.Damage); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task LuckyStrikeUsesRealRefinePacketAndCommitsEffectAfterSend() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); s.Client.PerfectionStatus.LuckyStrike = 100;
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, null, out var obj);
        Assert.Equal(2400u, obj.Damage); Assert.Equal(Effect.LuckyStrike, obj.Effect);
        byte[] bytes = Assert.Single(s.Queue); Assert.Equal((ushort)2319, BitConverter.ToUInt16(bytes, 2));
        using var payload = new MemoryStream(bytes, 4, bytes.Length - 4);
        var refine = Serializer.Deserialize<LegacyNativeMsgRefineEffect.RefineEffectProto>(payload);
        Assert.Equal(s.Player.UID, refine.Id); Assert.Equal(LegacyNativeMsgRefineEffect.RefineEffects.LuckyStrike, refine.Effect);
    });

    [Theory]
    [InlineData(2, false, 2880u, 4u)] [InlineData(1, true, 3600u, 134217728u)]
    public Task RealNinjaWhirlSigilScalesBothRefineryAndDashBranches(int seed, bool dash, uint expected, uint effect)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s, seed); Set(s.Player, "_class", 5000u);
        s.Client.Status.CriticalStrike = 15000; s.Client.Status.DashRate = dash ? 10000u : 0;
        s.Client.MyNinja!.Items[1] = new() { ItemID = 404, Position = 1, Level = 1 };
        LegacyNativeNinjaFile.gouyu_type[1] = new() { ItemID = 404, Level = 1, Power = -1, Damage = 20 };
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, new LegacyNativeMagicType.Magic { ID = 16420, Damage = 100 }, out var obj);
        Assert.Equal(expected, obj.Damage); Assert.Equal((Effect)effect, obj.Effect); Assert.Empty(s.Queue);
    });
}
