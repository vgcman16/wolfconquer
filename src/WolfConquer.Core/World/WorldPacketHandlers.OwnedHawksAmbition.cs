using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed class OwnedHawksAmbitionCast
    {
        internal bool WasCommitted => OwnedHawksCasts.TryGetValue(this, out var data) && data.Consumed;
        internal bool TryReadPreparedProposal(LegacyOrdinarySpellBinding binding, WorldSession expected,
            out WorldSession proposed, out uint power, out DateTimeOffset expiresAtUtc)
        {
            proposed = null!; power = 0; expiresAtUtc = default;
            if (!OwnedHawksCasts.TryGetValue(this, out var data) || !data.IsCurrentPlain(binding, expected)) return false;
            proposed = data.Proposed; power = data.Metadata.Power; expiresAtUtc = data.ExpiresAtUtc; return true;
        }

        internal bool IsCurrentPlain(LegacyOrdinarySpellBinding binding, WorldSession expected) =>
            OwnedHawksCasts.TryGetValue(this, out var data) && data.IsCurrentPlain(binding, expected);

        internal bool MatchesPreparedProjection(LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession proposed) =>
            OwnedHawksCasts.TryGetValue(this, out var data) && data.IsCurrentPlain(binding, expected) && data.MatchesProjection(proposed);

        // Only the actual prepared Store participant invokes this after its
        // successful complete proof. A forged empty token has no table entry.
        internal void PublishConsumedPlain()
        {
            if (OwnedHawksCasts.TryGetValue(this, out var data)) data.Consumed = true;
        }
    }

    private static readonly ConditionalWeakTable<OwnedHawksAmbitionCast, OwnedHawksCastData> OwnedHawksCasts = new();
    private sealed record OwnedHawksCastDraft(LegacyOrdinarySpellBinding Binding,
        LegacyOwnedHawksAmbitionMetadata Metadata, PlayerSpellSnapshotEntry Learned, TimeProvider Clock);

    private static bool TryPrepareOwnedHawksAmbitionCast(WorldPacketHandlerContext context, WorldSession expected,
        PlayerSpellSnapshotEntry learned, LegacyMagicRuntimeSpell spell, out OwnedHawksCastDraft? draft)
    {
        draft = null;
        if (context.Sessions is not { } store || context.WorldData is not { } catalog ||
            context.Connection.NativeGameClient?.OrdinarySpellBinding is not { } binding ||
            !ReferenceEquals(binding.Store, store) || !ReferenceEquals(binding.ItemCatalog, catalog)) return false;
        try
        {
            var metadata = new LegacyOwnedHawksAmbitionMetadata(catalog, learned.Level, spell);
            var attributes = new LegacyNativeAttributeMapProof(expected.Character.Attributes);
            if ((LegacyWindWalkerStatusProjection.Read(expected.Character with { Attributes = attributes.Fields })[8] & 1UL) != 0)
                return false; // Already active is refused before costs or output.
            var book = new LegacyPerformerSpellSource(expected.PlayerSpells.Spells);
            if (learned.Id != LegacyHawksAmbitionSpellId || !book.Rows.Any(row => row == learned) || !book.IsCurrent() ||
                !store.CanPrepareOwnedHawksAmbitionCast(context, expected, binding, metadata)) return false;
            var clock = store.RequireOwnedHawksAmbitionClock(binding);
            _ = clock.GetUtcNow().AddSeconds(metadata.Duration); // Bounds before resource admission.
            if (!store.IsOwnedHawksAmbitionClockCurrentPlain(binding, clock)) return false;
            draft = new(binding, metadata, learned, clock); return true;
        }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException or ArgumentException or OverflowException or FormatException)
        { return false; }
    }

    private static bool TryIssueOwnedHawksAmbitionCast(OwnedHawksCastDraft draft, WorldSession expected, WorldSession costCandidate,
        LegacyMagicRuntimeSpell spell, out OwnedHawksAmbitionCast? token)
    {
        token = null;
        var book = new LegacyPerformerSpellSource(expected.PlayerSpells.Spells);
        if (!book.Rows.Any(row => row == draft.Learned) || !book.IsCurrent() ||
            !draft.Metadata.IsCurrentPlain(draft.Binding.ItemCatalog) ||
            !draft.Binding.Store.IsOwnedHawksAmbitionClockCurrentPlain(draft.Binding, draft.Clock)) return false;
        var now = draft.Clock.GetUtcNow();
        var proposed = ApplyLegacyHawksAmbitionStatus(costCandidate,
            spell with { Damage = draft.Metadata.Power, Duration = draft.Metadata.Duration }, now);
        var issued = new OwnedHawksAmbitionCast();
        OwnedHawksCasts.Add(issued, new(draft, expected, proposed, book, now.AddSeconds(draft.Metadata.Duration)));
        token = issued; return true;
    }

    private sealed record OwnedHawksAdmission(WorldSession Session, WorldSessionStore.OwnedHawksOutputReceipt Output);

    private static async ValueTask<OwnedHawksAdmission?> TryPrepareOwnedHawksAmbitionAdmissionAsync(
        WorldPacketHandlerContext context, WorldSession session, PlayerSpellSnapshotEntry learned,
        LegacyMagicRuntimeSpell spell, CancellationToken cancellationToken)
    {
        if (!TryCaptureLegacyRootAdmissionOwner(context, session, null, out var owner) || owner.Store is null || owner.Identity is null)
            return null;
        session = owner.Expected;
        if (!TryPrepareOwnedHawksAmbitionCast(context, session, learned, spell, out var draft) || draft is null ||
            !TryValidateLegacyXpAdmissionPrerequisites(session, spell, out _) ||
            !LegacyRuneCastEligibility.TryCapture(context, session, spell, out var runeEligibility)) return null;
        owner.RuneEligibility = runeEligibility;
        LegacyGenericMagicAdmissionPlan plan;
        OwnedHawksAmbitionCast? cast;
        IReadOnlyList<byte[]>[] costPhases;
        try
        {
            if (!TryPrepareLegacyGenericMagicPlan(context, session, spell, ignoreAttackStamp: false,
                    draft.Clock.GetUtcNow(), out plan, out _) ||
                !TryIssueOwnedHawksAmbitionCast(draft, session, plan.Candidate, spell, out cast) || cast is null) return null;
            costPhases = plan.Frames.Length == 0 ? [Array.Empty<byte[]>()] :
                plan.Frames.Select(frame => (IReadOnlyList<byte[]>)new[] { frame }).ToArray();
        }
        catch (Exception error) when (IsLegacyRootPreparationFailure(error) || error is InvalidOperationException)
        { return null; }
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            owner.CommitAttempted = true;
            owner.Committed = owner.Store.TryCommitOwnedHawksAmbitionCosts(context, session, owner.Identity, cast,
                runeEligibility, out var receipt, out var output);
            if (!owner.Committed || receipt is null || output is null) return null;
            owner.CostOutput = receipt;
            session = receipt.Published;
            if (!IsLegacyRootCommittedRecordCurrent(owner, session)) return null;
            foreach (var phase in costPhases)
            {
                output.RequireCurrent(context);
                var current = await PublishLegacyRootAcceptedCostsAsync(context, owner, session, phase, cancellationToken).ConfigureAwait(false);
                if (current is null) return null;
                session = current;
                output.RequireCurrent(context);
            }
            return new(session, output);
        }
        catch (Exception error) when (owner.CommitAttempted || owner.Committed)
        {
            await ReportLegacyGenericAdmissionFaultAsync(context, owner, error).ConfigureAwait(false);
            throw;
        }
    }

    private sealed class OwnedHawksCastData
    {
        internal readonly LegacyOwnedHawksAmbitionMetadata Metadata;
        internal readonly WorldSession Expected, Proposed;
        internal readonly DateTimeOffset ExpiresAtUtc;
        internal bool Consumed;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyPerformerSpellSource book;
        private readonly TimeProvider clock;
        private readonly LegacyNativeAttributeMapProof beforeAttributes, afterAttributes;
        internal OwnedHawksCastData(OwnedHawksCastDraft draft, WorldSession expected, WorldSession proposed,
            LegacyPerformerSpellSource book, DateTimeOffset expiresAtUtc)
        {
            binding = draft.Binding; Metadata = draft.Metadata; Expected = expected;
            Proposed = proposed with { Character = new BoundHitpointsInputs(expected.Character, proposed.Character).After };
            this.book = book; ExpiresAtUtc = expiresAtUtc; clock = draft.Clock;
            beforeAttributes = new(expected.Character.Attributes); afterAttributes = new(Proposed.Character.Attributes);
            if ((LegacyWindWalkerStatusProjection.Read(expected.Character with { Attributes = beforeAttributes.Fields })[8] & 1UL) != 0)
                throw new InvalidOperationException("ownedHawksAmbitionAlreadyActive");
        }
        internal bool IsCurrentPlain(LegacyOrdinarySpellBinding actual, WorldSession expected) => !Consumed &&
            ReferenceEquals(binding, actual) && ReferenceEquals(expected, Expected) && book.IsCurrent() &&
            beforeAttributes.IsCurrentPlain() && afterAttributes.IsCurrentPlain() && Metadata.IsCurrentPlain(binding.ItemCatalog) &&
            WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate && Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) &&
            Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
            binding.Store.IsSpellFileWriteOwnerCurrent(binding.Connection, expected, binding.NativeIdentity, binding) &&
            binding.Store.IsOwnedHawksAmbitionClockCurrentPlain(binding, clock) &&
            binding.Store.IsOwnedPhysicalMetadataCatalogCurrent(binding, binding.ItemCatalog);

        // Potential serializer/record equality work is preparation only. The
        // generic participant then pins its canonical full after attributes.
        internal bool MatchesProjection(WorldSession target)
        {
            var attributes = new LegacyNativeAttributeMapProof(target.Character.Attributes).Fields;
            var paid = new LegacyNativeAttributeMapProof(target.Character.PaidNobility).Fields;
            if (!SameFields(attributes, afterAttributes.Fields) ||
                !SameFields(paid, new LegacyNativeAttributeMapProof(Proposed.Character.PaidNobility).Fields) ||
                !(target.Character with { Attributes = Proposed.Character.Attributes, PaidNobility = Proposed.Character.PaidNobility }).Equals(Proposed.Character) ||
                !PlayerItemSnapshotLoader.ToLegacyBytes(target.PlayerItems).AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(Proposed.PlayerItems)) ||
                !PlayerSpellSnapshotLoader.ToLegacyBytes(target.PlayerSpells).AsSpan().SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(Proposed.PlayerSpells)) ||
                !PlayerProficiencySnapshotLoader.ToLegacyBytes(target.PlayerProficiencies).AsSpan().SequenceEqual(PlayerProficiencySnapshotLoader.ToLegacyBytes(Proposed.PlayerProficiencies)))
                return false;
            return (target with { Character = Proposed.Character, PlayerItems = Proposed.PlayerItems,
                PlayerSpells = Proposed.PlayerSpells, PlayerProficiencies = Proposed.PlayerProficiencies }).Equals(Proposed);
        }
        private static bool SameFields(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right) =>
            left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }
}
