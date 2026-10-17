using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const string PlayerReceiveResponseRecoveryEffect = "recovery";
    private const string PlayerReceiveResponseAegisEffect = "Aegis4";
    private const string PlayerReceiveResponseTortoiseAttribute = "SuprtTortoiseGem";
    private const string PlayerReceiveResponseBlessAttribute = "HaveBless";
    private const string PlayerReceiveResponseRecoveryRoll10Attribute = "LegacyPlayerReceiveResponseRecoveryRoll10";
    private const string PlayerReceiveResponseRecoveryRoll5Attribute = "LegacyPlayerReceiveResponseRecoveryRoll5";
    private const string PlayerReceiveResponseAegisRoll10Attribute = "LegacyPlayerReceiveResponseAegisRoll10";
    private const string PlayerReceiveResponseAegisRoll5Attribute = "LegacyPlayerReceiveResponseAegisRoll5";

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerReceiveResponseRecoverySendsTargetEffectBeforeNonlethalDamage()
    {
        var result = await RunPlayerReceiveResponseEffectsMagicAttackAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PlayerReceiveResponseTortoiseAttribute] = "1",
                [PlayerReceiveResponseRecoveryRoll10Attribute] = "0",
                [PlayerReceiveResponseRecoveryRoll5Attribute] = "0",
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);
        AssertTargetPlayerReceiveResponseEffectBeforeNormalThunderDamage(
            result.TargetSent,
            PlayerReceiveResponseRecoveryEffect);
        AssertNoTargetPlayerReceiveResponseEffect(
            result.CasterSent.Concat(result.TargetSent).Concat(result.PeerSent),
            PlayerReceiveResponseAegisEffect);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerReceiveResponseAegisSendsTargetEffectBeforeNonlethalDamage()
    {
        var result = await RunPlayerReceiveResponseEffectsMagicAttackAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PlayerReceiveResponseBlessAttribute] = "1",
                [PlayerReceiveResponseAegisRoll10Attribute] = "0",
                [PlayerReceiveResponseAegisRoll5Attribute] = "0",
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);
        AssertTargetPlayerReceiveResponseEffectBeforeNormalThunderDamage(
            result.TargetSent,
            PlayerReceiveResponseAegisEffect);
        AssertNoTargetPlayerReceiveResponseEffect(
            result.CasterSent.Concat(result.TargetSent).Concat(result.PeerSent),
            PlayerReceiveResponseRecoveryEffect);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerReceiveResponseEffectsSkipOnLethalHit()
    {
        var result = await RunPlayerReceiveResponseEffectsMagicAttackAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PlayerReceiveResponseTortoiseAttribute] = "1",
                [PlayerReceiveResponseBlessAttribute] = "1",
                [PlayerReceiveResponseRecoveryRoll10Attribute] = "0",
                [PlayerReceiveResponseRecoveryRoll5Attribute] = "0",
                [PlayerReceiveResponseAegisRoll10Attribute] = "0",
                [PlayerReceiveResponseAegisRoll5Attribute] = "0",
            },
            checked((int)ExpectedPlayerThunderDamage));

        Assert.Equal(0, result.TargetSession.Character.MinHitPoints);
        AssertNoTargetPlayerReceiveResponseEffect(
            result.CasterSent.Concat(result.TargetSent).Concat(result.PeerSent),
            PlayerReceiveResponseRecoveryEffect);
        AssertNoTargetPlayerReceiveResponseEffect(
            result.CasterSent.Concat(result.TargetSent).Concat(result.PeerSent),
            PlayerReceiveResponseAegisEffect);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerReceiveResponseEffectsSkipWhenEitherRollMisses()
    {
        var result = await RunPlayerReceiveResponseEffectsMagicAttackAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PlayerReceiveResponseTortoiseAttribute] = "1",
                [PlayerReceiveResponseBlessAttribute] = "1",
                [PlayerReceiveResponseRecoveryRoll10Attribute] = "99",
                [PlayerReceiveResponseRecoveryRoll5Attribute] = "0",
                [PlayerReceiveResponseAegisRoll10Attribute] = "0",
                [PlayerReceiveResponseAegisRoll5Attribute] = "99",
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);
        AssertNoTargetPlayerReceiveResponseEffect(
            result.CasterSent.Concat(result.TargetSent).Concat(result.PeerSent),
            PlayerReceiveResponseRecoveryEffect);
        AssertNoTargetPlayerReceiveResponseEffect(
            result.CasterSent.Concat(result.TargetSent).Concat(result.PeerSent),
            PlayerReceiveResponseAegisEffect);
    }

    private static async Task<PlayerReceiveResponseEffectsHitResult> RunPlayerReceiveResponseEffectsMagicAttackAsync(
        IReadOnlyDictionary<string, string> targetAttributes,
        int targetHitpoints = 5_000)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var peerConnection = CreateSendingConnection(peerSent);
        var casterSession = CreateSession();
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            minHitPoints: targetHitpoints,
            attributes: targetAttributes);
        var peerSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "Peer",
            x: 831,
            y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        sessions.Set(peerConnection, peerSession);
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

        return new PlayerReceiveResponseEffectsHitResult(
            updatedCasterSession,
            updatedTargetSession,
            casterSent,
            targetSent,
            peerSent,
            log.ToString());
    }

    private static void AssertTargetPlayerReceiveResponseEffectBeforeNormalThunderDamage(
        IReadOnlyList<byte[]> frames,
        string effectName)
    {
        var effectIndex = SingleFrameIndex(frames, frame =>
            LegacyStringPacket.TryParse(frame, out var packet) &&
            IsTargetPlayerReceiveResponseEffect(packet, effectName));
        var thunderDamageIndex = SingleFrameIndex(frames, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var spell) &&
            spell.AttackerUid == PlayerUid &&
            spell.SpellId == ThunderSpellId &&
            spell.Targets.Count == 1 &&
            spell.Targets[0].Uid == TargetPlayerUid &&
            spell.Targets[0].Damage == ExpectedPlayerThunderDamage &&
            spell.Targets[0].Hit);

        Assert.True(effectIndex < thunderDamageIndex);
    }

    private static void AssertNoTargetPlayerReceiveResponseEffect(IEnumerable<byte[]> frames, string effectName)
    {
        Assert.DoesNotContain(
            ParseLegacyStrings(frames),
            packet => IsTargetPlayerReceiveResponseEffect(packet, effectName));
    }

    private static bool IsTargetPlayerReceiveResponseEffect(LegacyStringPacket packet, string effectName)
    {
        return packet.Uid == TargetPlayerUid &&
            packet.Id == LegacyStringId.Effect &&
            packet.Strings.SequenceEqual([effectName]);
    }

    private sealed record PlayerReceiveResponseEffectsHitResult(
        WorldSession CasterSession,
        WorldSession TargetSession,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        IReadOnlyList<byte[]> PeerSent,
        string Output);
}
