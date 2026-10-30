namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Only the actual owning Frame supplies a repair's configured participant roots.
    internal (string UsersRoot, string ItemsRoot) RequireItemRepairContext(
        WorldSessionStore store, WorldPacketHandlerContext context, WorldSession expected)
    {
        if (!ReferenceEquals(_sessions, store) || !ReferenceEquals(context.Sessions, store) ||
            !store.IsFramePersistenceCurrent(this, context.Connection, expected) ||
            !ReferenceEquals(context.Trades, _tradeStates) ||
            !ReferenceEquals(context.TradeOperations, _worldOperations) ||
            !ReferenceEquals(context.TradeRecovery, _tradeRecovery) ||
            !ReferenceEquals(context.WorldData, _worldData) || context.ProtocolProfile != _protocolProfile ||
            _snapshotLoader is null || context.PlayerItemsRoot != _snapshotLoader.PlayerItemsRoot ||
            string.IsNullOrWhiteSpace(_snapshotLoader.UsersRoot) ||
            string.IsNullOrWhiteSpace(_snapshotLoader.PlayerItemsRoot) ||
            !Path.IsPathFullyQualified(_snapshotLoader.UsersRoot) ||
            !Path.IsPathFullyQualified(_snapshotLoader.PlayerItemsRoot) ||
            string.IsNullOrWhiteSpace(expected.UsersRoot) || !Path.IsPathFullyQualified(expected.UsersRoot))
            throw new InvalidOperationException("itemRepairRequiresActualConfiguredFrame");
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(expected.UsersRoot)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(_snapshotLoader.UsersRoot)), comparison))
            throw new InvalidOperationException("itemRepairFrameCharacterRootChanged");
        return (_snapshotLoader.UsersRoot, _snapshotLoader.PlayerItemsRoot);
    }
}
