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
    private readonly LegacyPreparedSpellFileWrite.RuneCharacterPublication? runePublication;
    private readonly LegacyPreparedPairMovementWrite.CharacterPublication? pairMovementPublication;
    private readonly LegacyPreparedMovementWrite.CharacterPublication? movementPublication;
    private readonly WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation? basicPhysical;
    private readonly WorldSessionStore.OwnedDragonhowlDeath? dragonhowlDeath;
    private readonly LegacyPreparedBasicPhysicalWrite.CharacterPublication? physicalDeathPublication;
    private readonly LegacyPreparedTeleportDestinationWrite.CharacterPublication? teleportPublication;
    private readonly WorldPacketHandlers.OwnedFullAccountClassSpellRetirement? classRetirement;
    private readonly WorldPacketHandlers.OwnedContinuationQueryMutation? continuationQuery;
    internal WorldPacketHandlers.BoundHitpointsInputs Inputs { get; }
    internal WorldSession Proposed { get; }
    internal bool IsCommitted { get; private set; }
    private LegacyBoundHitpointsScalarWrite(WorldServerFrameHandler owner, WorldSessionStore store,
        ConquerTcpConnection connection, LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession proposed,
        WorldPacketHandlers.LegacyCapriceCharacterFields? caprice,
        LegacyPreparedSpellFileWrite.RuneCharacterPublication? runePublication,
        LegacyPreparedPairMovementWrite.CharacterPublication? pairMovementPublication,
        LegacyPreparedMovementWrite.CharacterPublication? movementPublication,
        WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation? basicPhysical,
        LegacyPreparedBasicPhysicalWrite.CharacterPublication? physicalDeathPublication,
        LegacyPreparedTeleportDestinationWrite.CharacterPublication? teleportPublication,
        WorldPacketHandlers.OwnedFullAccountClassSpellRetirement? classRetirement,
        WorldPacketHandlers.OwnedContinuationQueryMutation? continuationQuery,
        WorldSessionStore.OwnedDragonhowlDeath? dragonhowlDeath)
    {
        this.owner = owner; this.store = store; this.connection = connection; this.binding = binding; this.expected = expected;
        this.runePublication = runePublication;
        this.pairMovementPublication = pairMovementPublication;
        this.movementPublication = movementPublication;
        this.basicPhysical = basicPhysical; this.physicalDeathPublication = physicalDeathPublication;
        this.dragonhowlDeath = dragonhowlDeath;
        this.teleportPublication = teleportPublication;
        this.classRetirement = classRetirement;
        this.continuationQuery = continuationQuery;
        if (basicPhysical is not null && dragonhowlDeath is not null)
            throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsConflictingPhysicalPurposes");
        if (continuationQuery is not null && (caprice is not null || runePublication is not null ||
            pairMovementPublication is not null || movementPublication is not null || basicPhysical is not null ||
            physicalDeathPublication is not null || teleportPublication is not null || classRetirement is not null ||
            !ReferenceEquals(continuationQuery.Expected, expected) || !ReferenceEquals(continuationQuery.Proposed, proposed)))
            throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsForeignContinuationQueryPurpose");
        if (classRetirement is not null)
        {
            if (caprice is not null || runePublication is not null || pairMovementPublication is not null || movementPublication is not null ||
                basicPhysical is not null || physicalDeathPublication is not null || teleportPublication is not null)
                throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsForeignClassRetirementPurpose");
            classRetirement.RequireCurrent(store, connection, expected, proposed);
        }
        if (teleportPublication is not null)
        {
            if (caprice is not null || runePublication is not null || pairMovementPublication is not null || movementPublication is not null ||
                basicPhysical is not null || physicalDeathPublication is not null) throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsForeignTeleportPurpose");
            teleportPublication.RequireCurrent(store, connection, expected, proposed);
        }
        if (basicPhysical is not null)
        {
            if (caprice is not null || runePublication is not null || pairMovementPublication is not null || movementPublication is not null ||
                !ReferenceEquals(basicPhysical.ExpectedTarget, expected) || !ReferenceEquals(basicPhysical.TargetBinding, binding) ||
                basicPhysical.Death is not null && physicalDeathPublication is null ||
                !basicPhysical.IsCurrentPlain() || !basicPhysical.CanonicalOutcomesMatch(basicPhysical.Actor, proposed))
                throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsForeignBasicPhysicalPurpose");
            physicalDeathPublication?.RequireCurrent(store, connection, expected, proposed);
        }
        else if (dragonhowlDeath is not null)
        {
            if (caprice is not null || runePublication is not null || pairMovementPublication is not null || movementPublication is not null ||
                teleportPublication is not null || classRetirement is not null || continuationQuery is not null ||
                physicalDeathPublication is null || !ReferenceEquals(dragonhowlDeath.ExpectedTarget, expected) ||
                !ReferenceEquals(dragonhowlDeath.TargetBinding, binding) || !dragonhowlDeath.IsCurrentPlain() ||
                !dragonhowlDeath.Death.MatchesDragonhowlCanonical(proposed))
                throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsForeignDragonhowlDeathPurpose");
            physicalDeathPublication.RequireCurrent(store, connection, expected, proposed);
        }
        else if (physicalDeathPublication is not null)
            throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsDeathPurposeMissing");
        if (movementPublication is not null)
        {
            if (caprice is not null || runePublication is not null || pairMovementPublication is not null)
                throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsForeignMovementPublication");
            movementPublication.RequireCurrent(store, connection, expected, proposed);
        }
        if (pairMovementPublication is not null)
        {
            if (caprice is not null || runePublication is not null)
                throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsForeignPairPublication");
            pairMovementPublication.RequireCurrent(store, connection, expected, proposed);
        }
        if (runePublication is not null)
        {
            if (caprice is not null) throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsForeignRunePublication");
            runePublication.RequireCurrent(store, connection, expected, proposed);
        }
        if (caprice is not null && !caprice.Matches(expected.Character, proposed.Character))
            throw new LegacyNativeHitpointsRefusalException("capricePreparedResourcesInvalid");
        if (!OwnerIsCurrent()) throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsOwnerUnavailable");
        Inputs = new(expected.Character, proposed.Character);
        Proposed = proposed with { Character = Inputs.After };
        (path, before, after) = PrepareImages(binding, expected, Inputs, caprice, runePublication, pairMovementPublication,
            movementPublication, physicalDeathPublication, teleportPublication, classRetirement);
        if (!OwnerIsCurrent()) throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsOwnerChanged");
    }
    internal static LegacyBoundHitpointsScalarWrite Prepare(WorldServerFrameHandler owner, WorldSessionStore store,
        ConquerTcpConnection connection, LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession proposed,
        WorldPacketHandlers.LegacyCapriceCharacterFields? caprice = null,
        LegacyPreparedSpellFileWrite.RuneCharacterPublication? runePublication = null,
        LegacyPreparedPairMovementWrite.CharacterPublication? pairMovementPublication = null,
        LegacyPreparedMovementWrite.CharacterPublication? movementPublication = null,
        WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation? basicPhysical = null,
        LegacyPreparedBasicPhysicalWrite.CharacterPublication? physicalDeathPublication = null,
        LegacyPreparedTeleportDestinationWrite.CharacterPublication? teleportPublication = null,
        WorldPacketHandlers.OwnedFullAccountClassSpellRetirement? classRetirement = null,
        WorldPacketHandlers.OwnedContinuationQueryMutation? continuationQuery = null,
        WorldSessionStore.OwnedDragonhowlDeath? dragonhowlDeath = null) =>
        new(owner, store, connection, binding, expected, proposed, caprice, runePublication, pairMovementPublication, movementPublication,
            basicPhysical, physicalDeathPublication, teleportPublication, classRetirement, continuationQuery, dragonhowlDeath);
    internal byte[] CopyBeforeImage() => before.ToArray();
    internal byte[] CopyAfterImage() => after.ToArray();
    private bool OwnerIsCurrent() => store.IsFramePersistenceCurrent(owner, connection, expected) &&
        ReferenceEquals(binding.Store, store) && ReferenceEquals(binding.Connection, connection) &&
        binding.PhysicalOwnerIsCurrentPlain() && binding.Recovery.IsReadyForUsers(binding.UsersRoot) &&
        (basicPhysical is null || basicPhysical.IsCurrentPlain()) && (dragonhowlDeath is null || dragonhowlDeath.IsCurrentPlain()) &&
        (classRetirement is null || classRetirement.IsCurrentPlain()) &&
        (continuationQuery is null || continuationQuery.IsCurrentPlain(binding) || continuationQuery.IsConsumedCurrentPlain(binding));

    internal Outcome TryCommit(out string reason)
    {
        if (runePublication is not null)
        { reason = "ordinaryHitpointsRuneRequiresJointWriter"; return Outcome.Rejected; }
        if (pairMovementPublication is not null)
        { reason = "ordinaryHitpointsPairRequiresJointWriter"; return Outcome.Rejected; }
        if (movementPublication is not null)
        { reason = "ordinaryHitpointsMovementRequiresActualWriter"; return Outcome.Rejected; }
        if (physicalDeathPublication is not null)
        { reason = "ordinaryHitpointsDeathRequiresJointWriter"; return Outcome.Rejected; }
        if (teleportPublication is not null)
        { reason = "ordinaryHitpointsTeleportRequiresDestinationWriter"; return Outcome.Rejected; }
        if (classRetirement is not null)
        { reason = "ordinaryHitpointsClassRetirementRequiresJointWriter"; return Outcome.Rejected; }
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
                bool saved; bool uncertain;
                if (continuationQuery is null)
                    saved = CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(path, before, after, out uncertain, out failure);
                else
                {
                    var permit = store.IssueContinuationQueryFilePermit(continuationQuery, binding, expected, path, before, after);
                    saved = CharacterSnapshotFileCoordinator.TryReplaceContinuationCapturedImage(path, before, after, permit, out uncertain, out failure);
                }
                if (saved)
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
            // The pair capability takes the complete ordered two-file lock set.
            // Never acquire it while holding only this role's Character lock.
            if (pairMovementPublication is not null)
            {
                if (!OwnerIsCurrent() || !pairMovementPublication.RequireCommittedCharacter()) return false;
                IsCommitted = true; return true;
            }
            if (movementPublication is not null)
            {
                if (!OwnerIsCurrent() || !movementPublication.RequireCommittedCharacter()) return false;
                IsCommitted = true; return true;
            }
            if (physicalDeathPublication is not null)
            {
                if (!OwnerIsCurrent() || !physicalDeathPublication.RequireCommittedCharacter()) return false;
                IsCommitted = true; return true;
            }
            if (teleportPublication is not null)
            {
                if (!OwnerIsCurrent() || !teleportPublication.RequireCommittedCharacter()) return false;
                IsCommitted = true; return true;
            }
            if (classRetirement is not null)
            {
                if (!OwnerIsCurrent() || !classRetirement.RequireCommittedPair()) return false;
                IsCommitted = true; return true;
            }
            return SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
            {
                if (!OwnerIsCurrent() || runePublication is not null && !runePublication.RequireCommittedCharacter() ||
                    LegacyTradeJournal.HasPending(binding.UsersRoot) ||
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
