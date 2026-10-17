using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const long DirectArcherTargetHitpoints = 100_000;

    [Fact]
    public async Task HandlerDirectArcherAgainstVisiblePlayerSendsInteractionBeforeExactHpUpdateAndLogsHandling()
    {
        var result = await RunDirectArcherPlayerAttackAsync();

        var attackerAttack = AssertSingleDirectArcherPlayerAttack(result.AttackerSent);
        var targetAttack = AssertSingleDirectArcherPlayerAttack(result.TargetSent);
        Assert.Equal(attackerAttack.Query.Damage, targetAttack.Query.Damage);
        Assert.True(attackerAttack.Query.Damage > 0);
        Assert.Equal(MsgAttackEffect.None, attackerAttack.Query.Effect);

        var expectedHitpoints = DirectArcherTargetHitpoints - attackerAttack.Query.Damage;
        Assert.Equal(expectedHitpoints, result.TargetSession.Character.MinHitPoints);
        Assert.Contains(
            ParseMsgUpdates(result.TargetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                    entry.Value == checked((uint)expectedHitpoints)));
        Assert.Contains("WorldServer handled legacy direct player Archer", result.Output);

        var interactionIndex = SingleFrameIndex(
            result.TargetSent,
            frame => MsgAttackPacket.TryParse(frame, out var attack) &&
                attack.Query.AttackType == MsgAttackType.Archer &&
                attack.Query.OpponentUid == TargetPlayerUid);
        var hitpointsIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
        Assert.True(
            interactionIndex < hitpointsIndex,
            "MsgAttackPacket.cs sends the ordinary Archer interaction before ReceiveAttack.Player.Execute publishes HP.");
    }

    [Theory]
    [InlineData(LegacyPhysicalSpellsShurikenVortexFlag)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag)]
    public async Task HandlerDirectArcherRangeStatusReturnUsesZeroBaseExtraAttack(
        int targetStatusFlag)
    {
        var result = await RunDirectArcherPlayerAttackAsync(
            targetStatusFlag,
            useRangeStatusDamage: true);

        var attackerAttack = AssertSingleDirectArcherPlayerAttack(result.AttackerSent);
        var targetAttack = AssertSingleDirectArcherPlayerAttack(result.TargetSent);
        Assert.Equal(attackerAttack.Query.Damage, targetAttack.Query.Damage);
        Assert.Equal(checked((int)RangeStatusExpectedExtraAttack), targetAttack.Query.Damage);
        Assert.True(targetAttack.Query.Damage > 1);
        Assert.Equal(MsgAttackEffect.None, targetAttack.Query.Effect);
        Assert.Equal(
            DirectArcherTargetHitpoints - RangeStatusExpectedExtraAttack,
            result.TargetSession.Character.MinHitPoints);
        Assert.Contains(
            ParseMsgUpdates(result.TargetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                    entry.Value == DirectArcherTargetHitpoints - RangeStatusExpectedExtraAttack));
    }

    [Fact]
    public async Task HandlerDirectArcherDefensiveStanceReturnsBlockAfterFortyPercentReduction()
    {
        var baseline = await RunDirectArcherPlayerAttackAsync();
        var defended = await RunDirectArcherPlayerAttackAsync(DefensiveStanceStatusFlag);

        var baselineAttack = AssertSingleDirectArcherPlayerAttack(baseline.TargetSent);
        var defendedAttack = AssertSingleDirectArcherPlayerAttack(defended.TargetSent);
        var expectedDamage = baselineAttack.Query.Damage - (baselineAttack.Query.Damage * 40 / 100);
        Assert.Equal(expectedDamage, defendedAttack.Query.Damage);
        Assert.Equal(MsgAttackEffect.Block, defendedAttack.Query.Effect);
        Assert.Equal(
            DirectArcherTargetHitpoints - expectedDamage,
            defended.TargetSession.Character.MinHitPoints);
    }

    [Fact]
    public async Task HandlerDirectArcherNeptuneCurseSuppressesThePlayerHit()
    {
        var result = await RunDirectArcherPlayerAttackAsync(
            attackerStatusFlag: LegacyNeptuneCurseFlag);

        Assert.DoesNotContain(
            ParseAttacks(result.AttackerSent),
            attack => attack.Query.AttackType == MsgAttackType.Archer);
        Assert.Equal(DirectArcherTargetHitpoints, result.TargetSession.Character.MinHitPoints);
        Assert.Contains("global NeptuneCurse guard before AttackID dispatch", result.Output);
    }

    private static async Task<DirectArcherPlayerAttackResult> RunDirectArcherPlayerAttackAsync(
        int? targetStatusFlag = null,
        bool useRangeStatusDamage = false,
        int? attackerStatusFlag = null)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = useRangeStatusDamage
            ? CreateMagicCatalogWithItems(
                monsterFamily,
                [],
                CreateRangeStatusPhysicalDamageItems())
            : CreateMagicCatalog(monsterFamily, []);
        var sessions = new WorldSessionStore();
        var attackerSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var attackerConnection = CreateSendingConnection(attackerSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var attackerAttributes = attackerStatusFlag is null
            ? null
            : new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(attackerStatusFlag.Value),
            };
        var attackerSession = CreateSession(
            minHitPoints: DirectArcherTargetHitpoints,
            strength: 1_000,
            spellIds: [],
            attributes: attackerAttributes,
            playerItems: useRangeStatusDamage
                ? CreateRangeStatusEquipment(RangeStatusPhysicalIncreaseItemId, position: 10)
                : PlayerItemSnapshot.Empty);
        var targetAttributes = targetStatusFlag is null
            ? null
            : new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(targetStatusFlag.Value),
            };
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "DirectArcherTarget",
            x: 840,
            y: 584,
            minHitPoints: DirectArcherTargetHitpoints,
            strength: 1_000,
            spellIds: [],
            attributes: targetAttributes,
            playerItems: useRangeStatusDamage
                ? CreateRangeStatusEquipment(RangeStatusPhysicalDecreaseItemId, position: 11)
                : PlayerItemSnapshot.Empty);
        sessions.Set(attackerConnection, attackerSession);
        sessions.Set(targetConnection, targetSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                attackerConnection,
                log,
                attackerSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Archer,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        Assert.True(sessions.TryGet(targetConnection, out var finalTargetSession));
        Assert.NotNull(finalTargetSession);
        return new DirectArcherPlayerAttackResult(
            attackerSent,
            targetSent,
            finalTargetSession,
            log.ToString());
    }

    private static MsgAttackPacket AssertSingleDirectArcherPlayerAttack(IEnumerable<byte[]> frames)
    {
        return Assert.Single(
            ParseAttacks(frames),
            attack => attack.Query.AttackType == MsgAttackType.Archer &&
                attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == TargetPlayerUid);
    }

    private sealed record DirectArcherPlayerAttackResult(
        IReadOnlyList<byte[]> AttackerSent,
        IReadOnlyList<byte[]> TargetSent,
        WorldSession TargetSession,
        string Output);
}
