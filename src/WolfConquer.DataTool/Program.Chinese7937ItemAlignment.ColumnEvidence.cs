using System.Text;
using System.Text.Json;

internal static partial class Program
{
    private const int ItemColumnEvidenceMaximumJsonBytes = 65536;

    private static int WriteItemAlignmentColumnEvidence(ItemAlignmentCapture captured)
    {
        var result = captured.Alignment;
        var evidence = result.CreateColumnEvidence();
        var json = JsonSerializer.Serialize(new
        {
            FormatVersion = 2, ClientVersion = 7937, Status = result.Status.ToString(),
            result.ReferenceRows, result.CurrentRows, result.SharedIdentities,
            result.ReferenceOnlyIdentities, result.CurrentOnlyIdentities, result.FramedVectorBytes,
            ReferenceFields = 65, CurrentFields = 67, ComparedReferenceColumns = evidence.Count,
            ColumnsWithoutExactCandidate = result.Columns.Count(column => column.CurrentColumns.Length == 0),
            ColumnsWithOneExactCandidate = result.Columns.Count(column => column.CurrentColumns.Length == 1),
            ColumnsWithMultipleExactCandidates = result.Columns.Count(column => column.CurrentColumns.Length > 1),
            IndexBase = 0, ExcludedIdentityColumn = 0,
            ColumnEvidence = evidence.Select(column => new
            {
                column.ReferenceColumn, CurrentColumns = column.CurrentColumns.ToArray(),
                SharingReferenceColumns = column.SharingReferenceColumns.ToArray(), Status = column.Status.ToString(),
            }).ToArray(),
            captured.ReferenceSha256, captured.CurrentSha256,
            SemanticRolesProved = false, RuntimeBinding = false, GameplayActivated = false,
            InputsWritten = false, OutputFilesWritten = false,
        });
        if (Encoding.UTF8.GetByteCount(json) > ItemColumnEvidenceMaximumJsonBytes)
            return ItemAlignmentRefusal("ReportBounds");
        Console.WriteLine(json);
        return 0;
    }
}
