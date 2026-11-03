namespace WolfConquer.Tests;

internal sealed partial class OwnedModernSqlAuthDatabase
{
    internal OwnedSqlCommitAckDropProxy CreateCommitAckDropProxy(int skipCommitCount = 0)
    {
        if (!created || !markerReady || disposed || SchemaName != Prefix + nonce)
            throw new InvalidOperationException("A live, newly owned SQL fixture is required.");
        // Only this already-owned private schema's connection string enters the proxy.
        // The proxy returns repository access, never credentials or a connection string.
        return OwnedSqlCommitAckDropProxy.Start(scoped, skipCommitCount);
    }
    internal OwnedSqlCommitAckDropProxy CreateCommitAckGateProxy()
    {
        if (!created || !markerReady || disposed || SchemaName != Prefix + nonce)
            throw new InvalidOperationException("A live, newly owned SQL fixture is required.");
        return OwnedSqlCommitAckDropProxy.Start(scoped, skipCommitCount: 1, holdCommitAck: true);
    }
    internal OwnedSqlCommitAckDropProxy CreateCommitRequestGateProxy()
    {
        if (!created || !markerReady || disposed || SchemaName != Prefix + nonce)
            throw new InvalidOperationException("A live, newly owned SQL fixture is required.");
        return OwnedSqlCommitAckDropProxy.Start(scoped, skipCommitCount: 1, holdCommitRequest: true);
    }
}
