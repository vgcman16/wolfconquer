using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const string CounterKill16350BackDamageRollAttribute =
        "LegacyHeavensWonderBackDamageRoll";

    [Fact]
    public async Task HandlerCounterKill16350BackDamageForcedRollTenCastsFromLearnedDefenderWithSourceQueryShapeAndNoImmediateRetaliation()
    {
        var result = await RunCounterKill16350BackDamagePhysicalAttackAsync(
            learned: true,
            forcedRoll: 10);

        var targetAnimations = ParseSpellAnimations(result.TargetSent)
            .Where(packet => packet.SpellId == HeavensWonderRuntimeSpellId)
            .ToArray();
        var attackerAnimations = ParseSpellAnimations(result.AttackerSent)
            .Where(packet => packet.SpellId == HeavensWonderRuntimeSpellId)
            .ToArray();
        Assert.True(
            targetAnimations.Length == 1,
            $"Expected one defender-side Heaven's Wonder animation. Log={result.Output}");
        Assert.True(
            attackerAnimations.Length == 1,
            $"Expected one attacker-visible Heaven's Wonder animation. Log={result.Output}");

        foreach (var animation in new[]
        {
            targetAnimations[0],
            attackerAnimations[0],
        })
        {
            Assert.Equal(TargetPlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(0, animation.X);
            Assert.Equal(0, animation.Y);
            Assert.Equal(
                [result.Monster.Uid, PlayerUid],
                animation.Targets.Select(target => target.Uid).ToArray());
            Assert.DoesNotContain(animation.Targets, target => target.Uid == TargetPlayerUid);
        }

        var procIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == HeavensWonderRuntimeSpellId);
        var incomingPhysicalIndex = SingleFrameIndex(
            result.TargetSent,
            frame => MsgAttackPacket.TryParse(frame, out var attack) &&
                attack.Query.AttackType == MsgAttackType.Physical &&
                attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == TargetPlayerUid);
        Assert.True(
            procIndex < incomingPhysicalIndex,
            "Matching Physical.OnPlayer invokes BackDmg before the incoming physical interaction is emitted.");

        Assert.True(result.MonsterStates.TryGetVisibleInstance(
            result.Monster,
            DateTimeOffset.UtcNow,
            out var damagedMonster));
        Assert.True(damagedMonster.Hitpoints < result.Monster.MaxLife);
        Assert.True(result.AttackerRemainingHitpoints < 100_000);
        Assert.Contains(HeavensWonderRuntimeSpellId, result.TargetMagicCooldowns.Keys);
        AssertCounterKill16350BackDamageRetaliationRemainsDeferred(result);
    }

    [Theory]
    [InlineData(true, 11)]
    [InlineData(false, 0)]
    public async Task HandlerCounterKill16350BackDamageRequiresLearnedSpellAndPassingElevenPercentRoll(
        bool learned,
        int forcedRoll)
    {
        var result = await RunCounterKill16350BackDamagePhysicalAttackAsync(learned, forcedRoll);

        Assert.DoesNotContain(
            ParseSpellAnimations(result.AttackerSent.Concat(result.TargetSent)),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.True(result.MonsterStates.TryGetVisibleInstance(
            result.Monster,
            DateTimeOffset.UtcNow,
            out var untouchedMonster));
        Assert.Equal(result.Monster.MaxLife, untouchedMonster.Hitpoints);
        AssertCounterKill16350BackDamageRetaliationRemainsDeferred(result);
    }

    [Theory]
    [InlineData(LegacyShurikenVortexFlag)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag)]
    public async Task HandlerCounterKill16350BackDamagePhysicalDefenseEarlyReturnsDoNotProc(
        int targetStatusFlag)
    {
        var result = await RunCounterKill16350BackDamagePhysicalAttackAsync(
            learned: true,
            forcedRoll: 0,
            targetStatusFlag: targetStatusFlag);

        Assert.DoesNotContain(
            ParseSpellAnimations(result.AttackerSent.Concat(result.TargetSent)),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
    }

    [Fact]
    public async Task HandlerCounterKill16350BackDamageWildPhoenixEarlyReturnDoesNotProc()
    {
        var result = await RunCounterKill16350BackDamagePhysicalAttackAsync(
            learned: true,
            forcedRoll: 0,
            targetStatusFlag: LegacyWildPhoenixFlag,
            wildPhoenixCharges: 2);

        Assert.DoesNotContain(
            ParseSpellAnimations(result.AttackerSent.Concat(result.TargetSent)),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
    }

    [Fact]
    public async Task HandlerCounterKill16350BackDamagePhysicalDefensiveStanceStillProcs()
    {
        var result = await RunCounterKill16350BackDamagePhysicalAttackAsync(
            learned: true,
            forcedRoll: 0,
            targetStatusFlag: DefensiveStanceStatusFlag);

        Assert.Contains(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
    }

    private static async Task<CounterKill16350BackDamageResult>
        RunCounterKill16350BackDamagePhysicalAttackAsync(
            bool learned,
            int forcedRoll,
            int? targetStatusFlag = null,
            uint wildPhoenixCharges = 0)
    {
        var monsterAttributes = new Dictionary<string, string>(CreateHighLifeMonsterFamily().Attributes)
        {
            ["attack_range"] = "3",
            ["attack_min"] = "1000",
            ["attack_max"] = "1000",
        };
        var monsterFamily = CreateHighLifeMonsterFamily() with { Attributes = monsterAttributes };
        var spawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 1,
            boundX: 832,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateCounterKillRuntimeRow(
                HeavensWonderRuntimeSpellId,
                coolDown: 0,
                coldTime: 1_000));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        IReadOnlyList<ushort> targetSpellIds = learned ? [HeavensWonderRuntimeSpellId] : [];
        var attackerSession = CreateSession(
            minHitPoints: 100_000,
            strength: 1_000,
            spellIds: []);
        var targetAttributes = new Dictionary<string, string>
        {
            [CounterKill16350BackDamageRollAttribute] =
                forcedRoll.ToString(CultureInfo.InvariantCulture),
        };
        if (targetStatusFlag is not null)
        {
            targetAttributes["StatusFlags"] = BuildStatusFlags(targetStatusFlag.Value);
        }

        if (wildPhoenixCharges > 0)
        {
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007";
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] =
                wildPhoenixCharges.ToString(CultureInfo.InvariantCulture);
        }

        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "CounterKillTarget",
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
        Assert.Equal(
            forcedRoll.ToString(CultureInfo.InvariantCulture),
            targetSession.Character.Attributes[CounterKill16350BackDamageRollAttribute]);
        var resolvedLearnedSpell = LegacyMagicRuntime.TryResolveLearnedSpell(
            targetSession.PlayerSpells,
            worldData,
            HeavensWonderRuntimeSpellId,
            out _,
            out var resolvedSpell);
        Assert.Equal(learned, resolvedLearnedSpell);
        if (resolvedLearnedSpell)
        {
            Assert.Equal(HeavensWonderRuntimeSpellId, resolvedSpell.Id);
        }

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Physical,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        AssertSinglePhysicalAttack(attackerSent, TargetPlayerUid);
        AssertSinglePhysicalAttack(targetSent, TargetPlayerUid);
        Assert.True(sessions.TryGet(attackerConnection, out var finalAttackerSession));
        Assert.NotNull(finalAttackerSession);
        Assert.True(sessions.TryGet(targetConnection, out var finalTargetSession));
        Assert.NotNull(finalTargetSession);
        return new CounterKill16350BackDamageResult(
            monster,
            monsterStates,
            attackerSent,
            targetSent,
            finalAttackerSession.Character.MinHitPoints,
            finalTargetSession.MagicCooldowns ?? new Dictionary<ushort, DateTimeOffset>(),
            log.ToString());
    }

    private static void AssertCounterKill16350BackDamageRetaliationRemainsDeferred(
        CounterKill16350BackDamageResult result)
    {
        var allFrames = result.AttackerSent.Concat(result.TargetSent).ToArray();
        Assert.DoesNotContain(
            allFrames,
            frame => MsgAttackPacket.TryParse(ReadFrame(frame), out var attack) &&
                attack.Query.Uid == result.Monster.Uid);
        Assert.DoesNotContain(
            ParseSpellAnimations(allFrames),
            animation => animation.AttackerUid == result.Monster.Uid);
        Assert.DoesNotContain("WorldServer sent legacy monster retaliation", result.Output);
        Assert.DoesNotContain("WorldServer sent legacy magic monster retaliation", result.Output);
    }

    private sealed record CounterKill16350BackDamageResult(
        LegacyMonsterInstance Monster,
        MonsterRuntimeStateStore MonsterStates,
        IReadOnlyList<byte[]> AttackerSent,
        IReadOnlyList<byte[]> TargetSent,
        long AttackerRemainingHitpoints,
        IReadOnlyDictionary<ushort, DateTimeOffset> TargetMagicCooldowns,
        string Output);
}
