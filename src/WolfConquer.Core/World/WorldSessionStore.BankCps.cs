namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool IsBankCpsFrameContext(WorldPacketHandlerContext context, WorldSession expected) =>
        framePersistenceOwner?.IsBankCpsContext(this, context, expected, requireStoreGate: false) == true;

    internal string RequireBankCpsWriterContext(WorldPacketHandlerContext context, WorldSession expected)
    {
        if (framePersistenceOwner?.IsBankCpsContext(this, context, expected, requireStoreGate: true) != true)
            throw new InvalidOperationException("bankCpsRequiresActualConfiguredFrame");
        return context.PlayerItemsRoot!;
    }
}
