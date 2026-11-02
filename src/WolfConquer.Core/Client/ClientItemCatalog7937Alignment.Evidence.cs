namespace WolfConquer.Core.Client;

public enum ClientItemColumnEvidenceStatus
{
    MissingExactCandidate, SingleExactCandidate, AmbiguousCandidates, InsufficientDistinctTargetColumns,
}

/// <summary>Immutable ordinal-only evidence; no item identity, field value or semantic/runtime authority.</summary>
public sealed class ClientItemColumnEvidence
{
    private readonly int[] candidates;
    private readonly int[] sharing;

    internal ClientItemColumnEvidence(int source, int[] ownedCandidates, int[] ownedSharing, ClientItemColumnEvidenceStatus status)
    { ReferenceColumn = source; candidates = ownedCandidates; sharing = ownedSharing; Status = status; }

    public int ReferenceColumn { get; }
    public ReadOnlySpan<int> CurrentColumns => candidates;

    /// <summary>References with this same nonempty candidate set, including self; empty when no candidate exists.</summary>
    public ReadOnlySpan<int> SharingReferenceColumns => sharing;
    public ClientItemColumnEvidenceStatus Status { get; }
}

public sealed partial class ClientItemCatalog7937Alignment
{
    /// <summary>Projects the already complete comparison; does not compare rows again or choose ambiguous mappings.</summary>
    public IReadOnlyList<ClientItemColumnEvidence> CreateColumnEvidence()
    {
        var groups = columns.Where(column => column.CurrentColumns.Length != 0)
            .GroupBy(column => string.Join(",", column.CurrentColumns.ToArray()))
            .ToDictionary(group => group.Key, group => group.Select(column => column.ReferenceColumn).Order().ToArray(), StringComparer.Ordinal);
        var evidence = new ClientItemColumnEvidence[columns.Count];
        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var candidates = column.CurrentColumns.ToArray();
            int[] sharing = candidates.Length == 0 ? [] : groups[string.Join(",", candidates)];
            var status = ClassifyColumnEvidence(candidates.Length, sharing.Length);
            evidence[index] = new(column.ReferenceColumn, candidates, sharing, status);
        }
        return Array.AsReadOnly(evidence);
    }

    private static ClientItemColumnEvidenceStatus ClassifyColumnEvidence(int candidates, int references)
    {
        if (candidates == 0) return ClientItemColumnEvidenceStatus.MissingExactCandidate;
        if (references > candidates) return ClientItemColumnEvidenceStatus.InsufficientDistinctTargetColumns;
        return references == 1 && candidates == 1
            ? ClientItemColumnEvidenceStatus.SingleExactCandidate : ClientItemColumnEvidenceStatus.AmbiguousCandidates;
    }
}
