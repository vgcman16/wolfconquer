using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int LegacyFootballBallFlag = 0xAB;
    private const long LegacyFootballMapId = 3_844;

    [Fact]
    public async Task PlayerReceiveFootballAttackerInTournamentWithoutBallSkipsDamageAndDoesNotTransferBall()
    {
        var result = await RunPlayerReceiveFootballMagicAttackAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal(5_000, result.TargetSession.Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(result.CasterSession.Character.Attributes, LegacyFootballBallFlag));
        Assert.False(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, LegacyFootballBallFlag));

        AssertFootballSpellAnimationDamage(result.CasterSent, expectedDamage: 0);
        AssertFootballSpellAnimationDamage(result.TargetSent, expectedDamage: 0);
    }

    [Fact]
    public async Task PlayerReceiveFootballAttackerInTournamentTransfersBallAndSkipsDamage()
    {
        var result = await RunPlayerReceiveFootballMagicAttackAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyFootballBallFlag),
            });

        Assert.Equal(5_000, result.TargetSession.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(result.CasterSession.Character.Attributes, LegacyFootballBallFlag));
        Assert.False(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, LegacyFootballBallFlag));

        AssertFootballSpellAnimationDamage(result.CasterSent, expectedDamage: 0);
        AssertFootballSpellAnimationDamage(result.TargetSent, expectedDamage: 0);

        var updates = ParseMsgUpdates(result.CasterSent.Concat(result.TargetSent));
        AssertStatusFlagUpdate(updates, PlayerUid, LegacyFootballBallFlag, expectedSet: true);
        AssertStatusFlagUpdate(updates, TargetPlayerUid, LegacyFootballBallFlag, expectedSet: false);
    }

    private static async Task<PlayerReceiveFootballHitResult> RunPlayerReceiveFootballMagicAttackAsync(
        IReadOnlyDictionary<string, string> targetAttributes)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: LegacyFootballMapId,
            attributes: CreatePlayerReceiveFootballCasterAttributes());
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: targetAttributes,
            mapId: LegacyFootballMapId);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

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

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);

        return new PlayerReceiveFootballHitResult(
            updatedCasterSession,
            updatedTargetSession,
            casterSent,
            targetSent,
            log.ToString());
    }

    private static Dictionary<string, string> CreatePlayerReceiveFootballCasterAttributes()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyTournamentInTournament"] = "1",
            ["LegacyTournamentType"] = "FootBall",
            ["LegacyTournamentProcess"] = "Alive",
        };
    }

    private static void AssertFootballSpellAnimationDamage(
        IReadOnlyList<byte[]> frames,
        uint expectedDamage)
    {
        var spell = Assert.Single(ParseSpellAnimations(frames));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);
    }

    private static void AssertStatusFlagUpdate(
        IEnumerable<LegacyMsgUpdatePacket> updates,
        uint uid,
        int statusFlag,
        bool expectedSet)
    {
        Assert.Contains(updates, update =>
            update.Uid == uid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, statusFlag) == expectedSet));
    }

    private sealed record PlayerReceiveFootballHitResult(
        WorldSession CasterSession,
        WorldSession TargetSession,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        string Output);
}
