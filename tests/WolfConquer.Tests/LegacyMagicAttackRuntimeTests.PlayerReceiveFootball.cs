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

        // Football returns without mutating the retained SpellObj scalar: (queried301 + raw120)/2.
        AssertFootballSpellAnimationDamage(result.CasterSent, expectedDamage: 210);
        AssertFootballSpellAnimationDamage(result.TargetSent, expectedDamage: 210);
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

        // Passing the ball also skips HP mutation, not the already-calculated animation scalar.
        AssertFootballSpellAnimationDamage(result.CasterSent, expectedDamage: 210);
        AssertFootballSpellAnimationDamage(result.TargetSent, expectedDamage: 210);

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

        // Earn source Status.MagicAttack through the public query. Raw Spirit300 gives301,
        // native base HP900 clamps the raw5000 current HP, and MaxMana1500 does not refill500.
        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgActionPacket { ObjId = PlayerUid, Type = (MsgActionType)251 }.ToFrameBytes()));
        var statusBytes = Assert.Single(casterSent,
            bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(statusBytes), out var status));
        Assert.Equal(PlayerUid, status.Uid);
        Assert.Equal(301U, status.MagicAttack);
        Assert.Equal(900U, status.MaxHP);
        Assert.Equal(1500U, status.MaxMana);
        Assert.True(sessions.TryGet(casterConnection, out var queriedCasterSession));
        Assert.NotNull(queriedCasterSession);
        Assert.Equal(900L, queriedCasterSession.Character.MinHitPoints);
        Assert.Equal(500L, queriedCasterSession.Character.MinMana);
        context = context with { Session = queriedCasterSession };
        casterSent.Clear();
        targetSent.Clear(); // Only positively proved query setup frames; target remains unqueried HP5000.

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
        var animationBytes = Assert.Single(frames,
            bytes => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out _));
        Assert.Equal((ushort)2239, ReadFrame(animationBytes).Header.Opcode);
        var spell = Assert.Single(ParseSpellAnimations(frames));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal((ushort)832, spell.X);
        Assert.Equal((ushort)584, spell.Y);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        Assert.Equal((byte)0, spell.SpellLevel);
        Assert.Equal((byte)0, spell.LevelHu);
        Assert.Equal((ushort)0, spell.NextSpell);
        Assert.Equal((byte)0, spell.SpellEffect);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.DoesNotContain(ParseMsgUpdates(frames), update => update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
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
