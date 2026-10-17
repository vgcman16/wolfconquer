using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryRefreshLegacyMonsterLightningShieldSubject(
        WorldPacketHandlerContext context, long ownerUid, ref WorldSession session)
    {
        if (context.Sessions is not { } sessions) return session.Character.Uid == ownerUid;
        if (!sessions.TryGet(context.Connection, out var current) || current is null ||
            current.Character.Uid != ownerUid) return false;
        session = current;
        return true;
    }

    private static bool HasLegacyMonsterLightningShieldRegistration(
        WorldSession session, DateTime expiresAt, bool baseShield) =>
        HasLegacyStatusFlag(session.Character, LegacyLightningShieldFlag) == baseShield &&
        HasLegacyStatusFlag(session.Character, LegacyLightningShieldActivatedFlag) &&
        LegacyLightningShieldActivationLifetime.TryRead(session.Character, out var currentExpiry) &&
        currentExpiry == expiresAt;

    private static async ValueTask<WorldSession?> SendLegacyMonsterLightningShieldInsertionAsync(
        WorldPacketHandlerContext context, WorldSession session, long ownerUid,
        CancellationToken cancellationToken)
    {
        if (!TryRefreshLegacyMonsterLightningShieldSubject(context, ownerUid, ref session)) return null;
        if (!LegacyLightningShieldActivationLifetime.TryRead(session.Character, out var expiresAt))
            return session;
        var baseShield = HasLegacyStatusFlag(session.Character, LegacyLightningShieldFlag);
        var frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
            CheckedUInt32(ownerUid, nameof(ownerUid)),
            CreateLegacyRuntimeStatusFlagVector(session.Character)).ToFrameBytes();
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (!TryRefreshLegacyMonsterLightningShieldSubject(context, ownerUid, ref session)) return null;
        // A changed registration stops only this captured vector's peer phase.
        // The source physical action continues from current same-UID state.
        if (!HasLegacyMonsterLightningShieldRegistration(session, expiresAt, baseShield)) return session;
        if (context.Sessions is not { } sessions) return session;
        foreach (var candidate in sessions.Snapshot())
        {
            if (!TryRefreshLegacyMonsterLightningShieldSubject(context, ownerUid, ref session)) return null;
            if (!HasLegacyMonsterLightningShieldRegistration(session, expiresAt, baseShield)) return session;
            if (ReferenceEquals(candidate.Connection, context.Connection) ||
                !sessions.TryGet(candidate.Connection, out var peer) || peer is null ||
                peer.Character.Uid != candidate.Session.Character.Uid || peer.Character.Uid == ownerUid ||
                !CanSeePlayer(session.Character, peer.Character)) continue;
            await candidate.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!TryRefreshLegacyMonsterLightningShieldSubject(context, ownerUid, ref session)) return null;
            if (!HasLegacyMonsterLightningShieldRegistration(session, expiresAt, baseShield)) return session;
        }
        return session;
    }
}
