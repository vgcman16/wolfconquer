using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private enum LegacyClassicBuffTargetKind { Self, Monster, Player }
    private sealed class LegacyClassicBuffAdmissionRequest(uint targetUid)
    {
        internal uint TargetUid { get; } = targetUid;
        internal LegacyClassicBuffTargetKind Kind { get; set; }
        internal object? ActorIdentity { get; set; }
        internal long ActorMap { get; set; }
        internal long ActorX { get; set; }
        internal long ActorY { get; set; }
        internal ulong ActorDynamic { get; set; }
        internal WorldSessionStore.ProcParticipant? Peer { get; set; }
        internal MonsterRuntimeStateStore.ClassicBuffTarget? Monster { get; set; }
        internal string RefusalReason { get; set; } = "classicTargetOwnerChanged";
    }

    private static bool TryCaptureLegacyClassicBuffAdmission(WorldPacketHandlerContext context,
        LegacyRootAdmissionOwner owner, LegacyClassicBuffAdmissionRequest request, out string reason)
    {
        reason = "classicTargetUnavailable";
        var session = owner.Expected;
        request.ActorIdentity = owner.Identity;
        if (request.TargetUid == session.Character.Uid)
        {
            request.Kind = LegacyClassicBuffTargetKind.Self; reason = string.Empty; return true;
        }
        // Preserve the executor's actual monster-before-player resolution.
        if (TryFindVisibleMonsterTargetByUid(context, session.Character, request.TargetUid, out var target))
        {
            if (owner.Store is null || context.MonsterStates is not { } states)
            { reason = "classicMonsterOwnerUnavailable"; return false; }
            if (!states.TryCaptureClassicBuffTarget(target.Monster, session.Character.MapId,
                    ReadCharacterDynamicId(session.Character), session.Character.X, session.Character.Y,
                    LegacyViewThreshold, out var captured, out reason)) return false;
            request.ActorMap = session.Character.MapId; request.ActorX = session.Character.X;
            request.ActorY = session.Character.Y; request.ActorDynamic = ReadCharacterDynamicId(session.Character);
            request.Kind = LegacyClassicBuffTargetKind.Monster; request.Monster = captured; return true;
        }
        if (owner.Store is { } store && TryGetVisibleSessionByUid(context, session, request.TargetUid, out var peer) &&
            LegacyNativeClientPublication.TryCaptureIdentity(store, peer.Connection, peer.Session.Character.Uid, out var identity) &&
            LegacyNativeClientPublication.TryCurrent(store, peer.Connection, peer.Session.Character.Uid, identity, out var current) &&
            ReferenceEquals(current, peer.Session))
        {
            request.Kind = LegacyClassicBuffTargetKind.Player;
            request.Peer = new(peer.Connection, peer.Session, identity); reason = string.Empty; return true;
        }
        return false;
    }

    private static bool TryCommitLegacyClassicBuffAdmission(LegacyRootAdmissionOwner owner,
        LegacyClassicBuffAdmissionRequest target, WorldSession candidate,
        out WorldSessionStore.MagicCostOutputReceipt? output)
    {
        return owner.Store!.TryCommitClassicBuffCosts(owner.Connection, owner.Expected, owner.Identity!, candidate,
            target.Peer, target.Monster, out output, out var reason, owner.RuneEligibility) ? true : Refused(target, reason);
    }

    private static bool Refused(LegacyClassicBuffAdmissionRequest target, string reason)
    { target.RefusalReason = reason; return false; }

    private static void RequireClassicTargetCurrent(WorldSessionStore? store, ConquerTcpConnection connection,
        WorldSession expected, object? identity)
    {
        if (store is null && identity is null) return; // Existing detached self-only compatibility.
        if (store is null || identity is null || !LegacyNativeClientPublication.TryCurrent(store, connection,
                expected.Character.Uid, identity, out var current) || !ReferenceEquals(current, expected))
            throw new LegacyClassicBuffContinuationStoppedException("classicCommittedTargetChanged");
    }
}

internal sealed class LegacyClassicBuffContinuationStoppedException(string reason) : Exception(reason)
{
    internal string Reason { get; } = reason;
}
