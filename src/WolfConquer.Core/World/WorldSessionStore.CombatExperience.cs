using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryCommitCombatExperience(WorldPacketHandlerContext context, ConquerTcpConnection recipient,
        WorldSession expected, ulong gain, WorldPacketHandlers.OwnedCombatExperience purpose,
        out WorldSession? result, out LegacyClassLevelStatProgressionChange progression, out ActionPublication? receipt,
        out byte[][] frames, out WorldPacketHandlers.PreparedLevelUpRebuild? rebuild, out string refusal)
    {
        RefuseBoundSpellReentry(recipient);
        result = null; progression = default; receipt = null; frames = []; rebuild = null; refusal = "combatExperienceOwnerOrInputUnproven";
        if (!LegacyExperienceContinuationFence.AllowsOrdinaryPublication(expected, expected))
        { refusal = "experienceContinuationPendingRequiresOwner"; return false; }
        if (!purpose.Covers(this, context, recipient, expected, gain) || framePersistenceOwner is not { } frame ||
            context.WorldData is not { } catalog) return false;
        var recipientContext = context with { Connection = recipient, Session = expected };
        if (!frame.TryPrepareActionContext(this, recipientContext, expected, out recipientContext)) return false;
        WorldSession final = null!; object identity = null!; LegacyOrdinarySpellBinding binding = null!; ActionPublication prepared; byte[][] preparedFrames;
        lock (frame.RequireActionLifecycleGate(this))
        {
            if (!WorldPacketHandlers.TryEnterDragonhowlExperienceWriter(this, purpose, context, expected, out var procScope)) return false;
            using var proc = procScope;
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            if (!_connectionGates.TryGetValue(recipient, out var gate)) return false;
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(recipient, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                    holder.SpellBinding is not { } owner || !BindingIsCurrentPlain(owner, holder, expected) ||
                    !frame.IsActionLifecycleCurrent(this, recipientContext) || !owner.Recovery.IsReadyForUsers(owner.UsersRoot) ||
                    !owner.Recovery.TryRequireLiveJournalClear(out _) ||
                    !purpose.Covers(this, context, recipient, expected, gain)) return false;
                using var actors = new SpellActorScope(owner);
                using var teamScope = purpose.Roster?.TryEnter(owner);
                if (purpose.Roster is not null && teamScope is null) return false;
                LegacyCombatExperienceProjection projection;
                LegacyCombatExperienceMetadataWitness metadata;
                LegacyOwnedPhysicalMetadataWitness physical;
                try
                {
                    metadata = new(catalog);
                    physical = LegacyOwnedPhysicalMetadataWitness.CaptureCombatExperience(owner, catalog, expected);
                    if (expected.Character.Level < 140 &&
                        (!catalog.LevelExperienceByTypeAndLevel.TryGetValue((0, checked((int)expected.Character.Level)), out var threshold) ||
                         threshold.Experience <= 0)) return false;
                    projection = LegacyCombatExperienceProjection.Create(expected, catalog, gain);
                    if (projection.After.Level - expected.Character.Level > 1)
                    { refusal = "combatExperienceMultipleLevelsRequireContinuationOwner"; return false; }
                    if (projection.LevelChanged)
                    {
                        if (recipientContext.TradeOperations?.OwnsActiveExclusiveLease(recipientContext.FrameOperationLease) != true)
                        { refusal = "combatExperienceLevelUpExclusiveLeaseUnavailable"; return false; }
                    }
                    projection = projection with { After = new WorldPacketHandlers.BoundHitpointsInputs(
                        expected.Character, projection.After).After };
                    preparedFrames = WorldPacketHandlers.PrepareCombatExperienceFrames(projection);
                    if (projection.After.Level < 140 &&
                        (!catalog.LevelExperienceByTypeAndLevel.TryGetValue((0, checked((int)projection.After.Level)), out var finalThreshold) ||
                         finalThreshold.Experience <= 0)) return false;
                    if (projection.LevelChanged && expected.Character.Reborn == 0 &&
                        projection.After.Level <= (expected.Character.Class is >= 13002 and <= 13057 ? 110 : 120) &&
                        !projection.StatProgression.AppliedSourceStats) return false;
                    physical.Extend(expected with { Character = projection.After });
                }
                catch (Exception error) when (error is InvalidOperationException or InvalidDataException or OverflowException or ArgumentException)
                { return false; }
                var proposed = expected with { Character = projection.After };
                if (!LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, proposed)) return false;
                LegacyNativePlayerRuntime.OwnedHitpointsPublication vitals;
                LegacyNativePlayerRuntime.OwnedActorScalarsPublication scalars;
                WorldPacketHandlers.PreparedLevelUpRebuild? query = null;
                try
                {
                    purpose.Bind(owner, proposed);
                    if (projection.LevelChanged &&
                        (!WorldPacketHandlers.PreparedLevelUpRebuild.TryPrepare(recipientContext, purpose, owner, expected, proposed, out query) || query is null))
                    { refusal = "combatExperienceLevelUpRebuildOwnerUnavailable"; return false; }
                    vitals = LegacyNativePlayerRuntime.OwnedHitpointsPublication.PrepareBound(owner, expected, proposed);
                    scalars = LegacyNativePlayerRuntime.OwnedActorScalarsPublication.PrepareBound(owner, expected, proposed,
                        combatExperience: purpose);
                    prepared = new(this, recipientContext, frame, holder.NativeClientIdentity, owner, proposed,
                        LegacyActionState.RequireCurrent(proposed.Character));
                }
                catch (Exception error) when (error is InvalidOperationException or InvalidDataException or OverflowException or ArgumentException)
                { return false; }
                string path = Path.Combine(owner.UsersRoot, expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
                string fileRefusal = "combatExperienceCharacterImageInvalid";
                string[] paths = query?.LockedSpellPath is { } spellPath ? [path, spellPath] : [path];
                bool published = SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
                {
                byte[] before, after;
                try
                {
                    if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "combatExperience", out before, out _)) return false;
                    var text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
                    if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(before)) return false;
                    var disk = CharacterSnapshotParser.ParseText(text);
                    if (!LegacyCombatCharacterImageInputs.Matches(disk, expected.Character)) return false;
                    after = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(before, projection.After);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or
                    InvalidOperationException or OverflowException or ArgumentException or NotSupportedException or System.Security.SecurityException)
                { return false; }
                query?.BindCommitted(proposed);
                if (teamScope is not null && !teamScope.IsCurrentPlain() || query is not null && !query.IsCurrentPlain() ||
                    !metadata.IsCurrentPlain() || !physical.IsCurrentPlain() || !purpose.IsCurrentPlain() ||
                    !vitals.IsCurrentPlain() || !scalars.IsCurrentPlain() || !frame.IsActionLifecycleCurrent(this, recipientContext)) return false;
                bool saved; bool uncertain; string reason;
                try { saved = CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(path, before, after, out uncertain, out reason); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
                { saved = false; uncertain = true; reason = error.GetType().Name; }
                if (!saved)
                {
                    if (!uncertain) { fileRefusal = "combatExperienceCharacterReplace:" + reason; return false; }
                    FenceAttributeOutcome(holder, owner, new InvalidOperationException("combatExperienceOutcomeUnproven:" + reason));
                    throw new InvalidOperationException("combatExperienceOutcomeUnproven");
                }
                try
                {
                    if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "combatExperienceCommitted", out var savedImage, out _) ||
                        !savedImage.AsSpan().SequenceEqual(after) || teamScope is not null && !teamScope.IsCurrentPlain() ||
                        query is not null && !query.IsCurrentPlain() || !metadata.IsCurrentPlain() || !physical.IsCurrentPlain() || !purpose.IsCurrentPlain() ||
                        !vitals.IsCurrentPlain() || !scalars.IsCurrentPlain() || !frame.IsActionLifecycleCurrent(this, recipientContext))
                        throw new InvalidOperationException("combatExperienceCommittedOwnerChanged");
                    vitals.PublishPrepared(); scalars.PublishPrepared();
                    identity = holder.NativeClientIdentity; binding = owner; final = proposed;
                    holder.PublishPrepared(final, identity);
                    prepared.ConfirmCommitted();
                    WorldPacketHandlers.ConfirmDragonhowlExperiencePublication(this, purpose, expected, final, prepared,
                        continuationCredit: false);
                }
                catch (Exception error) { FenceAttributeOutcome(holder, owner, error); throw; }
                return true;
                });
                if (!published) { refusal = fileRefusal; return false; }
                rebuild = query;
                progression = projection.StatProgression;
            }
        }
        try { NotifyPublishedSessionChange(recipient, expected, final, identity, binding); }
        catch (Exception error)
        {
            prepared.FenceCommittedAttributeFailure(error);
            throw;
        }
        result = final; receipt = prepared; frames = preparedFrames; refusal = string.Empty;
        return true;
    }
}
