using System.Globalization;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private static readonly ConditionalWeakTable<ExperienceCreditReceipt, object> IssuedExperienceCredits = new();
    // Actual combat issuance only. This is deliberately not wired into the
    // public combat route until pending-marker fences and resume are joined.
    // The durable credit and its marker share one exact Character replacement;
    // the result sends no level/experience success output.
    internal bool TryCreditExperienceContinuation(WorldPacketHandlerContext context, ConquerTcpConnection recipient,
        WorldSession expected, ulong gain, WorldPacketHandlers.OwnedCombatExperience purpose,
        out WorldSession? credited, out LegacyExperienceContinuation? state, out ExperienceCreditReceipt? receipt, out string reason)
    {
        credited = null; state = null; receipt = null; reason = "experienceCreditOwnerUnavailable";
        RefuseBoundSpellReentry(recipient);
        if (!purpose.Covers(this, context, recipient, expected, gain) || framePersistenceOwner is not { } frame ||
            context.WorldData is not { } catalog) return false;
        var targetContext = context with { Connection = recipient, Session = expected };
        if (!frame.TryPrepareActionContext(this, targetContext, expected, out targetContext) ||
            targetContext.TradeOperations?.OwnsActiveExclusiveLease(targetContext.FrameOperationLease) != true) return false;

        WorldSession final = null!; LegacyExperienceContinuation savedState = null!;
        object identity = null!; LegacyOrdinarySpellBinding binding = null!; ActionPublication published = null!;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            if (!_connectionGates.TryGetValue(recipient, out var gate)) return false;
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(recipient, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                    holder.SpellBinding is not { } owner || !BindingIsCurrentPlain(owner, holder, expected) ||
                    !frame.IsActionLifecycleCurrent(this, targetContext) || !owner.Recovery.IsReadyForUsers(owner.UsersRoot) ||
                    !owner.Recovery.TryRequireLiveJournalClear(out _) || !purpose.Covers(this, context, recipient, expected, gain)) return false;
                using var actors = new SpellActorScope(owner);
                using var team = purpose.Roster?.TryEnter(owner);
                if (purpose.Roster is not null && team is null) return false;
                if (expected.Character.Level is < 1 or >= 140 || expected.Character.Experience < 0 || gain == 0 || gain > long.MaxValue ||
                    expected.Character.Attributes.Keys.Any(key => key.Equals(LegacyExperienceContinuationCodec.Key, StringComparison.OrdinalIgnoreCase))) return false;

                LegacyCombatExperienceMetadataWitness metadata;
                LegacyOwnedPhysicalMetadataWitness physical;
                LegacyExperienceContinuation credit;
                WorldSession proposed;
                try
                {
                    metadata = new(catalog);
                    physical = LegacyOwnedPhysicalMetadataWitness.CaptureCombatExperience(owner, catalog, expected);
                    if (!catalog.LevelExperienceByTypeAndLevel.TryGetValue((0, checked((int)expected.Character.Level)), out var threshold) ||
                        threshold.Experience <= 0) return false;
                    credit = LegacyExperienceContinuation.CreateCredit(expected.AccountId, checked((uint)expected.Character.Uid),
                        Guid.NewGuid(), checked((ushort)expected.Character.Level), checked((ulong)expected.Character.Experience), gain);
                    if (credit.RemainingExperience < checked((ulong)threshold.Experience)) return false; // no-level path is separate
                    var character = expected.Character with { Experience = checked((long)credit.RemainingExperience) };
                    character = LegacyExperienceContinuationCodec.WithState(character, expected.AccountId, credit);
                    // The durable image derives canonical scalar lines from
                    // the typed snapshot. Keep the actual holder's owned raw
                    // fields in the same projection before publishing it.
                    character = new WorldPacketHandlers.BoundHitpointsInputs(expected.Character, character).After;
                    proposed = expected with { Character = character };
                    physical.Extend(proposed);
                    if (!LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, proposed)) return false;
                }
                catch (Exception error) when (error is InvalidOperationException or InvalidDataException or OverflowException or ArgumentException)
                { return false; }

                LegacyNativePlayerRuntime.OwnedActorScalarsPublication scalars;
                try
                {
                    purpose.Bind(owner, proposed);
                    scalars = LegacyNativePlayerRuntime.OwnedActorScalarsPublication.PrepareBound(owner, expected, proposed,
                        combatExperience: purpose);
                    published = new(this, targetContext, frame, holder.NativeClientIdentity, owner, proposed,
                        LegacyActionState.RequireCurrent(proposed.Character));
                }
                catch (Exception error) when (error is InvalidOperationException or InvalidDataException or OverflowException or ArgumentException)
                { return false; }

                string path = Path.Combine(owner.UsersRoot, expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
                string fileReason = "experienceCreditCharacterImageInvalid";
                bool committed = CharacterSnapshotFileCoordinator.ExecuteLocked(path, () =>
                {
                    byte[] before, after;
                    try
                    {
                        if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceCredit", out before, out _) ||
                            LegacyExperienceContinuationCodec.ReadCharacterImage(before, expected.AccountId, checked((uint)expected.Character.Uid)) is not null)
                            return false;
                        var text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
                        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(before)) return false;
                        var disk = CharacterSnapshotParser.ParseText(text);
                        if (!LegacyCombatCharacterImageInputs.Matches(disk, expected.Character)) return false;
                        var scalarImage = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(before, proposed.Character);
                        after = LegacyExperienceContinuationCodec.ProjectCharacterImage(before, scalarImage,
                            expected.AccountId, checked((uint)expected.Character.Uid), null, credit);
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or
                        InvalidOperationException or OverflowException or ArgumentException or NotSupportedException or System.Security.SecurityException)
                    { return false; }
                    if (team is not null && !team.IsCurrentPlain() || !metadata.IsCurrentPlain() || !physical.IsCurrentPlain() ||
                        !purpose.IsCurrentPlain() || !scalars.IsCurrentPlain() || !frame.IsActionLifecycleCurrent(this, targetContext)) return false;
                    bool saved; bool uncertain; string failure;
                    try
                    {
                        var permit = IssueContinuationFilePermit(this, targetContext, owner, expected, path, before, after, credit: purpose);
                        saved = CharacterSnapshotFileCoordinator.TryReplaceContinuationCapturedImage(path, before, after, permit, out uncertain, out failure);
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
                    { saved = false; uncertain = true; failure = error.GetType().Name; }
                    if (!saved)
                    {
                        if (!uncertain) { fileReason = "experienceCreditReplace:" + failure; return false; }
                        FenceContinuationOutcome(holder, owner, new InvalidOperationException("experienceCreditOutcomeUnproven:" + failure));
                        throw new InvalidOperationException("experienceCreditOutcomeUnproven");
                    }
                    try
                    {
                        if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceCreditCommitted", out var observed, out _) ||
                            !observed.AsSpan().SequenceEqual(after) || team is not null && !team.IsCurrentPlain() ||
                            !metadata.IsCurrentPlain() || !physical.IsCurrentPlain() || !purpose.IsCurrentPlain() ||
                            !scalars.IsCurrentPlain() || !frame.IsActionLifecycleCurrent(this, targetContext))
                            throw new InvalidOperationException("experienceCreditCommittedOwnerChanged");
                        scalars.PublishPrepared();
                        identity = holder.NativeClientIdentity; binding = owner; final = proposed; savedState = credit;
                        holder.PublishPrepared(final, identity); published.ConfirmCommitted();
                        BeginCommittedContinuationRetirement(targetContext, owner, final, after);
                    }
                    catch (Exception error) { FenceContinuationOutcome(holder, owner, error); throw; }
                    return true;
                });
                if (!committed) { reason = fileReason; return false; }
            }
        }
        try { NotifyPublishedSessionChange(recipient, expected, final, identity, binding); }
        catch (Exception error) { published.FenceCommittedLevelUpFailure(error); throw; }
        credited = final; state = savedState;
        receipt = new ExperienceCreditReceipt(this, context, recipient, expected, gain, purpose, final, savedState, published);
        IssuedExperienceCredits.Add(receipt, new object());
        reason = string.Empty;
        return true;
    }

    // A real persisted credit's private receipt is a prerequisite to any
    // immediate continuation. A generic ActionPublication or a marker alone
    // cannot be converted into one.
    internal sealed class ExperienceCreditReceipt
    {
        private readonly WorldSessionStore store;
        private readonly WorldPacketHandlerContext context;
        private readonly ConquerTcpConnection recipient;
        private readonly WorldSession before;
        private readonly ulong gain;
        private readonly WorldPacketHandlers.OwnedCombatExperience purpose;
        private readonly WorldSession credited;
        private readonly LegacyExperienceContinuation state;
        private readonly ActionPublication publication;

        internal ExperienceCreditReceipt(WorldSessionStore store, WorldPacketHandlerContext context,
            ConquerTcpConnection recipient, WorldSession before, ulong gain,
            WorldPacketHandlers.OwnedCombatExperience purpose, WorldSession credited,
            LegacyExperienceContinuation state, ActionPublication publication)
        {
            this.store = store; this.context = context; this.recipient = recipient; this.before = before;
            this.gain = gain; this.purpose = purpose; this.credited = credited; this.state = state; this.publication = publication;
        }

        internal bool TryCurrent(out WorldSession current, out LegacyExperienceContinuation? pending)
        {
            current = null!; pending = null;
            try
            {
                if (!IssuedExperienceCredits.TryGetValue(this, out _) || !purpose.Covers(store, context, recipient, before, gain) ||
                    !publication.TryCurrent(out var actual) || !ReferenceEquals(actual, credited) ||
                    credited.UsersRoot is not { } usersRoot) return false;
                string path = Path.Combine(usersRoot, credited.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
                if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceCreditReceipt", out var image, out _) ||
                    !Equals(state, LegacyExperienceContinuationCodec.ReadCharacterImage(image, credited.AccountId,
                        checked((uint)credited.Character.Uid))) ||
                    !Equals(state, LegacyExperienceContinuationCodec.ReadCharacter(credited.Character, credited.AccountId))) return false;
                current = actual; pending = state; return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or
                InvalidOperationException or OverflowException or ArgumentException or NotSupportedException or System.Security.SecurityException)
            { return false; }
        }

        internal void FenceFailure(Exception error) => publication.FenceCommittedLevelUpFailure(error);
    }
}
