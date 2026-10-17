using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct LegacyThundercloudRangeDamage(
        uint Damage,
        MsgAttackEffect Effect,
        bool ConsumesSlayer);

    private static LegacyThundercloudRangeDamage CalculateLegacyThundercloudMonsterRangeDamage(
        WorldSession session,
        WorldDataCatalog? worldData,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        bool thunderboltActive)
    {
        if (monster.IsFloor)
        {
            return new LegacyThundercloudRangeDamage(1, MsgAttackEffect.None, ConsumesSlayer: false);
        }

        var fields = CalculateLegacyRuntimeStatusFields(session, worldData);
        var damage = GetLegacyDeterministicAttack(
            fields[LegacyPlayerStatusPacket.MaxAttackIndex],
            fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        damage = ApplyLegacyPlayerMonsterAjustAttack(damage, fields, session);
        damage = ApplyLegacyPlayerMonsterAjustMaxAttack(damage, session.Character);
        if (session.Character.Level > monster.Level)
        {
            damage = MulDivToUInt32(damage, 2, 1);
        }

        if (thunderboltActive)
        {
            damage = MulDivToUInt32(damage, 2, 1);
        }

        damage = MulDivToUInt32(damage, LegacyServerConfigPhysicalDamagePercent, 100);
        var defense = ReadMonsterDefense(monsterFamily);
        damage = damage <= defense ? 0 : damage - defense;

        var defense2 = ReadAttributeUInt32(
            monsterFamily.Attributes,
            "defense2",
            "defence2",
            "Defense2",
            "Defence2");
        damage = BigMulDivToUInt32(damage, defense2, 10_000);
        var dodge = ReadAttributeUInt32(monsterFamily.Attributes, "Dodge", "dodge");
        var dodgeReduction = (uint)Math.Min(100, (int)(dodge * 4 / 10));
        damage = dodgeReduction >= 100
            ? 0
            : MulDivToUInt32(damage, 100 - dodgeReduction, 100);
        damage = CalculateLegacyUserMonsterLevelDamage(
            damage,
            defense,
            CheckedInt32(session.Character.Level, nameof(session.Character.Level)),
            monster.Level,
            range: true);
        damage = Math.Max(
            damage,
            CalculateLegacyMinimumUserMonsterDamage(
                session.Character,
                session.PlayerItems,
                session.AlternateEquipmentActive));

        if (defense2 == 0)
        {
            damage = 1;
        }

        if (!monster.Boss && HasLegacyStatusFlag(session.Character, LegacySupermanFlag))
        {
            damage = MulDivToUInt32(damage, 10, 1);
        }

        var effectResult = ApplyLegacyAttackStatus7000MonsterRangeEffects(
            damage,
            fields,
            session.Character);
        damage = effectResult.Damage;

        if (monsterFamily.Id == 20_211)
        {
            damage = 1;
        }

        var consumesSlayer = HasLegacyStatusFlag(session.Character, LegacySlayerFlag);
        if (consumesSlayer)
        {
            damage = MulDivToUInt32(
                damage,
                ReadAttributeUInt32(session.Character.Attributes, "SlayerPercent", "SlayerDamagePercent"),
                100);
            damage = Math.Min(damage, monster.Hitpoints == 0 ? 0 : monster.Hitpoints - 1);
        }

        damage = LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceInfinityMonsterPhysicalDamage(
            damage,
            HasLegacyStatusFlag(session.Character, LegacyInfinityFlag),
            spellDamage: false,
            ReadAttributeUInt32(session.Character.Attributes, "InfinityDamage", "InfinityMonsterDamage"),
            ReadAttributeUInt32(session.Character.Attributes, "InfinitySDamage", "InfinitySpellDamage"));

        if (IsLegacyAttackStatus7000ForcedOneDamageMonster(monster))
        {
            damage = 1;
        }

        return new LegacyThundercloudRangeDamage(
            damage,
            effectResult.Effect,
            consumesSlayer);
    }

    private static LegacyThundercloudRangeDamage CalculateLegacyThundercloudSobNpcRangeDamage(
        WorldSession session,
        WorldDataCatalog? worldData)
    {
        var fields = CalculateLegacyRuntimeStatusFields(session, worldData);
        var damage = GetLegacyDeterministicAttack(
            fields[LegacyPlayerStatusPacket.MaxAttackIndex],
            fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        damage = ApplyLegacyPlayerMonsterAjustAttack(damage, fields, session);
        damage = MulDivToUInt32(damage, LegacyServerConfigPhysicalDamagePercent, 100);
        damage = BigMulDivToUInt32(
            damage,
            10_000,
            session.Character.Reborn >= 2 ? 5_000U : 10_000U);
        damage = Math.Max(1, damage);

        var effect = MsgAttackEffect.None;
        var criticalStrike = fields[LegacyPlayerStatusPacket.CriticalStrikeIndex];
        var refineryRoll = ReadLegacyPhysicalEffectRoll(
            session.Character,
            RuntimeLegacyPhysicalRefineryRollAttribute);
        if (IsLegacyAttackStatus7000SourceRate(40, refineryRoll) && criticalStrike != 0)
        {
            effect |= MsgAttackEffect.CriticalStrike;
            var criticalBonus = (ulong)damage * (criticalStrike / 100UL) / 100UL;
            damage = AddLegacySaturating(
                damage,
                criticalBonus > uint.MaxValue ? uint.MaxValue : (uint)criticalBonus);
        }

        if (session.Character.Class is >= 4_000 and <= 4_051)
        {
            damage = MulDivToUInt32(damage, 50, 100);
        }

        if (TryGetLegacyEquippedRuneLevel(
                session.PlayerItems,
                worldData,
                "Breakdown",
                out var breakdownLevel))
        {
            damage = AddLegacySaturating(
                damage,
                MulDivToUInt32(damage, (uint)(110 + breakdownLevel * 10), 100));
        }

        var consumesSlayer = HasLegacyStatusFlag(session.Character, LegacySlayerFlag);
        if (consumesSlayer)
        {
            damage = MulDivToUInt32(
                damage,
                ReadAttributeUInt32(session.Character.Attributes, "SlayerPercent", "SlayerDamagePercent"),
                100);
        }

        damage = LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceInfinityMonsterPhysicalDamage(
            damage,
            HasLegacyStatusFlag(session.Character, LegacyInfinityFlag),
            spellDamage: false,
            ReadAttributeUInt32(session.Character.Attributes, "InfinityDamage", "InfinityMonsterDamage"),
            ReadAttributeUInt32(session.Character.Attributes, "InfinitySDamage", "InfinitySpellDamage"));

        return new LegacyThundercloudRangeDamage(damage, effect, consumesSlayer);
    }
}
