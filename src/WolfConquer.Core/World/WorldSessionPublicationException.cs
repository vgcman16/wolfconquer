using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public enum WorldSessionPublicationStage { FramePersistence, Observer, ProcContinuation, MagicCostOutput }

// Construction belongs to the actual Store publisher. This outcome proves its
// historical memory publication, not a successful save of every sidecar.
public sealed class WorldSessionPublicationException : InvalidOperationException
{
    public WorldSession PublishedSession { get; }
    public WorldSessionPublicationStage Stage { get; }
    public Exception? FencingFailure { get; private set; }
    internal WorldSessionStore Store { get; }
    internal ConquerTcpConnection Connection { get; }
    internal object NativeIdentity { get; }
    internal LegacyOrdinarySpellBinding? Binding { get; }

    internal WorldSessionPublicationException(WorldSessionStore store, ConquerTcpConnection connection,
        object nativeIdentity, LegacyOrdinarySpellBinding? binding, WorldSession publishedSession,
        WorldSessionPublicationStage stage, Exception cause)
        : base("sessionPostPublicationFailure:" + stage + ":publishedStateRequiresRecovery", cause)
    {
        Store = store; Connection = connection; NativeIdentity = nativeIdentity;
        Binding = binding; PublishedSession = publishedSession; Stage = stage;
    }

    // Keep secondary quarantine failures inspectable without replacing the
    // published outcome, its stage, or its original observer/Frame cause.
    internal void RecordFencingFailure(Exception error)
    {
        FencingFailure = FencingFailure is null ? error :
            new AggregateException("Multiple publication fence operations failed.", FencingFailure, error);
    }
    internal bool BelongsTo(WorldSessionStore? store, ConquerTcpConnection connection) =>
        ReferenceEquals(Store, store) && ReferenceEquals(Connection, connection);
}
