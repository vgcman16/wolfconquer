using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryRefreshLegacyEquipmentMutationSession(
        WorldPacketHandlerContext context,
        long ownerUid,
        PlayerItemSnapshot expectedItems,
        ref WorldSession session)
    {
        if (context.Sessions is { } sessions)
        {
            if (!sessions.TryGet(context.Connection, out var current) ||
                current is null || current.Character.Uid != ownerUid ||
                !ReferenceEquals(current.PlayerItems, expectedItems))
            {
                return false;
            }

            session = current;
            return true;
        }

        return session.Character.Uid == ownerUid && ReferenceEquals(session.PlayerItems, expectedItems);
    }

}
