using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // The only producer is the real global reviver pass under its actual
    // Shared lease, after real selection of a current monster incarnation.
    internal sealed class ReviverAdmission { }
    internal sealed record ReviverAudience(ConquerTcpConnection Connection, long Uid, object Identity);
    private sealed record ReviverAdmissionData(WorldServerFrameHandler Frame, IDisposable Operation,
        ConquerTcpConnection Connection, WorldSession Expected, object Identity, DateTimeOffset Now,
        MonsterRuntimeStateStore.ClassicBuffTarget Monster, LegacyNativeAttributeMapProof Fields, ReviverAudience[] Audience);
    private static readonly ConditionalWeakTable<ReviverAdmission, ReviverAdmissionData> reviverAdmissions = new();

    private ReviverAdmission? PrepareReviverAdmission(IDisposable operation, ConquerTcpConnection connection,
        WorldSession expected, LegacyMonsterInstance selected, DateTimeOffset now)
    {
        if (!_worldOperations.OwnsActiveLease(operation) || !IsLegacyCharacterDead(expected.Character) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(_sessions, connection, expected.Character.Uid, out var identity) ||
            !LegacyNativeClientPublication.TryCurrent(_sessions, connection, expected.Character.Uid, identity, out var current) ||
            !ReferenceEquals(current, expected)) return null;
        var fields = LegacyNativeAttributeMapProof.CaptureRaw(expected.Character.Attributes);
        if (!_monsterStates.TryCaptureClassicBuffTarget(selected, expected.Character.MapId,
            ReadCharacterDynamicId(expected.Character), expected.Character.X, expected.Character.Y, 12,
            out var monster, out _) || monster is null || !fields.IsCurrentPlain()) return null;
        var audience = new List<ReviverAudience>();
        foreach (var entry in _sessions.Snapshot())
        {
            var uid = entry.Session.Character.Uid;
            if (!LegacyNativeClientPublication.TryCaptureIdentity(_sessions, entry.Connection, uid, out var peerIdentity) ||
                !LegacyNativeClientPublication.TryCurrent(_sessions, entry.Connection, uid, peerIdentity, out var peer) ||
                !ReferenceEquals(peer, entry.Session)) continue;
            audience.Add(new(entry.Connection, uid, peerIdentity));
        }
        if (!fields.IsCurrentPlain() || !_worldOperations.OwnsActiveLease(operation)) return null;
        var admission = new ReviverAdmission();
        reviverAdmissions.Add(admission, new(this, operation, connection, expected, identity, now, monster, fields, audience.ToArray()));
        return admission;
    }
    private static void DiscardReviverAdmission(ReviverAdmission admission) => reviverAdmissions.Remove(admission);

    internal static bool TryReadReviverAdmission(ReviverAdmission admission, WorldServerFrameHandler frame,
        WorldSessionStore store, ConquerTcpConnection connection, WorldSession expected,
        out object identity, out DateTimeOffset now, out MonsterRuntimeStateStore.ClassicBuffTarget monster,
        out ReviverAudience[] audience)
    {
        identity = null!; now = default; monster = null!; audience = null!;
        if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
            !reviverAdmissions.TryGetValue(admission, out var data) || !ReferenceEquals(data.Frame, frame) ||
            !ReferenceEquals(frame._sessions, store) || !ReferenceEquals(data.Connection, connection) ||
            !ReferenceEquals(data.Expected, expected) || !frame._worldOperations.OwnsActiveLease(data.Operation) ||
            frame._tradeRecovery.IsBlocked || !data.Fields.IsCurrentPlain()) return false;
        identity = data.Identity; now = data.Now; monster = data.Monster; audience = data.Audience; return true;
    }
    internal static bool ConsumeReviverAdmission(ReviverAdmission admission, WorldServerFrameHandler frame,
        WorldSessionStore store, ConquerTcpConnection connection, WorldSession expected) =>
        TryReadReviverAdmission(admission, frame, store, connection, expected, out _, out _, out _, out _) &&
        reviverAdmissions.Remove(admission);
    internal uint ComputeReviverSourceMaximum(WorldSession session)
    {
        uint hp = session.Character.MinHitPoints <= 0 ? 1U : CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints));
        return ComputeLegacyStartupMaxHitpoints(session.Character, hp, session.PlayerItems,
            session.CombatGear ?? CombatGearSnapshot.Empty, _worldData);
    }

    internal sealed record PreparedReviverFrames(byte[][] Owner, byte[][] Animation, byte[] Spawn);

    // DATA only: caller must hold the physical player's vector gate and the
    // genuine completed query/monster/Store proof before publishing anything.
    internal PreparedReviverFrames PrepareReviverFrames(WorldSession session, LegacyNativePlayerRuntime player,
        LegacyMonsterInstance reviver, uint maximumHitpoints)
        => PrepareReviverFramesCore(session, player, reviver, maximumHitpoints, null);

    // The Store has already preflighted this frame using its exact prepared
    // action owner. This byte array is DATA only, never an output permit.
    internal PreparedReviverFrames PrepareReviverFramesWithPreparedSpawn(WorldSession session, LegacyNativePlayerRuntime player,
        LegacyMonsterInstance reviver, uint maximumHitpoints, byte[] preparedSpawn)
        => PrepareReviverFramesCore(session, player, reviver, maximumHitpoints, preparedSpawn);

    private PreparedReviverFrames PrepareReviverFramesCore(WorldSession session, LegacyNativePlayerRuntime player,
        LegacyMonsterInstance reviver, uint maximumHitpoints, byte[]? preparedSpawn)
    {
        uint uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var owner = new List<byte[]>();
        uint currentHitpoints = checked((uint)session.Character.MinHitPoints);
        foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(uid, maximumHitpoints, currentHitpoints))
            owner.Add(update.ToFrameBytes());
        owner.Add(LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(uid, LegacyStaminaState.RequireCurrent(session.Character)).ToFrameBytes());
        var vector = LegacyNativeWindWalkerFanLifecycle.CaptureVector(player,
            LegacyNativePlayerFlag6Lifecycle.CaptureVector(player,
                LegacyPlayerDeathState.CreateStatusFlagVector(session.Character.Attributes, LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)));
        owner.Add(LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(uid, vector).ToFrameBytes());
        owner.Add(LegacyMsgUpdatePacket.CreateLegacyRuntimeMeshUpdate(uid, BuildLegacyCharacterMesh(session.Character)).ToFrameBytes());
        owner.Add(LegacyMapStatusPacket.FromMap(session.Character.MapId, _worldData?.MapsById).ToFrameBytes());
        var animation = new LegacySpellAnimationPacket(reviver.Uid, AttackedUid: 0, reviver.X, reviver.Y,
            SpellId: 1100, SpellLevel: 0, NextSpell: 0, LevelHu: 0, SpellEffect: 0,
            [new LegacySpellAnimationTarget(uid, Damage: 0, Hit: true, MsgAttackEffect.None)]).ToFrames();
        var spawn = preparedSpawn ?? LegacyPlayerSpawnPacket.FromSnapshot(session, session.PlayerItems,
            alternateEquipmentActive: session.AlternateEquipmentActive, maxLife: maximumHitpoints).ToFrameBytes();
        return new(owner.ToArray(), animation.ToArray(), spawn);
    }

    internal static bool ReviverAudienceCanSeePlayer(WorldSession viewer, WorldSession actor) =>
        CanSeePlayer(viewer.Character, actor.Character);
    internal static bool ReviverAudienceCanSeeMonster(WorldSession viewer, LegacyMonsterInstance monster) =>
        CanSeeMonster(viewer.Character, monster);

}
