using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal LegacyBoundHitpointsScalarWrite PrepareNativeHitpointsWrite(WorldSessionStore store,
        ConquerTcpConnection connection, LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession proposed,
        WorldPacketHandlers.LegacyCapriceCharacterFields? caprice = null)
    {
        if (!ReferenceEquals(_sessions, store) || !store.IsFramePersistenceCurrent(this, connection, expected) ||
            _snapshotLoader is null || !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(_snapshotLoader.UsersRoot)),
                binding.UsersRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsRegisteredFrameOwnerUnavailable");
        return LegacyBoundHitpointsScalarWrite.Prepare(this, store, connection, binding, expected, proposed, caprice);
    }
}
