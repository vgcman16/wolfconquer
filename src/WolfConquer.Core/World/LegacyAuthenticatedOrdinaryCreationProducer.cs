using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal enum LegacyAuthenticatedCreationInterruption { None, AfterIntent, AfterAllocation, AfterGameCommit }

// The private constructor consumes an actual synchronous Runtime creation
// borrow. A recipe/receipt by itself cannot construct this live producer.
internal sealed partial class LegacyAuthenticatedOrdinaryCreationProducer
{
    private readonly LegacyOwnedAdmissionRuntime.CreationLease lease;
    internal LegacyAuthenticatedOrdinaryCreationRecipe Recipe { get; }
    internal LegacyItemUidReservationRequest Request { get; }
    internal LegacyOrdinaryAdmissionUidOwner Ordinary => lease.Ordinary;
    internal uint Account => lease.AccountId;
    private readonly byte[] payload;
    private readonly CharacterSnapshot preparedCharacter;
    private LegacyItemUidProducerIntentSnapshot? intent;
    private LegacyTradeJournalChange[]? changes;
    private string? transaction;
    private bool issued, issuanceUncertain, complete;

    private LegacyAuthenticatedOrdinaryCreationProducer(LegacyOwnedAdmissionRuntime.CreationLease lease,
        LegacyAuthenticatedCharacterCreationProjection projection)
    {
        this.lease = lease;
        lease.RequireCurrentBefore();
        if (!projection.IsCurrent()) throw new InvalidDataException("Creation catalog projection is no longer current.");
        Recipe = new(Account, lease.Username, lease.TicketDigest, lease.Configuration.Roots,
            lease.Configuration.MailboxRoot, projection);
        payload = Recipe.Encode(); Request = Recipe.CreateRequest();
        preparedCharacter = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(
            projection.CopyCharacterImage(), out _));
    }

    internal static bool TryCommit(LegacyOwnedAdmissionRuntime.CreationLease lease,
        LegacyAuthenticatedCharacterCreationProjection projection, LegacyJournalInterruption? interruption,
        LegacyAuthenticatedCreationInterruption producerInterruption, CancellationToken cancellationToken,
        out CharacterSnapshot? character, out string reason)
    {
        character = null; reason = "authenticatedCreationRefused";
        LegacyAuthenticatedOrdinaryCreationProducer? producer = null;
        try
        {
            if (!Enum.IsDefined(producerInterruption)) return false;
            producer = new(lease, projection);
            cancellationToken.ThrowIfCancellationRequested();
            lease.NameClaim.RequireProjectedCharacterFits(projection.CopyCharacterImage());
            lease.Registry.RequireAuthenticatedCreationPreflight(lease, producer.Request, producer.payload);
            producer.RequireRawBeforeIssuance();
            cancellationToken.ThrowIfCancellationRequested();
            if (!lease.Registry.TryPrepareProducerIntent(lease.Configuration.ExpectedDomain, producer.Request,
                    producer.payload, out var prepared, out reason) || prepared is null)
            {
                producer.issuanceUncertain = reason.StartsWith(LegacyItemUidRegistry.RecoveryRequired, StringComparison.Ordinal);
                return false;
            }
            // Record issuance immediately: every later refusal/exception must
            // fence retry, even if consuming the live permit itself fails.
            producer.issued = true; producer.intent = prepared;
            lease.MarkIntentIssued(producer.Request, producer.payload);
            producer.Ordinary.RegisterAuthenticatedCreation(producer);
            if (producerInterruption == LegacyAuthenticatedCreationInterruption.AfterIntent)
            { reason = LegacyTradeJournal.RecoveryRequired + ":creationAfterIntent"; return false; }
            cancellationToken.ThrowIfCancellationRequested();
            if (!lease.Registry.TryReserveProducerIntent(lease.Configuration.ExpectedDomain, producer.Request,
                    producer.payload, out var allocated, out reason) || allocated?.Allocation is null) return false;
            producer.intent = allocated;
            var images = producer.Recipe.AfterImages(allocated.Allocation);
            producer.changes = LegacyAuthenticatedOrdinaryCreationRecipe.TargetKinds.Select(kind =>
                new LegacyTradeJournalChange(kind, producer.Account, null, images[kind]!)).ToArray();
            producer.RequireRawUnoccupiedBirths();
            if (producerInterruption == LegacyAuthenticatedCreationInterruption.AfterAllocation)
            { reason = LegacyTradeJournal.RecoveryRequired + ":creationAfterAllocation"; return false; }
            cancellationToken.ThrowIfCancellationRequested();
            if (!LegacyTradeJournal.TryCommitAuthenticatedOrdinaryCreation(producer, producer.changes,
                    interruption, out reason)) return false;
            if (producerInterruption == LegacyAuthenticatedCreationInterruption.AfterGameCommit)
            { reason = LegacyTradeJournal.RecoveryRequired + ":creationAfterGameCommit"; return false; }
            // Archive against the exact creation images, before ordinary login
            // legitimately changes Online/scalars or additive class spells.
            if (!producer.Ordinary.TryComplete(producer.Request.OperationId, out reason)) return false;
            producer.complete = true;
            lease.RetireCompletedPermit(producer);
            character = producer.preparedCharacter;
            reason = string.Empty; return true;
        }
        catch (Exception error) when (LegacyOwnedAdmissionOptions.IsConfigurationFailure(error))
        { reason = "authenticatedCreationRefused:" + error.GetType().Name; return false; }
        finally
        {
            if (producer is not null && (producer.issued || producer.issuanceUncertain) && !producer.complete)
                lease.FenceUncertainCreation();
        }
    }

    internal LegacyAuthenticatedOrdinaryCreationOperation CopyOperation()
    {
        if (!issued || intent?.Allocation is not { } receipt)
            throw new InvalidDataException("Creation has no actual allocated durable producer intent.");
        return new(payload.ToArray(), intent.PreparedGeneration, receipt);
    }

    internal Guid RequireCompletedPermitRetirement(LegacyOwnedAdmissionRuntime.CreationLease actualLease)
    {
        if (!ReferenceEquals(lease, actualLease) || !complete || !issued || intent?.Allocation is null ||
            intent.Request != Request || intent.Allocation.OperationId != Request.OperationId)
            throw new InvalidDataException("Permit retirement has no exact real completed producer/lease/operation.");
        return Request.OperationId;
    }

    internal void RequireIssuedOwner(LegacyOrdinaryAdmissionUidOwner owner)
    {
        lease.RequireCurrentBefore();
        if (!issued || !ReferenceEquals(owner, Ordinary) || intent is null || intent.Request != Request ||
            !intent.CopyPayload().AsSpan().SequenceEqual(payload))
            throw new InvalidDataException("Ordinary work fence lacks the actual issued creation owner.");
    }

    private void RequireRawBeforeIssuance()
    {
        lease.RequireCurrentBefore();
        var configured = lease.Configuration;
        if (!LegacyItemUidRawCatalog.TryScan(configured.ItemsRoot, configured.MailboxRoot,
                configured.RedeemFile, configured.ClaimFile, out var observed, out var reason) ||
            !lease.Registry.IsAuthenticatedCreationObservedMaximumInRange(lease, observed.Maximum))
            throw new InvalidDataException("Creation before-namespace is unproven: " + reason);
        lease.RequireCurrentBefore();
    }
}
