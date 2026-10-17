using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort DirectIronbone17650SpellId = 17_650;
    private const ushort DirectIronbone17650FallbackId = 17_460;
    private const uint DirectIronbone17650HelmetId = 111_765;
    private const int DirectIronbone17650SobUid = 65_765;
    private const int DirectIronbone17650FlashingNameFlag = 0;
    private const long DirectIronbone17650Hp = 100_000;

    [Theory]
    [InlineData("actual-soul-zero")]
    [InlineData("actual-soul-three")]
    [InlineData("resources-once")]
    [InlineData("overkill")]
    [InlineData("opening-solid")]
    [InlineData("mud-wall-before-player-scalar")]
    [InlineData("hawks-after-player-scalar")]
    [InlineData("wild-phoenix-before-player-scalar")]
    [InlineData("small-floor-strength-two")]
    [InlineData("small-floor-strength-nine")]
    [InlineData("shuriken-before-wild-phoenix")]
    [InlineData("tail-before-player-scalar")]
    [InlineData("line-boundaries")]
    [InlineData("endpoint-back-one")]
    [InlineData("endpoint-back-two")]
    [InlineData("endpoint-all-three-invalid")]
    [InlineData("vertical-query")]
    [InlineData("zero-x-query")]
    [InlineData("range-six-empty")]
    public async Task DirectIronbone17650PhysicalReplacementUsesItsOwnSourceContract(string scenario)
    {
        var result = await DirectIronbone17650RunAsync(scenario);
        var breaks = scenario is "vertical-query" or "zero-x-query" or "endpoint-all-three-invalid";
        var empty = scenario == "range-six-empty";
        var expectedDamage = scenario switch
        {
            "shuriken-before-wild-phoenix" or "wild-phoenix-before-player-scalar" => 0U,
            "mud-wall-before-player-scalar" => 168U,
            "hawks-after-player-scalar" => 169U,
            "small-floor-strength-two" => 2U,
            "small-floor-strength-nine" => 9U,
            _ => 337U,
        };
        var expectedX = scenario switch
        {
            "endpoint-back-one" => 839,
            "endpoint-back-two" => 838,
            "range-six-empty" => 836,
            _ => 840,
        };
        DirectIronbone17650AssertNoOrdinaryPhysical(result);
        if (!breaks)
        {
            var peers = result.Frames.Keys.Where(uid => uid != PlayerUid).OrderBy(uid => uid).ToArray();
            var selfIndex = Array.IndexOf(result.AnimationRecipients, PlayerUid);
            Assert.Equal(peers.Length, selfIndex);
            Assert.Equal((2 * peers.Length) + 1, result.AnimationRecipients.Length);
            Assert.Equal(peers, result.AnimationRecipients.Take(selfIndex).OrderBy(uid => uid));
            Assert.Equal(peers, result.AnimationRecipients.Skip(selfIndex + 1).OrderBy(uid => uid));
        }
        foreach (var (uid, frames) in result.Frames)
        {
            var animations = ParseSpellAnimations(frames);
            if (breaks)
            {
                Assert.Empty(animations);
                continue;
            }
            // Source case17650 calls BOTH SendRole then Send. RoleView.Role
            // sends the supplied packet to each visible peer; Send includes
            // self. Do not silently collapse the peer's two deliveries.
            Assert.Equal(uid == PlayerUid ? 1 : 2, animations.Count);
            Assert.All(animations, animation =>
            {
                Assert.Equal(DirectIronbone17650SpellId, animation.SpellId);
                Assert.Equal(PlayerUid, animation.AttackerUid);
                Assert.Equal(0U, animation.AttackedUid);
                Assert.Equal((expectedX, 584), ((int)animation.X, (int)animation.Y));
                Assert.Equal(result.Soul, animation.LevelHu);
                uint[] expectedUids = empty ? [] : scenario == "line-boundaries"
                    ? [TargetPlayerUid, TargetPlayerUid + 1, TargetPlayerUid + 2]
                    : [TargetPlayerUid];
                Assert.Equal(expectedUids.OrderBy(value => value),
                    animation.Targets.Select(hit => hit.Uid).OrderBy(value => value));
                Assert.All(animation.Targets, hit =>
                {
                    Assert.Equal(expectedDamage, hit.Damage);
                    Assert.True(hit.Hit); // SpellObj constructor defaults1, not damage-dependent.
                    Assert.Equal(MsgAttackEffect.None, hit.Effect);
                    Assert.Equal((ushort)0, hit.NewX);
                    Assert.Equal((ushort)0, hit.NewY);
                });
            });
            Assert.All(frames.Where(bytes => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out _)),
                bytes => Assert.Equal((ushort)2239, ReadFrame(bytes).Header.Opcode));
        }
        foreach (var (uid, target) in result.Targets)
        {
            var hit = !breaks && !empty && (uid == TargetPlayerUid ||
                scenario == "line-boundaries" && (uid == TargetPlayerUid + 1 || uid == TargetPlayerUid + 2));
            var initial = scenario == "overkill" && uid == TargetPlayerUid ? 5 : DirectIronbone17650Hp;
            Assert.Equal(hit ? Math.Max(0, initial - expectedDamage) : initial, target.Character.MinHitPoints);
        }
        Assert.Equal(((long)result.OriginX, 584L), (result.Caster.Character.X, result.Caster.Character.Y));
        Assert.Equal(DirectIronbone17650Hp, result.Caster.Character.MinHitPoints);
        Assert.Equal(result.CostsResources ? 493 : 500, result.Caster.Character.MinMana);
        Assert.Equal(result.CostsResources ? "95" : "100", result.Caster.Character.Attributes["Stamina"]);
        DirectIronbone17650AssertNoProgress(result);
        if (!breaks)
        {
            var initial = scenario == "overkill" ? 5 : DirectIronbone17650Hp;
            var expectedHp = empty ? initial : Math.Max(0, initial - expectedDamage);
            Assert.NotEmpty(result.AnimationTargetObservations);
            Assert.All(result.AnimationTargetObservations, hp => Assert.Equal(expectedHp, hp));
        }
        if (scenario == "opening-solid")
        {
            Assert.NotEmpty(result.SolidObservations);
            Assert.All(result.SolidObservations, hp => Assert.Equal(DirectIronbone17650Hp, hp));
        }
        if (result.CostsResources)
        {
            var casterUpdates = ParseMsgUpdates(result.Frames[PlayerUid]).SelectMany(update => update.Updates).ToArray();
            Assert.Equal(95UL, Assert.Single(casterUpdates, entry => entry.Type == LegacyMsgUpdateDataType.Stamina).Value);
            Assert.Equal(493UL, Assert.Single(casterUpdates, entry => entry.Type == LegacyMsgUpdateDataType.Mana).Value);
            Assert.NotEmpty(result.ResourceObservations);
            Assert.All(result.ResourceObservations, observation =>
            {
                Assert.Equal(DirectIronbone17650Hp, observation.TargetHitpoints);
                Assert.True(observation.FlashingName);
            });
        }
        if (scenario is "shuriken-before-wild-phoenix" or "wild-phoenix-before-player-scalar")
        {
            // Physical's early1 is scaled by the CALLER: floor(1*75/100)=0.
            // No invented minimum may convert this back to1. Shuriken must
            // return even before WildPhoenix; both return before normal tails.
            var shuriken = scenario == "shuriken-before-wild-phoenix";
            var target = result.Targets[TargetPlayerUid];
            Assert.Equal(shuriken ? "2" : "1",
                target.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
            Assert.Equal("18007", target.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute]);
            Assert.True(HasLegacyStatusFlag(target.Character.Attributes, LegacyWildPhoenixFlag));
            var archives = ParseMsgUpdates(result.Frames[TargetPlayerUid])
                .Where(update => update.Uid == TargetPlayerUid).SelectMany(update => update.Updates)
                .Where(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == LegacyWildPhoenixFlag).ToArray();
            if (shuriken)
            {
                Assert.Empty(archives);
            }
            else
            {
                var archive = Assert.Single(archives);
                Assert.Equal(32U, archive.Time);
                Assert.Equal(18_007U, archive.Damage);
                Assert.Equal(1U, archive.SpellLevel);
            }
        }
        if (scenario == "mud-wall-before-player-scalar")
        {
            // Physical983-985:450-floor(450*50/100)=225; caller75%=>168.
            // Reversing the stages gives337-floor(337*50/100)=169.
            var target = result.Targets[TargetPlayerUid];
            Assert.Equal(5_005, target.Character.Class);
            Assert.True(HasLegacyStatusFlag(target.Character.Attributes, PhysicalMudWallStatusFlag));
            Assert.Equal(BuildPhysicalMudWallMsgNinja(301, 7, 1), target.Character.Attributes["MsgNinja"]);
            Assert.False(target.Character.Attributes.ContainsKey("LegacyMudSigilImpeccablePower"));
        }
        if (scenario == "hawks-after-player-scalar")
        {
            // Receive.Player:109-113 consumes the target flag AFTER caller75%:
            // floor(450*.75)=337, then337-floor(337*50/100)=169, NOT168.
            Assert.False(HasLegacyStatusFlag(result.Targets[TargetPlayerUid].Character.Attributes, LegacyHawksAmbitionFlag));
        }
        if (scenario == "tail-before-player-scalar")
        {
            // Physical/BackDmg sees450 >= Tail400, so Tail is suppressed.
            // Applying75% too early would instead expose337 <400 and counter.
            Assert.Equal("2", result.Targets[TargetPlayerUid].Character.Attributes["LegacyRevengeTailChange"]);
            Assert.DoesNotContain(ParseSpellAnimations(result.Order), animation => animation.SpellId == RevengeTailAttachStatusSpellId);
        }
    }

    [Theory]
    [InlineData(18U, true)]
    [InlineData(19U, false)]
    public async Task DirectIronbone17650PhysicalSelectionHalvesActualRateBeforeTheStrictRoll(uint roll, bool selected)
    {
        var result = await DirectIronbone17650RunAsync(selected ? "rate-eighteen" : "rate-nineteen");
        Assert.Equal(roll, result.RateRoll);
        var animation = Assert.Single(ParseSpellAnimations(result.Frames[PlayerUid]));
        Assert.Equal(selected ? DirectIronbone17650SpellId : DirectIronbone17650FallbackId, animation.SpellId);
        Assert.Contains(animation.Targets, hit => hit.Uid == TargetPlayerUid && hit.Damage > 0);
        Assert.InRange(result.Targets[TargetPlayerUid].Character.MinHitPoints, 1, DirectIronbone17650Hp - 1);
        DirectIronbone17650AssertNoOrdinaryPhysical(result);
    }

    // These four rows intentionally depend on the pending shared player
    // Critical/Lucky/Dash foundation, not on neutral17490 proving that math.
    [Theory]
    [InlineData("critical", 573U, MsgAttackEffect.CriticalStrike)]
    [InlineData("dash", 506U, MsgAttackEffect.Dash)]
    [InlineData("lucky", 675U, MsgAttackEffect.LuckyStrike)]
    [InlineData("strike-lock", 337U, MsgAttackEffect.None)]
    public async Task DirectIronbone17650ScalesAfterSharedPhysicalPlayerEffects(
        string effect, uint expectedDamage, MsgAttackEffect expectedEffect)
    {
        var result = await DirectIronbone17650RunAsync("effect-" + effect);
        var animation = Assert.Single(ParseSpellAnimations(result.Frames[PlayerUid]));
        var hit = Assert.Single(animation.Targets);
        Assert.Equal(expectedDamage, hit.Damage);
        Assert.Equal(expectedEffect, hit.Effect);
        Assert.Equal(DirectIronbone17650Hp - expectedDamage, result.Targets[TargetPlayerUid].Character.MinHitPoints);
        DirectIronbone17650AssertNoOrdinaryPhysical(result);
        DirectIronbone17650AssertNoProgress(result);
        if (effect is "lucky" or "strike-lock")
        {
            var refineIndex = Array.FindIndex(result.Order, bytes => LegacyMsgRefineEffectPacket.TryParse(ReadFrame(bytes), out _));
            var hpIndex = Array.FindIndex(result.Order, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                update.Uid == TargetPlayerUid && update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
            Assert.True(refineIndex >= 0 && hpIndex > refineIndex);
            Assert.True(LegacyMsgRefineEffectPacket.TryParse(ReadFrame(result.Order[refineIndex]), out var refine));
            Assert.Equal((ushort)2319, ReadFrame(result.Order[refineIndex]).Header.Opcode);
            Assert.Equal(PlayerUid, refine.Id);
            Assert.Equal(0U, refine.Param);
            Assert.Equal(effect == "lucky" ? LegacyRefineEffect.LuckyStrike : LegacyRefineEffect.StrikeLockLevel, refine.Effect);
        }
    }

    [Theory]
    [InlineData("mixed-soul-zero")]
    [InlineData("mixed-soul-three")]
    public async Task DirectIronbone17650UsesNonPlayerMaxBonusButOnlyScalesPlayerReceives(string scenario)
    {
        var result = await DirectIronbone17650RunAsync(scenario);
        var animation = Assert.Single(ParseSpellAnimations(result.Frames[PlayerUid]));
        Assert.Equal((840, 584), ((int)animation.X, (int)animation.Y));
        Assert.Equal(result.Soul, animation.LevelHu);
        Assert.Equal(new[] { result.MonsterUid, TargetPlayerUid, (uint)DirectIronbone17650SobUid },
            animation.Targets.Select(hit => hit.Uid));
        Assert.Equal(new[] { 1800U, 337U, 1800U }, animation.Targets.Select(hit => hit.Damage));
        Assert.All(animation.Targets, hit => Assert.True(hit.Hit));
        Assert.Equal(98_200U, result.MonsterHitpoints);
        Assert.Equal(99_663, result.Targets[TargetPlayerUid].Character.MinHitPoints);
        Assert.Equal(98_200, result.SobHitpoints);
        var monsterHpIndex = Array.FindIndex(result.Order, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
            update.Uid == result.MonsterUid && update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
        var playerHpIndex = Array.FindIndex(result.Order, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
            update.Uid == TargetPlayerUid && update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
        var animationIndex = Array.FindIndex(result.Order, bytes => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(monsterHpIndex >= 0 && playerHpIndex > monsterHpIndex && animationIndex > playerHpIndex);
        Assert.NotEmpty(result.AnimationSobObservations);
        Assert.All(result.AnimationSobObservations, hp => Assert.Equal(98_200, hp));
        DirectIronbone17650AssertNoOrdinaryPhysical(result);
        DirectIronbone17650AssertNoProgress(result);
    }

    private static void DirectIronbone17650AssertNoOrdinaryPhysical(DirectIronbone17650Result result)
    {
        Assert.DoesNotContain(result.Order, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical);
    }

    private static void DirectIronbone17650AssertNoProgress(DirectIronbone17650Result result)
    {
        Assert.Equal(0L, result.Caster.Character.Experience);
        Assert.Equal(0, result.Caster.PlayerSpells.Spells.Single(entry => entry.Id == DirectIronbone17650SpellId).Experience);
        Assert.DoesNotContain(result.Order, bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(ParseMsgUpdates(result.Order).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.Experience);
    }

    private static async Task<DirectIronbone17650Result> DirectIronbone17650RunAsync(string scenario)
    {
        var mixed = scenario.StartsWith("mixed-", StringComparison.Ordinal);
        var effects = scenario.StartsWith("effect-", StringComparison.Ordinal);
        var strength = scenario == "small-floor-strength-two" ? 2 : scenario == "small-floor-strength-nine" ? 9 : 300;
        var costs = scenario is "resources-once" or "vertical-query" or "zero-x-query" or "endpoint-all-three-invalid";
        var originX = scenario == "zero-x-query" ? 8 : 830;
        var soul = (byte)(scenario.EndsWith("soul-zero", StringComparison.Ordinal) ? 0 : 3);
        var rateRoll = scenario == "rate-eighteen" ? 18U : scenario == "rate-nineteen" ? 19U : 0U;
        var family = CreateMonsterFamily() with
        {
            Attributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
            {
                ["life"] = "100000", ["level"] = "121", ["defense"] = "0",
                ["defense2"] = "10000", ["Boss"] = "1", ["attack_range"] = "0",
            },
        };
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 31_765, boundX: 838, boundY: 584) with { MapId = 1_000 };
        var sob = CreateMoveLineSobNpc(DirectIronbone17650SobUid, x: 840, y: 584) with
        {
            MapId = 1_000, Life = 100_000, MaxLife = 100_000,
        };
        // Actual DBServer17650 level0: sort90, raw30500, Human38,
        // D2=30205, D3=2, Monster7, RATE38, Range10. The zero-inclusive
        // playerMIN is0%, nonplayerMAX500%; these are ADDED bonuses.
        var row = CreateMagicRow(DirectIronbone17650SpellId, sort: 90,
            useMana: costs ? (ushort)7 : (ushort)0, damage: 30_500, experience: 1_000_000,
            range: scenario == "range-six-empty" ? (byte)6 : (byte)10,
            useStamina: costs ? (byte)5 : (byte)0, coolDown: 0, coldTime: 0,
            damageOnHuman: 38, damage2: 30_205, damage3: 2, damageOnMonster: 7, gDamage: 38);
        Assert.Equal("30500", row.Fields[10]);
        Assert.Equal("38", row.Fields[12]);
        Assert.Equal("38", row.Fields[35]);
        Assert.Equal("30205", row.Fields[36]);
        Assert.Equal("2", row.Fields[37]);
        Assert.Equal("7", row.Fields[44]);
        var rows = new List<MagicType>
        {
            row,
            CreateMagicRow(DirectIronbone17650FallbackId, sort: 87, useMana: 0, damage: 120,
                range: 20, experience: 1_000_000, useStamina: 0, coolDown: 0, coldTime: 0),
            CreateMagicRow(RevengeTailAttachStatusSpellId, sort: 6, useMana: 0, damage: 400, coldTime: 0),
            CreateMagicRow(WildPhoenixSpellId, sort: 6, useMana: 0, damage: 4, coldTime: 0,
                duration: 32, damageOnHuman: 18, damage2: 7),
        };
        var helmetFields = Enumerable.Repeat("0", 64).ToArray();
        helmetFields[0] = DirectIronbone17650HelmetId.ToString(CultureInfo.InvariantCulture);
        helmetFields[1] = "DirectIronbone17650Effects";
        helmetFields[39] = effects ? "1000" : "0";
        var helmet = new ItemType(checked((int)DirectIronbone17650HelmetId), "DirectIronbone17650Effects", helmetFields);
        var world = CreateMagicCatalogCore(family, mixed ? [spawn] : [], mixed ? [sob] : [], rows,
            items: [helmet]);
        if (scenario == "mud-wall-before-player-scalar")
        {
            // A typed, equipped301/level7 sigil resolves its synthetic test
            // power5000 through the real Gouyu Values[5] path, not a raw alias.
            AddPhysicalMudWallGouyuRow(world, CreatePhysicalMudWallGouyuRow(type: 301, level: 7, power: 5_000));
            var gouyu = Assert.Single(world.GouyuTypesByType[301]);
            Assert.Equal(7, gouyu.Level);
            Assert.Equal(5_000, gouyu.Values[5]);
        }
        var equipment = effects ? PlayerItemSnapshot.Empty with
        {
            EquipmentItems = [new PlayerItemSnapshotItem
            {
                ItemId = DirectIronbone17650HelmetId, Position = 1, Durability = 100, MaximumDurability = 100,
            }],
        } : PlayerItemSnapshot.Empty;
        var caster = CreateSession(mapId: 1_000, x: originX, classCode: 2_005, strength: strength, spirit: 0,
            minHitPoints: DirectIronbone17650Hp, minMana: 500, stamina: 100, playerItems: equipment,
            spellEntries: [CreatePlayerSpell(DirectIronbone17650SpellId) with { SoulLevel = soul, UseSpellSoul = soul },
                CreatePlayerSpell(DirectIronbone17650FallbackId)],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0", ["MaxHitpoints"] = "100000", ["BattlePower"] = "120",
                ["LegacyCombatGearAttackSpellIndex"] = "0",
                ["LegacyCombatGearAttackRateRoll"] = rateRoll.ToString(CultureInfo.InvariantCulture),
                ["LegacyPhysicalRefineryRoll"] = scenario == "effect-dash" ? "99" : "0",
                ["LegacyPhysicalPlayerLuckyBranchRoll"] = scenario is "effect-lucky" or "effect-strike-lock" ? "0" : "99",
                ["LegacyPhysicalPlayerCriticalRoll"] = "0", ["LegacyPhysicalDashRoll"] = "0",
                ["LegacyPhysicalLuckyStrikeRoll"] = "0", ["DashRate"] = effects ? "10000" : "0",
                ["PerfectionStatus.LuckyStrike"] = effects ? "100" : "0",
                ["HawksAmbitionPower"] = scenario == "hawks-after-player-scalar" ? "50" : "0",
            }) with
        {
            CombatGear = CombatGearSnapshot.Empty with
            {
                Count = 1, Items = [new CombatGearSnapshotItem(201, Level: 1, Exp: 0, Equip: 1,
                    Transform: 0, MasteryScore: 0, Animas: [], BlackJades: [])],
            },
        };
        WorldSession MakeTarget(uint uid, long x, long y, bool isPrimary)
        {
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxHitpoints"] = "100000", ["BattlePower"] = "120",
                ["PerfectionStatus.StrikeLock"] = scenario == "effect-strike-lock" ? "100" : "0",
                ["LegacyPhysicalPlayerStrikeLockRoll"] = "0",
            };
            if (isPrimary && scenario == "opening-solid")
            {
                attrs["LegacySpeffectLevel.Solid"] = "4";
                attrs["LegacySpeffectRoll.Solid"] = "0";
            }
            if (isPrimary && (scenario is "shuriken-before-wild-phoenix" or "wild-phoenix-before-player-scalar"))
            {
                attrs["StatusFlags"] = scenario == "shuriken-before-wild-phoenix"
                    ? BuildStatusFlags(LegacyPhysicalSpellsShurikenVortexFlag, LegacyWildPhoenixFlag)
                    : BuildStatusFlags(LegacyWildPhoenixFlag);
                attrs[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007";
                attrs[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2";
            }
            if (isPrimary && scenario == "mud-wall-before-player-scalar")
            {
                attrs["StatusFlags"] = BuildStatusFlags(PhysicalMudWallStatusFlag);
                attrs["MsgNinja"] = BuildPhysicalMudWallMsgNinja(301, 7, 1);
            }
            if (isPrimary && scenario == "hawks-after-player-scalar")
            {
                attrs["StatusFlags"] = BuildStatusFlags(LegacyHawksAmbitionFlag);
            }
            if (isPrimary && scenario == "tail-before-player-scalar")
            {
                attrs["StatusFlags"] = BuildStatusFlags(RevengeTailStatusFlag);
                attrs["LegacyRevengeTailChange"] = "2";
            }
            return CreateSession(uid: uid, name: "DirectIronbone17650Target", mapId: 1_000,
                x: x, y: y, classCode: isPrimary && scenario == "mud-wall-before-player-scalar" ? 5_005 : 2_005,
                strength: 0, spirit: 0,
                reborn: isPrimary && (scenario is "shuriken-before-wild-phoenix" or "wild-phoenix-before-player-scalar") ? 2 : 0,
                minHitPoints: isPrimary && scenario == "overkill" ? 5 : DirectIronbone17650Hp,
                spellIds: isPrimary && scenario == "tail-before-player-scalar" ? [RevengeTailAttachStatusSpellId] :
                    isPrimary && (scenario is "shuriken-before-wild-phoenix" or "wild-phoenix-before-player-scalar") ? [WildPhoenixSpellId] : [],
                attributes: attrs);
        }
        var primary = MakeTarget(TargetPlayerUid, scenario == "zero-x-query" ? 0 : scenario == "vertical-query" ? 830 : 839,
            scenario == "vertical-query" ? 593 : 584, isPrimary: true);
        var targets = new Dictionary<uint, WorldSession> { [TargetPlayerUid] = primary };
        if (scenario == "line-boundaries")
        {
            targets[TargetPlayerUid + 1] = MakeTarget(TargetPlayerUid + 1, 840, 584, false);
            targets[TargetPlayerUid + 2] = MakeTarget(TargetPlayerUid + 2, 835, 586, false);
            targets[TargetPlayerUid + 3] = MakeTarget(TargetPlayerUid + 3, 835, 587, false);
            targets[TargetPlayerUid + 4] = MakeTarget(TargetPlayerUid + 4, 841, 584, false);
        }
        var casterFields = LegacyPlayerStatusCalculator.CalculateFields(caster.Character, caster.PlayerItems, world, 100_000, prestigeLevel: 0);
        Assert.Equal(checked((uint)strength), casterFields[LegacyPlayerStatusPacket.MinAttackIndex]);
        Assert.Equal(checked((uint)(strength + 1)), casterFields[LegacyPlayerStatusPacket.MaxAttackIndex]);
        Assert.Equal(effects ? 1_000U : 0U, casterFields[LegacyPlayerStatusPacket.CriticalStrikeIndex]);
        Assert.Equal(0U, casterFields[LegacyPlayerStatusPacket.BreakthroughIndex]);
        Assert.Equal(0U, casterFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex]);
        var targetFields = LegacyPlayerStatusCalculator.CalculateFields(primary.Character, primary.PlayerItems, world,
            checked((uint)primary.Character.MinHitPoints), prestigeLevel: 0);
        Assert.Equal(0U, targetFields[LegacyPlayerStatusPacket.DefenceIndex]);
        Assert.Equal(0U, targetFields[LegacyPlayerStatusPacket.ImmunityIndex]);
        Assert.Equal(0U, targetFields[LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex]);
        if (scenario == "mud-wall-before-player-scalar")
        {
            Assert.Equal(5_005, primary.Character.Class);
            Assert.False(primary.AlternateEquipmentActive);
            Assert.True(HasLegacyStatusFlag(primary.Character.Attributes, PhysicalMudWallStatusFlag));
            Assert.Equal(BuildPhysicalMudWallMsgNinja(301, 7, 1), primary.Character.Attributes["MsgNinja"]);
        }
        if (scenario == "hawks-after-player-scalar")
        {
            Assert.True(HasLegacyStatusFlag(primary.Character.Attributes, LegacyHawksAmbitionFlag));
            Assert.Equal("50", caster.Character.Attributes["HawksAmbitionPower"]);
        }
        var blocked = scenario switch
        {
            "endpoint-back-one" => 1,
            "endpoint-back-two" => 2,
            "endpoint-all-three-invalid" => 3,
            _ => 0,
        };
        var terrain = new LegacyGameMapTerrainQuery([new LegacyDMapTerrain(1_000,
            "fixture://direct-ironbone17650.dmap", 900, 620, new DirectIronbone17650Cells(blocked))]);
        var monster = CreateRuntimeMonsterInstance(world, spawn, family);
        var monsters = new MonsterRuntimeStateStore();
        var sobs = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var captures = targets.Keys.Append(PlayerUid).ToDictionary(uid => uid, _ => new ConcurrentQueue<byte[]>());
        var order = new ConcurrentQueue<byte[]>();
        var resources = new ConcurrentQueue<DirectIronbone17650ResourceObservation>();
        var animationSobHp = new ConcurrentQueue<int>();
        var animationTargetHp = new ConcurrentQueue<long>();
        var solidHp = new ConcurrentQueue<long>();
        var animationRecipients = new ConcurrentQueue<uint>();
        var connections = new Dictionary<uint, ConquerTcpConnection>();
        ValueTask Capture(uint uid, ReadOnlyMemory<byte> memory)
        {
            var bytes = memory.ToArray();
            captures[uid].Enqueue(bytes);
            order.Enqueue(bytes);
            if (uid == TargetPlayerUid && LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var archive) &&
                archive.Uid == TargetPlayerUid && archive.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == StarArrowReceiveEnvelopeSolidFlag))
            {
                Assert.True(sessions.TryGet(connections[TargetPlayerUid], out var currentTarget));
                Assert.NotNull(currentTarget);
                solidHp.Enqueue(currentTarget.Character.MinHitPoints);
            }
            if (LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) && animation.SpellId == DirectIronbone17650SpellId)
            {
                animationRecipients.Enqueue(uid);
                Assert.True(sessions.TryGet(connections[TargetPlayerUid], out var currentTarget));
                Assert.NotNull(currentTarget);
                animationTargetHp.Enqueue(currentTarget.Character.MinHitPoints);
            }
            if (uid == PlayerUid && LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.Stamina or LegacyMsgUpdateDataType.Mana))
            {
                Assert.True(sessions.TryGet(connections[PlayerUid], out var currentCaster));
                Assert.True(sessions.TryGet(connections[TargetPlayerUid], out var currentTarget));
                Assert.NotNull(currentCaster);
                Assert.NotNull(currentTarget);
                resources.Enqueue(new DirectIronbone17650ResourceObservation(currentTarget.Character.MinHitPoints,
                    HasLegacyStatusFlag(currentCaster.Character.Attributes, DirectIronbone17650FlashingNameFlag)));
            }
            if (mixed && LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out _))
            {
                animationSobHp.Enqueue(sobs.GetVisible(sob).Life);
            }
            return ValueTask.CompletedTask;
        }
        foreach (var uid in captures.Keys)
        {
            connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: (frame, _) => Capture(uid, frame));
            sessions.Set(connections[uid], uid == PlayerUid ? caster : targets[uid]);
        }
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(connections[PlayerUid], TextWriter.Synchronized(log), caster,
            WorldData: world, Sessions: sessions, MonsterStates: monsters, SobNpcStates: sobs,
            Terrain: terrain, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PacketHandlers.HandleAsync(context, ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
            Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 120, Y: 121, AttackType: MsgAttackType.Physical,
            Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
        Assert.True(sessions.TryGet(connections[PlayerUid], out var finalCaster));
        Assert.NotNull(finalCaster);
        var finalTargets = targets.Keys.ToDictionary(uid => uid, uid =>
        {
            Assert.True(sessions.TryGet(connections[uid], out var target));
            Assert.NotNull(target);
            return target;
        });
        var monsterHp = mixed && monsters.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var finalMonster)
            ? finalMonster.Hitpoints : 0U;
        return new DirectIronbone17650Result(finalCaster, finalTargets,
            captures.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()), order.ToArray(), soul, costs, rateRoll,
            resources.ToArray(), monster.Uid, monsterHp, mixed ? sobs.GetVisible(sob).Life : 0, animationSobHp.ToArray(),
            originX, animationTargetHp.ToArray(), solidHp.ToArray(), animationRecipients.ToArray());
    }

    private sealed class DirectIronbone17650Cells(int blockedEndpointTiles) : IReadOnlyList<LegacyDMapCell>
    {
        public int Count => 900 * 620;
        public LegacyDMapCell this[int index]
        {
            get
            {
                ArgumentOutOfRangeException.ThrowIfNegative(index);
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
                var x = index % 900;
                var y = index / 900;
                var blocked = y == 584 && x <= 840 && x > 840 - blockedEndpointTiles;
                return new LegacyDMapCell(x, y, blocked ? (short)1 : (short)0, 1, 0);
            }
        }
        public IEnumerator<LegacyDMapCell> GetEnumerator() => Enumerable.Range(0, Count).Select(index => this[index]).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed record DirectIronbone17650ResourceObservation(long TargetHitpoints, bool FlashingName);
    private sealed record DirectIronbone17650Result(WorldSession Caster, IReadOnlyDictionary<uint, WorldSession> Targets,
        IReadOnlyDictionary<uint, byte[][]> Frames, byte[][] Order, byte Soul, bool CostsResources, uint RateRoll,
        DirectIronbone17650ResourceObservation[] ResourceObservations, uint MonsterUid, uint MonsterHitpoints,
        int SobHitpoints, int[] AnimationSobObservations, int OriginX, long[] AnimationTargetObservations, long[] SolidObservations,
        uint[] AnimationRecipients);
}
