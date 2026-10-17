namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static bool TryRefreshLegacyLightningShieldFamilySubject(
        WorldPacketHandlerContext context, WorldSession session, long uid, out WorldSession current)
    {
        if (context.Sessions is { } sessions)
        {
            if (sessions.TryGet(context.Connection, out var registered) &&
                registered is not null && registered.Character.Uid == uid)
            {
                current = registered;
                return true;
            }
        }
        else if (session.Character.Uid == uid)
        {
            current = session;
            return true;
        }
        current = null!;
        return false;
    }

    internal static ValueTask<WorldSession?> SendLegacyLightningShieldFamilyVectorAsync(
        WorldPacketHandlerContext context, WorldSession session, long uid,
        CancellationToken cancellationToken)
    {
        if (!TryRefreshLegacyLightningShieldFamilySubject(context, session, uid, out session))
            return ValueTask.FromResult<WorldSession?>(null);
        var baseActive = HasLegacyStatusFlag(session.Character, LegacyLightningShieldFlag);
        var activated = HasLegacyStatusFlag(session.Character, LegacyLightningShieldActivatedFlag);
        session.Character.Attributes.TryGetValue(RuntimeLegacyLightningShieldExpiresAtUtcAttribute, out var baseDeadline);
        session.Character.Attributes.TryGetValue(LegacyLightningShieldActivationLifetime.ExpiresAtLocalAttribute,
            out var activatedDeadline);
        var frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
            CheckedUInt32(uid, nameof(uid)), LegacyPlayerDeathState.CreateStatusFlagVector(
                session.Character.Attributes, LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)).ToFrameBytes();
        return SendLegacyLightningShieldFamilyFrameAsync(context, session, uid, frame, cancellationToken,
            current => HasLegacyStatusFlag(current.Character, LegacyLightningShieldFlag) == baseActive &&
                HasLegacyStatusFlag(current.Character, LegacyLightningShieldActivatedFlag) == activated &&
                (current.Character.Attributes.TryGetValue(RuntimeLegacyLightningShieldExpiresAtUtcAttribute,
                    out var currentBase) ? currentBase : null) == baseDeadline &&
                (current.Character.Attributes.TryGetValue(LegacyLightningShieldActivationLifetime.ExpiresAtLocalAttribute,
                    out var currentActivated) ? currentActivated : null) == activatedDeadline);
    }

    internal static async ValueTask<WorldSession?> SendLegacyLightningShieldFamilyFrameAsync(
        WorldPacketHandlerContext context, WorldSession session, long uid, byte[] frame,
        CancellationToken cancellationToken, Func<WorldSession, bool>? sameRegistration = null)
    {
        if (!TryRefreshLegacyLightningShieldFamilySubject(context, session, uid, out session)) return null;
        if (sameRegistration is not null && !sameRegistration(session)) return session;
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (!TryRefreshLegacyLightningShieldFamilySubject(context, session, uid, out session)) return null;
        // A superseded registration ends THIS captured vector, not the entered
        // AttachStatus toggle branch. Its remaining source operations still run.
        if (sameRegistration is not null && !sameRegistration(session)) return session;
        if (context.Sessions is not { } sessions) return session;
        foreach (var candidate in sessions.Snapshot())
        {
            if (!TryRefreshLegacyLightningShieldFamilySubject(context, session, uid, out session)) return null;
            if (sameRegistration is not null && !sameRegistration(session)) return session;
            if (ReferenceEquals(candidate.Connection, context.Connection) ||
                !sessions.TryGet(candidate.Connection, out var peer) || peer is null ||
                peer.Character.Uid != candidate.Session.Character.Uid || peer.Character.Uid == uid ||
                !CanSeePlayer(session.Character, peer.Character)) continue;
            await candidate.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!TryRefreshLegacyLightningShieldFamilySubject(context, session, uid, out session)) return null;
            if (sameRegistration is not null && !sameRegistration(session)) return session;
        }
        return session;
    }
}
