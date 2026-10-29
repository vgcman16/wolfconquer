namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static ValueTask HandleOwnedFullRestoreConsumableAsync(WorldPacketHandlerContext context,
        WorldSession supplied, uint uid, CancellationToken cancellationToken) =>
        HandleOwnedManagedConsumableAsync(context, supplied, uid, LegacyConsumableItemKind.FullRestore, cancellationToken);
}
