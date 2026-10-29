using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static WorldSession PublishLegacyReceiveAttackerContinuation(WorldPacketHandlerContext context,
        WorldSession proposed, WorldSessionStore.CapricePairReceipt? receipt, bool lightOfStamina = false)
    {
        if (receipt is not null)
            return lightOfStamina
                ? receipt.PublishLightOfStaminaContinuation(context.Sessions, context.Connection, proposed)
                : receipt.PublishAttackerContinuation(context.Sessions, context.Connection, proposed);
        context.Sessions?.Set(context.Connection, proposed); return proposed;
    }
    private static WorldSession PublishLegacyReceiveTargetContinuation(WorldPacketHandlerContext context,
        ConquerTcpConnection connection, WorldSession proposed, WorldSessionStore.CapricePairReceipt? receipt)
    {
        if (receipt is not null) return receipt.PublishTargetContinuation(context.Sessions, connection, proposed);
        context.Sessions?.Set(connection, proposed); return proposed;
    }
}
