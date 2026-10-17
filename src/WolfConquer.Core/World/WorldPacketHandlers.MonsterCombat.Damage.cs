using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyMonsterBossMinimumDamageAttribute = "LegacyMonsterBossMinimumDamage";
    private const ushort LegacyTripleAttackSpellId = 10_490;
    private const ushort LegacyMonsterHunterSpellId = 15_950;
    private const ushort LegacyCombatGearDragonhowlWarSuitNormalAttack1SpellId = 17_460;
    private const ushort LegacyCombatGearDragonhowlWarSuitNormalAttack2SpellId = 17_470;
    private const ushort LegacyCombatGearDragonhowlWarSuitNormalAttack3SpellId = 17_480;
    private const ushort LegacyCombatGearDragonhowlTripleAttackSpellId = 17_490;
    private const ushort LegacyCombatGearBloodlustWarSuitNormalAttack1SpellId = 17_500;
    private const ushort LegacyCombatGearBloodlustWarSuitNormalAttack2SpellId = 17_510;
    private const ushort LegacyCombatGearBloodlustWarSuitNormalAttack3SpellId = 17_520;
    private const ushort LegacyCombatGearBloodlustTripleAttackSpellId = 17_530;
    private const ushort LegacyCombatGearRedcurseWarSuitNormalAttack1SpellId = 17_540;
    private const ushort LegacyCombatGearRedcurseWarSuitNormalAttack2SpellId = 17_550;
    private const ushort LegacyCombatGearRedcurseWarSuitNormalAttack3SpellId = 17_560;
    private const ushort LegacyCombatGearRedcurseTripleAttackSpellId = 17_570;
    private const ushort LegacyCombatGearCrackShotSpellId = 18_690;
    private const ushort LegacyCombatGearOverlordSpellId = 21_490;
    private const int LegacyMagicLevelFieldIndex = 8;
    private const int LegacyMagicGDamageFieldIndex = 10;
    private const int LegacyMonsterCombatDragonSwingFlag = 0xA0;

    private readonly record struct LegacyMonsterPhysicalDamageResult(
        uint Damage,
        bool Dodged,
        bool SendZeroDamage = false,
        uint MonsterMinAttack = 0,
        uint MonsterMaxAttack = 0,
        uint SelectedAttack = 0,
        uint PlayerPhysicalDefense = 0,
        uint PlayerAdjustedPhysicalDefense = 0,
        uint PlayerItemBless = 0,
        uint PlayerTortoiseDamagePercent = 0,
        bool ConsumesHawksAmbition = false,
        uint DamageBeforeDodge = 0,
        bool AllowsLightningShield = false);

    private readonly record struct LegacyMonsterMagicDamageResult(
        uint Damage,
        bool SendZeroDamage = false,
        bool ConsumesHawksAmbition = false);

    private readonly record struct LegacyPlayerPlayerPhysicalSpellDamageResult(
        uint Damage,
        MsgAttackEffect Effect);

    private static int GetLegacyMonsterNameType(int attackerLevel, int monsterLevel)
    {
        var levelDelta = attackerLevel - monsterLevel;
        if (levelDelta >= 3)
        {
            return 1;
        }

        if (levelDelta >= 0)
        {
            return 2;
        }

        return levelDelta >= -5 ? 3 : 4;
    }

    private static uint CalculateLegacyPlayerMonsterDamage(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        MsgAttackQuery query)
    {
        if (LegacyMonsterDamageRules.TakesOnePlayerDamage(monster, monsterFamily))
        {
            return 1;
        }

        var fields = CalculateLegacyRuntimeStatusFields(session, context.WorldData);
        var minAttack = fields[LegacyPlayerStatusPacket.MinAttackIndex];
        var maxAttack = fields[LegacyPlayerStatusPacket.MaxAttackIndex];

        var damage = SelectLegacyPlayerMonsterPhysicalAttack(minAttack, maxAttack);
        damage = ApplyLegacyPlayerMonsterAjustAttack(damage, fields, session);
        damage = ApplyLegacyPlayerMonsterAjustMaxAttack(damage, session.Character);

        var defense = ReadMonsterDefense(monsterFamily);
        damage = damage <= defense ? 0 : damage - defense;
        damage = ApplyLegacyPlayerMonsterPhysicalStatusMultipliers(
            damage,
            session.Character,
            monster,
            monsterDamageBonusPercent: CalculateLegacyPlayerMonsterPhysicalDamageBonusPercent(
                session,
                context.WorldData,
                monster));
        damage = Math.Max(
            damage,
            CalculateLegacyMinimumUserMonsterDamage(
                session.Character,
                session.PlayerItems,
                session.AlternateEquipmentActive));
        damage = CalculateLegacyUserMonsterLevelDamage(
            damage,
            defense,
            CheckedInt32(session.Character.Level, nameof(session.Character.Level)),
            monster.Level,
            range: false);

        var defense2 = ReadAttributeUInt32(monsterFamily.Attributes, "defense2", "defence2", "Defense2", "Defence2");
        if (defense2 > 0)
        {
            damage = BigMulDivToUInt32(damage, defense2, 10_000);
            damage = LegacyCombatDamageCalculator.CalculateLegacyExtraAttack(
                damage,
                fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex],
                defenseDecrease: 0);
        }

        damage = ApplyLegacyPlayerMonsterPhysicalPostDefense2Effects(
            damage,
            fields,
            session.Character);

        if (LegacyMonsterDamageRules.IsLegacyGuardFamily(monster, monsterFamily))
        {
            damage /= 10;
        }

        damage = ApplyLegacyDeathSighInfinityMonsterPhysicalDamage(
            damage,
            session.Character,
            activeSpell: false);

        return Math.Max(1, damage);
    }

    private static uint ApplyLegacyHawksAmbitionDamageReduction(
        CharacterSnapshot character,
        uint damage,
        out bool consumed)
    {
        consumed = HasLegacyStatusFlag(character, LegacyHawksAmbitionFlag);
        if (!consumed)
        {
            return damage;
        }

        var power = ReadAttributeUInt32(character.Attributes, "HawksAmbitionPower");
        if (power == 0 || damage == 0)
        {
            return damage;
        }

        var reduction = (ulong)damage * power / 100UL;
        return reduction >= damage ? 0 : damage - checked((uint)reduction);
    }

    private static LegacyMonsterPhysicalDamageResult CalculateLegacyMonsterPhysicalDamage(
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        WorldSession session,
        WorldDataCatalog? worldData,
        bool melee = false,
        bool allowDodge = false)
    {
        if (HasLegacyStatusFlag(session.Character, LegacyShurikenVortexFlag))
        {
            return new LegacyMonsterPhysicalDamageResult(1, Dodged: false);
        }

        if (!melee && HasLegacyStatusFlag(session.Character, LegacyImmortalForceFlag))
        {
            return new LegacyMonsterPhysicalDamageResult(0, Dodged: false, SendZeroDamage: true);
        }

        if (string.Equals(monster.Name, "AncientDragon", StringComparison.Ordinal))
        {
            return new LegacyMonsterPhysicalDamageResult(0, Dodged: false, SendZeroDamage: true);
        }

        var minAttack = ReadAttributeUInt32(monsterFamily.Attributes, "attack_min");
        var maxAttack = ReadAttributeUInt32(monsterFamily.Attributes, "attack_max");
        var fields = CalculateLegacyRuntimeStatusFields(session, worldData);

        var attributeDefense = ReadAttributeUInt32(
            session.Character.Attributes,
            "AjustDefense",
            "AdjustDefense",
            "Defense",
            "Defence");
        var defense = Math.Max(attributeDefense, fields[LegacyPlayerStatusPacket.DefenceIndex]);
        var selectedBossMinimumDamage = monster.Boss
            ? SelectLegacyMonsterBossMinimumDamage(session.Character)
            : (uint?)null;

        var dodge = 0U;
        uint? firstDodgeRoll = null;
        uint? secondDodgeRoll = null;
        var dodged = false;
        if (allowDodge)
        {
            var attributeDodge = ReadAttributeUInt32(session.Character.Attributes, "Dodge", "DodgeRate");
            dodge = Math.Max(attributeDodge, fields[LegacyPlayerStatusPacket.DodgeIndex]);
            if (dodge > 0)
            {
                firstDodgeRoll = ReadLegacyMonsterDodgeRoll(
                    session.Character,
                    RuntimeLegacyMonsterDodgeRollAttribute);
                if (firstDodgeRoll.Value % 150 > 60)
                {
                    secondDodgeRoll = ReadLegacyMonsterDodgeRoll(
                        session.Character,
                        RuntimeLegacyMonsterDodgeSecondaryRollAttribute);
                }

                dodged = !LegacyMonsterPlayerPhysicalDamageCalculator.CheckLegacyDodgeHit(
                    dodge,
                    firstDodgeRoll.Value,
                    secondDodgeRoll);
            }
        }

        var itemBless = fields[LegacyPlayerStatusPacket.ItemBlessIndex];
        var tortoiseDamagePercent = fields[LegacyPlayerStatusPacket.DamageIndex];
        var defenseAdjustments = ReadLegacyMonsterTargetPhysicalDefenseAdjustments(session.Character);
        var adjustedDefense = LegacyCombatDamageCalculator.CalculateAdjustedPhysicalDefense(
            defense,
            tortoiseDamagePercent,
            defenseAdjustments);
        var attackRoll = SelectLegacyMonsterPhysicalAttackRoll(session.Character, minAttack, maxAttack);
        var selectedAttack = LegacyMonsterPlayerPhysicalDamageCalculator.SelectLegacyAttack(
            minAttack,
            maxAttack,
            attackRoll);
        var damageBeforeDodge = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromSelectedAttackWithDefense(
            selectedAttack,
            playerPhysicalDefense: defense,
            playerBlessPercent: itemBless,
            playerTortoiseGemDamagePercent: tortoiseDamagePercent,
            physicalDamagePercent: LegacyServerConfigPhysicalDamagePercent,
            selectedBossMinimumDamage: selectedBossMinimumDamage,
            playerDefenseAdjustments: defenseAdjustments);
        damageBeforeDodge = ApplyLegacyHawksAmbitionDamageReduction(
            session.Character,
            damageBeforeDodge,
            out var consumesHawksAmbition);
        var damage = dodged ? 0 : damageBeforeDodge;
        return new LegacyMonsterPhysicalDamageResult(
            damage,
            dodged,
            SendZeroDamage: dodged || damage == 0,
            minAttack,
            maxAttack,
            selectedAttack,
            defense,
            adjustedDefense,
            itemBless,
            tortoiseDamagePercent,
            consumesHawksAmbition,
            damageBeforeDodge,
            AllowsLightningShield: true);
    }

    private static uint SelectLegacyMonsterBossMinimumDamage(CharacterSnapshot character)
    {
        if (TryReadUInt32Attribute(
                character.Attributes,
                out var forcedDamage,
                RuntimeLegacyMonsterBossMinimumDamageAttribute))
        {
            return forcedDamage;
        }

        return checked((uint)Random.Shared.Next(
            (int)LegacyMonsterPlayerPhysicalDamageCalculator.BossMinimumDamage,
            (int)LegacyMonsterPlayerPhysicalDamageCalculator.BossMaximumExclusiveDamage));
    }

    private static uint SelectLegacyMonsterPhysicalAttackRoll(
        CharacterSnapshot character,
        uint minAttack,
        uint maxAttack)
    {
        var lower = Math.Min(minAttack, maxAttack);
        var upper = Math.Max(minAttack, maxAttack);
        var width = upper - lower;
        if (width == 0)
        {
            return 0;
        }

        if (TryReadUInt32Attribute(
                character.Attributes,
                out var forcedRoll,
                RuntimeLegacyMonsterPhysicalAttackRollAttribute))
        {
            return forcedRoll % width;
        }

        return checked((uint)Random.Shared.NextInt64(width));
    }

    private static LegacyPhysicalDefenseAdjustments ReadLegacyMonsterTargetPhysicalDefenseAdjustments(
        CharacterSnapshot character)
    {
        var hookMoonPower = ReadAttributeUInt32(
            character.Attributes,
            "HookMoonAttackedPower",
            "HookMoonPower");
        var hasHookMoon = IsLegacyRuntimeBoolAttributeSet(
            character,
            "HookMoon",
            "HookMoonActive");

        return new LegacyPhysicalDefenseAdjustments(
            MagicShieldOrDefensePotion:
                HasLegacyStatusFlag(character, LegacyMagicShieldFlag) ||
                IsLegacyRuntimeBoolAttributeSet(character, "OnDefensePotion", "DefensePotion"),
            DragonSwing:
                HasLegacyStatusFlag(character, LegacyMonsterCombatDragonSwingFlag) ||
                IsLegacyRuntimeBoolAttributeSet(character, "DragonSwing", "DragonSwingActive"),
            DivineGuard:
                HasLegacyStatusFlag(character, LegacyDivineGuardFlag) ||
                IsLegacyRuntimeBoolAttributeSet(character, "DivineGuard", "DivineGuardActive"),
            HookMoonAttackedPowerPercent: hasHookMoon && hookMoonPower > 0 ? hookMoonPower : null);
    }

    private static bool IsLegacyRuntimeBoolAttributeSet(CharacterSnapshot character, params string[] names)
    {
        foreach (var name in names)
        {
            var raw = ReadAttributeString(character.Attributes, name);
            if (raw.Length > 0 && TryParseLegacyBool(raw, out var parsed) && parsed)
            {
                return true;
            }
        }

        return false;
    }

    private static uint ReadLegacyMonsterDodgeRoll(CharacterSnapshot character, string attributeName)
    {
        return TryReadUInt32Attribute(character.Attributes, out var forcedRoll, attributeName)
            ? forcedRoll % 150
            : checked((uint)Random.Shared.Next(0, 150));
    }

    private static LegacyMonsterMagicDamageResult CalculateLegacyMonsterMagicDamage(
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        WorldSession session,
        WorldDataCatalog? worldData)
    {
        if (HasLegacyStatusFlag(session.Character, LegacyShurikenVortexFlag))
        {
            return new LegacyMonsterMagicDamageResult(1);
        }

        if (HasLegacyStatusFlag(session.Character, LegacyImmortalForceFlag) ||
            string.Equals(monster.Name, "AncientDragon", StringComparison.Ordinal))
        {
            return new LegacyMonsterMagicDamageResult(0, SendZeroDamage: true);
        }

        var damage = ReadAttributeUInt32(monsterFamily.Attributes, "attack_max");
        if (damage == 0)
        {
            damage = 1;
        }

        var fields = CalculateLegacyRuntimeStatusFields(session, worldData);
        damage = LegacyCombatDamageCalculator.DecreaseLegacyBless(
            damage,
            fields[LegacyPlayerStatusPacket.ItemBlessIndex]);

        var attributeMagicDefense = ReadAttributeUInt32(
            session.Character.Attributes,
            "MagicDefence",
            "MagicDefense",
            "MDefence",
            "MDefense",
            "MagicDef",
            "MDef");
        var attributeMagicDamageDecrease = ReadAttributeUInt32(
            session.Character.Attributes,
            "MagicDamageDecrease",
            "MagicDamageDefense",
            "MagicDamageDefence");
        var magicDefense = Math.Max(attributeMagicDefense, fields[LegacyPlayerStatusPacket.MagicDefenceIndex]);
        var magicDamageDecrease = Math.Max(
            attributeMagicDamageDecrease,
            fields[LegacyPlayerStatusPacket.MagicDamageDecreaseIndex]);
        var totalReduction = checked((ulong)magicDefense + magicDamageDecrease);
        damage = totalReduction >= damage ? 1 : damage - (uint)totalReduction;
        damage = ApplyLegacyHawksAmbitionDamageReduction(
            session.Character,
            damage,
            out var consumesHawksAmbition);
        return new LegacyMonsterMagicDamageResult(
            damage,
            SendZeroDamage: damage == 0,
            consumesHawksAmbition);
    }

    private static uint CalculateLegacyPlayerMonsterPhysicalSpellDamage(
        WorldSession session,
        WorldDataCatalog? worldData,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        LegacyMagicRuntimeSpell spell)
    {
        if (LegacyMonsterDamageRules.TryGetLegacyPhysicalOnMonsterFloorDamage(monster, out var floorDamage))
        {
            return floorDamage;
        }

        if (LegacyMonsterDamageRules.TakesOnePlayerDamage(monster, monsterFamily))
        {
            return LegacyMonsterDamageRules.ApplyLegacyPhysicalSpellNonLethalClamp(
                spell.Id,
                monster.Hitpoints,
                damage: 1);
        }

        var fields = CalculateLegacyRuntimeStatusFields(session, worldData);
        var damage = GetLegacyDeterministicAttack(
            fields[LegacyPlayerStatusPacket.MaxAttackIndex],
            fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        damage = ApplyLegacyPlayerMonsterAjustAttack(damage, fields, session);
        damage = ApplyLegacyPlayerMonsterAjustMaxAttack(damage, session.Character);

        var defense = ReadMonsterDefense(monsterFamily);
        damage = damage <= defense ? 0 : damage - defense;
        damage = ApplyLegacyPlayerMonsterPhysicalStatusMultipliers(
            damage,
            session.Character,
            monster,
            physicalSpellDamagePercent: Math.Max(10, spell.Damage),
            monsterDamageBonusPercent: CalculateLegacyPlayerMonsterPhysicalDamageBonusPercent(
                session,
                worldData,
                monster));
        damage = Math.Max(
            damage,
            CalculateLegacyMinimumUserMonsterDamage(
                session.Character,
                session.PlayerItems,
                session.AlternateEquipmentActive));
        damage = CalculateLegacyUserMonsterLevelDamage(
            damage,
            defense,
            CheckedInt32(session.Character.Level, nameof(session.Character.Level)),
            monster.Level,
            range: false);

        var defense2 = ReadAttributeUInt32(monsterFamily.Attributes, "defense2", "defence2", "Defense2", "Defence2");
        if (defense2 == 0)
        {
            return 0;
        }

        damage = BigMulDivToUInt32(damage, defense2, 10_000);
        damage = LegacyCombatDamageCalculator.CalculateLegacyExtraAttack(
            damage,
            fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex],
            defenseDecrease: 0);
        damage = Math.Max(1, damage);
        var whirlSigilSageDamagePercent = GetLegacyWhirlSigilSageDamagePercent(session, worldData, spell.Id);
        damage = ApplyLegacyPlayerMonsterPhysicalPostDefense2Effects(
            damage,
            fields,
            session.Character,
            whirlSigilSageDamagePercent);
        if (LegacyMonsterDamageRules.IsLegacyGuardFamily(monster, monsterFamily))
        {
            damage /= 10;
        }

        damage = ApplyLegacyDeathSighInfinityMonsterPhysicalDamage(
            damage,
            session.Character,
            activeSpell: true);

        return LegacyMonsterDamageRules.ApplyLegacyPhysicalSpellNonLethalClamp(
            spell.Id,
            monster.Hitpoints,
            damage);
    }

    private static uint CalculateLegacyPlayerMonsterPhysicalFactorDamage(
        WorldSession session,
        WorldDataCatalog? worldData,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint factor)
    {
        if (LegacyMonsterDamageRules.TakesOnePlayerDamage(monster, monsterFamily))
        {
            return 1;
        }

        var fields = CalculateLegacyRuntimeStatusFields(session, worldData);
        var damage = GetLegacyDeterministicAttack(
            fields[LegacyPlayerStatusPacket.MaxAttackIndex],
            fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        damage = ApplyLegacyPlayerMonsterAjustAttack(damage, fields, session);
        damage = ApplyLegacyPlayerMonsterAjustMaxAttack(damage, session.Character);

        var defense = ReadMonsterDefense(monsterFamily);
        damage = damage <= defense ? 0 : damage - defense;
        damage = ApplyLegacyPlayerMonsterPhysicalStatusMultipliers(
            damage,
            session.Character,
            monster,
            multipleDamage: factor);
        damage = Math.Max(
            damage,
            CalculateLegacyMinimumUserMonsterDamage(
                session.Character,
                session.PlayerItems,
                session.AlternateEquipmentActive));
        damage = CalculateLegacyUserMonsterLevelDamage(
            damage,
            defense,
            CheckedInt32(session.Character.Level, nameof(session.Character.Level)),
            monster.Level,
            range: false);

        var defense2 = ReadAttributeUInt32(monsterFamily.Attributes, "defense2", "defence2", "Defense2", "Defence2");
        if (defense2 == 0)
        {
            return 0;
        }

        damage = BigMulDivToUInt32(damage, defense2, 10_000);
        damage = LegacyCombatDamageCalculator.CalculateLegacyExtraAttack(
            damage,
            fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex],
            defenseDecrease: 0);
        damage = Math.Max(1, damage);
        damage = ApplyLegacyPlayerMonsterPhysicalPostDefense2Effects(
            damage,
            fields,
            session.Character);
        if (LegacyMonsterDamageRules.IsLegacyGuardFamily(monster, monsterFamily))
        {
            damage /= 10;
        }

        damage = ApplyLegacyDeathSighInfinityMonsterPhysicalDamage(
            damage,
            session.Character,
            activeSpell: false);

        return damage;
    }

    private static uint ApplyLegacyPlayerMonsterPhysicalStatusMultipliers(
        uint damage,
        CharacterSnapshot character,
        LegacyMonsterInstance monster,
        uint physicalSpellDamagePercent = 100,
        uint multipleDamage = 0,
        uint monsterDamageBonusPercent = 0)
    {
        if (HasLegacyStatusFlag(character, LegacySupermanFlag))
        {
            damage = LegacyCombatDamageCalculator.AddLegacySaturating(
                damage,
                LegacyCombatDamageCalculator.AddLegacySaturating(damage, damage));
        }

        if (HasLegacyStatusFlag(character, LegacyFatalStrikeFlag))
        {
            damage = MulDivToUInt32(damage, 500, 100);
        }
        else if (multipleDamage != 0)
        {
            damage = MulDivToUInt32(damage, multipleDamage, 1);
        }
        else
        {
            damage = MulDivToUInt32(
                damage,
                AddLegacySaturating(physicalSpellDamagePercent, monsterDamageBonusPercent),
                100);
        }

        if (HasLegacyStatusFlag(character, LegacyManiacDanceFlag))
        {
            damage = MulDivToUInt32(damage, monster.Boss ? 3U : 10U, 1);
        }

        if (HasLegacyStatusFlag(character, LegacyOblivionFlag))
        {
            damage = MulDivToUInt32(damage, 200, 100);
        }

        return damage;
    }

    private static uint ApplyLegacyDeathSighInfinityMonsterPhysicalDamage(
        uint damage,
        CharacterSnapshot character,
        bool activeSpell)
    {
        damage = LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceDeathSighMonsterPhysicalDamage(
            damage,
            HasLegacyStatusFlag(character, LegacyDeathSighFlag),
            activeSpell,
            ReadAttributeUInt32(character.Attributes, "DeathSighPassive"),
            ReadAttributeUInt32(character.Attributes, "DeathSighActive"));

        return LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceInfinityMonsterPhysicalDamage(
            damage,
            HasLegacyStatusFlag(character, LegacyInfinityFlag),
            activeSpell,
            ReadAttributeUInt32(character.Attributes, "InfinityDamage", "InfinityMonsterDamage"),
            ReadAttributeUInt32(character.Attributes, "InfinitySDamage", "InfinitySpellDamage"));
    }

    private static uint CalculateLegacyPlayerMonsterMagicDamageBonusPercent(
        WorldSession session,
        WorldDataCatalog? worldData,
        LegacyMonsterInstance monster)
    {
        return monster.Boss && TryGetLegacyBossKillerRuneLevel(session, worldData, out var bossKillerLevel)
            ? checked((uint)(210 + (bossKillerLevel * 10)))
            : 0U;
    }

    private static uint CalculateLegacyPlayerMonsterPhysicalDamageBonusPercent(
        WorldSession session,
        WorldDataCatalog? worldData,
        LegacyMonsterInstance monster)
    {
        var bonus = CalculateLegacyPlayerMonsterMagicDamageBonusPercent(session, worldData, monster);
        if (TryGetLegacyMonsterHunterDamageBonusPercent(session, worldData, out var monsterHunterPercent))
        {
            bonus = LegacyCombatDamageCalculator.AddLegacySaturating(
                bonus,
                monsterHunterPercent);
        }

        return bonus;
    }

    private static bool TryGetLegacyBossKillerRuneLevel(
        WorldSession session,
        WorldDataCatalog? worldData,
        out byte level)
    {
        if (TryReadUInt32Attribute(
                session.Character.Attributes,
                out var attributeLevel,
                "BossKillerLevel",
                "BossKillerRuneLevel",
                "RuneBossKillerLevel"))
        {
            level = checked((byte)Math.Min(attributeLevel, byte.MaxValue));
            return true;
        }

        return TryGetLegacyEquippedRuneLevel(session.PlayerItems, worldData, "BossKiller", out level);
    }

    private static bool TryGetLegacyMonsterHunterDamageBonusPercent(
        WorldSession session,
        WorldDataCatalog? worldData,
        out uint percent)
    {
        percent = 0;
        foreach (var spell in session.PlayerSpells.Spells)
        {
            if (spell.Id != LegacyMonsterHunterSpellId)
            {
                continue;
            }

            return TryReadLegacyMagicRawGDamage(worldData, LegacyMonsterHunterSpellId, spell.Level, out percent);
        }

        return false;
    }

    private static bool TryReadLegacyMagicRawGDamage(
        WorldDataCatalog? worldData,
        ushort spellId,
        ushort spellLevel,
        out uint damage)
    {
        damage = 0;
        if (worldData is null ||
            !worldData.MagicByTypeId.TryGetValue(spellId, out var rows))
        {
            return false;
        }

        foreach (var row in rows)
        {
            if (row.Fields.Count <= LegacyMagicGDamageFieldIndex ||
                !TryParseUInt32(row.Fields[LegacyMagicLevelFieldIndex], out var rowLevel) ||
                rowLevel != spellLevel ||
                !TryParseUInt32(row.Fields[LegacyMagicGDamageFieldIndex], out var rawDamage))
            {
                continue;
            }

            damage = rawDamage % 1_000;
            return true;
        }

        return false;
    }

    private static uint ApplyLegacyPlayerMonsterPhysicalPostDefense2Effects(
        uint damage,
        IReadOnlyList<uint> fields,
        CharacterSnapshot character,
        uint whirlSigilSageDamagePercent = 0)
    {
        var luckyStrikeRatePercent = fields[LegacyPlayerStatusPacket.LuckyStrikePercentIndex] / 100;
        var criticalStrike = fields[LegacyPlayerStatusPacket.CriticalStrikeIndex];
        var dashRate = fields[LegacyPlayerStatusPacket.DashRateIndex];
        var luckyStrikeRoll = ReadLegacyPhysicalEffectRoll(
            character,
            RuntimeLegacyPhysicalLuckyStrikeRollAttribute);
        var refineryRoll = ReadLegacyPhysicalEffectRoll(
            character,
            RuntimeLegacyPhysicalRefineryRollAttribute);
        var dashRoll = 99U;
        if (refineryRoll % 100 >= 40)
        {
            dashRoll = ReadLegacyPhysicalEffectRoll(
                character,
                RuntimeLegacyPhysicalDashRollAttribute);
        }

        return LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourcePostDefense2Effects(
            damage,
            luckyStrikeRatePercent,
            criticalStrike,
            dashRate,
            luckyStrikeRoll,
            refineryRoll,
            dashRoll,
            whirlSigilSageDamagePercent).Damage;
    }

    private static bool TryGetLegacyGouyuTypeDamagePercent(
        WorldDataCatalog? worldData,
        int type,
        uint level,
        out uint damagePercent)
    {
        damagePercent = 0;
        if (level > int.MaxValue ||
            worldData is null ||
            !worldData.GouyuTypesByType.TryGetValue(type, out var rows))
        {
            return false;
        }

        foreach (var row in rows)
        {
            if (row.Level != (int)level ||
                row.Values.Count <= LegacyGouyuTypeDamageValueIndex ||
                row.Values[LegacyGouyuTypeDamageValueIndex] <= 0)
            {
                continue;
            }

            damagePercent = checked((uint)row.Values[LegacyGouyuTypeDamageValueIndex]);
            return true;
        }

        return false;
    }

    private static uint ReadLegacyPhysicalEffectRoll(CharacterSnapshot character, string attributeName)
    {
        return TryReadUInt32Attribute(character.Attributes, out var forcedRoll, attributeName)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
    }

    private static uint CalculateLegacyPlayerPlayerPhysicalSpellDamage(
        WorldSession attacker,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog? worldData,
        uint damageDivisor)
    {
        return CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
            attacker,
            target,
            spell,
            worldData,
            damageDivisor).Damage;
    }

    private static LegacyPlayerPlayerPhysicalSpellDamageResult CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
        WorldSession attacker,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog? worldData,
        uint damageDivisor)
    {
        return CalculateLegacyPlayerPlayerPhysicalSpellDamageWithIncreaseAttackResult(
            attacker,
            target,
            spell,
            worldData,
            damageDivisor,
            increaseAttack: 0);
    }

    private static LegacyPlayerPlayerPhysicalSpellDamageResult CalculateLegacyDirectPlayerPlayerPhysicalDamageResult(
        WorldSession attacker,
        WorldSession target,
        WorldDataCatalog? worldData)
    {
        var attackerFields = CalculateLegacyRuntimeStatusFields(attacker, worldData);
        var targetFields = CalculateLegacyRuntimeStatusFields(target, worldData);
        var damage = GetLegacyDeterministicAttack(
            attackerFields[LegacyPlayerStatusPacket.MaxAttackIndex],
            attackerFields[LegacyPlayerStatusPacket.MinAttackIndex]);
        damage = ApplyLegacyPlayerAjustAttack(damage, attackerFields, attacker.Character);

        var attributeDefense = ReadAttributeUInt32(
            target.Character.Attributes,
            "AjustDefense",
            "AdjustDefense",
            "Defense",
            "Defence");
        var defense = Math.Max(attributeDefense, targetFields[LegacyPlayerStatusPacket.DefenceIndex]);
        damage = damage <= defense ? 1 : damage - defense;
        damage = MulDivToUInt32(damage, LegacyServerConfigPhysicalDamagePercent, 100);

        return FinishLegacyPlayerPlayerPhysicalDamage(
            attacker,
            target,
            attackerFields,
            targetFields,
            damage,
            MsgAttackEffect.None,
            damageDivisor: 1);
    }

    private static uint CalculateLegacyPlayerPlayerPhysicalSpellDamageWithIncreaseAttack(
        WorldSession attacker,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog? worldData,
        uint damageDivisor,
        int increaseAttack)
    {
        return CalculateLegacyPlayerPlayerPhysicalSpellDamageWithIncreaseAttackResult(
            attacker,
            target,
            spell,
            worldData,
            damageDivisor,
            increaseAttack).Damage;
    }

    private static LegacyPlayerPlayerPhysicalSpellDamageResult CalculateLegacyPlayerPlayerPhysicalSpellDamageWithIncreaseAttackResult(
        WorldSession attacker,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog? worldData,
        uint damageDivisor,
        int increaseAttack)
    {
        var attackerFields = CalculateLegacyRuntimeStatusFields(attacker, worldData);
        var targetFields = CalculateLegacyRuntimeStatusFields(target, worldData);
        var effect = MsgAttackEffect.None;
        var damage = GetLegacyDeterministicAttack(
            attackerFields[LegacyPlayerStatusPacket.MaxAttackIndex],
            attackerFields[LegacyPlayerStatusPacket.MinAttackIndex]);
        damage = ApplyLegacyPlayerAjustAttack(damage, attackerFields, attacker.Character);

        var attributeDefense = ReadAttributeUInt32(
            target.Character.Attributes,
            "AjustDefense",
            "AdjustDefense",
            "Defense",
            "Defence");
        var defense = Math.Max(attributeDefense, targetFields[LegacyPlayerStatusPacket.DefenceIndex]);
        damage = damage <= defense ? 1 : damage - defense;

        damage = spell.Id switch
        {
            LegacyWindstormSpellId => MulDivToUInt32(damage, 50, 350),
            LegacyTripleAttackSpellId => MulDivToUInt32(damage, 75, 100),
            LegacyWaterPrisonSpellId or LegacyWaterPrisonPassiveSpellId =>
                MulDivToUInt32(damage, spell.Damage2 % 1_000, 180),
            LegacyLayTrap.DustDetachmentSpellId or LegacyLayTrap.SickleWindSpellId =>
                MulDivToUInt32(damage, spell.DamageOnHuman % 1_000, 175),
            LegacyLayTrap.DustDetachmentPassiveSpellId or LegacyLayTrap.SickleWindPassiveSpellId =>
                MulDivToUInt32(damage, spell.DamageOnHuman % 1_000, 280),
            LegacyWildFireballPassiveSpellId =>
                MulDivToUInt32(damage, spell.DamageOnHuman % 1_000, 350),
            LegacyPiratePheasantBeakSpellId =>
                MulDivToUInt32(
                    MulDivToUInt32(damage, spell.DamageOnHuman % 1_000, 350),
                    ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
                    100),
            LegacyThunderStrikerLineSpellId =>
                MulDivToUInt32(MulDivToUInt32(damage, 97, 100), spell.DamageOnHuman % 1_000, 100),
            LegacySelectorCrackingShockSpellId =>
                MulDivToUInt32(MulDivToUInt32(damage, 82, 100), spell.DamageOnMonster % 1_000, 100),
            LegacyTripleBlastsSpellId
                when LegacyGetDistance(
                    attacker.Character.X,
                    attacker.Character.Y,
                    target.Character.X,
                    target.Character.Y) < 50 =>
                MulDivToUInt32(damage, 70, 100),
            LegacySwirlingStormSpellId
                when LegacyGetDistance(
                    attacker.Character.X,
                    attacker.Character.Y,
                    target.Character.X,
                    target.Character.Y) < 50 =>
                MulDivToUInt32(damage, 110, 100),
            LegacyMortalWoundSpellId
                when LegacyGetDistance(
                    attacker.Character.X,
                    attacker.Character.Y,
                    target.Character.X,
                    target.Character.Y) < 50 =>
                MulDivToUInt32(damage, 80, 100),
            LegacyEagleEyeSpellId
                when LegacyGetDistance(
                    attacker.Character.X,
                    attacker.Character.Y,
                    target.Character.X,
                    target.Character.Y) < 50 =>
                MulDivToUInt32(damage, 45, 100),
            LegacyMoveLineSpellId
                when LegacyGetDistance(
                    attacker.Character.X,
                    attacker.Character.Y,
                    target.Character.X,
                    target.Character.Y) < 50 =>
                MulDivToUInt32(damage, 101, 100),
            LegacyBlisteringWaveSpellId
                when LegacyGetDistance(
                    attacker.Character.X,
                    attacker.Character.Y,
                    target.Character.X,
                    target.Character.Y) < 50 =>
                MulDivToUInt32(damage, 50, 100),
            LegacyKineticSparkSpellId
                when LegacyGetDistance(
                    attacker.Character.X,
                    attacker.Character.Y,
                    target.Character.X,
                    target.Character.Y) < 50 =>
                MulDivToUInt32(damage, 30, 100),
            LegacyChargingVortexSpellId =>
                MulDivToUInt32(damage, 100, 100),
            _ => MulDivToUInt32(damage, ResolveLegacyPlayerPlayerPhysicalSpellPercent(spell, increaseAttack), 100),
        };
        return FinishLegacyPlayerPlayerPhysicalDamage(
            attacker,
            target,
            attackerFields,
            targetFields,
            damage,
            effect,
            damageDivisor);
    }

    private static LegacyPlayerPlayerPhysicalSpellDamageResult FinishLegacyPlayerPlayerPhysicalDamage(
        WorldSession attacker,
        WorldSession target,
        IReadOnlyList<uint> attackerFields,
        IReadOnlyList<uint> targetFields,
        uint damage,
        MsgAttackEffect effect,
        uint damageDivisor)
    {
        damage = DecreaseLegacyBless(damage, targetFields[LegacyPlayerStatusPacket.ItemBlessIndex]);
        damage = ApplyLegacyPlayerPlayerPhysicalExtraAttack(
            damage,
            attackerFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex],
            targetFields[LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex]);
        var targetTortoiseDamagePercent = ResolveLegacyPlayerPlayerTargetTortoiseDamagePercent(
            target,
            targetFields);
        if (HasLegacyStatusFlag(attacker.Character, LegacyBonePulseStatusFlag))
        {
            effect |= MsgAttackEffect.BonePulseDestroy;
            targetTortoiseDamagePercent = ApplyLegacyBonePulseTortoiseReduction(
                targetTortoiseDamagePercent,
                ReadLegacyBonePulsePower(attacker.Character));
        }

        if (targetTortoiseDamagePercent > 0)
        {
            damage = DecreaseLegacyBless(damage, Math.Min(targetTortoiseDamagePercent, 25U));
        }

        if (damageDivisor > 1)
        {
            damage /= damageDivisor;
        }

        return new LegacyPlayerPlayerPhysicalSpellDamageResult(Math.Max(1, damage), effect);
    }

    private static LegacyPlayerPlayerPhysicalSpellDamageResult ApplyLegacyPlayerPlayerPhysicalDefensiveStance(
        WorldSession target,
        LegacyPlayerPlayerPhysicalSpellDamageResult damageResult)
    {
        return HasLegacyStatusFlag(target.Character, LegacyDefensiveStanceFlag)
            ? damageResult with
            {
                Damage = damageResult.Damage / 2,
                Effect = MsgAttackEffect.Block,
            }
            : damageResult;
    }

    private static LegacyPlayerPlayerPhysicalSpellDamageResult ApplyLegacyPlayerPlayerPhysicalSpellInfinity(
        WorldSession attacker,
        LegacyMagicRuntimeSpell spell,
        LegacyPlayerPlayerPhysicalSpellDamageResult damageResult)
    {
        if (!HasLegacyStatusFlag(attacker.Character, LegacyInfinityFlag))
        {
            return damageResult;
        }

        var infinitySpellDamagePercent = ReadAttributeUInt32(
            attacker.Character.Attributes,
            "InfinitySDamage",
            "InfinitySpellDamage");
        var damage = MulDivToUInt32(damageResult.Damage, infinitySpellDamagePercent, 100);
        if (spell.Id is LegacyBlisteringWaveSpellId or LegacyMortalWoundSpellId)
        {
            damage = MulDivToUInt32(damage, infinitySpellDamagePercent, 80);
        }

        return damageResult with { Damage = damage };
    }

    private static uint ResolveLegacyPlayerPlayerTargetTortoiseDamagePercent(
        WorldSession target,
        IReadOnlyList<uint> targetFields)
    {
        var fieldPercent = targetFields[LegacyPlayerStatusPacket.DamageIndex];
        var attributePercent = ReadAttributeUInt32(
            target.Character.Attributes,
            RuntimeLegacyTortoiseDamagePercentAttribute,
            RuntimeTortoiseDamagePercentAttribute,
            RuntimeTortoiseGemDamagePercentAttribute);
        return Math.Max(fieldPercent, attributePercent);
    }

    private static uint ReadLegacyBonePulsePower(CharacterSnapshot character)
    {
        return ReadAttributeUInt32(
            character.Attributes,
            RuntimeBonePulsePowerAttribute,
            RuntimeLegacyBonePulsePowerAttribute,
            "BonePulse");
    }

    private static uint ApplyLegacyBonePulseTortoiseReduction(uint tortoiseDamagePercent, uint bonePulsePower)
    {
        if (tortoiseDamagePercent == 0 || bonePulsePower == 0)
        {
            return tortoiseDamagePercent;
        }

        var reduction = (ulong)tortoiseDamagePercent * bonePulsePower / 100UL;
        return reduction >= tortoiseDamagePercent
            ? 0
            : tortoiseDamagePercent - checked((uint)reduction);
    }

    private static uint ResolveLegacyPlayerPlayerPhysicalSpellPercent(LegacyMagicRuntimeSpell spell, int increaseAttack)
    {
        return spell.Id switch
        {
            // Matching lqtxfwd Physical.OnPlayer clauses for the revalidated player-target IDs.
            LegacySuperTwofoldBladeSpellId when increaseAttack == 1 => 133,
            LegacyOmnipotenceSpellId => 10,
            LegacyHookMoonSpellId => spell.Damage3 % 1_000,
            LegacyThunderStrikerLayTrapSpellId or
            LegacyCleanSweepSpellId or
            LegacyAttackStatus15880SpellId or
            LegacyDeadlyStrikeSpellId or
            LegacyDeathSighSpellId or
            LegacyAxeShadowStatusSetterSpellId or
            LegacySacredBlessingSpellId or
            LegacyCelestialDanceStatusSetterSpellId => spell.DamageOnHuman % 1_000,
            LegacyHerculesSpellId => 100,
            LegacyBloodyScytheSpellId => 102,
            LegacyFastBladeSpellId or LegacyScentSwordSpellId => 110,
            LegacyMegaboltSpellId => 123,
            LegacyThunderStrikeSpellId => spell.DamageOnHuman % 1_000,
            LegacyWildDashAttackSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyIronboneSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyIronbonePassiveSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyRevengeAttackSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyArrowBladesSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyDragonTransformationPassiveSpellId => spell.DamageOnHuman % 1_000,
            LegacyDragonRisingSpellId => spell.DamageOnHuman % 1_000,
            LegacySuanniRoarSpellId => spell.DamageOnHuman % 1_000,
            LegacyDevouringStrikeSpellId => spell.DamageOnHuman % 1_000,
            LegacyPirateDrukyleSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateSpitfireSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateLavaSeaSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateStarVolcanoSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateCaptiveArrowSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateSandMistSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateSpitfirePassiveSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateStarVolcanoPassiveSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateLavaSeaPassiveSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateDrukylePassiveSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateHolySanctionSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateIceAgeSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateGiantGunSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateHolySanctionPassiveSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateTwospiendSpearPassiveSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateGiantGunPassiveSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateThunderlordAttackSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateIceAgePassiveSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPiratePheasantBeakPassiveSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyPirateTwospiendSpearSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyCombatGearDragonhowlWarSuitNormalAttack1SpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyCombatGearDragonhowlWarSuitNormalAttack2SpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyCombatGearDragonhowlWarSuitNormalAttack3SpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyCombatGearDragonhowlTripleAttackSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyCombatGearBloodlustWarSuitNormalAttack1SpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyCombatGearBloodlustWarSuitNormalAttack2SpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyCombatGearBloodlustWarSuitNormalAttack3SpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyCombatGearBloodlustTripleAttackSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyCombatGearRedcurseWarSuitNormalAttack1SpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyCombatGearRedcurseWarSuitNormalAttack2SpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyCombatGearRedcurseWarSuitNormalAttack3SpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyCombatGearRedcurseTripleAttackSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyCombatGearCrackShotSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyCombatGearOverlordSpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacySeaBurialSpellId => 110,
            LegacySelectorCannonBarrageSpellId => spell.Damage,
            LegacyWarSuit402NormalAttack1SpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyWarSuit402NormalAttack2SpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyWarSuit402NormalAttack3SpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyWildwindSpellId => spell.DamageOnMonster % 1_000,
            LegacyHeavensWrathSpellId => spell.DamageOnMonster % 1_000,
            LegacySelectorThunderRampageSpellId => spell.DamageOnHuman % 1_000,
            LegacyWindstormBattleaxeSpellId => spell.DamageOnMonster % 1_000,
            LegacyRageOfWarSpellId => spell.DamageOnMonster % 1_000,
            LegacyNormalAttack1SpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyNormalAttack2SpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyNormalAttack3SpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyWarSuit401NormalAttack1SpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyWarSuit401NormalAttack2SpellId => ResolveLegacyCombatGearPlayerBonusSpellPercent(spell),
            LegacyBonePulseSpellId => spell.Damage2 % 1_000,
            LegacyWhirlShurikenSpellId => spell.Damage2 % 1_000,
            LegacyWhirlShurikenPassiveSpellId => spell.Damage2 % 1_000,
            LegacyLightningSlashSpellId => spell.Damage2 % 1_000,
            LegacyLightningSlashPassiveSpellId => spell.Damage2 % 1_000,
            LegacyWildFireballSpellId => spell.Damage2 % 1_000,
            LegacyAngerOfStomperSpellId => spell.Damage3 % 1_000,
            _ => spell.Damage,
        };
    }

    private static uint ResolveLegacyCombatGearPlayerBonusSpellPercent(LegacyMagicRuntimeSpell spell)
    {
        var selectedPercent = 0U;
        IncludeCandidate(ResolveLegacyCombatGearDamagePercentCandidate(spell.GDamage, spell.GDamage, isGDamage: true));
        IncludeCandidate(ResolveLegacyCombatGearDamagePercentCandidate(spell.DamageOnHuman, spell.GDamage));
        IncludeCandidate(ResolveLegacyCombatGearDamagePercentCandidate(spell.Damage2, spell.GDamage));
        IncludeCandidate(ResolveLegacyCombatGearDamagePercentCandidate(spell.Damage3, spell.GDamage));
        IncludeCandidate(ResolveLegacyCombatGearDamagePercentCandidate(spell.DamageOnMonster, spell.GDamage));

        return 100 + selectedPercent;

        void IncludeCandidate(uint candidate)
        {
            if (candidate != 0 &&
                (selectedPercent == 0 || candidate < selectedPercent))
            {
                selectedPercent = candidate;
            }
        }
    }

    private static uint ResolveLegacyCombatGearDamagePercentCandidate(
        uint rawValue,
        uint rawGDamage,
        bool isGDamage = false)
    {
        if (isGDamage)
        {
            return rawValue is > 30_000 and <= 31_000
                ? rawValue % 1_000
                : 0;
        }

        return rawValue > 30_000 && rawGDamage <= 31_000
            ? rawValue % 1_000
            : 0;
    }

    private static uint ApplyLegacyPlayerPlayerPhysicalExtraAttack(
        uint damage,
        uint physicalDamageIncrease,
        uint physicalDamageDecrease)
    {
        damage = AddLegacySaturating(damage, physicalDamageIncrease);
        return damage <= physicalDamageDecrease ? 1 : damage - physicalDamageDecrease;
    }

    private static uint CalculateLegacySobNpcPhysicalSpellDamage(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog? worldData,
        uint damageMultiplier)
    {
        var fields = CalculateLegacyRuntimeStatusFields(session, worldData);
        var damage = GetLegacyDeterministicAttack(
            fields[LegacyPlayerStatusPacket.MaxAttackIndex],
            fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        damage = AddLegacyPercentBonus(damage, fields[LegacyPlayerStatusPacket.PhysicalPercentIndex]);
        damage = MulDivToUInt32(damage, spell.Damage, 100);
        damage = ApplyLegacySobNpcPhysicalSpellEffects(
            damage,
            fields,
            session.Character,
            GetLegacyWhirlSigilSageDamagePercent(session, worldData, spell.Id));
        damage = AddLegacySaturating(damage, fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex]);
        if (damageMultiplier > 1)
        {
            damage = MulDivToUInt32(damage, damageMultiplier, 1);
        }

        return Math.Max(1, damage);
    }

    private static uint CalculateLegacyDirectSobNpcPhysicalDamage(
        WorldSession session,
        WorldDataCatalog? worldData)
    {
        var fields = CalculateLegacyRuntimeStatusFields(session, worldData);
        var damage = GetLegacyDeterministicAttack(
            fields[LegacyPlayerStatusPacket.MaxAttackIndex],
            fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        damage = ApplyLegacyPlayerMonsterAjustAttack(damage, fields, session);
        damage = ApplyLegacyPlayerMonsterAjustMaxAttack(damage, session.Character);
        damage = MulDivToUInt32(damage, LegacyServerConfigPhysicalDamagePercent, 100);
        damage = ApplyLegacyDeathSighInfinityMonsterPhysicalDamage(
            damage,
            session.Character,
            activeSpell: false);
        damage = ApplyLegacyPlayerMonsterPhysicalPostDefense2Effects(
            damage,
            fields,
            session.Character);
        damage = AddLegacySaturating(damage, fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex]);
        return Math.Max(1, damage);
    }

    private static uint ApplyLegacySobNpcPhysicalSpellEffects(
        uint damage,
        IReadOnlyList<uint> fields,
        CharacterSnapshot character,
        uint whirlSigilSageDamagePercent)
    {
        var criticalStrike = fields[LegacyPlayerStatusPacket.CriticalStrikeIndex];
        var refineryRoll = ReadLegacyPhysicalEffectRoll(
            character,
            RuntimeLegacyPhysicalRefineryRollAttribute);
        if (refineryRoll % 100 < 40)
        {
            return criticalStrike == 0
                ? damage
                : ApplyLegacySobNpcPhysicalSpellCriticalDamage(
                    damage,
                    criticalStrike,
                    whirlSigilSageDamagePercent);
        }

        var dashRate = fields[LegacyPlayerStatusPacket.DashRateIndex];
        var dashRoll = ReadLegacyPhysicalEffectRoll(
            character,
            RuntimeLegacyPhysicalDashRollAttribute);
        return dashRate / 100 > dashRoll % 100
            ? ApplyLegacySobNpcPhysicalSpellCriticalDamage(
                damage,
                criticalStrike,
                whirlSigilSageDamagePercent)
            : damage;
    }

    private static uint ApplyLegacySobNpcPhysicalSpellCriticalDamage(
        uint damage,
        uint criticalStrike,
        uint whirlSigilSageDamagePercent)
    {
        var result = (ulong)damage + ((ulong)damage * (criticalStrike / 100UL) / 100UL);
        result += result * whirlSigilSageDamagePercent / 100UL;
        return result > uint.MaxValue ? uint.MaxValue : (uint)result;
    }

}

