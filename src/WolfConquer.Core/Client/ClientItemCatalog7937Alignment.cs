namespace WolfConquer.Core.Client;

/// <summary>Complete shared-ID byte evidence only; no semantic mapping or accepting runtime adapter.</summary>
public sealed partial class ClientItemCatalog7937Alignment
{
    public const int MaximumInputBytes = 64 * 1024 * 1024;
    public const int MaximumRows = 100_000;
    public const int MaximumVectorBytes = 256 * 1024 * 1024;

    /// <summary>Compares every shared numeric identity; the optional finite budget may only lower the fixed ceiling.</summary>
    public static bool TryAnalyze(ClientItemCatalogDocument? reference, Chinese7937ItemCatalogDocument? current,
        out ClientItemCatalog7937Alignment? result, out ClientItemCatalogAlignmentFailure failure,
        int maximumVectorBytes = MaximumVectorBytes)
    {
        result = null; failure = ClientItemCatalogAlignmentFailure.RequiredDocument;
        if (reference is null || current is null) return false;
        if (maximumVectorBytes is <= 0 or > MaximumVectorBytes)
        { failure = ClientItemCatalogAlignmentFailure.InvalidBudget; return false; }
        if (!ReferenceWithinBounds(reference) || !CurrentWithinBounds(current))
        { failure = ClientItemCatalogAlignmentFailure.InputBounds; return false; }
        var rows = JoinRows(reference, current);
        if (rows.Count == 0)
        {
            result = new(reference.Rows.Count, current.Rows.Count, 0, 0, [],
                ClientItemCatalogAlignmentStatus.NoSharedIdentities, []);
            failure = ClientItemCatalogAlignmentFailure.None;
            return true;
        }
        if (!TryMeasureVectors(rows, maximumVectorBytes, out var lengths, out var total))
        { failure = ClientItemCatalogAlignmentFailure.VectorBounds; return false; }
        var columns = CompareVectors(BuildVectors(rows, false, lengths[0]), BuildVectors(rows, true, lengths[1]));
        var (status, mapping) = Classify(columns);
        result = new(reference.Rows.Count, current.Rows.Count, rows.Count, total, columns, status, mapping);
        failure = ClientItemCatalogAlignmentFailure.None;
        return true;
    }

    private static bool ReferenceWithinBounds(ClientItemCatalogDocument document)
    {
        if (document.Rows.Count is <= 0 or > MaximumRows) return false;
        long bytes = 0;
        foreach (var row in document.Rows)
        {
            bytes += ClientItemCatalogDocument.FieldCount * 2 + row.LineEndingByteCount;
            for (var column = 0; column < row.FieldCount; column++) bytes += row.FieldBytes(column).Length;
            if (bytes > MaximumInputBytes) return false;
        }
        return true;
    }

    private static bool CurrentWithinBounds(Chinese7937ItemCatalogDocument document)
    {
        if (document.Rows.Count is <= 0 or > MaximumRows) return false;
        long bytes = 0;
        foreach (var row in document.Rows)
        {
            bytes += Chinese7937ItemCatalogDocument.FieldCount * 2 + 2;
            for (var column = 0; column < row.FieldCount; column++) bytes += row.FieldBytes(column).Length;
            if (bytes > MaximumInputBytes) return false;
        }
        return true;
    }
}
