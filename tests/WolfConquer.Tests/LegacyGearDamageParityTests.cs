using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGearDamageParityTests
{
    private const int LegacySupermanFlag = 0x12;
    private const int LegacyFatalStrikeFlag = 0x2F;
    private const int LegacyInfinityFlag = 0xD7;
    private const int LegacyDeathSighFlag = 0xFD;

    [Fact]
    public void LegacyMonsterReceiveAttackHighDefenseGearFloorsMonsterPhysicalHitToOne()
    {
        var catalog = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 130_003,
                    name: "SourceClueArmor",
                    physicalDefence: 1_200),
            ]);
        var ungearedStatus = LegacyPlayerStatusCalculator.Create(
            CreateSnapshot(),
            PlayerItemSnapshot.Empty,
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0);
        var gearedStatus = LegacyPlayerStatusCalculator.Create(
            CreateSnapshot(),
            EquipmentSnapshot(Equipment(itemId: 130_003, position: 3)),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0);

        // Source clue: references/DecompiledSource\GameServer AttackHandler/ReceiveAttack/Monster.cs floors
        // non-boss monster-vs-player physical damage when adjusted player defense covers the selected attack.
        var ungearedDamage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromSelectedAttackWithStatusFields(
            selectedAttack: 800,
            targetStatusFields: ungearedStatus.Fields);
        var gearedDamage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromSelectedAttackWithStatusFields(
            selectedAttack: 800,
            targetStatusFields: gearedStatus.Fields);

        Assert.Equal(0U, ungearedStatus.Defence);
        Assert.Equal(1_200U, gearedStatus.Defence);
        Assert.Equal(800U, ungearedDamage);
        Assert.Equal(1U, gearedDamage);
    }

    [Fact]
    public void LegacyAttackHandlerHighAttackGearRaisesPlayerPhysicalDamageAgainstMonster()
    {
        var catalog = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 480_003,
                    name: "SourceClueClub",
                    maxAttack: 6_000,
                    minAttack: 5_000),
            ]);
        var ungearedStatus = LegacyPlayerStatusCalculator.Create(
            CreateSnapshot(),
            PlayerItemSnapshot.Empty,
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0);
        var gearedStatus = LegacyPlayerStatusCalculator.Create(
            CreateSnapshot(),
            EquipmentSnapshot(Equipment(itemId: 480_003, position: 4)),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0);

        // Source clue: Game.MsgServer.AttackHandler physical damage uses the attack fields built from worn gear.
        var ungearedDamage = LegacyPlayerMonsterPhysicalDamageCalculator.CalculateFromStatusFields(
            ungearedStatus.Fields,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 140,
            monsterLevel: 120);
        var gearedDamage = LegacyPlayerMonsterPhysicalDamageCalculator.CalculateFromStatusFields(
            gearedStatus.Fields,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 140,
            monsterLevel: 120);

        Assert.Equal(0U, ungearedStatus.MinAttack);
        Assert.Equal(1U, ungearedStatus.MaxAttack);
        Assert.Equal(5_000U, gearedStatus.MinAttack);
        Assert.Equal(6_001U, gearedStatus.MaxAttack);
        Assert.True(gearedDamage > ungearedDamage);
        Assert.True(gearedDamage > 10_000U);
    }

    [Fact]
    public void LegacyMagicRuntimeDividesPlayerMagicDamageByTenForGuardFamilies()
    {
        var session = CreateWorldSession();
        var fields = new uint[LegacyPlayerStatusPacket.FieldCount];
        fields[LegacyPlayerStatusPacket.MagicAttackIndex] = 10_000;
        var spell = CreateRuntimeSpell(1_000, LegacyMagicSort.Attack, damage: 0);
        var normal = MonsterFamily("Bandit", defense: 0, defense2: 10_000, level: 140);
        var guard = MonsterFamily("Guard1", defense: 0, defense2: 10_000, level: 140);

        var normalDamage = LegacyMagicRuntime.CalculatePlayerMonsterDamage(
            session,
            worldData: null,
            normal,
            spell,
            fields);
        var guardDamage = LegacyMagicRuntime.CalculatePlayerMonsterDamage(
            session,
            worldData: null,
            guard,
            spell,
            fields);

        Assert.True(normalDamage > 0);
        Assert.Equal(normalDamage / 10, guardDamage);
    }

    [Fact]
    public void LegacyMagicRuntimeAppliesInfinitySpellPercentAfterGuardReduction()
    {
        var session = CreateWorldSession();
        var fields = new uint[LegacyPlayerStatusPacket.FieldCount];
        fields[LegacyPlayerStatusPacket.MagicAttackIndex] = 10_000;
        var spell = CreateRuntimeSpell(1_000, LegacyMagicSort.Attack, damage: 0);
        var guard = MonsterFamily("Guard1", defense: 0, defense2: 10_000, level: 140);

        var normalDamage = LegacyMagicRuntime.CalculatePlayerMonsterDamage(
            session,
            worldData: null,
            guard,
            spell,
            fields);
        var infinityDamage = LegacyMagicRuntime.CalculatePlayerMonsterDamage(
            session,
            worldData: null,
            guard,
            spell,
            fields,
            hasInfinity: true,
            infinityMonsterDamagePercent: 175,
            infinitySpellDamagePercent: 200);

        Assert.Equal(2_160U, normalDamage);
        Assert.Equal(4_320U, infinityDamage);
    }

    [Fact]
    public void LegacyMagicRuntimeAppliesSourceLevelAdvantageBeforeMonsterDefense()
    {
        var session = CreateWorldSession();
        var fields = new uint[LegacyPlayerStatusPacket.FieldCount];
        fields[LegacyPlayerStatusPacket.MagicAttackIndex] = 1_000;
        var spell = CreateRuntimeSpell(1_000, LegacyMagicSort.Attack, damage: 0);
        var normal = MonsterFamily("Bandit", defense: 100, defense2: 10_000, level: 140);

        var damage = LegacyMagicRuntime.CalculatePlayerMonsterDamage(
            session,
            worldData: null,
            normal,
            spell,
            fields);

        Assert.Equal(1_940U, damage);
    }

    [Fact]
    public void LegacyMagicRuntimeIgnoresNonZeroDefense2AndOnlyClampsZeroDefense2()
    {
        var session = CreateWorldSession();
        var fields = new uint[LegacyPlayerStatusPacket.FieldCount];
        fields[LegacyPlayerStatusPacket.MagicAttackIndex] = 1_000;
        var spell = CreateRuntimeSpell(1_000, LegacyMagicSort.Attack, damage: 0);
        var fullDefense2 = MonsterFamily("Bandit", defense: 100, defense2: 10_000, level: 140);
        var halfDefense2 = MonsterFamily("Bandit", defense: 100, defense2: 5_000, level: 140);
        var zeroDefense2 = MonsterFamily("Bandit", defense: 100, defense2: 0, level: 140);

        var fullDamage = LegacyMagicRuntime.CalculatePlayerMonsterDamage(
            session,
            worldData: null,
            fullDefense2,
            spell,
            fields);
        var halfDamage = LegacyMagicRuntime.CalculatePlayerMonsterDamage(
            session,
            worldData: null,
            halfDefense2,
            spell,
            fields);
        var zeroDamage = LegacyMagicRuntime.CalculatePlayerMonsterDamage(
            session,
            worldData: null,
            zeroDefense2,
            spell,
            fields);

        Assert.Equal(1_940U, fullDamage);
        Assert.Equal(fullDamage, halfDamage);
        Assert.Equal(1U, zeroDamage);
    }

    [Fact]
    public void LegacyMagicRuntimeAppliesSourceMinimumDamageAfterLevelScaling()
    {
        var session = CreateWorldSession(
            EquipmentSnapshot(
                Equipment(itemId: 500_329, position: 4),
                Equipment(itemId: 421_335, position: 5)));
        var fields = new uint[LegacyPlayerStatusPacket.FieldCount];
        fields[LegacyPlayerStatusPacket.MagicAttackIndex] = 1;
        var spell = CreateRuntimeSpell(1_000, LegacyMagicSort.Attack, damage: 0);
        var normal = MonsterFamily("Bandit", defense: 100, defense2: 10_000, level: 140);

        var damage = LegacyMagicRuntime.CalculatePlayerMonsterDamage(
            session,
            worldData: null,
            normal,
            spell,
            fields);

        // Source: Base.AdjustMinDamageUser2Monster = 1 + player level / 10 + left/right weapon ID tails.
        Assert.Equal(29U, damage);
    }

    [Fact]
    public void LegacyMagicRuntimeAppliesSlayerBeforeInfinityAndCapsAtOneRemainingHitpoint()
    {
        var session = CreateWorldSession();
        var fields = new uint[LegacyPlayerStatusPacket.FieldCount];
        fields[LegacyPlayerStatusPacket.MagicAttackIndex] = 10_000;
        var spell = CreateRuntimeSpell(1_000, LegacyMagicSort.Attack, damage: 0);
        var normal = MonsterFamily("Bandit", defense: 0, defense2: 10_000, level: 140);

        var damage = LegacyMagicRuntime.CalculatePlayerMonsterDamage(
            session,
            worldData: null,
            normal,
            spell,
            fields,
            hasInfinity: true,
            infinityMonsterDamagePercent: 175,
            infinitySpellDamagePercent: 200,
            hasSlayer: true,
            slayerPercent: 1_000,
            slayerTargetHitpoints: 5_000);

        Assert.Equal(9_998U, damage);
    }

    [Fact]
    public void LegacyMagicRuntimeAppliesLuckyStrikeAndSkillCriticalBeforeLevelScaling()
    {
        var session = CreateWorldSession();
        var fields = new uint[LegacyPlayerStatusPacket.FieldCount];
        fields[LegacyPlayerStatusPacket.MagicAttackIndex] = 1_000;
        fields[LegacyPlayerStatusPacket.LuckyStrikePercentIndex] = 10_000;
        fields[LegacyPlayerStatusPacket.SkillCStrikeIndex] = 1_500;
        var spell = CreateRuntimeSpell(1_000, LegacyMagicSort.Attack, damage: 0);
        var normal = MonsterFamily("Bandit", defense: 100, defense2: 10_000, level: 140);

        var result = LegacyMagicRuntime.CalculatePlayerMonsterDamageWithEffects(
            session,
            worldData: null,
            normal,
            spell,
            fields,
            luckyStrikeRoll: 0,
            magicCriticalStrikeRoll: 0);

        Assert.Equal(4_592U, result.Damage);
        Assert.Equal(MsgAttackEffect.LuckyStrike | MsgAttackEffect.CriticalStrike, result.Effect);
    }

    [Fact]
    public void LegacyPlayerMagicRuntimeAppliesSoulGearFinalMagicDamageAfterMagicDefense()
    {
        var worldData = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 201_003,
                    name: "SourceClueFinalMagicDamage",
                    magicAttack: 400),
                ItemTypeDefinition(
                    itemTypeId: 130_003,
                    name: "SourceClueMagicDefense"),
                ItemTypeDefinition(
                    itemTypeId: 202_003,
                    name: "SourceClueFinalMagicReduction",
                    magicDefence: 300),
            ],
            itemAddBonuses:
            [
                ItemAddBonusDefinition(itemTypeId: 130_000, level: 1, magicDefence: 990),
            ]);
        var spell = CreateRuntimeSpell(1_000, LegacyMagicSort.Attack, damage: 1_000);
        var attacker = CreateWorldSession(
            EquipmentSnapshot(Equipment(itemId: 201_003, position: 10)));
        var target = CreateWorldSession(
            EquipmentSnapshot(
                Equipment(itemId: 130_003, position: 3, plus: 1),
                Equipment(itemId: 202_003, position: 11)));

        var damage = InvokePlayerPlayerMagicDamage(attacker, target, spell, worldData);
        var attackerFields = LegacyPlayerStatusCalculator.CalculateFields(
            attacker.Character,
            attacker.PlayerItems,
            worldData,
            checked((uint)attacker.Character.MinHitPoints),
            prestigeLevel: 0,
            attacker.CombatGear);
        var targetFields = LegacyPlayerStatusCalculator.CalculateFields(
            target.Character,
            target.PlayerItems,
            worldData,
            checked((uint)target.Character.MinHitPoints),
            prestigeLevel: 0,
            target.CombatGear);
        var afterMagicDefense = attackerFields[LegacyPlayerStatusPacket.MagicAttackIndex] + spell.Damage;
        afterMagicDefense -= targetFields[LegacyPlayerStatusPacket.MagicDefenceIndex];
        var sourceOrderedDamage = afterMagicDefense +
            attackerFields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex] -
            targetFields[LegacyPlayerStatusPacket.MagicDamageDecreaseIndex];
        var oldLocalOrdering = 1U +
            attackerFields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex];

        // Source: Magic.OnPlayer applies CalcaulateDeffence, then Base.CalculateExtraAttack.
        Assert.Equal(400U, attackerFields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex]);
        Assert.Equal(990U, targetFields[LegacyPlayerStatusPacket.MagicDefenceIndex]);
        Assert.Equal(300U, targetFields[LegacyPlayerStatusPacket.MagicDamageDecreaseIndex]);
        Assert.Equal(sourceOrderedDamage, damage);
        Assert.NotEqual(oldLocalOrdering, damage);
    }

    [Fact]
    public void LegacyPlayerMagicRuntimeIgnoresFinalMagicReductionOnWrongGearSlot()
    {
        var worldData = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 202_003,
                    name: "SourceClueWrongSlotFinalMagicReduction",
                    magicDefence: 300),
            ]);
        var spell = CreateRuntimeSpell(1_000, LegacyMagicSort.Attack, damage: 1_000);
        var attacker = CreateWorldSession();
        var target = CreateWorldSession(
            EquipmentSnapshot(Equipment(itemId: 202_003, position: 10)));

        var damage = InvokePlayerPlayerMagicDamage(attacker, target, spell, worldData);
        var targetFields = LegacyPlayerStatusCalculator.CalculateFields(
            target.Character,
            target.PlayerItems,
            worldData,
            checked((uint)target.Character.MinHitPoints),
            prestigeLevel: 0,
            target.CombatGear);

        Assert.Equal(0U, targetFields[LegacyPlayerStatusPacket.MagicDamageDecreaseIndex]);
        Assert.Equal(1_001U, damage);
    }

    [Fact]
    public void LegacyPhysicalSpellRuntimeDividesPlayerDamageByTenForGuardFamilies()
    {
        var worldData = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 480_003,
                    name: "SourceClueClub",
                    maxAttack: 6_000,
                    minAttack: 5_000),
            ]);
        var session = CreateWorldSession(EquipmentSnapshot(Equipment(itemId: 480_003, position: 4)));
        var spell = CreateRuntimeSpell(1_280, LegacyMagicSort.PhysicalSpells, damage: 100);
        var normal = MonsterFamily("Bandit", defense: 0, defense2: 10_000, level: 120);
        var guard = MonsterFamily("Guard1", defense: 0, defense2: 10_000, level: 120);
        var normalMonster = MonsterInstance(normal, "Bandit");
        var guardMonster = MonsterInstance(guard, "Guard1");

        var normalDamage = InvokePlayerMonsterPhysicalSpellDamage(session, worldData, normalMonster, normal, spell);
        var guardDamage = InvokePlayerMonsterPhysicalSpellDamage(session, worldData, guardMonster, guard, spell);

        Assert.True(normalDamage > 0);
        Assert.Equal(normalDamage / 10, guardDamage);
    }

    [Fact]
    public void LegacyPhysicalSpellRuntimeAppliesCriticalStrikeAfterDefense2BeforeGuard()
    {
        var worldData = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 480_003,
                    name: "SourceClueClub",
                    maxAttack: 1_000,
                    minAttack: 1_000,
                    criticalStrike: 15_000),
            ]);
        var session = CreateWorldSession(
            EquipmentSnapshot(Equipment(itemId: 480_003, position: 4)),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyPhysicalLuckyStrikeRoll"] = "99",
                ["LegacyPhysicalRefineryRoll"] = "0",
                ["LegacyPhysicalDashRoll"] = "99",
            });
        var spell = CreateRuntimeSpell(1_280, LegacyMagicSort.PhysicalSpells, damage: 100);
        var family = MonsterFamily("Guard1", defense: 100, defense2: 10_000, level: 120);
        var monster = MonsterInstance(family, "Guard1");

        var damage = InvokePlayerMonsterPhysicalSpellDamage(session, worldData, monster, family, spell);
        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            session.Character,
            session.PlayerItems,
            worldData,
            checked((uint)session.Character.MinHitPoints),
            prestigeLevel: 0,
            session.CombatGear);
        var preGuard = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            fields[LegacyPlayerStatusPacket.MinAttackIndex],
            fields[LegacyPlayerStatusPacket.MaxAttackIndex],
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: checked((int)session.Character.Level),
            monsterLevel: monster.Level,
            finalPhysicalIncreasePercent: fields[LegacyPlayerStatusPacket.PhysicalPercentIndex],
            finalPhysicalDamageIncrease: fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex],
            deterministicRoll: (fields[LegacyPlayerStatusPacket.MaxAttackIndex] -
                fields[LegacyPlayerStatusPacket.MinAttackIndex] - 1) / 2);
        var postEffect = LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourcePostDefense2Effects(
            preGuard,
            luckyStrikeRatePercent: 0,
            criticalStrike: fields[LegacyPlayerStatusPacket.CriticalStrikeIndex],
            dashRate: 0,
            luckyStrikeRoll: 99,
            refineryRoll: 0,
            dashRoll: 99);

        Assert.Equal(2_150U, preGuard);
        Assert.Equal(4_300U, postEffect.Damage);
        Assert.Equal(postEffect.Damage / 10, damage);
    }

    [Fact]
    public void LegacyPhysicalSpellRuntimeAppliesPhysicalDamageIncreaseAgainstMonsters()
    {
        var worldData = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 480_003,
                    name: "SourceClueClub",
                    maxAttack: 1_000,
                    minAttack: 1_000),
                ItemTypeDefinition(
                    itemTypeId: 201_003,
                    name: "SourceCluePhysicalDamageIncrease",
                    maxAttack: 5_000),
            ]);
        var session = CreateWorldSession(
            EquipmentSnapshot(
                Equipment(itemId: 480_003, position: 4),
                Equipment(itemId: 201_003, position: 10)));
        var spell = CreateRuntimeSpell(1_280, LegacyMagicSort.PhysicalSpells, damage: 100);
        var family = MonsterFamily("Bandit", defense: 100, defense2: 10_000, level: 120);
        var monster = MonsterInstance(family, "Bandit");

        var damage = InvokePlayerMonsterPhysicalSpellDamage(session, worldData, monster, family, spell);
        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            session.Character,
            session.PlayerItems,
            worldData,
            checked((uint)session.Character.MinHitPoints),
            prestigeLevel: 0,
            session.CombatGear);

        // Source: Physical.OnMonster applies CalculateExtraAttack after Defense2.
        Assert.Equal(5_000U, fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex]);
        Assert.Equal(7_150U, damage);
    }

    [Fact]
    public void LegacyPhysicalSpellRuntimeAppliesPlayerExtraAttackAfterBlessBeforeCallerDivisor()
    {
        var worldData = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 480_003,
                    name: "SourceClueClub",
                    maxAttack: 1_000,
                    minAttack: 1_000),
                ItemTypeDefinition(
                    itemTypeId: 201_003,
                    name: "SourceCluePhysicalDamageIncrease",
                    maxAttack: 500),
                ItemTypeDefinition(
                    itemTypeId: 202_003,
                    name: "SourceCluePhysicalDamageDecrease",
                    physicalDefence: 200),
            ]);
        var attacker = CreateWorldSession(
            EquipmentSnapshot(
                Equipment(itemId: 480_003, position: 4),
                Equipment(itemId: 201_003, position: 10)));
        var target = CreateWorldSession(
            EquipmentSnapshot(Equipment(itemId: 202_003, position: 11)),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AjustDefense"] = "100",
            });
        var spell = CreateRuntimeSpell(10_490, LegacyMagicSort.PhysicalSpells, damage: 100);

        var damage = InvokePlayerPlayerPhysicalSpellDamage(
            attacker,
            target,
            spell,
            worldData,
            damageDivisor: 2);
        var attackerFields = LegacyPlayerStatusCalculator.CalculateFields(
            attacker.Character,
            attacker.PlayerItems,
            worldData,
            checked((uint)attacker.Character.MinHitPoints),
            prestigeLevel: 0,
            attacker.CombatGear);
        var targetFields = LegacyPlayerStatusCalculator.CalculateFields(
            target.Character,
            target.PlayerItems,
            worldData,
            checked((uint)target.Character.MinHitPoints),
            prestigeLevel: 0,
            target.CombatGear);

        // Source: Physical.OnPlayer applies spell-specific damage and bless before Base.CalculateExtraAttack.
        var afterDefense = attackerFields[LegacyPlayerStatusPacket.MinAttackIndex] - 100U;
        var afterTripleAttack = afterDefense * 75U / 100U;
        var afterExtraAttack = afterTripleAttack +
            attackerFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex] -
            targetFields[LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex];
        var oldLocalOrdering = (attackerFields[LegacyPlayerStatusPacket.MinAttackIndex] +
            attackerFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex] -
            100U) * 75U / 100U / 2U;

        Assert.Equal(500U, attackerFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex]);
        Assert.Equal(200U, targetFields[LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex]);
        Assert.Equal(afterExtraAttack / 2U, damage);
        Assert.NotEqual(oldLocalOrdering, damage);
    }

    [Fact]
    public void LegacyMonsterPhysicalRuntimeDoesNotUsePlayerPhysicalDamageDecrease()
    {
        var worldData = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 202_003,
                    name: "SourceCluePhysicalDamageDecrease",
                    physicalDefence: 500),
            ]);
        var ungeared = CreateWorldSession();
        var geared = CreateWorldSession(
            EquipmentSnapshot(Equipment(itemId: 202_003, position: 11)));
        var family = MonsterFamily("Bandit", defense: 0, defense2: 10_000, level: 120);
        var monster = MonsterInstance(family, "Bandit");

        var ungearedDamage = InvokeMonsterPhysicalDamage(ungeared, worldData, monster, family);
        var gearedDamage = InvokeMonsterPhysicalDamage(geared, worldData, monster, family);
        var gearedFields = LegacyPlayerStatusCalculator.CalculateFields(
            geared.Character,
            geared.PlayerItems,
            worldData,
            checked((uint)geared.Character.MinHitPoints),
            prestigeLevel: 0,
            geared.CombatGear);

        // Source: MsgMonster.ActionHandler.PhysicalAttack subtracts client.AjustDefense,
        // while Physical.OnPlayer/Range.OnPlayer use PhysicalDamageDecrease in CalculateExtraAttack.
        Assert.Equal(500U, gearedFields[LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex]);
        Assert.Equal(0U, gearedFields[LegacyPlayerStatusPacket.DefenceIndex]);
        Assert.Equal(1_802U, ungearedDamage);
        Assert.Equal(ungearedDamage, gearedDamage);
    }

    [Fact]
    public void LegacyPhysicalSpellRuntimeUsesSourceHalfRollInsteadOfRawAttackMidpoint()
    {
        var worldData = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 480_003,
                    name: "SourceClueClub",
                    maxAttack: 2_000,
                    minAttack: 1_000),
            ]);
        var session = CreateWorldSession(
            EquipmentSnapshot(Equipment(itemId: 480_003, position: 4)),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyPhysicalLuckyStrikeRoll"] = "99",
                ["LegacyPhysicalRefineryRoll"] = "40",
                ["LegacyPhysicalDashRoll"] = "99",
            });
        var spell = CreateRuntimeSpell(1_280, LegacyMagicSort.PhysicalSpells, damage: 100);
        var family = MonsterFamily("Bandit", defense: 100, defense2: 10_000, level: 120);
        var monster = MonsterInstance(family, "Bandit");

        var damage = InvokePlayerMonsterPhysicalSpellDamage(session, worldData, monster, family, spell);
        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            session.Character,
            session.PlayerItems,
            worldData,
            checked((uint)session.Character.MinHitPoints),
            prestigeLevel: 0,
            session.CombatGear);
        // Source: Base.GetDamage selects MinAttack + random(range) / 2; use the middle source roll here.
        var sourceMiddleRoll = (fields[LegacyPlayerStatusPacket.MaxAttackIndex] -
            fields[LegacyPlayerStatusPacket.MinAttackIndex] - 1) / 2;
        var expectedDamage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            fields[LegacyPlayerStatusPacket.MinAttackIndex],
            fields[LegacyPlayerStatusPacket.MaxAttackIndex],
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: checked((int)session.Character.Level),
            monsterLevel: monster.Level,
            finalPhysicalIncreasePercent: fields[LegacyPlayerStatusPacket.PhysicalPercentIndex],
            deterministicRoll: sourceMiddleRoll,
            physicalSpellDamagePercent: 100);
        var rawMidpointAttack = checked((uint)(((ulong)fields[LegacyPlayerStatusPacket.MinAttackIndex] +
            fields[LegacyPlayerStatusPacket.MaxAttackIndex]) / 2UL));
        var rawMidpointDamage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            rawMidpointAttack,
            rawMidpointAttack,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: checked((int)session.Character.Level),
            monsterLevel: monster.Level,
            physicalSpellDamagePercent: 100);

        Assert.Equal(1_000U, fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        Assert.Equal(2_001U, fields[LegacyPlayerStatusPacket.MaxAttackIndex]);
        Assert.Equal(expectedDamage, damage);
        Assert.True(damage < rawMidpointDamage);
    }

    [Fact]
    public void LegacyPhysicalSpellRuntimeAddsSobPhysicalDamageIncreaseAfterSpellDamageBeforePhysicalSpellsMultiplier()
    {
        var worldData = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 480_003,
                    name: "SourceClueClub",
                    maxAttack: 1_000,
                    minAttack: 1_000),
                ItemTypeDefinition(
                    itemTypeId: 201_003,
                    name: "SourceCluePhysicalDamageIncrease",
                    maxAttack: 500),
            ]);
        var session = CreateWorldSession(
            EquipmentSnapshot(
                Equipment(itemId: 480_003, position: 4),
                Equipment(itemId: 201_003, position: 10)));
        var spell = CreateRuntimeSpell(1_280, LegacyMagicSort.PhysicalSpells, damage: 50);

        var damage = InvokeSobNpcPhysicalSpellDamage(session, spell, worldData, damageMultiplier: 5);
        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            session.Character,
            session.PlayerItems,
            worldData,
            checked((uint)session.Character.MinHitPoints),
            prestigeLevel: 0,
            session.CombatGear);

        // Source: Physical.OnNpcs applies spell damage, then CalculateExtraAttack; PhysicalSpells multiplies by 5.
        Assert.Equal(500U, fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex]);
        Assert.Equal(5_000U, damage);
    }

    [Fact]
    public void LegacyPhysicalSpellRuntimeReadsWhirlSigilSageDamageFromEquippedMsgNinja()
    {
        var worldData = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 480_003,
                    name: "SourceClueClub",
                    maxAttack: 1_000,
                    minAttack: 1_000,
                    criticalStrike: 15_000),
            ],
            gouyuTypes:
            [
                GouyuTypeDefinition(id: 199, type: 404, level: 9, damagePercent: 25),
            ]);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyPhysicalLuckyStrikeRoll"] = "99",
            ["LegacyPhysicalRefineryRoll"] = "0",
            ["LegacyPhysicalDashRoll"] = "99",
            ["MsgNinja"] = MsgNinjaWithEquippedItem(itemType: 404, level: 9, position: 1),
        };
        var session = CreateWorldSession(
            EquipmentSnapshot(Equipment(itemId: 480_003, position: 4)),
            attributes: attributes,
            classCode: 5051);
        var spell = CreateRuntimeSpell(16_420, LegacyMagicSort.PhysicalSpells, damage: 100);
        var family = MonsterFamily("Bandit", defense: 100, defense2: 10_000, level: 120);
        var monster = MonsterInstance(family, "Bandit");

        var damage = InvokePlayerMonsterPhysicalSpellDamage(session, worldData, monster, family, spell);
        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            session.Character,
            session.PlayerItems,
            worldData,
            checked((uint)session.Character.MinHitPoints),
            prestigeLevel: 0,
            session.CombatGear);
        var preEffects = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            fields[LegacyPlayerStatusPacket.MinAttackIndex],
            fields[LegacyPlayerStatusPacket.MaxAttackIndex],
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: checked((int)session.Character.Level),
            monsterLevel: monster.Level,
            finalPhysicalIncreasePercent: fields[LegacyPlayerStatusPacket.PhysicalPercentIndex],
            finalPhysicalDamageIncrease: fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex],
            deterministicRoll: (fields[LegacyPlayerStatusPacket.MaxAttackIndex] -
                fields[LegacyPlayerStatusPacket.MinAttackIndex] - 1) / 2);
        var expected = LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourcePostDefense2Effects(
            preEffects,
            luckyStrikeRatePercent: 0,
            criticalStrike: fields[LegacyPlayerStatusPacket.CriticalStrikeIndex],
            dashRate: 0,
            luckyStrikeRoll: 99,
            refineryRoll: 0,
            dashRoll: 99,
            whirlSigilSageDamagePercent: 25);

        Assert.Equal(2_150U, preEffects);
        Assert.Equal(5_375U, expected.Damage);
        Assert.Equal(expected.Damage, damage);
    }

    [Fact]
    public void LegacyPhysicalSpellRuntimeAppliesDeathSighThenInfinityAfterPostDefense2()
    {
        var worldData = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 480_003,
                    name: "SourceClueClub",
                    maxAttack: 1_000,
                    minAttack: 1_000),
            ]);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyPhysicalLuckyStrikeRoll"] = "99",
            ["LegacyPhysicalRefineryRoll"] = "40",
            ["LegacyPhysicalDashRoll"] = "99",
            ["DeathSighActive"] = "50",
            ["DeathSighPassive"] = "125",
            ["InfinitySDamage"] = "200",
            ["InfinityDamage"] = "175",
        };
        var session = CreateWorldSession(
            EquipmentSnapshot(Equipment(itemId: 480_003, position: 4)),
            statusFlags: BuildStatusFlags(LegacyDeathSighFlag, LegacyInfinityFlag),
            attributes: attributes);
        var spell = CreateRuntimeSpell(1_280, LegacyMagicSort.PhysicalSpells, damage: 100);
        var family = MonsterFamily("Bandit", defense: 100, defense2: 10_000, level: 120);
        var monster = MonsterInstance(family, "Bandit");

        var damage = InvokePlayerMonsterPhysicalSpellDamage(session, worldData, monster, family, spell);
        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            session.Character,
            session.PlayerItems,
            worldData,
            checked((uint)session.Character.MinHitPoints),
            prestigeLevel: 0,
            session.CombatGear);
        var preEffects = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            fields[LegacyPlayerStatusPacket.MinAttackIndex],
            fields[LegacyPlayerStatusPacket.MaxAttackIndex],
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: checked((int)session.Character.Level),
            monsterLevel: monster.Level,
            finalPhysicalIncreasePercent: fields[LegacyPlayerStatusPacket.PhysicalPercentIndex],
            finalPhysicalDamageIncrease: fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex],
            deterministicRoll: (fields[LegacyPlayerStatusPacket.MaxAttackIndex] -
                fields[LegacyPlayerStatusPacket.MinAttackIndex] - 1) / 2);
        var afterDeathSigh = LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceDeathSighMonsterPhysicalDamage(
            preEffects,
            hasDeathSigh: true,
            activeSpell: true,
            passivePercent: 125,
            activePercent: 50);
        var expected = LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceInfinityMonsterPhysicalDamage(
            afterDeathSigh,
            hasInfinity: true,
            spellDamage: true,
            monsterDamagePercent: 175,
            spellDamagePercent: 200);

        Assert.Equal(2_150U, preEffects);
        Assert.Equal(6_450U, expected);
        Assert.Equal(expected, damage);
    }

    [Fact]
    public void LegacyRuntimePostDefense2EffectsUseForcedDashRollWhenRefineryMisses()
    {
        var fields = new uint[LegacyPlayerStatusPacket.FieldCount];
        fields[LegacyPlayerStatusPacket.CriticalStrikeIndex] = 10_000;
        fields[LegacyPlayerStatusPacket.DashRateIndex] = 10_000;
        var character = CreateSnapshot(
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyPhysicalLuckyStrikeRoll"] = "99",
                ["LegacyPhysicalRefineryRoll"] = "40",
                ["LegacyPhysicalDashRoll"] = "0",
            });

        var damage = InvokePlayerMonsterPhysicalPostDefense2Effects(
            damage: 980,
            fields,
            character);

        Assert.Equal(1_960U, damage);
    }

    [Fact]
    public void LegacyPhysicalSpellRuntimeKeepsDefense2ZeroDamageAtZero()
    {
        var worldData = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 480_003,
                    name: "SourceClueClub",
                    maxAttack: 6_000,
                    minAttack: 5_000),
            ]);
        var session = CreateWorldSession(EquipmentSnapshot(Equipment(itemId: 480_003, position: 4)));
        var spell = CreateRuntimeSpell(1_280, LegacyMagicSort.PhysicalSpells, damage: 100);
        var monsterFamily = MonsterFamily("Defense2Zero", defense: 0, defense2: 0, level: 120);
        var monster = MonsterInstance(monsterFamily, "Defense2Zero");

        var damage = InvokePlayerMonsterPhysicalSpellDamage(session, worldData, monster, monsterFamily, spell);

        Assert.Equal(0U, damage);
    }

    [Fact]
    public void LegacyPhysicalSpellRuntimePreventsSourceNonLethalSpellsFromKillingMonsters()
    {
        var worldData = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 480_003,
                    name: "SourceClueClub",
                    maxAttack: 6_000,
                    minAttack: 5_000),
            ]);
        var session = CreateWorldSession(EquipmentSnapshot(Equipment(itemId: 480_003, position: 4)));
        var regularSpell = CreateRuntimeSpell(1_280, LegacyMagicSort.PhysicalSpells, damage: 100);
        var nonLethalSpell = CreateRuntimeSpell(16_540, LegacyMagicSort.PhysicalSpells, damage: 100);
        var family = MonsterFamily("Bandit", defense: 0, defense2: 10_000, level: 120);
        var monster = MonsterInstance(family, "Bandit", hitpoints: 100);

        var regularDamage = InvokePlayerMonsterPhysicalSpellDamage(session, worldData, monster, family, regularSpell);
        var nonLethalDamage = InvokePlayerMonsterPhysicalSpellDamage(session, worldData, monster, family, nonLethalSpell);

        Assert.True(regularDamage > monster.Hitpoints);
        Assert.Equal(0U, nonLethalDamage);
    }

    [Fact]
    public void LegacyPhysicalSpellRuntimeAppliesNonLethalClampAfterSpecialOneDamageMonsterRules()
    {
        var session = CreateWorldSession();
        var nonLethalSpell = CreateRuntimeSpell(16_550, LegacyMagicSort.PhysicalSpells, damage: 100);
        var family = MonsterFamily("AncientDragon", defense: 0, defense2: 10_000, level: 120);
        var lowHpMonster = MonsterInstance(family, "AncientDragon", hitpoints: 2);
        var safeHpMonster = MonsterInstance(family, "AncientDragon", hitpoints: 3);

        var lowHpDamage = InvokePlayerMonsterPhysicalSpellDamage(session, CreateCatalog([]), lowHpMonster, family, nonLethalSpell);
        var safeHpDamage = InvokePlayerMonsterPhysicalSpellDamage(session, CreateCatalog([]), safeHpMonster, family, nonLethalSpell);

        Assert.Equal(0U, lowHpDamage);
        Assert.Equal(1U, safeHpDamage);
    }

    [Fact]
    public void LegacyPhysicalSpellRuntimeAppliesSupermanDamageBeforeMonsterDefense2()
    {
        var worldData = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 480_003,
                    name: "SourceClueClub",
                    maxAttack: 1_000,
                    minAttack: 1_000),
            ]);
        var normalSession = CreateWorldSession(EquipmentSnapshot(Equipment(itemId: 480_003, position: 4)));
        var supermanSession = CreateWorldSession(
            EquipmentSnapshot(Equipment(itemId: 480_003, position: 4)),
            statusFlags: BuildStatusFlags(LegacySupermanFlag));
        var spell = CreateRuntimeSpell(1_280, LegacyMagicSort.PhysicalSpells, damage: 100);
        var family = MonsterFamily("Bandit", defense: 100, defense2: 10_000, level: 120);
        var monster = MonsterInstance(family, "Bandit");

        var normalDamage = InvokePlayerMonsterPhysicalSpellDamage(normalSession, worldData, monster, family, spell);
        var supermanDamage = InvokePlayerMonsterPhysicalSpellDamage(supermanSession, worldData, monster, family, spell);
        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            supermanSession.Character,
            supermanSession.PlayerItems,
            worldData,
            checked((uint)supermanSession.Character.MinHitPoints),
            prestigeLevel: 0,
            supermanSession.CombatGear);
        var expected = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            fields[LegacyPlayerStatusPacket.MinAttackIndex],
            fields[LegacyPlayerStatusPacket.MaxAttackIndex],
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: checked((int)supermanSession.Character.Level),
            monsterLevel: monster.Level,
            finalPhysicalIncreasePercent: fields[LegacyPlayerStatusPacket.PhysicalPercentIndex],
            finalPhysicalDamageIncrease: fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex],
            deterministicRoll: (fields[LegacyPlayerStatusPacket.MaxAttackIndex] -
                fields[LegacyPlayerStatusPacket.MinAttackIndex] - 1) / 2,
            physicalSpellDamagePercent: 100,
            hasSuperman: true);

        Assert.True(supermanDamage > normalDamage);
        Assert.Equal(expected, supermanDamage);
    }

    [Fact]
    public void LegacyPhysicalSpellRuntimeLetsFatalStrikeOverrideSpellDamagePercent()
    {
        var worldData = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 480_003,
                    name: "SourceClueClub",
                    maxAttack: 1_000,
                    minAttack: 1_000),
            ]);
        var session = CreateWorldSession(
            EquipmentSnapshot(Equipment(itemId: 480_003, position: 4)),
            statusFlags: BuildStatusFlags(LegacyFatalStrikeFlag));
        var spell = CreateRuntimeSpell(1_280, LegacyMagicSort.PhysicalSpells, damage: 350);
        var family = MonsterFamily("Bandit", defense: 100, defense2: 10_000, level: 120);
        var monster = MonsterInstance(family, "Bandit");

        var damage = InvokePlayerMonsterPhysicalSpellDamage(session, worldData, monster, family, spell);
        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            session.Character,
            session.PlayerItems,
            worldData,
            checked((uint)session.Character.MinHitPoints),
            prestigeLevel: 0,
            session.CombatGear);
        var expected = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            fields[LegacyPlayerStatusPacket.MinAttackIndex],
            fields[LegacyPlayerStatusPacket.MaxAttackIndex],
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: checked((int)session.Character.Level),
            monsterLevel: monster.Level,
            finalPhysicalIncreasePercent: fields[LegacyPlayerStatusPacket.PhysicalPercentIndex],
            finalPhysicalDamageIncrease: fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex],
            deterministicRoll: (fields[LegacyPlayerStatusPacket.MaxAttackIndex] -
                fields[LegacyPlayerStatusPacket.MinAttackIndex] - 1) / 2,
            physicalSpellDamagePercent: 350,
            hasFatalStrike: true);

        Assert.Equal(expected, damage);
    }

    private static PlayerItemSnapshot EquipmentSnapshot(params PlayerItemSnapshotItem[] equipment)
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: equipment,
            RuneItems: []);
    }

    private static PlayerItemSnapshotItem Equipment(uint itemId, ushort position, byte plus = 0)
    {
        return new PlayerItemSnapshotItem
        {
            ItemId = itemId,
            Durability = 100,
            Position = position,
            Plus = plus,
        };
    }

    private static ItemAddBonus ItemAddBonusDefinition(
        int itemTypeId,
        int level,
        int magicDefence)
    {
        var values = Enumerable.Repeat(0, 8).ToArray();
        values[5] = magicDefence;
        return new ItemAddBonus(itemTypeId, level, values, 0, "fixture://itemadd");
    }

    private static CharacterSnapshot CreateSnapshot(
        string? statusFlags = null,
        IReadOnlyDictionary<string, string>? attributes = null,
        long classCode = 7051)
    {
        var attributeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(statusFlags))
        {
            attributeMap["StatusFlags"] = statusFlags;
        }

        if (attributes is not null)
        {
            foreach (var attribute in attributes)
            {
                attributeMap[attribute.Key] = attribute.Value;
            }
        }

        return new CharacterSnapshot(
            Uid: 1,
            Name: "GearDamageParity",
            Body: 1008,
            Face: 154,
            Hair: 27,
            Class: classCode,
            FirstClass: 0,
            SecondClass: 7050,
            Reborn: 2,
            Level: 140,
            Experience: 0,
            MapId: 1015,
            X: 830,
            Y: 584,
            PreviousMapId: 1000,
            PreviousMapX: 500,
            PreviousMapY: 650,
            Strength: 0,
            Agility: 36,
            Vitality: 447,
            Spirit: 0,
            AttributePoints: 12,
            MinHitPoints: 100,
            MinMana: 5_000,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 7,
            SourceFile: "gear-damage-parity.ini",
            Attributes: attributeMap,
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static WorldSession CreateWorldSession(
        PlayerItemSnapshot? playerItems = null,
        string? statusFlags = null,
        IReadOnlyDictionary<string, string>? attributes = null,
        long classCode = 7051)
    {
        return new WorldSession(
            AccountId: 1,
            Username: "gear-damage-parity",
            CreateSnapshot(statusFlags, attributes, classCode),
            playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty,
            CombatGear: CombatGearSnapshot.Empty);
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var wordCount = Math.Max(
            10,
            flags
                .Where(flag => flag >= 0)
                .Select(flag => flag / 32 + 1)
                .DefaultIfEmpty(10)
                .Max());
        var words = new ulong[wordCount];
        foreach (var flag in flags)
        {
            if (flag < 0)
            {
                continue;
            }

            var wordIndex = flag / 32;
            var bitIndex = flag % 32;
            words[wordIndex] |= 1UL << bitIndex;
        }

        return string.Join('/', words) + "/";
    }

    private static LegacyMagicRuntimeSpell CreateRuntimeSpell(
        ushort spellId,
        LegacyMagicSort sort,
        uint damage)
    {
        return new LegacyMagicRuntimeSpell(
            Id: spellId,
            Sort: sort,
            Name: "GearDamageParitySpell",
            Level: 0,
            UseMana: 0,
            Damage: damage,
            Experience: 0,
            MaxTargets: 1,
            Range: 10,
            NeedLevel: 1,
            WeaponType: 0,
            UseStamina: 0,
            CoolDown: 0,
            ColdTime: 0);
    }

    private static Monster MonsterFamily(string name, uint defense, uint defense2, byte level)
    {
        return new Monster(
            Id: name == "Guard1" ? 900 : 901,
            name,
            new Dictionary<string, string>
            {
                ["id"] = name == "Guard1" ? "900" : "901",
                ["name"] = name,
                ["life"] = "70000",
                ["lookface"] = "900",
                ["level"] = level.ToString(),
                ["attack_min"] = "901",
                ["attack_max"] = "902",
                ["defense"] = defense.ToString(),
                ["defence"] = defense.ToString(),
                ["defense2"] = defense2.ToString(),
                ["defence2"] = defense2.ToString(),
            },
            []);
    }

    private static LegacyMonsterInstance MonsterInstance(Monster monsterFamily, string name, uint hitpoints = 70_000)
    {
        return new LegacyMonsterInstance(
            Uid: 500_000,
            MapId: 1002,
            X: 100,
            Y: 100,
            Mesh: 900,
            Level: byte.Parse(monsterFamily.Attributes["level"]),
            Hitpoints: hitpoints,
            MaxLife: 70_000,
            MonsterId: checked((uint)monsterFamily.Id),
            name,
            Boss: false,
            GeneratorId: 1,
            SpawnIndex: 0);
    }

    private static uint InvokePlayerMonsterPhysicalSpellDamage(
        WorldSession session,
        WorldDataCatalog worldData,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<uint>(method.Invoke(null, [session, worldData, monster, monsterFamily, spell]));
    }

    private static uint InvokePlayerPlayerPhysicalSpellDamage(
        WorldSession attacker,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog worldData,
        uint damageDivisor)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<uint>(method.Invoke(null, [attacker, target, spell, worldData, damageDivisor]));
    }

    private static uint InvokeMonsterPhysicalDamage(
        WorldSession session,
        WorldDataCatalog worldData,
        LegacyMonsterInstance monster,
        Monster monsterFamily)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "CalculateLegacyMonsterPhysicalDamage",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(null, [monster, monsterFamily, session, worldData, false, false]);
        Assert.NotNull(result);
        return ReadUIntResult(result, "Damage");
    }

    private static uint ReadUIntResult(object result, string propertyName)
    {
        var property = result.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        var value = property.GetValue(result);
        Assert.NotNull(value);
        return Assert.IsType<uint>(value);
    }

    private static uint InvokePlayerPlayerMagicDamage(
        WorldSession attacker,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog worldData)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "CalculateLegacyPlayerPlayerMagicDamage",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<uint>(method.Invoke(null, [attacker, target, spell, worldData]));
    }

    private static uint InvokePlayerMonsterPhysicalPostDefense2Effects(
        uint damage,
        IReadOnlyList<uint> fields,
        CharacterSnapshot character)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "ApplyLegacyPlayerMonsterPhysicalPostDefense2Effects",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<uint>(method.Invoke(null, [damage, fields, character, 0U]));
    }

    private static uint InvokeSobNpcPhysicalSpellDamage(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog worldData,
        uint damageMultiplier)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<uint>(method.Invoke(null, [session, spell, worldData, damageMultiplier]));
    }

    private static string MsgNinjaWithEquippedItem(uint itemType, uint level, uint position)
    {
        var fields = Enumerable.Repeat("0", 20)
            .Concat(
            [
                itemType.ToString(),
                level.ToString(),
                position.ToString(),
                "0",
                "0",
            ]);
        return string.Join('/', fields) + "/";
    }

    private static GouyuType GouyuTypeDefinition(int id, int type, int level, int damagePercent)
    {
        var values = Enumerable.Repeat(0, 10).ToArray();
        values[6] = damagePercent;
        return new GouyuType(
            id,
            type,
            level,
            values,
            "fixture://gouyu-type.txt",
            id,
            string.Empty,
            []);
    }

    private static ItemType ItemTypeDefinition(
        int itemTypeId,
        string name,
        int maxAttack = 0,
        int minAttack = 0,
        int physicalDefence = 0,
        int magicAttack = 0,
        int magicDefence = 0,
        int criticalStrike = 0)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = itemTypeId.ToString();
        fields[1] = name;
        fields[13] = maxAttack.ToString();
        fields[14] = minAttack.ToString();
        fields[15] = physicalDefence.ToString();
        fields[29] = magicAttack.ToString();
        fields[30] = magicDefence.ToString();
        fields[39] = criticalStrike.ToString();

        return new ItemType(itemTypeId, name, fields);
    }

    private static WorldDataCatalog CreateCatalog(
        IReadOnlyList<ItemType> items,
        IReadOnlyList<ItemAddBonus>? itemAddBonuses = null,
        IReadOnlyList<GouyuType>? gouyuTypes = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-13T00:00:00Z"),
                "fixture://legacy-gear-damage-parity",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items,
            itemAddBonuses: itemAddBonuses ?? [],
            levelExperience: [],
            runeLevelExperience: [],
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: [],
            furniture: [],
            hairColorTypes: [],
            hairFaceStorageTypes: [],
            coatStorageTypes: [],
            coatStorageAttributes: [],
            coatColorRules: [],
            xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [],
            xuanbaoRandomAttributes: [],
            christmasNpcs: [],
            tutorTypes: [],
            tutorBattleLimitTypes: [],
            beastAttributes: [],
            fateExperience: [],
            spiritRates: [],
            hundredWeapons: [],
            gouyuTypes: gouyuTypes ?? [],
            gouyuImmortals: [],
            specialEffectAttributes: [],
            daoqiDictTypes: [],
            syndicateFormationTypes: [],
            syndicateSkillTypes: [],
            disdainRules: [],
            jiangHuAttributes: [],
            jiangHuCultivateStatuses: [],
            refineryBoxes: [],
            refinerySouls: [],
            refineryMaterials: [],
            stateCountFiles: [],
            binarySeedFiles: [],
            emptySeedFiles: [],
            backupSeedFiles: [],
            pokerTables: [],
            rouletteTables: [],
            npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, "", 0, []), [], "fixture://activity"),
            meltingTypeRules: [],
            lotteryPrizes: [],
            arenaRecords: [],
            elitePkRecords: [],
            teamElitePkRecords: [],
            skillTeamPkRecords: [],
            classPkWarRecords: [],
            combatGearRanks: [],
            prestigeRanks: [],
            innerPowerRanks: [],
            runeRankings: [],
            staticStatues: [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: [],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: [],
            monsterSpawns: [],
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://neigong", 0, "", new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [],
            shopItems: [],
            exchangeShopGoods: [],
            exchangeShopGoodsEx: [],
            exchangeShopLevels: [],
            honorShopItems: [],
            honorShopRecommendations: [],
            racePointShopItems: [],
            racePointShopRecommendations: [],
            goldenLeagueShopItems: [],
            goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(),
            booths: [],
            boothItems: []);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }
}
