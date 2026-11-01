namespace WolfConquer.Core.World;

internal sealed partial class LegacySpellPublicationOwner
{
    private int querySuccessorIssued;

    internal bool TryAdvanceAfterAdmissionQuery(WorldPacketHandlers.CompletedAdmissionContinuationQuery completed,
        out LegacySpellPublicationOwner? successor)
    {
        successor = null;
        if (Volatile.Read(ref querySuccessorIssued) != 0 || !completed.CoversPreQueryHolder(boundSession) ||
            !completed.TryCurrent(out var current) || !NativeOwnerIsCurrent() || trades.HasConnectionEnded(connection) ||
            current.AccountId != account || current.Character.Uid != uid || !ReferenceEquals(current.NativeGameClient, native) ||
            !LegacyNativeClientPublication.TryCurrent(sessions, connection, uid, identity, out var actual) ||
            !ReferenceEquals(current, actual) || !plan.TryPrepareQuerySuccessor(boundSession.PlayerSpells, current.PlayerSpells, out var after) ||
            after is null || !completed.TryCurrent(out actual) || !ReferenceEquals(actual, current)) return false;
        var prepared = new LegacySpellPublicationOwner(sessions, trades, connection, identity, current, after);
        if (!prepared.IsCurrent() || !completed.TryCurrent(out actual) || !ReferenceEquals(actual, current) ||
            Interlocked.CompareExchange(ref querySuccessorIssued, 1, 0) != 0) return false;
        successor = prepared; return true;
    }
}
