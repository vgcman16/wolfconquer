using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Effect = WolfConquer.Core.World.LegacyNativeMsgAttackPacket.AttackEffect;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Theory]
    [InlineData(false, false, 1500u)] [InlineData(true, false, 2700u)]
    [InlineData(false, true, 3000u)] [InlineData(true, true, 5400u)]
    public Task DeathSighAndInfinityUseSourceOrderWithActualPassiveActivePowers(bool active, bool infinity, uint expected)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); s.Flag(Flag.DeathSigh);
        s.Player.DeathSighPassive = 125; s.Player.DeathSighActive = 125;
        s.Player.InfinityDamage = s.Player.InfinitySDamage = 200; if (infinity) s.Flag(Flag.Infinity);
        LegacyNativeMagicType.Magic? spell = active ? new() { ID = 500, Damage = 100, Passive = false } : null;
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, spell, out var obj); Assert.Equal(expected, obj.Damage);
    });

    [Fact]
    public Task PassiveSpellUsesReplacementDeathSighAndSpellSpecificInfinityPower() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); s.Flag(Flag.DeathSigh); s.Flag(Flag.Infinity);
        s.Player.DeathSighPassive = 125; s.Player.DeathSighActive = 999;
        s.Player.InfinityDamage = 0; s.Player.InfinitySDamage = 150;
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, new LegacyNativeMagicType.Magic { ID = 500, Damage = 100, Passive = true }, out var obj);
        Assert.Equal(2250u, obj.Damage);
    });

    [Fact]
    public Task WholeCallerInvokesGenuineGearAttackEffectWithAliasedDamageBeforeInfinity() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s, 2); s.Gear(LegacyNativeCombatGear.TypeID.Dragonhowl, 2000);
        s.Client.Status.CriticalStrike = 15000; s.Flag(Flag.DragonPierceCrit); s.Flag(Flag.Infinity); s.Player.InfinityDamage = 150;
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, null, out var obj);
        Assert.Equal(3960u, obj.Damage); Assert.Equal(Effect.CriticalStrike | Effect.Destroy, obj.Effect);
    });

    [Theory]
    [InlineData("AncientDragon", 1u)] [InlineData("WarDevil", 1u)] [InlineData("FireSpriteElite", 1u)]
    [InlineData("firesprite", 1200u)] [InlineData("Ordinary", 1200u)]
    public Task MonsterNamesApplyExactCaseSensitiveLateOverrides(string name, uint expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); monster.Name = name;
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, null, out var obj); Assert.Equal(expected, obj.Damage);
    });

    [Theory]
    [InlineData(1199u, 1200u)] [InlineData(1200u, 0u)] [InlineData(1201u, 0u)] [InlineData(1202u, 1200u)]
    public Task NonlethalSigilCheckPreservesUInt32UnderflowInsteadOfFixingItsNativeComparison(uint hp, uint expected)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); monster.HitPoints = hp;
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, new LegacyNativeMagicType.Magic { ID = 16540, Damage = 100 }, out var obj);
        Assert.Equal(expected, obj.Damage); Assert.Equal(hp, monster.HitPoints);
    });

    [Theory]
    [InlineData(0u, 1u, 1800u)] [InlineData(1u, 2u, 1920u)] [InlineData(2u, 3u, 2100u)]
    [InlineData(3u, 1u, 1800u)] [InlineData(4u, 5u, 1200u)] [InlineData(uint.MaxValue, 0u, 1200u)]
    public Task KylinCounterKeepsAllThreeBonusesResetAndOverflow(uint prior, uint after, uint expected)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); s.Player.KylinSigil = prior;
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, new LegacyNativeMagicType.Magic { ID = 16330, Damage = 100 }, out var obj);
        Assert.Equal(after, s.Player.KylinSigil); Assert.Equal(expected, obj.Damage);
    });

    [Theory]
    [InlineData(false, 1200u)] [InlineData(true, 39200u)]
    public Task ActualNonemptyEquippedAssassinRuneAddsAutoHuntBonusLast(bool autoHunt, uint expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); s.Player.Reborn = 2; s.Player.OnAutoHunt = autoHunt;
        const uint id = 4031107;
        LegacyNativePoolItems.ItemsBase![id] = new LegacyNativeItemRuneMetadata { ID = id, RuneSpellID = id, Name = "Assassin" };
        var item = new LegacyNativeGameItemRuntime { Uid = 25, ITEM_ID = id, Position = 101 };
        Assert.True(s.Client.Rune!.Add(item)); Assert.Single(s.Client.Rune.WornObjects);
        byte level = 0; Assert.True(s.Client.Rune.IsEquipped("Assassin", ref level)); Assert.Equal(7, level);
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, null, out var obj); Assert.Equal(expected, obj.Damage);
    });

    [Fact]
    public Task GuardDivisionAndFamily20211OccurBeforeLateEffects() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); monster.Family.Settings |= LegacyNativeMonsterSettings.Guard;
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, null, out var obj); Assert.Equal(120u, obj.Damage);
        monster.Family.ID = 20211; s.Flag(Flag.Infinity); s.Player.InfinityDamage = 200;
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, null, out obj); Assert.Equal(2u, obj.Damage);
    });
}
