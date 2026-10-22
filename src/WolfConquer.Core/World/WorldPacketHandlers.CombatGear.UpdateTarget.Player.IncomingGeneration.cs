using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// One entered receive, never persistent player state or a UID-indexed registry.
internal sealed record LegacyCombatGearReceiveParticipants(
    WorldSessionStore? Store,
    ConquerTcpConnection IncomingConnection,
    long IncomingUid,
    object? IncomingIdentity,
    ConquerTcpConnection ProcOwnerConnection,
    long ProcOwnerUid,
    object? ProcOwnerIdentity);

public static partial class WorldPacketHandlers
{
    private static LegacyPlayerMagicReceiveAbortReason TryCaptureLegacyCombatGearReceiveParticipants(
        WorldPacketHandlerContext context, LegacyPlayerReceiveCombatGearRequest request,
        out LegacyCombatGearReceiveParticipants participants)
    {
        participants = null!;
        if (request.AttackerSession.Character.Uid != request.AttackerUid)
            return LegacyPlayerMagicReceiveAbortReason.AttackerLost;
        if (request.Target.Session.Character.Uid != request.TargetUid)
            return LegacyPlayerMagicReceiveAbortReason.TargetLost;

        object? incomingIdentity = request.IncomingNativeClientIdentity;
        if (incomingIdentity is null && context.RootFirstFamilyInvocation is { } root &&
            ReferenceEquals(root.Connection, context.Connection) && root.OwnerUid == request.AttackerUid)
            incomingIdentity = root.NativeClientIdentity;
        object? procOwnerIdentity = null;
        if (context.Sessions is { } world)
        {
            // Prefer the original native caster reference already carried by 1000/1001.
            // Otherwise capture at this receive's entry, BEFORE its first awaited opening phase.
            if (incomingIdentity is null)
            {
                if (!LegacyNativeClientPublication.TryCaptureIdentity(
                        world, context.Connection, request.AttackerUid, out var capturedIncoming))
                    return LegacyPlayerMagicReceiveAbortReason.AttackerLost;
                incomingIdentity = capturedIncoming;
            }
            if (!LegacyNativeClientPublication.TryCurrent(
                    world, context.Connection, request.AttackerUid, incomingIdentity, out _))
                return LegacyPlayerMagicReceiveAbortReason.AttackerLost;
            if (!LegacyNativeClientPublication.TryCaptureIdentity(
                    world, request.Target.Connection, request.TargetUid, out var capturedProcOwner) ||
                !LegacyNativeClientPublication.TryCurrent(
                    world, request.Target.Connection, request.TargetUid, capturedProcOwner, out _))
                return LegacyPlayerMagicReceiveAbortReason.TargetLost;
            procOwnerIdentity = capturedProcOwner;
        }

        participants = new(context.Sessions, context.Connection, request.AttackerUid, incomingIdentity,
            request.Target.Connection, request.TargetUid, procOwnerIdentity);
        return LegacyPlayerMagicReceiveAbortReason.None;
    }

    private static LegacyPlayerMagicReceiveAbortReason RefreshLegacyCombatGearCapturedParticipants(
        WorldPacketHandlerContext context, LegacyPlayerReceiveCombatGearRequest request,
        LegacyCombatGearReceiveParticipants original,
        ref WorldSession attacker, ref WorldActiveSession target)
    {
        // Original incoming loss wins; never replace either captured token after an await.
        if (!ReferenceEquals(context.Connection, original.IncomingConnection) ||
            request.AttackerUid != original.IncomingUid ||
            !ReferenceEquals(context.Sessions, original.Store))
            return LegacyPlayerMagicReceiveAbortReason.AttackerLost;
        if (!ReferenceEquals(request.Target.Connection, original.ProcOwnerConnection) ||
            request.TargetUid != original.ProcOwnerUid)
            return LegacyPlayerMagicReceiveAbortReason.TargetLost;

        if (original.Store is { } world)
        {
            if (original.IncomingIdentity is not { } incomingIdentity ||
                !LegacyNativeClientPublication.TryCurrent(world, original.IncomingConnection,
                    original.IncomingUid, incomingIdentity, out var currentAttacker))
                return LegacyPlayerMagicReceiveAbortReason.AttackerLost;
            attacker = currentAttacker;
            if (original.ProcOwnerIdentity is not { } procOwnerIdentity ||
                !LegacyNativeClientPublication.TryCurrent(world, original.ProcOwnerConnection,
                    original.ProcOwnerUid, procOwnerIdentity, out var currentTarget))
                return LegacyPlayerMagicReceiveAbortReason.TargetLost;
            target = request.Target with { Session = currentTarget };
        }
        else
        {
            // No Store has no native holder token: preserve only the existing local-UID fallback.
            if (attacker.Character.Uid != original.IncomingUid)
                return LegacyPlayerMagicReceiveAbortReason.AttackerLost;
            if (target.Session.Character.Uid != original.ProcOwnerUid)
                return LegacyPlayerMagicReceiveAbortReason.TargetLost;
        }
        return LegacyPlayerMagicReceiveAbortReason.None;
    }
}
