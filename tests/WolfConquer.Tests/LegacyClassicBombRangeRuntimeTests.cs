using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort ClassicBombSnowSpellId = 5_010;
    private const ushort ClassicBombRageSpellId = 7_020;
    private const ushort ClassicBombArrowRainSpellId = 8_030;
    private const uint ClassicBombBoundaryTargetUid = TargetPlayerUid + 80;
    private const byte ClassicBombRange = 5;
    private const long ClassicBombTargetHitpoints = 100_000;

    [Theory]
    [InlineData(ClassicBombSnowSpellId)]
    [InlineData(ClassicBombRageSpellId)]
    public async Task HandlerClassicBombUsesStrictCasterRangeAndPublishesPlayerHpBeforeSharedAnimation(
        ushort spellId)
    {
        var result = await RunClassicBombPlayerAttackAsync(spellId, includeBoundaryTarget: true);

        var animation = Assert.Single(
            ParseSpellAnimations(result.CasterSent),
            candidate => candidate.SpellId == spellId);
        Assert.Equal([TargetPlayerUid], animation.Targets.Select(target => target.Uid).ToArray());
        Assert.Equal(
            ClassicBombTargetHitpoints,
            result.BoundaryTargetSession!.Character.MinHitPoints);

        var hpIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
        var animationIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == spellId);
        Assert.True(
            hpIndex < animationIndex,
            "Bomb.cs calls ReceiveAttack.Player.Execute before sending its one shared animation.");
    }

    [Fact]
    public async Task HandlerClassicBombRangeStatusReturnPrecedesWildPhoenix()
    {
        var result = await RunClassicBombPlayerAttackAsync(
            ClassicBombSnowSpellId,
            targetStatusFlag: LegacyPhysicalSpellsShurikenVortexFlag,
            wildPhoenixCharges: 1,
            useRangeStatusDamage: true);

        var animation = Assert.Single(
            ParseSpellAnimations(result.CasterSent),
            candidate => candidate.SpellId == ClassicBombSnowSpellId);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.Equal(RangeStatusExpectedExtraAttack, target.Damage);
        Assert.Equal(
            ClassicBombTargetHitpoints - RangeStatusExpectedExtraAttack,
            result.TargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            result.TargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
    }

    [Fact]
    public async Task HandlerArrowRainRemovesXpListBeforeSharedAnimation()
    {
        var result = await RunClassicBombPlayerAttackAsync(
            ClassicBombArrowRainSpellId,
            casterHasXpList: true);

        Assert.False(HasLegacyStatusFlag(result.CasterSession.Character.Attributes, LegacyXpListFlag));
        var expectedXpListFrame = LegacyMsgUpdatePacket
            .CreateLegacyRuntimeXPListUpdate(PlayerUid, value: 0)
            .ToFrameBytes();
        var xpListIndex = Assert.Single(
            result.CasterSent
                .Select((frame, index) => (frame, index))
                .Where(candidate => candidate.frame.SequenceEqual(expectedXpListFrame))
                .Select(candidate => candidate.index));
        var expectedStatusFrame = LegacyMsgUpdatePacket
            .CreateLegacyRuntimeStatusFlagVectorUpdate(
                PlayerUid,
                LegacyPlayerDeathState.CreateStatusFlagVector(
                    result.CasterSession.Character.Attributes,
                    LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
            .ToFrameBytes();
        var statusIndex = Assert.Single(
            result.CasterSent
                .Select((frame, index) => (frame, index))
                .Where(candidate => candidate.frame.SequenceEqual(expectedStatusFrame))
                .Select(candidate => candidate.index));
        var animationIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == ClassicBombArrowRainSpellId);
        Assert.True(xpListIndex < statusIndex);
        Assert.True(statusIndex < animationIndex);
    }

    private static async Task<ClassicBombPlayerAttackResult> RunClassicBombPlayerAttackAsync(
        ushort spellId,
        bool includeBoundaryTarget = false,
        int? targetStatusFlag = null,
        uint wildPhoenixCharges = 0,
        bool useRangeStatusDamage = false,
        bool casterHasXpList = false)
    {
        var monsterFamily = CreateMonsterFamily();
        var row = CreateMagicRow(
            spellId,
            sort: (byte)LegacyMagicSort.Bomb,
            useMana: 0,
            damage: 100,
            maxTargets: 0,
            range: ClassicBombRange,
            useStamina: 0,
            coldTime: 0);
        var worldData = useRangeStatusDamage
            ? CreateMagicCatalogWithItems(
                monsterFamily,
                [],
                CreateRangeStatusPhysicalDamageItems(),
                row)
            : CreateMagicCatalog(monsterFamily, [], row);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var boundaryTargetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        ConquerTcpConnection? boundaryTargetConnection = includeBoundaryTarget
            ? CreateSendingConnection(boundaryTargetSent)
            : null;

        var casterAttributes = casterHasXpList
            ? new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag),
            }
            : null;
        var casterSession = CreateSession(
            minHitPoints: ClassicBombTargetHitpoints,
            strength: 1_000,
            spellIds: [spellId],
            attributes: casterAttributes,
            playerItems: useRangeStatusDamage
                ? CreateRangeStatusEquipment(RangeStatusPhysicalIncreaseItemId, position: 10)
                : PlayerItemSnapshot.Empty);

        var targetAttributes = new Dictionary<string, string>();
        if (targetStatusFlag is not null)
        {
            targetAttributes["StatusFlags"] = BuildStatusFlags(targetStatusFlag.Value);
        }

        if (wildPhoenixCharges > 0)
        {
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007";
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] =
                wildPhoenixCharges.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "ClassicBombTarget",
            x: 834,
            y: 584,
            minHitPoints: ClassicBombTargetHitpoints,
            strength: 1_000,
            spellIds: [],
            attributes: targetAttributes.Count == 0 ? null : targetAttributes,
            playerItems: useRangeStatusDamage
                ? CreateRangeStatusEquipment(RangeStatusPhysicalDecreaseItemId, position: 11)
                : PlayerItemSnapshot.Empty);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        if (boundaryTargetConnection is not null)
        {
            sessions.Set(
                boundaryTargetConnection,
                CreateSession(
                    uid: ClassicBombBoundaryTargetUid,
                    name: "ClassicBombBoundary",
                    x: 835,
                    y: 584,
                    minHitPoints: ClassicBombTargetHitpoints,
                    strength: 1_000,
                    spellIds: []));
        }

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
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
                SpellId: spellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        WorldSession? finalBoundaryTarget = null;
        if (boundaryTargetConnection is not null)
        {
            Assert.True(sessions.TryGet(boundaryTargetConnection, out finalBoundaryTarget));
            Assert.NotNull(finalBoundaryTarget);
        }

        return new ClassicBombPlayerAttackResult(
            finalCaster,
            finalTarget,
            finalBoundaryTarget,
            casterSent,
            targetSent,
            boundaryTargetSent,
            log.ToString());
    }

    private sealed record ClassicBombPlayerAttackResult(
        WorldSession CasterSession,
        WorldSession TargetSession,
        WorldSession? BoundaryTargetSession,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        IReadOnlyList<byte[]> BoundaryTargetSent,
        string Output);
}
