using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMagicSupportRuntimeTests
{
    private const uint CasterUid = 1_000;
    private const uint TargetUid = 2_000;

    [Fact]
    public void TryCreateSupportSpellUsesLegacyRecruitDamageNormalization()
    {
        Assert.True(LegacyMagicSupportRuntime.TryCreateSupportSpell(
            CreateMagicRow(rowId: 1_170, typeId: 1_170, sort: 2, name: "Nectar", damage: 5_000),
            out var nectar));
        Assert.Equal(1_170, nectar.Id);
        Assert.Equal((LegacyMagicSort)2, nectar.Sort);
        Assert.Equal(5_000U, nectar.RawDamage);
        Assert.Equal(5_000U, nectar.Power);

        Assert.True(LegacyMagicSupportRuntime.TryCreateSupportSpell(
            CreateMagicRow(rowId: 1_200, typeId: 1_190, sort: 2, name: "SpiritHealing", damage: 25_000),
            out var spiritHealing));
        Assert.Equal(1_190, spiritHealing.Id);
        Assert.Equal(25_000U, spiritHealing.RawDamage);
        Assert.Equal(100U, spiritHealing.Power);

        var info = LegacyMagicSupportRuntime.Classify(spiritHealing);
        Assert.True(info.Supported);
        Assert.Equal(LegacyMagicSupportKind.Healing, info.Kind);
        Assert.Contains("Recruit", info.SourceEvidence);
    }

    [Fact]
    public void CalculateRecruitNormalHealingModelsMonsterOverflowAndTeamRange()
    {
        var spell = Spell(1_170, sort: 2, power: 500, range: 5);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000);
        var monster = Actor(TargetUid, LegacyMagicSupportTargetKind.Monster, x: 101, y: 100, hp: 800, maxHp: 1_000);
        var nearMember = Actor(3_000, LegacyMagicSupportTargetKind.TeamMember, x: 104, y: 100, hp: 750, maxHp: 1_000);
        var edgeMember = Actor(4_000, LegacyMagicSupportTargetKind.TeamMember, x: 105, y: 100, hp: 100, maxHp: 1_000);

        var outcome = LegacyMagicSupportRuntime.CalculateHealing(
            spell,
            caster,
            monster.Uid,
            monster,
            [nearMember, edgeMember]);

        Assert.Equal(LegacyMagicSupportKind.Healing, outcome.Kind);
        Assert.Equal(500U, outcome.SpellExperienceDamage);
        Assert.Equal(2, outcome.Targets.Count);

        var monsterOutcome = Assert.Single(outcome.Targets, target => target.Uid == monster.Uid);
        Assert.Equal(0U, monsterOutcome.HitpointDelta);
        Assert.Equal(800U, monsterOutcome.NewHitpoints);
        Assert.Contains("suppressing exact/full overheal", monsterOutcome.Reason);

        var teamOutcome = Assert.Single(outcome.Targets, target => target.Uid == nearMember.Uid);
        Assert.Equal(250U, teamOutcome.HitpointDelta);
        Assert.Equal(1_000U, teamOutcome.NewHitpoints);
        Assert.DoesNotContain(outcome.Targets, target => target.Uid == edgeMember.Uid);
    }

    [Fact]
    public void CalculateRecruitNormalHealingHealsSuppliedTeamMembersInsideRange()
    {
        var spell = Spell(1_170, sort: 2, power: 300, range: 5);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000);
        var teamMember = Actor(3_000, LegacyMagicSupportTargetKind.TeamMember, x: 104, y: 100, hp: 700, maxHp: 1_000);

        var outcome = LegacyMagicSupportRuntime.CalculateHealing(
            spell,
            caster,
            opponentUid: 0,
            primaryTarget: null,
            [teamMember]);

        var target = Assert.Single(outcome.Targets);
        Assert.Equal(teamMember.Uid, target.Uid);
        Assert.Equal(LegacyMagicSupportTargetKind.TeamMember, target.Kind);
        Assert.Equal(teamMember.Uid, target.AnimationUid);
        Assert.Equal(300U, target.HitpointDelta);
        Assert.Equal(1_000U, target.NewHitpoints);
        Assert.Contains("distance strictly less than Spell.Range", target.Reason);
    }

    [Fact]
    public void CalculateRecruitNormalHealingSkipsSuppliedTeamMembersAtAndOutsideRange()
    {
        var spell = Spell(1_170, sort: 2, power: 300, range: 5);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000);
        var atRange = Actor(3_000, LegacyMagicSupportTargetKind.TeamMember, x: 105, y: 100, hp: 700, maxHp: 1_000);
        var outsideRange = Actor(4_000, LegacyMagicSupportTargetKind.TeamMember, x: 106, y: 100, hp: 700, maxHp: 1_000);

        var outcome = LegacyMagicSupportRuntime.CalculateHealing(
            spell,
            caster,
            opponentUid: 0,
            primaryTarget: null,
            [atRange, outsideRange]);

        Assert.Empty(outcome.Targets);
    }

    [Fact]
    public void CalculateSingleTargetRecruitHealsVisiblePlayerButAnimatesCasterUid()
    {
        var spell = Spell(1_190, sort: 2, power: 100, range: 5);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000);
        var player = Actor(TargetUid, LegacyMagicSupportTargetKind.Player, x: 101, y: 100, hp: 50, maxHp: 200);

        var outcome = LegacyMagicSupportRuntime.CalculateHealing(spell, caster, player.Uid, player);

        var target = Assert.Single(outcome.Targets);
        Assert.Equal(player.Uid, target.Uid);
        Assert.Equal(caster.Uid, target.AnimationUid);
        Assert.Equal(100U, target.HitpointDelta);
        Assert.Equal(150U, target.NewHitpoints);
        Assert.Contains("caster UID", target.Reason);
    }

    [Theory]
    [InlineData(1_005)]
    [InlineData(1_190)]
    public void CalculateSingleTargetRecruitHealingDoesNotFanOutToSuppliedTeamMembers(int spellId)
    {
        var spell = Spell((ushort)spellId, sort: 2, power: 100, range: 5);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000);
        var player = Actor(TargetUid, LegacyMagicSupportTargetKind.Player, x: 101, y: 100, hp: 50, maxHp: 200);
        var teamMember = Actor(3_000, LegacyMagicSupportTargetKind.TeamMember, x: 102, y: 100, hp: 200, maxHp: 1_000);

        var outcome = LegacyMagicSupportRuntime.CalculateHealing(
            spell,
            caster,
            player.Uid,
            player,
            [teamMember]);

        var target = Assert.Single(outcome.Targets);
        Assert.Equal(player.Uid, target.Uid);
        Assert.DoesNotContain(outcome.Targets, result => result.Uid == teamMember.Uid);
        Assert.Contains("single-target healing branch", outcome.Reason);
    }

    [Fact]
    public void CalculateReviveSupportRevivesTargetAndConsumesXpListForRevive()
    {
        var spell = Spell(1_050, sort: 7, power: 100, duration: 250);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000);
        var deadTarget = Actor(TargetUid, LegacyMagicSupportTargetKind.Player, x: 101, y: 100, hp: 0, maxHp: 5_000, stamina: 12, xpCount: 88);

        var outcome = LegacyMagicSupportRuntime.CalculateReviveSupport(spell, caster, deadTarget);

        Assert.Equal(LegacyMagicSupportKind.Revive, outcome.Kind);
        Assert.True(outcome.Supported);
        Assert.True(outcome.ConsumesXpList);
        Assert.Equal(250U, outcome.SpellExperienceDamage);
        Assert.Contains("Revive removes XPList", outcome.Reason);
        var target = Assert.Single(outcome.Targets);
        Assert.Equal(deadTarget.Uid, target.Uid);
        Assert.Equal(LegacyMagicSupportTargetKind.Player, target.Kind);
        Assert.Equal(deadTarget.Uid, target.AnimationUid);
        Assert.True(target.Revived);
        Assert.Equal(5_000U, target.HitpointDelta);
        Assert.Equal(5_000U, target.NewHitpoints);
        Assert.Equal(88, target.StaminaDelta);
        Assert.Equal(100, target.NewStamina);
        Assert.Equal(0, target.NewXpCount);
        Assert.Contains("Revive/Pray visible-player branch", target.Reason);
        Assert.Contains("Player.Revive", Assert.Single(outcome.PendingRuntimeEffects, value => value.Contains("Player.Revive")));
    }

    [Fact]
    public void CalculateReviveSupportPrayRevivesVisiblePlayerWithoutConsumingXpList()
    {
        var spell = Spell(1_100, sort: 7, power: 100, duration: 125);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000);
        var deadTarget = Actor(TargetUid, LegacyMagicSupportTargetKind.Player, x: 101, y: 100, hp: 0, maxHp: 5_000, stamina: 12, xpCount: 88);

        var outcome = LegacyMagicSupportRuntime.CalculateReviveSupport(spell, caster, deadTarget);

        Assert.Equal(LegacyMagicSupportKind.Revive, outcome.Kind);
        Assert.False(outcome.ConsumesXpList);
        Assert.Equal(125U, outcome.SpellExperienceDamage);
        Assert.Contains("Pray revives", outcome.Reason);
        var target = Assert.Single(outcome.Targets);
        Assert.Equal(deadTarget.Uid, target.Uid);
        Assert.Equal(deadTarget.Uid, target.AnimationUid);
        Assert.True(target.Revived);
        Assert.Equal(5_000U, target.NewHitpoints);
        Assert.Equal(100, target.NewStamina);
        Assert.Equal(0, target.NewXpCount);
    }

    [Fact]
    public void CalculateReviveSupportWatcherMapBlocksTargetsAndSpellExperience()
    {
        var spell = Spell(1_100, sort: 7, power: 100, duration: 125);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000);
        var deadTarget = Actor(TargetUid, LegacyMagicSupportTargetKind.Player, x: 101, y: 100, hp: 0, maxHp: 5_000);

        var outcome = LegacyMagicSupportRuntime.CalculateReviveSupport(
            spell,
            caster,
            deadTarget,
            casterIsWatching: true);

        Assert.Equal(LegacyMagicSupportKind.Revive, outcome.Kind);
        Assert.Empty(outcome.Targets);
        Assert.Equal(0U, outcome.SpellExperienceDamage);
        Assert.False(outcome.ConsumesXpList);
        Assert.False(outcome.HasObservableEffect);
        Assert.Contains("watcher-only maps", outcome.Reason);
    }

    [Fact]
    public void CalculateMercyPrayerBlocksSoulShackleBeforeAnimation()
    {
        var spell = Spell(16_610, sort: 7, power: 100);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000);
        var shackledTarget = Actor(
            TargetUid,
            LegacyMagicSupportTargetKind.TeamMember,
            x: 101,
            y: 100,
            hp: 0,
            maxHp: 5_000,
            hasSoulShackle: true,
            isTeamMember: true);

        var outcome = LegacyMagicSupportRuntime.CalculateReviveSupport(
            spell,
            caster,
            shackledTarget,
            mercyPrayerWeaponEligible: true,
            casterHasTeam: true);

        Assert.Empty(outcome.Targets);
        Assert.Equal(0U, outcome.SpellExperienceDamage);
        Assert.Contains("SoulShackle", outcome.Reason);
    }

    [Theory]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    public void CalculateMercyPrayerRevivesTeamOrGuildEligibleTargets(
        bool isTeamMember,
        bool isGuildMember,
        bool casterHasTeam,
        bool targetHasTeam)
    {
        var spell = Spell(16_610, sort: 7, power: 100, duration: 175);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000);
        var deadTarget = Actor(
            TargetUid,
            isTeamMember ? LegacyMagicSupportTargetKind.TeamMember : LegacyMagicSupportTargetKind.Player,
            x: 101,
            y: 100,
            hp: 0,
            maxHp: 5_000,
            stamina: 12,
            xpCount: 88,
            isTeamMember: isTeamMember,
            isGuildMember: isGuildMember);

        var outcome = LegacyMagicSupportRuntime.CalculateReviveSupport(
            spell,
            caster,
            deadTarget,
            mercyPrayerWeaponEligible: true,
            casterHasTeam: casterHasTeam,
            targetHasTeam: targetHasTeam);

        Assert.Equal(LegacyMagicSupportKind.Revive, outcome.Kind);
        Assert.False(outcome.ConsumesXpList);
        Assert.Equal(175U, outcome.SpellExperienceDamage);
        Assert.Contains("team member or guild member", outcome.Reason);
        var target = Assert.Single(outcome.Targets);
        Assert.Equal(deadTarget.Uid, target.Uid);
        Assert.True(target.Revived);
        Assert.Equal(5_000U, target.NewHitpoints);
        Assert.Equal(100, target.NewStamina);
        Assert.Equal(0, target.NewXpCount);
    }

    [Fact]
    public void CalculateMercyPrayerBlocksNonEligibleTargetsWithoutTargetOutcomes()
    {
        var spell = Spell(16_610, sort: 7, power: 100, duration: 175);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000);
        var stranger = Actor(TargetUid, LegacyMagicSupportTargetKind.Player, x: 101, y: 100, hp: 0, maxHp: 5_000);
        var isolatedTeamMember = Actor(
            3_000,
            LegacyMagicSupportTargetKind.TeamMember,
            x: 102,
            y: 100,
            hp: 0,
            maxHp: 5_000,
            isTeamMember: true);

        var strangerOutcome = LegacyMagicSupportRuntime.CalculateReviveSupport(
            spell,
            caster,
            stranger,
            mercyPrayerWeaponEligible: true,
            casterHasTeam: true,
            targetHasTeam: true);
        var isolatedOutcome = LegacyMagicSupportRuntime.CalculateReviveSupport(
            spell,
            caster,
            isolatedTeamMember,
            mercyPrayerWeaponEligible: true);

        Assert.Empty(strangerOutcome.Targets);
        Assert.Equal(175U, strangerOutcome.SpellExperienceDamage);
        Assert.Contains("no target is revived without team or guild eligibility", strangerOutcome.Reason);
        Assert.Empty(isolatedOutcome.Targets);
        Assert.Equal(175U, isolatedOutcome.SpellExperienceDamage);
        Assert.Contains("no target is revived without team or guild eligibility", isolatedOutcome.Reason);
    }

    [Fact]
    public void CalculateDetachStatusModelsSuccessAndMissWithDeterministicRoll()
    {
        var spell = Spell(6_004, sort: 1, power: 100, rate: 40);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000, battlePower: 100);
        var target = Actor(
            TargetUid,
            LegacyMagicSupportTargetKind.Player,
            x: 101,
            y: 100,
            hp: 1_000,
            maxHp: 5_000,
            hasFly: true,
            hasHeavensWrath: true,
            battlePower: 120);

        var success = LegacyMagicSupportRuntime.CalculateDetachStatus(spell, caster, target, rateRoll: 19);
        var successTarget = Assert.Single(success.Targets);
        Assert.True(successTarget.Hit);
        Assert.True(successTarget.RemovedFly);
        Assert.Equal(100U, successTarget.Damage);
        Assert.Equal(900U, successTarget.NewHitpoints);
        Assert.Equal(250U, success.SpellExperienceDamage);

        var miss = LegacyMagicSupportRuntime.CalculateDetachStatus(spell, caster, target, rateRoll: 20);
        var missTarget = Assert.Single(miss.Targets);
        Assert.False(missTarget.Hit);
        Assert.False(missTarget.RemovedFly);
        Assert.Equal(6_004U, missTarget.Damage);
        Assert.Equal(1_000U, missTarget.NewHitpoints);
    }

    [Fact]
    public void CalculateDragonRoarRestoresTeamStaminaWithSourceCaps()
    {
        var spell = Spell(12_280, sort: 11, power: 20, maxTargets: 12);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000);
        var normal = Actor(3_000, LegacyMagicSupportTargetKind.TeamMember, x: 112, y: 100, hp: 1_000, maxHp: 1_000, stamina: 90);
        var blessed = Actor(4_000, LegacyMagicSupportTargetKind.TeamMember, x: 100, y: 111, hp: 1_000, maxHp: 1_000, stamina: 140, hasHeavenBlessing: true);
        var far = Actor(5_000, LegacyMagicSupportTargetKind.TeamMember, x: 113, y: 100, hp: 1_000, maxHp: 1_000, stamina: 0);
        var casterMirror = Actor(CasterUid, LegacyMagicSupportTargetKind.TeamMember, x: 100, y: 100, hp: 1_000, maxHp: 1_000, stamina: 50);

        var outcome = LegacyMagicSupportRuntime.CalculateDispatchXp(
            spell,
            caster,
            opponentUid: 0,
            primaryTarget: null,
            [normal, blessed, far, casterMirror],
            casterHadXpList: false,
            casterHasTeam: true);

        Assert.Equal(LegacyMagicSupportKind.DragonRoarStamina, outcome.Kind);
        Assert.True(outcome.Supported);
        Assert.False(outcome.ConsumesXpList);
        Assert.Equal(200U, outcome.SpellExperienceDamage);
        Assert.Equal(2, outcome.Targets.Count);
        var normalOutcome = Assert.Single(outcome.Targets, target => target.Uid == normal.Uid);
        Assert.Equal(LegacyMagicSupportTargetKind.TeamMember, normalOutcome.Kind);
        Assert.Equal(10, normalOutcome.StaminaDelta);
        Assert.Equal(100, normalOutcome.NewStamina);
        Assert.Contains("distance <= Spell.MaxTargets", normalOutcome.Reason);
        var blessedOutcome = Assert.Single(outcome.Targets, target => target.Uid == blessed.Uid);
        Assert.Equal(10, blessedOutcome.StaminaDelta);
        Assert.Equal(150, blessedOutcome.NewStamina);
        Assert.DoesNotContain(outcome.Targets, target => target.Uid == far.Uid);
        Assert.DoesNotContain(outcome.Targets, target => target.Uid == caster.Uid);
    }

    [Fact]
    public void CalculateGenericDispatchXpBlocksBeforeObservableEffectsWhenXpListIsAbsent()
    {
        var spell = Spell(1_040, sort: 11, power: 100);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000, xpCount: 10);
        var target = Actor(TargetUid, LegacyMagicSupportTargetKind.Player, x: 101, y: 100, hp: 1_000, maxHp: 1_000, xpCount: 50);
        var teamMember = Actor(3_000, LegacyMagicSupportTargetKind.TeamMember, x: 117, y: 100, hp: 1_000, maxHp: 1_000, xpCount: 90);

        var outcome = LegacyMagicSupportRuntime.CalculateDispatchXp(
            spell,
            caster,
            target.Uid,
            target,
            [teamMember],
            casterHadXpList: false,
            casterHasTeam: true);

        Assert.Equal(LegacyMagicSupportKind.DispatchXp, outcome.Kind);
        Assert.True(outcome.Supported);
        Assert.False(outcome.ConsumesXpList);
        Assert.False(outcome.HasObservableEffect);
        Assert.Empty(outcome.Targets);
        Assert.Empty(outcome.Summons);
        Assert.Equal(0, outcome.RemovedCloneCount);
        Assert.Equal(0U, outcome.SpellExperienceDamage);
        Assert.Contains("returns before animation", outcome.Reason);
        Assert.Contains("RemoveFlag(XPList) fails", outcome.Reason);
        Assert.Contains("consume the caster XPList flag", Assert.Single(outcome.PendingRuntimeEffects));
    }

    [Fact]
    public void CalculateGenericDispatchXpConsumesXpListAndAddsXpCountToCasterVisiblePlayerAndTeam()
    {
        var spell = Spell(1_040, sort: 11, power: 100);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000, xpCount: 10);
        var target = Actor(TargetUid, LegacyMagicSupportTargetKind.Player, x: 101, y: 100, hp: 1_000, maxHp: 1_000, xpCount: 50);
        var teamMember = Actor(3_000, LegacyMagicSupportTargetKind.TeamMember, x: 117, y: 100, hp: 1_000, maxHp: 1_000, xpCount: 90);
        var edgeMember = Actor(4_000, LegacyMagicSupportTargetKind.TeamMember, x: 118, y: 100, hp: 1_000, maxHp: 1_000, xpCount: 90);
        var casterMirror = Actor(CasterUid, LegacyMagicSupportTargetKind.TeamMember, x: 100, y: 100, hp: 1_000, maxHp: 1_000, xpCount: 10);

        var selfOutcome = LegacyMagicSupportRuntime.CalculateDispatchXp(
            spell,
            caster,
            caster.Uid,
            primaryTarget: null,
            casterHadXpList: true);

        Assert.True(selfOutcome.ConsumesXpList);
        Assert.Equal(200U, selfOutcome.SpellExperienceDamage);
        var selfTarget = Assert.Single(selfOutcome.Targets);
        Assert.Equal(caster.Uid, selfTarget.Uid);
        Assert.Equal(LegacyMagicSupportTargetKind.Caster, selfTarget.Kind);
        Assert.Equal(20, selfTarget.XpCountDelta);
        Assert.Equal(30, selfTarget.NewXpCount);
        Assert.Contains("self branch", selfTarget.Reason);

        var outcome = LegacyMagicSupportRuntime.CalculateDispatchXp(
            spell,
            caster,
            target.Uid,
            target,
            [teamMember, edgeMember, casterMirror],
            casterHadXpList: true,
            casterHasTeam: true);

        Assert.Equal(LegacyMagicSupportKind.DispatchXp, outcome.Kind);
        Assert.True(outcome.Supported);
        Assert.True(outcome.ConsumesXpList);
        Assert.True(outcome.HasObservableEffect);
        Assert.Equal(200U, outcome.SpellExperienceDamage);
        Assert.Equal(2, outcome.Targets.Count);
        var playerOutcome = Assert.Single(outcome.Targets, result => result.Uid == target.Uid);
        Assert.Equal(LegacyMagicSupportTargetKind.Player, playerOutcome.Kind);
        Assert.Equal(target.Uid, playerOutcome.AnimationUid);
        Assert.Equal(20, playerOutcome.XpCountDelta);
        Assert.Equal(70, playerOutcome.NewXpCount);
        Assert.Contains("visible-player branch", playerOutcome.Reason);
        var teamOutcome = Assert.Single(outcome.Targets, result => result.Uid == teamMember.Uid);
        Assert.Equal(LegacyMagicSupportTargetKind.TeamMember, teamOutcome.Kind);
        Assert.Equal(20, teamOutcome.XpCountDelta);
        Assert.Equal(110, teamOutcome.NewXpCount);
        Assert.Contains("distance strictly less than 18", teamOutcome.Reason);
        Assert.DoesNotContain(outcome.Targets, result => result.Uid == edgeMember.Uid);
        Assert.DoesNotContain(outcome.Targets, result => result.Uid == caster.Uid);
    }

    [Theory]
    [InlineData(false, false, true, 200, 1)]
    [InlineData(false, true, false, 200, 1)]
    [InlineData(false, false, false, 0, 0)]
    [InlineData(true, false, false, 200, 0)]
    public void CalculateGenericDispatchXpSpellExperienceFollowsTargetAndTeamRules(
        bool casterHasTeam,
        bool selfCast,
        bool visibleTarget,
        int expectedDamage,
        int expectedTargets)
    {
        var spell = Spell(1_040, sort: 11, power: 100);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000, xpCount: 10);
        var target = Actor(TargetUid, LegacyMagicSupportTargetKind.Player, x: 101, y: 100, hp: 1_000, maxHp: 1_000, xpCount: 50);
        var opponentUid = selfCast ? caster.Uid : visibleTarget ? target.Uid : 0;
        LegacyMagicSupportActor? primaryTarget = visibleTarget ? target : null;

        var outcome = LegacyMagicSupportRuntime.CalculateDispatchXp(
            spell,
            caster,
            opponentUid,
            primaryTarget,
            casterHadXpList: true,
            casterHasTeam: casterHasTeam);

        Assert.Equal((uint)expectedDamage, outcome.SpellExperienceDamage);
        Assert.Equal(expectedTargets, outcome.Targets.Count);
        Assert.True(outcome.ConsumesXpList);
    }

    [Fact]
    public void CalculateDragonRoarSpellExperienceFollowsTeamContextWithoutXpList()
    {
        var spell = Spell(12_280, sort: 11, power: 20, maxTargets: 12);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000);

        var noTeam = LegacyMagicSupportRuntime.CalculateDispatchXp(
            spell,
            caster,
            opponentUid: 0,
            primaryTarget: null,
            casterHadXpList: false,
            casterHasTeam: false);

        Assert.False(noTeam.ConsumesXpList);
        Assert.False(noTeam.HasObservableEffect);
        Assert.Equal(0U, noTeam.SpellExperienceDamage);

        var emptyTeam = LegacyMagicSupportRuntime.CalculateDispatchXp(
            spell,
            caster,
            opponentUid: 0,
            primaryTarget: null,
            casterHadXpList: false,
            casterHasTeam: true);

        Assert.False(emptyTeam.ConsumesXpList);
        Assert.False(emptyTeam.HasObservableEffect);
        Assert.Equal(200U, emptyTeam.SpellExperienceDamage);
    }

    [Fact]
    public void CalculateCallPetRemovesExistingClonesBeforeVerification()
    {
        var spell = Spell(12_090, sort: 23, power: 100);
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000);

        var outcome = LegacyMagicSupportRuntime.CalculateCallPet(spell, caster, castX: 100, castY: 100, existingCloneCount: 3);

        Assert.Equal(LegacyMagicSupportKind.RemoveExistingSummons, outcome.Kind);
        Assert.Equal(3, outcome.RemovedCloneCount);
        Assert.Empty(outcome.Summons);
        Assert.Equal(0U, outcome.SpellExperienceDamage);
        Assert.Contains("before CanUseSpell", outcome.Reason);
    }

    [Fact]
    public void CalculateCallPetCreatesShadowCloneAndThundercloudDescriptors()
    {
        var caster = Actor(CasterUid, LegacyMagicSupportTargetKind.Caster, x: 100, y: 100, hp: 1_000, maxHp: 1_000);

        var shadowClone = LegacyMagicSupportRuntime.CalculateCallPet(
            Spell(12_090, sort: 23, power: 570, level: 4),
            caster,
            castX: 100,
            castY: 100,
            existingCloneCount: 0);
        var cloneSummon = Assert.Single(shadowClone.Summons);
        Assert.Equal(LegacyMagicSupportKind.ShadowClone, cloneSummon.Kind);
        Assert.Equal([3, 10_003], cloneSummon.CloneFlags);
        Assert.Equal(1_000U, shadowClone.SpellExperienceDamage);

        var thundercloud = LegacyMagicSupportRuntime.CalculateCallPet(
            Spell(12_840, sort: 23, power: 264, level: 5),
            caster,
            castX: 120,
            castY: 121,
            existingCloneCount: 0,
            doubleThunderRuneEquipped: true);
        var cloudSummon = Assert.Single(thundercloud.Summons);
        Assert.Equal(LegacyMagicSupportKind.Thundercloud, cloudSummon.Kind);
        Assert.Equal(2, cloudSummon.Count);
        Assert.Equal(9U, cloudSummon.PetFlag);
        Assert.Equal(999_996_641U, cloudSummon.MonsterFamilyId);
        Assert.Equal(3_843U, cloudSummon.FloorItemId);
        Assert.Equal(13_190U, cloudSummon.AttackSpellId);
        Assert.Equal(
            LegacyMagicSupportKind.Thundercloud,
            LegacyMagicSupportRuntime.Classify(Spell(12_840, sort: 23, power: 264)).Kind);
        Assert.Contains("MonsterFamily copy", Assert.Single(thundercloud.PendingRuntimeEffects, value => value.Contains("MonsterFamily copy")));
    }

    private static LegacyMagicSupportSpell Spell(
        ushort id,
        int sort,
        uint power,
        byte level = 0,
        int rate = 100,
        uint duration = 0,
        byte maxTargets = 0,
        byte range = 0)
    {
        return new LegacyMagicSupportSpell(
            id,
            (LegacyMagicSort)sort,
            $"Spell{id}",
            level,
            UseMana: 0,
            RawDamage: power,
            power,
            rate,
            duration,
            Experience: 0,
            maxTargets,
            range,
            Status: 0,
            NeedLevel: 0,
            WeaponType: 0,
            UseStamina: 0,
            CoolDown: 0,
            ColdTime: 0);
    }

    private static LegacyMagicSupportActor Actor(
        uint uid,
        LegacyMagicSupportTargetKind kind,
        ushort x,
        ushort y,
        uint hp,
        uint maxHp,
        ushort stamina = 0,
        ushort xpCount = 0,
        bool hasHeavenBlessing = false,
        bool hasSoulShackle = false,
        bool hasFly = false,
        bool hasHeavensWrath = false,
        bool hasInfinity = false,
        bool keepsXpCountOnRevive = false,
        int battlePower = 0,
        bool isTeamMember = false,
        bool isGuildMember = false)
    {
        return new LegacyMagicSupportActor(
            uid,
            kind,
            x,
            y,
            hp,
            maxHp,
            stamina,
            xpCount,
            hasHeavenBlessing,
            hasSoulShackle,
            hasFly,
            hasHeavensWrath,
            hasInfinity,
            keepsXpCountOnRevive,
            battlePower,
            isTeamMember,
            isGuildMember);
    }

    private static MagicType CreateMagicRow(
        int rowId,
        ushort typeId,
        byte sort,
        string name,
        byte level = 0,
        ushort useMana = 0,
        uint damage = 0,
        int rate = 100,
        uint duration = 0,
        byte maxTargets = 0,
        byte range = 0,
        int status = 0,
        uint experience = 0,
        ushort needLevel = 0,
        uint weaponType = 0,
        byte useStamina = 0,
        ushort coolDown = 0,
        int coldTime = 0)
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = rowId.ToString();
        fields[1] = typeId.ToString();
        fields[2] = sort.ToString();
        fields[3] = name;
        fields[8] = level.ToString();
        fields[9] = useMana.ToString();
        fields[10] = damage.ToString();
        fields[12] = rate.ToString();
        fields[13] = duration.ToString();
        fields[14] = maxTargets.ToString();
        fields[15] = range.ToString();
        fields[16] = status.ToString();
        fields[18] = experience.ToString();
        fields[20] = needLevel.ToString();
        fields[22] = weaponType.ToString();
        fields[29] = useStamina.ToString();
        fields[33] = coolDown.ToString();
        fields[47] = coldTime.ToString();
        return new MagicType(rowId, typeId, name, fields);
    }
}
