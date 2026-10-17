using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerCounterKill16350MagicOnPlayerPassingRollCastsBeforeIncomingSpellAnimation()
    {
        var result = await RunCounterKill16350MagicOnPlayerAttackAsync();

        AssertCounterKill16350MagicOnPlayerProcAndOrdering(result);
    }

    [Theory]
    [InlineData(LegacyShurikenVortexFlag, false)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag, false)]
    [InlineData(LegacyWildPhoenixFlag, true)]
    [InlineData(AuraMagicDefenderFlag, false)]
    public async Task HandlerCounterKill16350MagicOnPlayerDefenseEarlyReturnsDoNotProc(
        int targetStatusFlag,
        bool configureWildPhoenix)
    {
        var result = await RunCounterKill16350MagicOnPlayerAttackAsync(
            targetStatusFlag,
            configureWildPhoenix);

        Assert.DoesNotContain(
            ParseSpellAnimations(result.CasterSent.Concat(result.TargetSent)),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);

        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == ThunderSpellId);
        var target = Assert.Single(incoming.Targets, target => target.Uid == TargetPlayerUid);
        Assert.Equal(1U, target.Damage);
        Assert.Equal(
            targetStatusFlag == AuraMagicDefenderFlag
                ? MsgAttackEffect.Imunity
                : MsgAttackEffect.None,
            target.Effect);
        if (configureWildPhoenix)
        {
            Assert.Equal("1", result.RemainingWildPhoenixCharges);
        }
    }

    [Fact]
    public async Task HandlerCounterKill16350MagicOnPlayerDefensiveStanceRemainsEligible()
    {
        var result = await RunCounterKill16350MagicOnPlayerAttackAsync(
            DefensiveStanceStatusFlag,
            configureWildPhoenix: false);

        AssertCounterKill16350MagicOnPlayerProcAndOrdering(result);
    }

    private static async Task<CounterKill16350MagicOnPlayerResult>
        RunCounterKill16350MagicOnPlayerAttackAsync(
            int? targetStatusFlag = null,
            bool configureWildPhoenix = false)
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
            CreateMagicRow(
                ThunderSpellId,
                sort: 1,
                useMana: 7,
                damage: 120,
                coolDown: 0,
                coldTime: 0),
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

        if (configureWildPhoenix)
        {
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007";
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2";
        }

        var casterSession = CreateSession(
            minHitPoints: 100_000,
            spellIds: [ThunderSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "MagicBackDamageTarget",
            x: 831,
            y: 584,
            minHitPoints: 100_000,
            stamina: 100,
            spellIds: [HeavensWonderRuntimeSpellId],
            attributes: targetAttributes);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
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
                SpellId: ThunderSpellId)).ToFrameBytes()));

        string? remainingWildPhoenixCharges = null;
        if (sessions.TryGet(targetConnection, out var updatedTargetSession) &&
            updatedTargetSession is not null &&
            updatedTargetSession.Character.Attributes.TryGetValue(
                RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute,
                out var remainingCharges))
        {
            remainingWildPhoenixCharges = remainingCharges;
        }

        return new CounterKill16350MagicOnPlayerResult(
            monster,
            monsterStates,
            casterSent,
            targetSent,
            remainingWildPhoenixCharges,
            log.ToString());
    }

    private static void AssertCounterKill16350MagicOnPlayerProcAndOrdering(
        CounterKill16350MagicOnPlayerResult result)
    {
        var targetAnimations = ParseSpellAnimations(result.TargetSent);
        var proc = Assert.Single(
            targetAnimations,
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.Equal(TargetPlayerUid, proc.AttackerUid);
        Assert.Equal(0U, proc.AttackedUid);
        Assert.Equal(0, proc.X);
        Assert.Equal(0, proc.Y);
        Assert.Equal(
            [result.Monster.Uid, PlayerUid],
            proc.Targets.Select(target => target.Uid).ToArray());

        var procIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == HeavensWonderRuntimeSpellId);
        var incomingMagicIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == ThunderSpellId &&
                animation.AttackerUid == PlayerUid);
        Assert.True(
            procIndex < incomingMagicIndex,
            "Matching Magic.OnPlayer invokes BackDmg before the incoming magic animation is emitted.");

        Assert.True(result.MonsterStates.TryGetVisibleInstance(
            result.Monster,
            DateTimeOffset.UtcNow,
            out var damagedMonster));
        Assert.True(damagedMonster.Hitpoints < result.Monster.MaxLife);
    }

    private sealed record CounterKill16350MagicOnPlayerResult(
        LegacyMonsterInstance Monster,
        MonsterRuntimeStateStore MonsterStates,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        string? RemainingWildPhoenixCharges,
        string Output);
}
