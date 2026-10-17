using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerCounterKill16350EagleEyePassingRollCastsBeforeIncomingAnimation()
    {
        var result = await RunCounterKill16350EagleEyeAttackAsync();

        var proc = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.Equal(TargetPlayerUid, proc.AttackerUid);
        Assert.Equal(0U, proc.AttackedUid);
        Assert.Equal((0, 0), (proc.X, proc.Y));
        Assert.Equal(
            [result.Monster.Uid, PlayerUid],
            proc.Targets.Select(target => target.Uid).ToArray());

        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == EagleEyeCastSpellId);
        Assert.Equal(TargetPlayerUid, Assert.Single(incoming.Targets).Uid);

        var procIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == HeavensWonderRuntimeSpellId);
        var incomingIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == EagleEyeCastSpellId);
        Assert.True(
            procIndex < incomingIndex,
            "Matching DirectAttack Physical.OnPlayer invokes BackDmg before EagleEye sends its animation.");

        Assert.True(result.MonsterStates.TryGetVisibleInstance(
            result.Monster,
            DateTimeOffset.UtcNow,
            out var damagedMonster));
        Assert.True(damagedMonster.Hitpoints < result.Monster.MaxLife);
        Assert.True(result.AttackerRemainingHitpoints < 100_000);
        Assert.Contains(HeavensWonderRuntimeSpellId, result.TargetMagicCooldowns.Keys);
    }

    [Theory]
    [InlineData(LegacyPhysicalSpellsShurikenVortexFlag)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag)]
    public async Task HandlerCounterKill16350EagleEyeStatusEarlyReturnsDoNotProcAndScaleToZero(
        int targetStatusFlag)
    {
        var result = await RunCounterKill16350EagleEyeAttackAsync(targetStatusFlag);

        Assert.DoesNotContain(
            ParseSpellAnimations(result.AttackerSent.Concat(result.TargetSent)),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == EagleEyeCastSpellId);
        Assert.Equal(0U, Assert.Single(incoming.Targets).Damage);
        Assert.Equal(100_000, result.TargetRemainingHitpoints);
    }

    [Fact]
    public async Task HandlerCounterKill16350EagleEyeWildPhoenixEarlyReturnDoesNotProcAndScalesToZero()
    {
        var result = await RunCounterKill16350EagleEyeAttackAsync(
            LegacyWildPhoenixFlag,
            wildPhoenixCharges: 1);

        Assert.DoesNotContain(
            ParseSpellAnimations(result.AttackerSent.Concat(result.TargetSent)),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == EagleEyeCastSpellId);
        Assert.Equal(0U, Assert.Single(incoming.Targets).Damage);
        Assert.Equal(100_000, result.TargetRemainingHitpoints);
        Assert.Equal("0", result.RemainingWildPhoenixCharges);
    }

    [Fact]
    public async Task HandlerCounterKill16350EagleEyeDefensiveStanceRemainsEligible()
    {
        var result = await RunCounterKill16350EagleEyeAttackAsync(DefensiveStanceStatusFlag);

        Assert.Contains(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
    }

    private static async Task<CounterKill16350EagleEyeResult>
        RunCounterKill16350EagleEyeAttackAsync(
            int? targetStatusFlag = null,
            uint wildPhoenixCharges = 0)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 1,
            boundX: 832,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateEagleEyeCastRow(),
            CreateCounterKillRuntimeRow(
                HeavensWonderRuntimeSpellId,
                coolDown: 0,
                coldTime: 1_000),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var targetAttributes = new Dictionary<string, string>
        {
            [CounterKill16350BackDamageRollAttribute] = 0.ToString(CultureInfo.InvariantCulture),
        };
        if (targetStatusFlag is not null)
        {
            targetAttributes["StatusFlags"] = BuildStatusFlags(targetStatusFlag.Value);
        }

        IReadOnlyList<ushort> targetSpellIds = [HeavensWonderRuntimeSpellId];
        if (wildPhoenixCharges > 0)
        {
            targetSpellIds = [HeavensWonderRuntimeSpellId, WildPhoenixSpellId];
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007";
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] =
                wildPhoenixCharges.ToString(CultureInfo.InvariantCulture);
        }

        var attackerSession = CreateSession(
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellIds: [EagleEyeCastSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "EagleEyeBackDamageTarget",
            x: 831,
            y: 584,
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellIds: targetSpellIds,
            attributes: targetAttributes);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var attackerSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var attackerConnection = CreateSendingConnection(attackerSent);
        var targetConnection = CreateSendingConnection(targetSent);
        sessions.Set(attackerConnection, attackerSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            attackerConnection,
            log,
            attackerSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: new SobNpcRuntimeStateStore());

        Assert.True(await InvokeLegacyEagleEyeCastAsync(
            context,
            attackerSession,
            CreateEagleEyeQuery(TargetPlayerUid),
            attackerSession.PlayerSpells.Spells[0],
            CreateEagleEyeRuntimeSpell()));

        Assert.True(sessions.TryGet(attackerConnection, out var finalAttackerSession));
        Assert.NotNull(finalAttackerSession);
        Assert.True(sessions.TryGet(targetConnection, out var finalTargetSession));
        Assert.NotNull(finalTargetSession);
        finalTargetSession.Character.Attributes.TryGetValue(
            RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute,
            out var remainingWildPhoenixCharges);
        return new CounterKill16350EagleEyeResult(
            monster,
            monsterStates,
            attackerSent,
            targetSent,
            finalAttackerSession.Character.MinHitPoints,
            finalTargetSession.Character.MinHitPoints,
            finalTargetSession.MagicCooldowns ?? new Dictionary<ushort, DateTimeOffset>(),
            remainingWildPhoenixCharges,
            log.ToString());
    }

    private sealed record CounterKill16350EagleEyeResult(
        LegacyMonsterInstance Monster,
        MonsterRuntimeStateStore MonsterStates,
        IReadOnlyList<byte[]> AttackerSent,
        IReadOnlyList<byte[]> TargetSent,
        long AttackerRemainingHitpoints,
        long TargetRemainingHitpoints,
        IReadOnlyDictionary<ushort, DateTimeOffset> TargetMagicCooldowns,
        string? RemainingWildPhoenixCharges,
        string Output);
}
