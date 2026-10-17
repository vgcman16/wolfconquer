using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort LegacyMsgRefineEffectOpcode = 2_319;
    private const uint LegacyMirrorOfSinRefineEffect = 10;

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerMirrorOfSinSendsRefineEffectOpensXpListAndKeepsDamage()
    {
        var result = await RunPlayerReceiveMirrorOfSinMagicAttackAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PerfectionStatus.MirrorOfSin"] = "100",
                ["LegacyMirrorOfSinRoll"] = "0",
                ["LegacyMirrorOfSinXPListRoll"] = "0",
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);

        AssertMirrorOfSinRefineEffect(result.CasterSent.Concat(result.TargetSent));

        Assert.Contains(
            ParseMsgUpdates(result.CasterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.XPList &&
                    entry.Value == 1));

        Assert.True(HasLegacyStatusFlag(result.CasterSession.Character.Attributes, LegacyXpListFlag));
        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerMirrorOfSinDoesNotRefreshExistingXpList()
    {
        var result = await RunPlayerReceiveMirrorOfSinMagicAttackAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PerfectionStatus.MirrorOfSin"] = "100",
                ["LegacyMirrorOfSinRoll"] = "0",
                ["LegacyMirrorOfSinXPListRoll"] = "0",
                ["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag),
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);

        AssertMirrorOfSinRefineEffect(result.CasterSent.Concat(result.TargetSent));

        Assert.DoesNotContain(
            ParseMsgUpdates(result.CasterSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.XPList);
        Assert.True(HasLegacyStatusFlag(result.CasterSession.Character.Attributes, LegacyXpListFlag));
    }

    private static async Task<PlayerReceiveMirrorOfSinHitResult> RunPlayerReceiveMirrorOfSinMagicAttackAsync(
        IReadOnlyDictionary<string, string> casterAttributes)
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
        var casterSession = CreateSession(attributes: casterAttributes);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584);
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

        return new PlayerReceiveMirrorOfSinHitResult(
            updatedCasterSession,
            updatedTargetSession,
            casterSent,
            targetSent,
            log.ToString());
    }

    private static void AssertMirrorOfSinRefineEffect(IEnumerable<byte[]> frames)
    {
        Assert.Contains(
            ParseLegacyMsgRefineEffects(frames),
            effect => effect.Id == TargetPlayerUid &&
                effect.Param == TargetPlayerUid &&
                effect.Effect == LegacyMirrorOfSinRefineEffect);
    }

    private static void AssertNormalThunderDamage(IReadOnlyList<byte[]> frames)
    {
        var spell = Assert.Single(ParseSpellAnimations(frames));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
    }

    private static List<LegacyMsgRefineEffectCapture> ParseLegacyMsgRefineEffects(IEnumerable<byte[]> frames)
    {
        var effects = new List<LegacyMsgRefineEffectCapture>();
        foreach (var bytes in frames)
        {
            var frame = ReadFrame(bytes);
            if (frame.Header.Opcode != LegacyMsgRefineEffectOpcode)
            {
                continue;
            }

            if (TryParseLegacyMsgRefineEffect(frame.Payload.ToArray(), out var effect))
            {
                effects.Add(effect);
            }
        }

        return effects;
    }

    private static bool TryParseLegacyMsgRefineEffect(
        ReadOnlySpan<byte> payload,
        out LegacyMsgRefineEffectCapture effect)
    {
        uint id = 0;
        uint param = 0;
        uint refineEffect = 0;
        var offset = 0;
        while (offset < payload.Length)
        {
            if (!TryReadProtobufVarint(payload, ref offset, out var tag))
            {
                effect = default;
                return false;
            }

            var field = tag >> 3;
            var wireType = tag & 0x7;
            if (wireType != 0 || !TryReadProtobufVarint(payload, ref offset, out var value))
            {
                effect = default;
                return false;
            }

            if (field == 1)
            {
                id = checked((uint)value);
            }
            else if (field == 2)
            {
                param = checked((uint)value);
            }
            else if (field == 3)
            {
                refineEffect = checked((uint)value);
            }
        }

        effect = new LegacyMsgRefineEffectCapture(id, param, refineEffect);
        return true;
    }

    private static bool TryReadProtobufVarint(ReadOnlySpan<byte> payload, ref int offset, out ulong value)
    {
        value = 0;
        var shift = 0;
        while (offset < payload.Length && shift < 64)
        {
            var current = payload[offset++];
            value |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                return true;
            }

            shift += 7;
        }

        return false;
    }

    private sealed record PlayerReceiveMirrorOfSinHitResult(
        WorldSession CasterSession,
        WorldSession TargetSession,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        string Output);

    private readonly record struct LegacyMsgRefineEffectCapture(
        uint Id,
        uint Param,
        uint Effect);
}
