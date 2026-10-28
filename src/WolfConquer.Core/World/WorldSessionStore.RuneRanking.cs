using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Rank is a leaf of the exact live holder; never a copied UID or persistence callback.
    internal bool TryUpdateRuneRanking(ConquerTcpConnection connection, WorldSession expected, LegacyRuneRankingState ranking) =>
        TryUpdateRuneRankingCore(connection, expected, expectedNativeIdentity: null, ranking: ranking);

    internal bool TryUpdateRuneRankingWithNativeIdentity(ConquerTcpConnection connection,
        WorldSession expected, object expectedNativeIdentity, LegacyRuneRankingState ranking)
    {
        ArgumentNullException.ThrowIfNull(expectedNativeIdentity);
        return TryUpdateRuneRankingCore(connection, expected, expectedNativeIdentity, ranking);
    }

    private bool TryUpdateRuneRankingCore(ConquerTcpConnection connection, WorldSession expected,
        object? expectedNativeIdentity, LegacyRuneRankingState ranking)
    {
        ArgumentNullException.ThrowIfNull(connection); ArgumentNullException.ThrowIfNull(expected); ArgumentNullException.ThrowIfNull(ranking);
        RefuseBoundSpellReentry(connection);
        using var packageScope = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
        if (!_connectionGates.TryGetValue(connection, out var gate)) return false;
        lock (gate)
        {
            using var scope = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(connection, out var holder) ||
                !RuneRankingOwnerIsCurrentPlain(connection, holder, expected, expectedNativeIdentity)) return false;
            var binding = holder.SpellBinding;
            if (binding is null && (connection.NativeGameClient?.OrdinarySpellBinding is not null ||
                expected.NativeGameClient?.OrdinarySpellBinding is not null)) return false;
            using var actors = binding is null ? null : new SpellActorScope(binding);
            if (binding is not null && !BindingIsCurrentPlain(binding, holder, expected)) return false;
            if (!LegacyItemSaveGraph.TryCapture(expected.PlayerItems, out var graph)) return false;
            var stable = graph.WithOwnedItemsForPersistence(expected);
            using var lease = ranking.PrepareUpdate(stable.Character, stable.PlayerItems);
            if (!graph.IsCurrent(expected.PlayerItems) || !lease.IsCurrentPlain() ||
                !RuneRankingOwnerIsCurrentPlain(connection, holder, expected, expectedNativeIdentity) ||
                binding is not null && !BindingIsCurrentPlain(binding, holder, expected)) return false;
            lease.Publish();
            return true;
        }
    }

    private bool RuneRankingOwnerIsCurrentPlain(ConquerTcpConnection connection, SessionHolder holder,
        WorldSession expected, object? expectedNativeIdentity) =>
        _sessions.TryGetValue(connection, out var actual) && ReferenceEquals(actual, holder) &&
        ReferenceEquals(holder.Read(), expected) && (expectedNativeIdentity is null ||
            ReferenceEquals(holder.NativeClientIdentity, expectedNativeIdentity));
}
