using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Issued only by the concrete Frame whose Store admitted this bound player.
internal sealed partial class LegacyBoundHitpointsScalarWrite
{
    internal enum Outcome { Rejected, Committed, RecoveryRequired }
    private readonly WorldServerFrameHandler owner;
    private readonly WorldSessionStore store;
    private readonly ConquerTcpConnection connection;
    private readonly LegacyOrdinarySpellBinding binding;
    private readonly WorldSession expected;
    private readonly string path;
    private readonly byte[] before, after;
    internal WorldPacketHandlers.BoundHitpointsInputs Inputs { get; }
    internal WorldSession Proposed { get; }
    internal bool IsCommitted { get; private set; }
    private LegacyBoundHitpointsScalarWrite(WorldServerFrameHandler owner, WorldSessionStore store,
        ConquerTcpConnection connection, LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession proposed,
        WorldPacketHandlers.LegacyCapriceCharacterFields? caprice)
    {
        this.owner = owner; this.store = store; this.connection = connection; this.binding = binding; this.expected = expected;
        if (caprice is not null && !caprice.Matches(expected.Character, proposed.Character))
            throw new LegacyNativeHitpointsRefusalException("capricePreparedResourcesInvalid");
        if (!OwnerIsCurrent()) throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsOwnerUnavailable");
        Inputs = new(expected.Character, proposed.Character);
        Proposed = proposed with { Character = Inputs.After };
        (path, before, after) = PrepareImages(binding, expected, Inputs, caprice);
        if (!OwnerIsCurrent()) throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsOwnerChanged");
    }
    internal static LegacyBoundHitpointsScalarWrite Prepare(WorldServerFrameHandler owner, WorldSessionStore store,
        ConquerTcpConnection connection, LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession proposed,
        WorldPacketHandlers.LegacyCapriceCharacterFields? caprice = null) =>
        new(owner, store, connection, binding, expected, proposed, caprice);
    internal byte[] CopyBeforeImage() => before.ToArray();
    internal byte[] CopyAfterImage() => after.ToArray();
    private bool OwnerIsCurrent() => store.IsFramePersistenceCurrent(owner, connection, expected) &&
        ReferenceEquals(binding.Store, store) && ReferenceEquals(binding.Connection, connection) &&
        binding.PhysicalOwnerIsCurrentPlain() && binding.Recovery.IsReadyForUsers(binding.UsersRoot);

    internal Outcome TryCommit(out string reason)
    {
        var failure = "ordinaryHitpointsOwnerChanged"; var phase = Outcome.Rejected;
        try
        {
            var result = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
            {
                if (!OwnerIsCurrent()) return Outcome.Rejected;
                if (LegacyTradeJournal.HasPending(binding.UsersRoot))
                { failure = "ordinaryHitpointsRecoveryRequired:pendingJournal"; return Outcome.RecoveryRequired; }
                if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "ordinaryHitpoints", out var actual, out failure) ||
                    !actual.AsSpan().SequenceEqual(before))
                { failure = "ordinaryHitpointsCharacterBaselineStale"; return Outcome.Rejected; }
                if (!OwnerIsCurrent()) return Outcome.Rejected;
                phase = Outcome.RecoveryRequired;
                if (CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(path, before, after, out var uncertain, out failure))
                { IsCommitted = true; return Outcome.Committed; }
                return uncertain ? Outcome.RecoveryRequired : Outcome.Rejected;
            });
            reason = failure; return result;
        }
        catch (Exception error) when (IsFileFailure(error))
        {
            reason = "ordinaryHitpointsWrite:" + error.GetType().Name;
            if (phase == Outcome.Rejected) return Outcome.Rejected;
            try
            {
                if (CharacterSnapshotFileBaseline.TryReadBytes(path, "ordinaryHitpoints", out var actual, out _))
                {
                    if (actual.AsSpan().SequenceEqual(after)) { IsCommitted = true; reason = string.Empty; return Outcome.Committed; }
                    if (actual.AsSpan().SequenceEqual(before)) return Outcome.Rejected;
                }
            }
            catch (Exception observation) when (IsFileFailure(observation)) { }
            return Outcome.RecoveryRequired;
        }
    }

    // Called only after the existing real Store transaction writer returned true.
    // Its full exact afterimage, not an equal HP scalar, is the publication proof.
    internal bool RequireExternallyCommitted()
    {
        try
        {
            return SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
            {
                if (!OwnerIsCurrent() || LegacyTradeJournal.HasPending(binding.UsersRoot) ||
                    !CharacterSnapshotFileBaseline.TryReadBytes(path, "ordinaryHitpoints", out var actual, out _) ||
                    !actual.AsSpan().SequenceEqual(after)) return false;
                IsCommitted = true; return true;
            });
        }
        catch (Exception error) when (IsFileFailure(error)) { return false; }
    }
    internal bool CoversCommittedPublication(WorldServerFrameHandler actualOwner, WorldSessionStore actualStore,
        ConquerTcpConnection actualConnection, WorldSession? previous, WorldSession current) => IsCommitted &&
        ReferenceEquals(owner, actualOwner) && ReferenceEquals(store, actualStore) && ReferenceEquals(connection, actualConnection) &&
        ReferenceEquals(expected, previous) && ReferenceEquals(Proposed.Character, current.Character) && Inputs.ConsumePostludePlain();
    private static bool IsFileFailure(Exception error) => error is IOException or UnauthorizedAccessException or ArgumentException or
        NotSupportedException or System.Security.SecurityException or FormatException or OverflowException;
}

internal sealed class LegacyNativeHitpointsRefusalException(string reason) : InvalidOperationException(reason) { }
internal sealed class LegacyNativeHitpointsRecoveryRequiredException(string reason) : Exception(reason) { }
