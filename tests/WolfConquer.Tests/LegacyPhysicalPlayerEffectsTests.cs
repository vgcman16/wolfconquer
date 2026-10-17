using System.Collections.Concurrent;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort PhysicalPlayerEffectsSpellId = 17_490;
    private const uint PhysicalPlayerEffectsCasterHelmetId = 111_020;
    private const uint PhysicalPlayerEffectsTargetHelmetId = 111_030;
    private const uint PhysicalPlayerEffectsObserverUid = TargetPlayerUid + 100;

    [Theory]
    [InlineData("critical", 765U, MsgAttackEffect.CriticalStrike, LegacyRefineEffect.None)]
    [InlineData("refinery-fails-dash", 675U, MsgAttackEffect.Dash, LegacyRefineEffect.None)]
    [InlineData("dash-suppresses-breakthrough", 675U, MsgAttackEffect.Dash, LegacyRefineEffect.None)]
    [InlineData("critical-zero", 450U, MsgAttackEffect.None, LegacyRefineEffect.None)]
    [InlineData("refinery-success-critical-miss", 450U, MsgAttackEffect.None, LegacyRefineEffect.None)]
    [InlineData("lucky", 900U, MsgAttackEffect.LuckyStrike, LegacyRefineEffect.LuckyStrike)]
    [InlineData("strike-lock", 450U, MsgAttackEffect.None, LegacyRefineEffect.StrikeLockLevel)]
    [InlineData("lucky-branch-lucky-miss", 450U, MsgAttackEffect.None, LegacyRefineEffect.None)]
    [InlineData("fractional-lucky", 900U, MsgAttackEffect.LuckyStrike, LegacyRefineEffect.LuckyStrike)]
    public async Task PhysicalPlayerEffects17490UsesIndependentSourceDrawsBeforeBreakthroughAndReceive(
        string scenario,
        uint expectedDamage,
        MsgAttackEffect expectedEffect,
        LegacyRefineEffect expectedRefine)
    {
        var result = await PhysicalPlayerEffectsRunAsync(scenario);

        // Physical.cs:395-540 gates the whole block on real CriticalStrike!=0.
        // Refinery, the1% Lucky branch, ordinary critical, Lucky, StrikeLock and
        // Dash have independent draws. Dash belongs only to the refinery/BP
        // ELSE; a missed critical or missed/locked Lucky cannot fall into Dash.
        // With neutral base300 and no penetration: Crit170% =>510+255=765;
        // Dash150% =>450+225=675; Lucky200% =>600+300=900; otherwise300+150=450.
        foreach (var frames in new[] { result.CasterFrames, result.TargetFrames })
        {
            var animation = Assert.Single(ParseSpellAnimations(frames));
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(PhysicalPlayerEffectsSpellId, animation.SpellId);
            Assert.Equal((839, 584), ((int)animation.X, (int)animation.Y));
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.True(target.Hit);
            Assert.Equal(expectedDamage, target.Damage);
            Assert.Equal(expectedEffect, target.Effect);
            var animationBytes = Assert.Single(frames, bytes =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out _));
            Assert.Equal((ushort)2239, ReadFrame(animationBytes).Header.Opcode);
        }

        Assert.Equal(100_000L - expectedDamage, result.Target.Character.MinHitPoints);
        Assert.Equal(100_000, result.Caster.Character.MinHitPoints);
        Assert.Equal(500, result.Caster.Character.MinMana);
        Assert.Equal("100", result.Caster.Character.Attributes["Stamina"]);
        Assert.DoesNotContain(result.DeliveryOrder, bytes =>
            MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical);
        var hp = ParseMsgUpdates(result.TargetFrames)
            .Where(update => update.Uid == TargetPlayerUid)
            .SelectMany(update => update.Updates)
            .Where(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).ToArray();
        Assert.Equal(checked((ulong)(100_000L - expectedDamage)), Assert.Single(hp).Value);

        if (expectedRefine == LegacyRefineEffect.None)
        {
            Assert.Empty(result.RefineObservations);
            Assert.DoesNotContain(result.DeliveryOrder, bytes =>
                LegacyMsgRefineEffectPacket.TryParse(ReadFrame(bytes), out _));
        }
        else
        {
            // Source Physical.cs:500-515 uses TARGET.View.SendView(me:true),
            // attacker Id, default dwParam0. The target-only observer at856 is
            //17 tiles from target839 but26 from caster830: refine must reach it,
            // while this attacker-centered fixed15-line animation must not.
            Assert.Equal(3, result.RefineObservations.Length);
            Assert.Equal(TargetPlayerUid, result.RefineObservations[0].RecipientUid);
            Assert.All(result.RefineObservations, observation =>
            {
                Assert.Equal(100_000, observation.TargetHitpoints);
                Assert.Equal(PlayerUid, observation.CasterUid);
                Assert.Equal(TargetPlayerUid, observation.TargetUid);
            });
            foreach (var frames in new[] { result.CasterFrames, result.TargetFrames, result.ObserverFrames })
            {
                var refineBytes = Assert.Single(frames, bytes =>
                    LegacyMsgRefineEffectPacket.TryParse(ReadFrame(bytes), out _));
                Assert.Equal((ushort)2319, ReadFrame(refineBytes).Header.Opcode);
                Assert.True(LegacyMsgRefineEffectPacket.TryParse(ReadFrame(refineBytes), out var refine));
                Assert.Equal(PlayerUid, refine.Id);
                Assert.Equal(0U, refine.Param);
                Assert.Equal(expectedRefine, refine.Effect);
            }
            Assert.Empty(ParseSpellAnimations(result.ObserverFrames));
            Assert.NotNull(result.Observer);
            Assert.Equal(100_000, result.Observer.Character.MinHitPoints);
            var lastRefineIndex = Enumerable.Range(0, result.DeliveryOrder.Length).Where(index =>
                LegacyMsgRefineEffectPacket.TryParse(ReadFrame(result.DeliveryOrder[index]), out _)).Max();
            var hpIndices = Enumerable.Range(0, result.DeliveryOrder.Length).Where(index =>
                LegacyMsgUpdatePacket.TryParse(ReadFrame(result.DeliveryOrder[index]), out var update) &&
                update.Uid == TargetPlayerUid && update.Updates.Any(entry =>
                    entry.Type is LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints)).ToArray();
            Assert.NotEmpty(hpIndices);
            Assert.All(hpIndices, index => Assert.True(lastRefineIndex < index));
        }
    }

    [Theory]
    [InlineData("attacker")]
    [InlineData("target")]
    public async Task PhysicalPlayerEffects17490RefineSendIdentityLossStopsOpeningReceiveWithoutOverwritingReplacement(
        string participant)
    {
        var result = await PhysicalPlayerEffectsRunAsync("lucky-rebind", participant);

        Assert.Equal(1, result.MutationCount);
        PhysicalPlayerEffectsAssertIdentityLossStopsRefineFanout(result);
        Assert.NotNull(result.CasterBeforeMutation);
        Assert.NotNull(result.TargetBeforeMutation);
        Assert.Equal(PlayerUid, result.CasterBeforeMutation.Character.Uid);
        Assert.Equal(TargetPlayerUid, result.TargetBeforeMutation.Character.Uid);
        Assert.Equal(100_000, result.CasterBeforeMutation.Character.MinHitPoints);
        Assert.Equal(100_000, result.TargetBeforeMutation.Character.MinHitPoints);
        var targetRefineBytes = Assert.Single(result.TargetFrames, bytes =>
            LegacyMsgRefineEffectPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(LegacyMsgRefineEffectPacket.TryParse(ReadFrame(targetRefineBytes), out var refine));
        Assert.Equal((ushort)2319, ReadFrame(targetRefineBytes).Header.Opcode);
        Assert.Equal(PlayerUid, refine.Id);
        Assert.Equal(0U, refine.Param);
        Assert.Equal(LegacyRefineEffect.LuckyStrike, refine.Effect);

        var registered = participant == "attacker" ? result.Caster : result.Target;
        var other = participant == "attacker" ? result.Target : result.Caster;
        Assert.Same(result.Replacement, registered);
        Assert.Equal(77_777, registered.Character.MinHitPoints);
        Assert.Equal(77, registered.Character.MinMana);
        Assert.Equal("33", registered.Character.Attributes["Stamina"]);
        Assert.Equal("replacement", registered.Character.Attributes["PhysicalPlayerEffectsIdentityMarker"]);
        Assert.Equal(100_000, other.Character.MinHitPoints);
        Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder), update =>
            update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints));
        Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder), update =>
            update.Uid == result.Replacement.Character.Uid);
        Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == StarArrowReceiveEnvelopeSolidFlag);
        Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, StarArrowReceiveEnvelopeSolidFlag));
        Assert.False(HasLegacyStatusFlag(result.Target.Character.Attributes, StarArrowReceiveEnvelopeSolidFlag));
        Assert.Empty(ParseSpellAnimations(result.DeliveryOrder));
        Assert.DoesNotContain(result.DeliveryOrder, bytes =>
            MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) && attack.Query.AttackType == MsgAttackType.Physical);
    }

    [Theory]
    [InlineData("early-shuriken-critical", true)]
    [InlineData("early-shuriken-lucky", true)]
    [InlineData("early-wild-phoenix-critical", false)]
    [InlineData("early-wild-phoenix-lucky", false)]
    public async Task PhysicalPlayerEffects17490EarlyFixedOneBypassesGuaranteedCriticalAndLucky(
        string scenario,
        bool shuriken)
    {
        var result = await PhysicalPlayerEffectsRunAsync(scenario);

        // Physical.cs:157-165 returns before all Critical/Lucky/Dash work.
        // Both fixtures carry REAL WildPhoenix2; Shuriken must bypass even
        // that defender, while WildPhoenix alone must consume exactly one.
        // Public fields preflight Critical10000 (100%), with independent
        // source rolls forcing either ordinary critical or the Lucky branch.
        foreach (var frames in new[] { result.CasterFrames, result.TargetFrames })
        {
            var animationBytes = Assert.Single(frames, bytes =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out _));
            Assert.Equal((ushort)2239, ReadFrame(animationBytes).Header.Opcode);
            Assert.True(LegacySpellAnimationPacket.TryParse(ReadFrame(animationBytes), out var animation));
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(PhysicalPlayerEffectsSpellId, animation.SpellId);
            Assert.Equal((839, 584), ((int)animation.X, (int)animation.Y));
            var hit = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, hit.Uid);
            Assert.Equal(1U, hit.Damage);
            Assert.True(hit.Hit);
            Assert.Equal(MsgAttackEffect.None, hit.Effect);
        }
        Assert.Equal(99_999, result.Target.Character.MinHitPoints);
        Assert.Equal(100_000, result.Caster.Character.MinHitPoints);
        Assert.Equal(500, result.Caster.Character.MinMana);
        Assert.Equal("100", result.Caster.Character.Attributes["Stamina"]);
        Assert.Equal(shuriken ? "2" : "1",
            result.Target.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.Equal("18007",
            result.Target.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute]);
        Assert.True(HasLegacyStatusFlag(result.Target.Character.Attributes, LegacyWildPhoenixFlag));
        Assert.Empty(result.RefineObservations);
        Assert.DoesNotContain(result.DeliveryOrder, bytes =>
            LegacyMsgRefineEffectPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(result.DeliveryOrder, bytes =>
            MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical);
        var hp = ParseMsgUpdates(result.TargetFrames)
            .Where(update => update.Uid == TargetPlayerUid)
            .SelectMany(update => update.Updates)
            .Where(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).ToArray();
        Assert.Equal(99_999UL, Assert.Single(hp).Value);
        var wpArchives = ParseMsgUpdates(result.TargetFrames)
            .Where(update => update.Uid == TargetPlayerUid)
            .SelectMany(update => update.Updates)
            .Where(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag).ToArray();
        if (shuriken)
        {
            Assert.Empty(wpArchives);
        }
        else
        {
            var archive = Assert.Single(wpArchives);
            Assert.Equal(32U, archive.Time);
            Assert.Equal(18_007U, archive.Damage);
            Assert.Equal(1U, archive.SpellLevel);
            var archiveIndex = Array.FindIndex(result.TargetFrames, bytes =>
                LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                update.Uid == TargetPlayerUid && update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill && entry.Flag == LegacyWildPhoenixFlag));
            var hpIndex = Array.FindIndex(result.TargetFrames, bytes =>
                LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                update.Uid == TargetPlayerUid && update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Hitpoints));
            Assert.True(archiveIndex >= 0 && hpIndex > archiveIndex);
            Assert.Equal((ushort)2176, ReadFrame(result.TargetFrames[archiveIndex]).Header.Opcode);
        }
    }

    [Theory]
    [InlineData("attacker")]
    [InlineData("target")]
    public async Task PhysicalPlayerEffects17490TargetFirstRefineRemovalDoesNotRestoreRegistrationOrStartReceive(
        string participant)
    {
        var result = await PhysicalPlayerEffectsRunAsync("lucky-remove", removeParticipant: participant);

        Assert.Equal(1, result.MutationCount);
        PhysicalPlayerEffectsAssertIdentityLossStopsRefineFanout(result);
        Assert.NotNull(result.CasterBeforeMutation);
        Assert.NotNull(result.TargetBeforeMutation);
        Assert.Equal(PlayerUid, result.CasterBeforeMutation.Character.Uid);
        Assert.Equal(TargetPlayerUid, result.TargetBeforeMutation.Character.Uid);
        Assert.Equal(100_000, result.CasterBeforeMutation.Character.MinHitPoints);
        Assert.Equal(100_000, result.TargetBeforeMutation.Character.MinHitPoints);
        var firstRefineBytes = Assert.Single(result.TargetFrames, bytes =>
            LegacyMsgRefineEffectPacket.TryParse(ReadFrame(bytes), out _));
        Assert.Equal((ushort)2319, ReadFrame(firstRefineBytes).Header.Opcode);
        Assert.True(LegacyMsgRefineEffectPacket.TryParse(ReadFrame(firstRefineBytes), out var refine));
        Assert.Equal(PlayerUid, refine.Id);
        Assert.Equal(0U, refine.Param);
        Assert.Equal(LegacyRefineEffect.LuckyStrike, refine.Effect);
        Assert.Equal(participant != "attacker", result.CasterRegistered);
        Assert.Equal(participant != "target", result.TargetRegistered);
        // An intentionally missing participant is represented below by its
        // immutable pre-removal snapshot, NEVER by a synthetic registration.
        Assert.Equal(100_000, result.Caster.Character.MinHitPoints);
        Assert.Equal(100_000, result.Target.Character.MinHitPoints);
        Assert.NotNull(result.Observer);
        Assert.Equal(100_000, result.Observer.Character.MinHitPoints);
        Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder), update =>
            update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints));
        Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == StarArrowReceiveEnvelopeSolidFlag);
        Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, StarArrowReceiveEnvelopeSolidFlag));
        Assert.False(HasLegacyStatusFlag(result.Target.Character.Attributes, StarArrowReceiveEnvelopeSolidFlag));
        Assert.Empty(ParseSpellAnimations(result.DeliveryOrder));
        Assert.DoesNotContain(result.DeliveryOrder, bytes =>
            MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical);
    }

    private static void PhysicalPlayerEffectsAssertIdentityLossStopsRefineFanout(PhysicalPlayerEffectsResult result)
    {
        // The awaited first target publication is itself a reentry boundary.
        // Both ORIGINAL identities must still exist before any later peer send.
        var observation = Assert.Single(result.RefineObservations);
        Assert.Equal(TargetPlayerUid, observation.RecipientUid);
        Assert.Equal(100_000, observation.TargetHitpoints);
        Assert.Equal(PlayerUid, observation.CasterUid);
        Assert.Equal(TargetPlayerUid, observation.TargetUid);
        Assert.Single(result.DeliveryOrder, bytes =>
            LegacyMsgRefineEffectPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(result.CasterFrames, bytes =>
            LegacyMsgRefineEffectPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(result.ObserverFrames, bytes =>
            LegacyMsgRefineEffectPacket.TryParse(ReadFrame(bytes), out _));
    }

    private static async Task<PhysicalPlayerEffectsResult> PhysicalPlayerEffectsRunAsync(
        string scenario,
        string? replaceParticipant = null,
        string? removeParticipant = null)
    {
        var earlyShuriken = scenario.StartsWith("early-shuriken-", StringComparison.Ordinal);
        var earlyReturn = earlyShuriken || scenario.StartsWith("early-wild-phoenix-", StringComparison.Ordinal);
        var earlyLucky = earlyReturn && scenario.EndsWith("-lucky", StringComparison.Ordinal);
        var criticalStrike = earlyReturn ? 10_000U : scenario == "critical-zero" ? 0U : 1_000U;
        var immunity = scenario == "refinery-fails-dash" ? 500U : 0U;
        var breakthrough = scenario == "dash-suppresses-breakthrough" ? 20U : 0U;
        var luckyBranch = scenario is "critical-zero" or "lucky" or "strike-lock" or "fractional-lucky" or
            "lucky-branch-lucky-miss" or "lucky-rebind" or "lucky-remove" || earlyLucky;
        var luckyRate = scenario == "fractional-lucky" ? "0.5" :
            scenario == "lucky-branch-lucky-miss" ? "50" : "100";
        var strikeLockRate = scenario == "strike-lock" ? "100" : "0";
        var hasObserver = scenario is "lucky" or "strike-lock" or "fractional-lucky" or "lucky-rebind" or "lucky-remove";
        var magicRows = new List<MagicType>
        {
            CreateMagicRow(PhysicalPlayerEffectsSpellId, sort: 99, useMana: 0,
                damage: 30_250, experience: 1_000_000, range: 4, useStamina: 0,
                coolDown: 0, coldTime: 0, damageOnHuman: 0, damage2: 33,
                damage3: 2, damageOnMonster: 30_085, gDamage: 0),
        };
        if (earlyReturn)
        {
            magicRows.Add(CreateMagicRow(WildPhoenixSpellId, sort: 6, useMana: 0, damage: 4,
                coldTime: 0, duration: 32, damageOnHuman: 18, damage2: 7));
        }
        var worldData = CreateMagicCatalogCore(CreateMonsterFamily(), spawns: [], sobNpcs: [],
            magicRows: magicRows,
            items:
            [
                PhysicalPlayerEffectsCreateHelmet(PhysicalPlayerEffectsCasterHelmetId, criticalStrike, immunity: 0,
                    breakthrough: breakthrough),
                PhysicalPlayerEffectsCreateHelmet(PhysicalPlayerEffectsTargetHelmetId, criticalStrike: 0, immunity: immunity),
            ]);
        var caster = CreateSession(mapId: 1_000, classCode: 2_005, strength: 300, spirit: 0,
            minHitPoints: 100_000, minMana: 500, stamina: 100, spellIds: [PhysicalPlayerEffectsSpellId],
            playerItems: PhysicalPlayerEffectsEquipHelmet(PhysicalPlayerEffectsCasterHelmetId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0",
                ["MaxHitpoints"] = "100000",
                ["BattlePower"] = breakthrough > 0 ? "100" : "120",
                ["LegacyCombatGearAttackSpellIndex"] = "3",
                ["LegacyCombatGearAttackRateRoll"] = "0",
                ["DashRate"] = "10000",
                ["PerfectionStatus.LuckyStrike"] = luckyRate,
                ["LegacyPhysicalRefineryRoll"] = scenario == "refinery-fails-dash" ? "7" : "0",
                ["LegacyPhysicalBreakthroughRefineryRoll"] = "0",
                ["LegacyPhysicalPlayerLuckyBranchRoll"] = luckyBranch ? "0" : "99",
                ["LegacyPhysicalPlayerCriticalRoll"] = scenario == "refinery-success-critical-miss" ? "99" : "0",
                ["LegacyPhysicalLuckyStrikeRoll"] = scenario == "lucky-branch-lucky-miss" ? "99" : "0",
                ["LegacyPhysicalDashRoll"] = "0",
            }) with
        {
            CombatGear = CombatGearSnapshot.Empty with
            {
                Count = 1,
                Items =
                [
                    new CombatGearSnapshotItem(201, Level: 1, Exp: 0, Equip: 1,
                        Transform: 0, MasteryScore: 0, Animas: [], BlackJades: []),
                ],
            },
        };
        var targetAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MaxHitpoints"] = "100000",
            ["BattlePower"] = breakthrough > 0 ? "200" : "120",
            ["PerfectionStatus.StrikeLock"] = strikeLockRate,
            ["LegacyPhysicalPlayerStrikeLockRoll"] = "0",
        };
        if (earlyReturn)
        {
            targetAttributes["StatusFlags"] = earlyShuriken
                ? BuildStatusFlags(LegacyPhysicalSpellsShurikenVortexFlag, LegacyWildPhoenixFlag)
                : BuildStatusFlags(LegacyWildPhoenixFlag);
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007";
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2";
        }
        if (replaceParticipant is not null || removeParticipant is not null)
        {
            // If identity refresh is delayed until after the receive, this forced
            // opening hook exposes it even before the stale HP write/animation.
            targetAttributes["LegacySpeffectLevel.Solid"] = "4";
            targetAttributes["LegacySpeffectRoll.Solid"] = "0";
        }
        var target = CreateSession(uid: TargetPlayerUid, name: "PhysicalPlayerEffectsTarget",
            mapId: 1_000, x: 839, y: 584, classCode: 2_005, strength: 0, spirit: 0,
            minHitPoints: 100_000, reborn: earlyReturn ? 2 : 0,
            spellIds: earlyReturn ? [WildPhoenixSpellId] : [],
            playerItems: PhysicalPlayerEffectsEquipHelmet(PhysicalPlayerEffectsTargetHelmetId),
            attributes: targetAttributes);
        var casterFields = LegacyPlayerStatusCalculator.CalculateFields(
            caster.Character, caster.PlayerItems, worldData, 100_000, prestigeLevel: 0);
        var targetFields = LegacyPlayerStatusCalculator.CalculateFields(
            target.Character, target.PlayerItems, worldData, 100_000, prestigeLevel: 0);
        Assert.Equal(300U, casterFields[LegacyPlayerStatusPacket.MinAttackIndex]);
        Assert.Equal(301U, casterFields[LegacyPlayerStatusPacket.MaxAttackIndex]);
        Assert.Equal(criticalStrike, casterFields[LegacyPlayerStatusPacket.CriticalStrikeIndex]);
        Assert.Equal(immunity, targetFields[LegacyPlayerStatusPacket.ImmunityIndex]);
        Assert.Equal(0U, targetFields[LegacyPlayerStatusPacket.DefenceIndex]);
        Assert.Equal(breakthrough, casterFields[LegacyPlayerStatusPacket.BreakthroughIndex]);
        Assert.Equal(0U, targetFields[LegacyPlayerStatusPacket.CounteractionIndex]);
        // These are source PerfectionStatus DOUBLE chances, not the different
        // raw relic LuckyStrikePercent field. DashRate is source raw hundredths.
        Assert.Equal(0U, casterFields[LegacyPlayerStatusPacket.LuckyStrikePercentIndex]);
        Assert.Equal(0U, targetFields[LegacyPlayerStatusPacket.StrikeLockPercentIndex]);
        Assert.Equal("10000", caster.Character.Attributes["DashRate"]);
        Assert.Equal(luckyRate, caster.Character.Attributes["PerfectionStatus.LuckyStrike"]);
        Assert.Equal(strikeLockRate, target.Character.Attributes["PerfectionStatus.StrikeLock"]);

        var observer = hasObserver ? CreateSession(uid: PhysicalPlayerEffectsObserverUid,
            name: "PhysicalPlayerEffectsObserver", mapId: 1_000, x: 856, y: 584,
            classCode: 2_005, strength: 0, spirit: 0, minHitPoints: 100_000, spellIds: [],
            attributes: new Dictionary<string, string> { ["MaxHitpoints"] = "100000" }) : null;
        var replacement = CreateSession(uid: TargetPlayerUid + 2_000, name: "PhysicalPlayerEffectsReplacement",
            mapId: 1_000, x: 832, y: 584, minHitPoints: 77_777, minMana: 77, stamina: 33, spellIds: [],
            attributes: new Dictionary<string, string>
            {
                ["MaxHitpoints"] = "77777",
                ["PhysicalPlayerEffectsIdentityMarker"] = "replacement",
            });
        var sessions = new WorldSessionStore();
        var casterFrames = new ConcurrentQueue<byte[]>();
        var targetFrames = new ConcurrentQueue<byte[]>();
        var observerFrames = new ConcurrentQueue<byte[]>();
        var deliveryOrder = new ConcurrentQueue<byte[]>();
        var observations = new ConcurrentQueue<PhysicalPlayerEffectsRefineObservation>();
        var mutationCount = 0;
        WorldSession? casterBeforeMutation = null;
        WorldSession? targetBeforeMutation = null;
        ConquerTcpConnection? casterConnection = null;
        ConquerTcpConnection? targetConnection = null;

        ValueTask Capture(uint recipientUid, ConcurrentQueue<byte[]> frames, ReadOnlyMemory<byte> frame)
        {
            var bytes = frame.ToArray();
            frames.Enqueue(bytes);
            deliveryOrder.Enqueue(bytes);
            if (LegacyMsgRefineEffectPacket.TryParse(ReadFrame(bytes), out var refine))
            {
                var casterRegistered = sessions.TryGet(casterConnection!, out var currentCaster);
                var targetRegistered = sessions.TryGet(targetConnection!, out var currentTarget);
                if (removeParticipant is null || Volatile.Read(ref mutationCount) == 0)
                {
                    Assert.True(casterRegistered);
                    Assert.NotNull(currentCaster);
                    Assert.True(targetRegistered);
                    Assert.NotNull(currentTarget);
                }
                // If an incorrect implementation continues fan-out after
                // removal, let the explicit no-later-delivery assertions fail;
                // do not throw inside this callback merely for requested absence.
                if (currentCaster is not null && currentTarget is not null)
                {
                    observations.Enqueue(new PhysicalPlayerEffectsRefineObservation(
                        recipientUid, currentTarget.Character.MinHitPoints,
                        checked((uint)currentCaster.Character.Uid), checked((uint)currentTarget.Character.Uid)));
                }
                if ((replaceParticipant is not null || removeParticipant is not null) && recipientUid == TargetPlayerUid &&
                    refine.Effect == LegacyRefineEffect.LuckyStrike &&
                    Interlocked.CompareExchange(ref mutationCount, 1, 0) == 0)
                {
                    Assert.NotNull(currentCaster);
                    Assert.NotNull(currentTarget);
                    casterBeforeMutation = currentCaster;
                    targetBeforeMutation = currentTarget;
                    var participant = removeParticipant ?? replaceParticipant;
                    var affectedConnection = participant == "attacker" ? casterConnection! : targetConnection!;
                    if (removeParticipant is not null)
                    {
                        Assert.True(sessions.TryRemove(affectedConnection, out var removed));
                        Assert.Same(participant == "attacker" ? currentCaster : currentTarget, removed);
                    }
                    else
                    {
                        sessions.Set(affectedConnection, replacement);
                    }
                }
            }
            return ValueTask.CompletedTask;
        }

        casterConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) => Capture(PlayerUid, casterFrames, frame));
        targetConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9959),
            sendAsync: (frame, _) => Capture(TargetPlayerUid, targetFrames, frame));
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        ConquerTcpConnection? observerConnection = null;
        if (observer is not null)
        {
            observerConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9960),
                sendAsync: (frame, _) => Capture(PhysicalPlayerEffectsObserverUid, observerFrames, frame));
            sessions.Set(observerConnection, observer);
        }
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(casterConnection, TextWriter.Synchronized(log), caster,
            WorldData: worldData, Sessions: sessions, MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(), ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 120, Y: 121,
                AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
        var casterRegistered = sessions.TryGet(casterConnection, out var currentCaster);
        var targetRegistered = sessions.TryGet(targetConnection, out var currentTarget);
        if (removeParticipant != "attacker")
        {
            Assert.True(casterRegistered);
            Assert.NotNull(currentCaster);
        }
        if (removeParticipant != "target")
        {
            Assert.True(targetRegistered);
            Assert.NotNull(currentTarget);
        }
        if (observerConnection is not null)
        {
            Assert.True(sessions.TryGet(observerConnection, out observer));
            Assert.NotNull(observer);
        }
        return new PhysicalPlayerEffectsResult(
            currentCaster ?? casterBeforeMutation ?? caster,
            currentTarget ?? targetBeforeMutation ?? target,
            observer, replacement,
            casterFrames.ToArray(), targetFrames.ToArray(), observerFrames.ToArray(), deliveryOrder.ToArray(),
            observations.ToArray(), casterBeforeMutation, targetBeforeMutation, Volatile.Read(ref mutationCount),
            casterRegistered, targetRegistered);
    }

    private static ItemType PhysicalPlayerEffectsCreateHelmet(
        uint itemId, uint criticalStrike, uint immunity, uint breakthrough = 0)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = itemId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        fields[1] = "PhysicalPlayerEffectsNeutralHelmet";
        fields[39] = criticalStrike.ToString(System.Globalization.CultureInfo.InvariantCulture);
        fields[41] = immunity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        fields[44] = breakthrough.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemId), "PhysicalPlayerEffectsNeutralHelmet", fields);
    }

    private static PlayerItemSnapshot PhysicalPlayerEffectsEquipHelmet(uint itemId)
    {
        return PlayerItemSnapshot.Empty with
        {
            EquipmentItems =
            [
                new PlayerItemSnapshotItem
                {
                    ItemId = itemId, Position = 1, Durability = 100, MaximumDurability = 100,
                },
            ],
        };
    }

    private sealed record PhysicalPlayerEffectsRefineObservation(
        uint RecipientUid,
        long TargetHitpoints,
        uint CasterUid,
        uint TargetUid);

    private sealed record PhysicalPlayerEffectsResult(
        WorldSession Caster,
        WorldSession Target,
        WorldSession? Observer,
        WorldSession Replacement,
        byte[][] CasterFrames,
        byte[][] TargetFrames,
        byte[][] ObserverFrames,
        byte[][] DeliveryOrder,
        PhysicalPlayerEffectsRefineObservation[] RefineObservations,
        WorldSession? CasterBeforeMutation,
        WorldSession? TargetBeforeMutation,
        int MutationCount,
        bool CasterRegistered,
        bool TargetRegistered);
}
