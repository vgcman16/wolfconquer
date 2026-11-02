using System.Collections.ObjectModel;

namespace WolfConquer.Core.Client;

public enum ClientItemCatalogAlignmentStatus
{
    NoSharedIdentities, MissingExactColumnMatches, InsufficientDistinctTargetColumns,
    AmbiguousCompleteByteAlignments, UniqueCompleteByteAlignment,
}

public enum ClientItemCatalogAlignmentFailure { None, RequiredDocument, InvalidBudget, InputBounds, VectorBounds }
public readonly record struct ClientItemColumnMatch(int ReferenceColumn, int CurrentColumn);

/// <summary>Zero-based opaque column indices; field0 is excluded because it is only the numeric identity join.</summary>
public sealed class ClientItemColumnCandidates
{
    private readonly int[] targets;
    internal ClientItemColumnCandidates(int source, int[] ownedTargets) { ReferenceColumn = source; targets = ownedTargets; }
    public int ReferenceColumn { get; }
    public ReadOnlySpan<int> CurrentColumns => targets;
}

public sealed partial class ClientItemCatalog7937Alignment
{
    private readonly ReadOnlyCollection<ClientItemColumnCandidates> columns;
    private readonly ClientItemColumnMatch[] mapping;

    private ClientItemCatalog7937Alignment(int referenceRows, int currentRows, int shared, int bytes,
        ClientItemColumnCandidates[] ownedColumns, ClientItemCatalogAlignmentStatus status, ClientItemColumnMatch[] ownedMapping)
    {
        ReferenceRows = referenceRows; CurrentRows = currentRows; SharedIdentities = shared; FramedVectorBytes = bytes;
        columns = Array.AsReadOnly(ownedColumns); Status = status; mapping = ownedMapping;
    }

    public int ReferenceRows { get; }
    public int CurrentRows { get; }
    public int SharedIdentities { get; }
    public int ReferenceOnlyIdentities => ReferenceRows - SharedIdentities;
    public int CurrentOnlyIdentities => CurrentRows - SharedIdentities;
    public int FramedVectorBytes { get; }
    public ClientItemCatalogAlignmentStatus Status { get; }
    public IReadOnlyList<ClientItemColumnCandidates> Columns => columns;

    /// <summary>Empty unless complete injective byte-unique correspondence exists. Never semantic/runtime authority.</summary>
    public ReadOnlySpan<ClientItemColumnMatch> UniqueByteMapping => mapping;

    private static (ClientItemCatalogAlignmentStatus, ClientItemColumnMatch[]) Classify(ClientItemColumnCandidates[] columns)
    {
        if (columns.Any(column => column.CurrentColumns.Length == 0))
            return (ClientItemCatalogAlignmentStatus.MissingExactColumnMatches, []);
        var groups = columns.GroupBy(column => string.Join(",", column.CurrentColumns.ToArray())).ToArray();
        if (groups.Any(group => group.Count() > group.First().CurrentColumns.Length))
            return (ClientItemCatalogAlignmentStatus.InsufficientDistinctTargetColumns, []);
        if (groups.Any(group => group.Count() != 1 || group.First().CurrentColumns.Length != 1))
            return (ClientItemCatalogAlignmentStatus.AmbiguousCompleteByteAlignments, []);
        return (ClientItemCatalogAlignmentStatus.UniqueCompleteByteAlignment,
            columns.Select(column => new ClientItemColumnMatch(column.ReferenceColumn, column.CurrentColumns[0])).ToArray());
    }
}
