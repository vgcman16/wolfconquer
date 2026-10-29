namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> SendLegacyProcVisibleAsync(WorldPacketHandlerContext context,
        WorldSessionStore.ProcReceipt receipt, byte[] frame, CancellationToken cancellationToken)
    {
        if (!await receipt.SendAsync([frame], cancellationToken).ConfigureAwait(false)) return false;
        var world = context.Sessions!;
        var owner = receipt.Published;
        var viewers = new List<WorldSessionStore.ProcParticipant>();
        foreach (var candidate in world.Snapshot())
        {
            if (ReferenceEquals(candidate.Connection, context.Connection) || candidate.Session.Character.Uid == owner.Character.Uid ||
                !CanSeePlayer(owner.Character, candidate.Session.Character) ||
                !LegacyNativeClientPublication.TryCaptureIdentity(world, candidate.Connection, candidate.Session.Character.Uid, out var identity)) continue;
            viewers.Add(new(candidate.Connection, candidate.Session, identity));
        }
        foreach (var viewer in viewers)
        {
            if (!receipt.RequirePreparedCurrent()) return false;
            if (!LegacyNativeClientPublication.TryCurrent(world, viewer.Connection, viewer.Expected.Character.Uid,
                viewer.Identity, out var peer) || !CanSeePlayer(owner.Character, peer.Character)) continue;
            try { await viewer.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false); }
            catch (Exception error) { throw receipt.Failure(error); }
            if (!receipt.RequirePreparedCurrent()) return false;
        }
        return true;
    }
}
