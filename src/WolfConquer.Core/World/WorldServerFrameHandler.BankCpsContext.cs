namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // These actual Frame fields identify its configured transaction participants.
    // Numeric NPC IDs, nonnull roots or a caller's replacement barrier do not.
    internal bool IsBankCpsContext(WorldSessionStore store, WorldPacketHandlerContext context,
        WorldSession expected, bool requireStoreGate)
    {
        if (!ReferenceEquals(_sessions, store) || !ReferenceEquals(context.Sessions, store) ||
            !ReferenceEquals(context.Trades, _tradeStates) || !ReferenceEquals(context.TradeOperations, _worldOperations) ||
            !ReferenceEquals(context.TradeRecovery, _tradeRecovery) || !ReferenceEquals(context.WorldData, _worldData) ||
            context.ProtocolProfile != _protocolProfile || _snapshotLoader is null ||
            context.PlayerItemsRoot != _snapshotLoader.PlayerItemsRoot ||
            string.IsNullOrWhiteSpace(_snapshotLoader.UsersRoot) || !Path.IsPathFullyQualified(_snapshotLoader.UsersRoot) ||
            string.IsNullOrWhiteSpace(_snapshotLoader.PlayerItemsRoot) || !Path.IsPathFullyQualified(_snapshotLoader.PlayerItemsRoot) ||
            string.IsNullOrWhiteSpace(expected.UsersRoot) || !Path.IsPathFullyQualified(expected.UsersRoot) ||
            !store.TryGet(context.Connection, out var current) || !ReferenceEquals(current, expected) ||
            requireStoreGate && !store.IsFramePersistenceCurrent(this, context.Connection, expected)) return false;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(expected.UsersRoot)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(_snapshotLoader.UsersRoot)), comparison);
    }
}
