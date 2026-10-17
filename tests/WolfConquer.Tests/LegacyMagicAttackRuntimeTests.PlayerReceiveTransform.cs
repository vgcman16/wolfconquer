using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int PlayerReceiveTransformMeshField = 1;
    private const int PlayerReceiveTransformUidField = 2;
    private const long PlayerReceiveTransformId = 2;
    private const ulong PlayerReceiveTransformClearedMesh = 1_003;

    [Fact]
    public async Task PlayerReceiveTransformLethalThunderFinishesCasterTransformBeforeTargetDeath()
    {
        var result = await RunPlayerReceiveTransformThunderAttackAsync(
            targetHitpoints: checked((int)ExpectedPlayerThunderDamage));

        Assert.Equal(0, result.TargetSession.Character.MinHitPoints);
        Assert.Equal("0", AssertCharacterAttribute(result.CasterSession, "TransformationID"));
        Assert.Equal("0", AssertCharacterAttribute(result.CasterSession, "TransformationId"));
        Assert.False(result.CasterSession.Character.Attributes.ContainsKey(
            LegacyTransformState.ActiveAttribute));
        Assert.False(result.CasterSession.Character.Attributes.ContainsKey(
            LegacyTransformState.ExpiresAtUtcAttribute));
        Assert.False(result.CasterSession.Character.Attributes.ContainsKey(
            LegacyTransformState.MaxHitpointsAttribute));

        Assert.Contains(
            ParseMsgActions(result.CasterSent),
            action => action.Type == (MsgActionType)118 &&
                action.ObjId == PlayerUid);

        var equipmentIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacyShowEquipmentPacket.TryParse(frame, out var equipment) &&
                equipment.Action == LegacyShowEquipmentPacket.ShowAction);
        // The admitted source Finish query has AddRune+=0, clamp, and
        // final explicit pairs. Raw normal HP is Spirit300*3=900, no gear.
        var hitpointFrames = result.CasterSent.Select((bytes, index) => (Bytes: bytes, Index: index))
            .Where(frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame.Bytes), out var update) &&
                update.Uid == PlayerUid && update.Updates.Any(entry =>
                    entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints))
            .ToArray();
        Assert.Equal(6, hitpointFrames.Length); // Include the pre-clamp3000 as well as both900 values.
        (ulong Maximum, ulong Hitpoints)[] phases = [(900, 3_000), (900, 900), (900, 900)];
        for (var index = 0; index < phases.Length; index++)
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(hitpointFrames[index * 2].Bytes), out var maximum));
            var maxEntry = Assert.Single(maximum.Updates);
            Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maxEntry.Type);
            Assert.Equal(phases[index].Maximum, maxEntry.Value);
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(hitpointFrames[index * 2 + 1].Bytes), out var combined));
            var phase = phases[index];
            Assert.Collection(combined.Updates,
                max => { Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, max.Type); Assert.Equal(phase.Maximum, max.Value); },
                hp => { Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, hp.Type); Assert.Equal(phase.Hitpoints, hp.Value); });
            Assert.Equal(hitpointFrames[index * 2].Index + 1, hitpointFrames[index * 2 + 1].Index);
        }
        var queryStatusIndex = SingleFrameIndex(result.CasterSent, frame =>
            LegacyPlayerStatusPacket.TryParse(frame, out var status) && status.Uid == PlayerUid);
        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(result.CasterSent[queryStatusIndex]), out var queryStatus));
        Assert.Equal(900U, queryStatus.MaxHP);
        var perfectionIndex = SingleFrameIndex(result.CasterSent, frame =>
            LegacyUserTotalRefineLevPacket.TryParse(frame, out var perfection) && perfection.Uid == PlayerUid);
        var finishActionIndex = SingleFrameIndex(
            result.CasterSent,
            frame => MsgActionPacket.TryParse(frame, out var action) &&
                action.Type == MsgActionType.FinishTransform &&
                action.ObjId == PlayerUid);
        Assert.True(equipmentIndex < hitpointFrames[0].Index);
        Assert.True(hitpointFrames[1].Index < queryStatusIndex);
        Assert.True(queryStatusIndex < hitpointFrames[2].Index);
        Assert.Equal(hitpointFrames[3].Index + 1, hitpointFrames[4].Index);
        Assert.True(hitpointFrames[^1].Index < perfectionIndex);
        Assert.True(perfectionIndex < finishActionIndex);

        // Source query appearance plus FinishTransform's own later appearance.
        // Capture ALL caster spawns; do not hide an unexpected mesh/value.
        var peerSpawns = result.PeerSent.Select((bytes, index) => (Bytes: bytes, Index: index))
            .Where(frame => LegacyPlayerSpawnPacket.TryParse(ReadFrame(frame.Bytes), out var spawn) &&
                spawn.GetNumericField(PlayerReceiveTransformUidField) == PlayerUid).ToArray();
        Assert.Equal(2, peerSpawns.Length);
        for (var index = 0; index < peerSpawns.Length; index++)
        {
            Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(peerSpawns[index].Bytes), out var spawn));
            Assert.Equal(PlayerReceiveTransformClearedMesh, spawn.GetNumericField(PlayerReceiveTransformMeshField));
            Assert.Equal(index == 0 ? 3_000UL : 900UL, spawn.GetNumericField(21));
            Assert.Equal(900UL, spawn.GetNumericField(23));
        }
        Assert.True(peerSpawns[0].Index < peerSpawns[1].Index);
        var peerDeathIndex = SingleFrameIndex(
            result.PeerSent,
            frame => MsgAttackPacket.TryParse(frame, out var attack) &&
                attack.Query.AttackType == MsgAttackType.Death &&
                attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == TargetPlayerUid);
        Assert.All(peerSpawns, spawn => Assert.True(spawn.Index < peerDeathIndex));
    }

    [Fact]
    public async Task PlayerReceiveTransformNonlethalThunderKeepsCasterTransform()
    {
        var result = await RunPlayerReceiveTransformThunderAttackAsync(
            targetHitpoints: checked((int)ExpectedPlayerThunderDamage + 1));

        Assert.Equal(1, result.TargetSession.Character.MinHitPoints);
        Assert.Equal(
            PlayerReceiveTransformId.ToString(CultureInfo.InvariantCulture),
            AssertCharacterAttribute(result.CasterSession, "TransformationID"));
        Assert.Equal(
            PlayerReceiveTransformId.ToString(CultureInfo.InvariantCulture),
            AssertCharacterAttribute(result.CasterSession, "TransformationId"));

        Assert.DoesNotContain(
            ParseMsgActions(result.CasterSent),
            action => action.Type == (MsgActionType)118 &&
                action.ObjId == PlayerUid);
        Assert.DoesNotContain(
            ParseLegacyPlayerSpawns(result.PeerSent),
            spawn => spawn.GetNumericField(PlayerReceiveTransformUidField) == PlayerUid &&
                spawn.GetNumericField(PlayerReceiveTransformMeshField) == PlayerReceiveTransformClearedMesh);
    }

    private static async Task<PlayerReceiveTransformHitResult> RunPlayerReceiveTransformThunderAttackAsync(
        int targetHitpoints)
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
        var casterSession = CreateSession(
            minHitPoints: 3_000,
            attributes: CreatePlayerReceiveTransformCasterAttributes());
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            minHitPoints: targetHitpoints);
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

        return new PlayerReceiveTransformHitResult(
            updatedCasterSession,
            updatedTargetSession,
            casterSent,
            peerSent);
    }

    private static Dictionary<string, string> CreatePlayerReceiveTransformCasterAttributes()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TransformationID"] = PlayerReceiveTransformId.ToString(CultureInfo.InvariantCulture),
            ["TransformationId"] = PlayerReceiveTransformId.ToString(CultureInfo.InvariantCulture),
            [LegacyTransformState.ActiveAttribute] = "1",
            [LegacyTransformState.ExpiresAtUtcAttribute] =
                DateTimeOffset.UtcNow.AddMinutes(5).ToString("O", CultureInfo.InvariantCulture),
            [LegacyTransformState.MaxHitpointsAttribute] = "3000",
        };
    }

    private static string AssertCharacterAttribute(WorldSession session, string key)
    {
        Assert.True(session.Character.Attributes.TryGetValue(key, out var value));
        return value;
    }

    private static List<LegacyPlayerSpawnPacket> ParseLegacyPlayerSpawns(IEnumerable<byte[]> frames)
    {
        var spawns = new List<LegacyPlayerSpawnPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyPlayerSpawnPacket.TryParse(ReadFrame(bytes), out var spawn))
            {
                spawns.Add(spawn);
            }
        }

        return spawns;
    }

    private sealed record PlayerReceiveTransformHitResult(
        WorldSession CasterSession,
        WorldSession TargetSession,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> PeerSent);
}
