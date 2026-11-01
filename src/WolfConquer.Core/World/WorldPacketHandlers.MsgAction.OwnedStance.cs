using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct StanceViewer(ConquerTcpConnection Connection, long Uid, object Identity, byte[]? PrayFrame);

    private static async ValueTask HandleChangeStanceAsync(WorldPacketHandlerContext context,
        MsgActionPacket packet, CancellationToken cancellationToken)
    {
        // Preserve the separate component/file-only compatibility route. An
        // attached or retired ordinary player can never fall back into it.
        if (context.Connection.NativeGameClient?.Player?.HasOwnedActionAttachment != true &&
            context.Session?.NativeGameClient?.Player?.HasOwnedActionAttachment != true)
        {
            await HandleUnboundChangeStanceAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (context.Session is not { } expected || context.Sessions is not { } store) return;
        var plan = StanceProjection.Prepare(expected, packet);
        var viewers = CaptureStanceViewers(context, plan);
        if (!store.TryCommitStance(context, plan, out var receipt) || receipt is null) return;

        // Source SendView completes the skin fanout before it begins action
        // fanout. Per-recipient bytes and all existing metadata remain intact.
        if (!await SendStancePhaseAsync(context, receipt, viewers, plan.SkinFrame, cancellationToken).ConfigureAwait(false) ||
            !await SendStancePhaseAsync(context, receipt, viewers, plan.ActionFrame, cancellationToken).ConfigureAwait(false)) return;
        var prayers = 0;
        foreach (var source in viewers)
        {
            if (source.PrayFrame is null || !receipt.TryCurrent(out var current)) continue;
            if (!TryCurrentStanceViewer(context, source, current.Character, out var target) ||
                LegacyGetDistance(current.Character.X, current.Character.Y, target.Character.X, target.Character.Y) > 4) continue;
            if (!await SendStancePhaseAsync(context, receipt, viewers, source.PrayFrame, cancellationToken).ConfigureAwait(false)) return;
            prayers++;
        }
        if (plan.PickFrame is not null)
        {
            if (!receipt.TryCurrent(out _)) return;
            await context.Connection.SendAsync(plan.PickFrame, cancellationToken).ConfigureAwait(false);
            if (!receipt.TryCurrent(out _)) return;
        }
        await context.Log.WriteLineAsync("WorldServer handled owned MsgAction ChangeStance " +
            $"uid={expected.Character.Uid} action={plan.Action} skinType={plan.Skin} visiblePlayerCount={viewers.Count} " +
            $"castPrayEchoes={prayers} activePickRemoved={plan.RemovedActivePick}; " +
            "source=MsgDataPacket.ChangeStance full UInt32 state, low16 echo.").ConfigureAwait(false);
    }

    private static List<StanceViewer> CaptureStanceViewers(WorldPacketHandlerContext context, StanceProjection plan)
    {
        var viewers = new List<StanceViewer>();
        if (context.Sessions is not { } store) return viewers;
        bool pray = HasLegacyStatusFlag(plan.Expected.Character, LegacyCastPrayFlag);
        foreach (var candidate in store.Snapshot())
        {
            long uid = candidate.Session.Character.Uid;
            if (ReferenceEquals(candidate.Connection, context.Connection) || uid == plan.Expected.Character.Uid ||
                !CanSeePlayer(candidate.Session.Character, plan.Expected.Character) ||
                !LegacyNativeClientPublication.TryCaptureIdentity(store, candidate.Connection, uid, out var identity)) continue;
            byte[]? prayer = pray ? (plan.Reply with { Timestamp = CheckedUInt32(uid, nameof(uid)) }).ToFrameBytes() : null;
            viewers.Add(new(candidate.Connection, uid, identity, prayer));
        }
        return viewers;
    }

    private static bool TryCurrentStanceViewer(WorldPacketHandlerContext context, StanceViewer viewer,
        CharacterSnapshot owner, out WorldSession current)
    {
        current = null!;
        return context.Sessions is { } store && context.Trades?.HasConnectionEnded(viewer.Connection) != true &&
            LegacyNativeClientPublication.TryCurrent(store, viewer.Connection, viewer.Uid, viewer.Identity, out current) &&
            CanSeePlayer(current.Character, owner);
    }

    private static async ValueTask<bool> SendStancePhaseAsync(WorldPacketHandlerContext context,
        WorldSessionStore.ActionPublication receipt, IReadOnlyList<StanceViewer> viewers, byte[] frame,
        CancellationToken cancellationToken)
    {
        if (!receipt.TryCurrent(out _)) return false;
        // Commitment preceded transport. A send failure never rolls back the
        // action, refunds resources or schedules a replay of remaining output.
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (!receipt.TryCurrent(out _)) return false;
        foreach (var viewer in viewers)
        {
            if (!receipt.TryCurrent(out var current)) return false;
            if (!TryCurrentStanceViewer(context, viewer, current.Character, out _)) continue;
            await viewer.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!receipt.TryCurrent(out _)) return false;
        }
        return true;
    }
}
