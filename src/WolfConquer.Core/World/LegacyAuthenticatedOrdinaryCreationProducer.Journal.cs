using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyAuthenticatedOrdinaryCreationProducer
{
    internal void BeginJournal(IReadOnlyList<LegacyTradeJournalChange> proposed, string identity,
        LegacyAuthenticatedOrdinaryCreationOperation operation)
    {
        if (transaction is not null || !Guid.TryParseExact(identity, "N", out var id) || id == Guid.Empty ||
            identity != id.ToString("N")) throw new InvalidDataException("Creation journal identity is not fresh/canonical.");
        RequireChanges(proposed);
        transaction = identity;
        RequireJournalBoundary(proposed, identity, operation);
    }

    internal void RequireJournalBoundary(IReadOnlyList<LegacyTradeJournalChange> proposed, string identity,
        LegacyAuthenticatedOrdinaryCreationOperation operation)
    {
        lease.RequirePlainOwnerCurrent();
        if (transaction != identity || !issued || complete || !Recipe.Projection.IsCurrent())
            throw new InvalidDataException("Creation journal producer or catalog changed.");
        var expected = CopyOperation();
        if (operation.PreparedGeneration != expected.PreparedGeneration || operation.Allocation != expected.Allocation ||
            !operation.Payload.AsSpan().SequenceEqual(expected.Payload))
            throw new InvalidDataException("Creation journal operation differs from its actual producer.");
        RequireChanges(proposed);
        lease.Registry.RequireAuthenticatedCreationJournal(lease, operation);
        lease.NameClaim.RequireCurrentAtWriteBoundary(proposed, identity);
        lease.RequirePlainOwnerCurrent();
    }

    internal void RequireJournalPreparedTargets(IReadOnlyList<string> paths,
        IReadOnlyList<LegacyTradeJournalChange> proposed)
    {
        RequireChanges(proposed);
        if (transaction is null || paths.Count != proposed.Count)
            throw new InvalidDataException("Creation journal targets are not prepared.");
        for (var index = 0; index < paths.Count; index++)
            if (!string.Equals(Path.GetFullPath(paths[index]), Recipe.Roots.PathFor(proposed[index].Kind, Account),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidDataException("Creation journal path differs from its actual prepared target.");
        RequireJournalBoundary(proposed, transaction, CopyOperation());
    }

    private void RequireChanges(IReadOnlyList<LegacyTradeJournalChange> proposed)
    {
        if (changes is null || proposed.Count != changes.Length)
            throw new InvalidDataException("Creation journal has no complete prepared image set.");
        for (var index = 0; index < changes.Length; index++)
        {
            var expected = changes[index]; var actual = proposed[index];
            if (actual is null || actual.Kind != expected.Kind || actual.FileId != expected.FileId ||
                actual.Before is not null || !actual.After.AsSpan().SequenceEqual(expected.After))
                throw new InvalidDataException("Creation journal changes differ from their actual prepared images.");
        }
    }

    internal void RequireRawScanRoots(string items, string mailbox, string redeem, string claim)
    {
        lease.RequirePlainOwnerCurrent();
        var actual = lease.Configuration;
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (!comparer.Equals(items, actual.ItemsRoot) || !comparer.Equals(mailbox, actual.MailboxRoot) ||
            !comparer.Equals(redeem, actual.RedeemFile) || !comparer.Equals(claim, actual.ClaimFile))
            throw new InvalidDataException("Creation raw scan differs from its actual configured namespace.");
        lease.Registry.RequireAuthenticatedCreationJournal(lease, CopyOperation());
        if (!Recipe.IsBefore(LegacyTradeJournal.CaptureAuthenticatedCreationInputs(Recipe.Roots, Recipe.MailboxRoot, Account)))
            throw new InvalidDataException("Creation raw scan requires every actual target still absent.");
    }

    internal void RequireUnoccupiedPhysicalBirth(uint uid)
    {
        var receipt = intent?.Allocation ?? throw new InvalidDataException("Creation scan lacks its actual allocation.");
        if (uid >= receipt.First && (ulong)uid < (ulong)receipt.First + (uint)receipt.Count)
            throw new InvalidDataException("A physical input already owns a reserved creation birth UID.");
    }

    private void RequireRawUnoccupiedBirths()
    {
        var configured = lease.Configuration;
        if (!LegacyItemUidRawCatalog.TryScanAuthenticatedCreation(configured.ItemsRoot, configured.MailboxRoot,
                configured.RedeemFile, configured.ClaimFile, this, out var observed, out var reason) ||
            !lease.Registry.IsAuthenticatedCreationObservedMaximumInRange(lease, observed.Maximum))
            throw new InvalidDataException("Creation registered namespace is unproven: " + reason);
    }
}
