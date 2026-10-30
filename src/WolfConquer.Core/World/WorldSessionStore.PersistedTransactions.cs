using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private static readonly ConditionalWeakTable<ConquerTcpConnection, TransactionGateOrder> TransactionGateOrders = new();
    private static long _nextTransactionGateOrder;

    // Every multi-connection owner must share this order, including native teams.
    // Independent order registries over the same connection gates can deadlock.
    internal static long GetConnectionGateOrder(ConquerTcpConnection connection) =>
        TransactionGateOrders.GetValue(connection, static _ =>
            new TransactionGateOrder(Interlocked.Increment(ref _nextTransactionGateOrder))).Value;

    // Persist callbacks save every changed field; the ordinary callback is not repeated.
    internal bool TryCommitSession(ConquerTcpConnection connection, WorldSession expected,
        WorldSession updated, Func<bool> persist) => TryCommitSession(connection, expected, updated, persist, out _);

    internal bool TryCommitSession(ConquerTcpConnection connection, WorldSession expected,
        WorldSession updated, Func<bool> persist, out WorldSession? final)
        => TryCommitSessionCore(connection, expected, updated, persist, null, out final);

    internal bool TryCommitRuneItemSpellSession(ConquerTcpConnection connection, WorldSession expected,
        WorldSession updated, LegacyPreparedSpellFileWrite.RuneCharacterPublication runePublication,
        Func<bool> persist, out WorldSession? final)
    {
        ArgumentNullException.ThrowIfNull(runePublication);
        return TryCommitSessionCore(connection, expected, updated, persist, runePublication, out final);
    }

    private bool TryCommitSessionCore(ConquerTcpConnection connection, WorldSession expected,
        WorldSession updated, Func<bool> persist, LegacyPreparedSpellFileWrite.RuneCharacterPublication? runePublication,
        out WorldSession? final)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(updated);
        ArgumentNullException.ThrowIfNull(persist);
        final = null;
        RefuseBoundSpellReentry(connection);
        using var packageScope = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected, updated);
        var gate = _connectionGates.GetValue(connection, static _ => new object());
        lock (gate)
        {
            using var scope = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, updated)) return false;
            using var publication = new PreparedSessionSpellPublication(this, connection, holder, expected, updated,
                runePublication: runePublication);
            publication.ArmFinalProof();
            if (!publication.ConsumeBeforeWriterPlain()) throw InvalidSpellPublication();
            bool persisted;
            try { persisted = persist(); }
            catch (LegacyCommittedSpellPublicationException) { FailCommittedSpellPublication(holder, publication.Binding); throw; }
            if (!persisted) return false;
            if (!publication.AcceptExternallyPersistedHitpoints() || !publication.IsCurrentPlain())
                FailCommittedSpellPublication(holder, publication.Binding);
            publication.PublishNative();
            publication.PublishHolder();
            final = publication.FinalSession;
            return true;
        }
    }

    internal bool TryCommitPair(ConquerTcpConnection firstConnection, WorldSession expectedFirst, WorldSession updatedFirst,
        ConquerTcpConnection secondConnection, WorldSession expectedSecond, WorldSession updatedSecond, Func<bool> persist) =>
        TryCommitPair(firstConnection, expectedFirst, updatedFirst, secondConnection, expectedSecond, updatedSecond,
            persist, out _, out _);

    // Both complete plans precede the writer. No comparer, allocation or callout
    // separates native/holder stores after its successful return.
    internal bool TryCommitPair(ConquerTcpConnection firstConnection, WorldSession expectedFirst, WorldSession updatedFirst,
        ConquerTcpConnection secondConnection, WorldSession expectedSecond, WorldSession updatedSecond, Func<bool> persist,
        out WorldSession? finalFirst, out WorldSession? finalSecond)
    {
        ArgumentNullException.ThrowIfNull(firstConnection);
        ArgumentNullException.ThrowIfNull(secondConnection);
        ArgumentNullException.ThrowIfNull(expectedFirst);
        ArgumentNullException.ThrowIfNull(expectedSecond);
        ArgumentNullException.ThrowIfNull(updatedFirst);
        ArgumentNullException.ThrowIfNull(updatedSecond);
        ArgumentNullException.ThrowIfNull(persist);
        finalFirst = finalSecond = null;
        RefuseBoundSpellReentry(firstConnection);
        RefuseBoundSpellReentry(secondConnection);
        if (ReferenceEquals(firstConnection, secondConnection)) return false;
        using var packageScope = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages,
            expectedFirst, expectedSecond, updatedFirst, updatedSecond);
        var firstGate = _connectionGates.GetValue(firstConnection, static _ => new object());
        var secondGate = _connectionGates.GetValue(secondConnection, static _ => new object());
        var firstOrder = GetConnectionGateOrder(firstConnection);
        var secondOrder = GetConnectionGateOrder(secondConnection);
        var outer = firstOrder < secondOrder ? firstGate : secondGate;
        var inner = firstOrder < secondOrder ? secondGate : firstGate;
        lock (outer)
        lock (inner)
        {
            using var scope = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(firstConnection, out var firstHolder) ||
                !_sessions.TryGetValue(secondConnection, out var secondHolder) ||
                !ReferenceEquals(firstHolder.Read(), expectedFirst) || !ReferenceEquals(secondHolder.Read(), expectedSecond) ||
                !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expectedFirst, updatedFirst) ||
                !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expectedSecond, updatedSecond)) return false;
            using var first = new PreparedSessionSpellPublication(this, firstConnection, firstHolder, expectedFirst, updatedFirst);
            using var second = new PreparedSessionSpellPublication(this, secondConnection, secondHolder, expectedSecond, updatedSecond);
            if (first.Binding is not null && second.Binding is not null &&
                ReferenceEquals(first.Binding.Client, second.Binding.Client)) throw InvalidSpellPublication();
            first.ArmFinalProof(); second.ArmFinalProof();
            if (!first.ConsumeBeforeWriterPlain() || !second.ConsumeBeforeWriterPlain()) throw InvalidSpellPublication();
            bool persisted;
            try { persisted = persist(); }
            catch (LegacyCommittedSpellPublicationException)
            {
                FailCommittedPairPublication(firstHolder, first.Binding, secondHolder, second.Binding); throw;
            }
            if (!persisted) return false;
            if (!first.AcceptExternallyPersistedHitpoints() || !second.AcceptExternallyPersistedHitpoints() ||
                !first.IsCurrentPlain() || !second.IsCurrentPlain())
            {
                FailCommittedPairPublication(firstHolder, first.Binding, secondHolder, second.Binding);
            }
            first.PublishNative(); second.PublishNative();
            first.PublishHolder(); second.PublishHolder();
            finalFirst = first.FinalSession; finalSecond = second.FinalSession;
            return true;
        }
    }
    private static void FailCommittedPairPublication(SessionHolder firstHolder, LegacyOrdinarySpellBinding? first,
        SessionHolder secondHolder, LegacyOrdinarySpellBinding? second)
    {
        first?.Recovery.Block("ordinarySpellRecoveryRequired:durablePairPublicationUnproven");
        second?.Recovery.Block("ordinarySpellRecoveryRequired:durablePairPublicationUnproven");
        firstHolder.Clear(); secondHolder.Clear();
        throw new LegacyCommittedSpellPublicationException();
    }

    private sealed record TransactionGateOrder(long Value);
}
