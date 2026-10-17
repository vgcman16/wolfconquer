using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession?> SendLegacyPhysicalMonsterInteractionAsync(
        WorldPacketHandlerContext context, WorldSession session, long ownerUid,
        LegacyMonsterInstance monster, MsgAttackPacket packet, bool aroundMonster,
        CancellationToken cancellationToken)
    {
        var scope = new LegacyEquipmentQueryPublicationScope(ownerUid);
        var current = RefreshLegacyEquipmentQueryPublication(context, session, scope);
        if (!current.Completed) return null;
        var frame = packet.ToFrameBytes();
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        current = RefreshLegacyEquipmentQueryPublication(context, current.Session!, scope);
        if (!current.Completed) return null;
        if (context.Sessions is not { } sessions) return current.Session;

        foreach (var candidate in sessions.Snapshot())
        {
            current = RefreshLegacyEquipmentQueryPublication(context, current.Session!, scope);
            if (!current.Completed) return null;
            if (ReferenceEquals(candidate.Connection, context.Connection) ||
                !sessions.TryGet(candidate.Connection, out var peer) || peer is null ||
                peer.Character.Uid != candidate.Session.Character.Uid || peer.Character.Uid == ownerUid ||
                !(aroundMonster ? CanSeeMonster(peer.Character, monster)
                    : CanSeePlayer(current.Session!.Character, peer.Character)))
                continue;
            await candidate.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            current = RefreshLegacyEquipmentQueryPublication(context, current.Session!, scope);
            if (!current.Completed) return null;
        }
        return current.Session;
    }
}
