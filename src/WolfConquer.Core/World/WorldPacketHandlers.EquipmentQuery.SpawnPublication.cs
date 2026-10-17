using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static async ValueTask<(LegacyEquipmentQueryPublicationResult Current, int ViewerCount)>
        SendLegacyCurrentPlayerSpawnToPeersAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyEquipmentQueryPublicationScope scope,
            CancellationToken cancellationToken,
            PlayerItemSnapshot? capturedAppearanceItems = null,
            bool? capturedAlternate = null)
    {
        var current = RefreshLegacyEquipmentQueryPublication(context, session, scope);
        if (!current.Completed || context.Sessions is not { } sessions) return (current, 0);
        var viewers = 0;
        foreach (var candidate in sessions.Snapshot())
        {
            current = RefreshLegacyEquipmentQueryPublication(context, current.Session!, scope);
            if (!current.Completed) return (current, viewers);
            session = current.Session!;
            if (ReferenceEquals(candidate.Connection, context.Connection) ||
                !sessions.TryGet(candidate.Connection, out var peer) || peer is null ||
                peer.Character.Uid != candidate.Session.Character.Uid || peer.Character.Uid == scope.OwnerUid ||
                !CanSeePlayer(session.Character, peer.Character))
                continue;
            var bytes = LegacyPlayerSpawnPacket.FromSnapshot(
                    session.Character, capturedAppearanceItems ?? session.PlayerItems,
                    capturedAlternate ?? session.AlternateEquipmentActive,
                    maxLife: GetLegacyEquipmentRuntimeMaxHitpoints(session, context.WorldData))
                .ToFrameBytes();
            await candidate.Connection.SendAsync(bytes, cancellationToken).ConfigureAwait(false);
            current = RefreshLegacyEquipmentQueryPublication(context, session, scope);
            if (!current.Completed) return (current, viewers);
            viewers++;
        }

        return (current, viewers);
    }
}
