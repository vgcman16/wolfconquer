using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerCounterKill16350PhysicalSpellsPassingRollCastsBeforeIncomingAnimation()
    {
        var result = await RunCounterKill16350PhysicalSpellsAttackAsync();

        var proc = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.Equal(TargetPlayerUid, proc.AttackerUid);
        Assert.Equal(0U, proc.AttackedUid);
        Assert.Equal(0, proc.X);
        Assert.Equal(0, proc.Y);
        Assert.Equal(
            [result.Monster.Uid, PlayerUid],
            proc.Targets.Select(target => target.Uid).ToArray());

        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == TripleAttackPhysicalSpellsSpellId);
        Assert.Equal(3, incoming.Targets.Count);
        Assert.All(incoming.Targets, target => Assert.Equal(TargetPlayerUid, target.Uid));

        var procIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == HeavensWonderRuntimeSpellId);
        var incomingIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == TripleAttackPhysicalSpellsSpellId);
        Assert.True(
            procIndex < incomingIndex,
            "Matching Physical.OnPlayer invokes BackDmg before PhysicalSpells sends its three-hit animation.");

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
    public async Task HandlerCounterKill16350PhysicalSpellsStatusEarlyReturnsDoNotProc(
        int targetStatusFlag)
    {
        var result = await RunCounterKill16350PhysicalSpellsAttackAsync(targetStatusFlag);

        Assert.DoesNotContain(
            ParseSpellAnimations(result.AttackerSent.Concat(result.TargetSent)),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
    }

    [Fact]
    public async Task HandlerCounterKill16350PhysicalSpellsWildPhoenixEarlyReturnDoesNotProc()
    {
        var result = await RunCounterKill16350PhysicalSpellsAttackAsync(
            LegacyWildPhoenixFlag,
            wildPhoenixCharges: 3);

        Assert.DoesNotContain(
            ParseSpellAnimations(result.AttackerSent.Concat(result.TargetSent)),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
    }

    [Fact]
    public async Task HandlerCounterKill16350PhysicalSpellsDefensiveStanceRemainsEligible()
    {
        var result = await RunCounterKill16350PhysicalSpellsAttackAsync(DefensiveStanceStatusFlag);

        Assert.Contains(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
    }

    private static async Task<CounterKill16350PhysicalSpellsResult>
        RunCounterKill16350PhysicalSpellsAttackAsync(
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
            CreateTripleAttackPhysicalSpellsRow(),
            CreateCounterKillRuntimeRow(
                HeavensWonderRuntimeSpellId,
                coolDown: 0,
                coldTime: 1_000));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var targetAttributes = new Dictionary<string, string>
        {
            [CounterKill16350BackDamageRollAttribute] = 0.ToString(CultureInfo.InvariantCulture),
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

        var attackerSession = CreateSession(
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellIds: [TripleAttackPhysicalSpellsSpellId],
            legacyRandomSpell: TripleAttackPhysicalSpellsSpellId);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "PhysicalSpellsBackDamageTarget",
            x: 831,
            y: 584,
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellIds: [HeavensWonderRuntimeSpellId],
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

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: TripleAttackPhysicalSpellsSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(attackerConnection, out var finalAttackerSession));
        Assert.NotNull(finalAttackerSession);
        Assert.True(sessions.TryGet(targetConnection, out var finalTargetSession));
        Assert.NotNull(finalTargetSession);
        return new CounterKill16350PhysicalSpellsResult(
            monster,
            monsterStates,
            attackerSent,
            targetSent,
            finalAttackerSession.Character.MinHitPoints,
            finalTargetSession.MagicCooldowns ?? new Dictionary<ushort, DateTimeOffset>(),
            log.ToString());
    }

    private sealed record CounterKill16350PhysicalSpellsResult(
        LegacyMonsterInstance Monster,
        MonsterRuntimeStateStore MonsterStates,
        IReadOnlyList<byte[]> AttackerSent,
        IReadOnlyList<byte[]> TargetSent,
        long AttackerRemainingHitpoints,
        IReadOnlyDictionary<ushort, DateTimeOffset> TargetMagicCooldowns,
        string Output);
}
