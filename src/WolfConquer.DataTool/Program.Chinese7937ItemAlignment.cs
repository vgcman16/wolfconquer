using System.Security.Cryptography;
using System.Text.Json;
using WolfConquer.Core.Client;

internal static partial class Program
{
    private static int InspectChinese7937ItemAlignment(string[] args)
    {
        if (args.Length != 6 || args[1] != "7937" ||
            !TryItemAlignmentPin(args[3], out var referencePin) || !TryItemAlignmentPin(args[5], out var currentPin))
            return ItemAlignmentRefusal("Arguments");
        try
        {
            var captured = CaptureItemAlignment(args[2], referencePin, args[4], currentPin, out var failure);
            if (captured is null) return ItemAlignmentRefusal(failure);
            var result = captured.Alignment;
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                FormatVersion = 1, ClientVersion = 7937, Status = result.Status.ToString(),
                result.ReferenceRows, result.CurrentRows, result.SharedIdentities,
                result.ReferenceOnlyIdentities, result.CurrentOnlyIdentities, result.FramedVectorBytes,
                ReferenceFields = 65, CurrentFields = 67, ComparedReferenceColumns = result.Columns.Count,
                ColumnsWithoutExactCandidate = result.Columns.Count(column => column.CurrentColumns.Length == 0),
                ColumnsWithOneExactCandidate = result.Columns.Count(column => column.CurrentColumns.Length == 1),
                ColumnsWithMultipleExactCandidates = result.Columns.Count(column => column.CurrentColumns.Length > 1),
                captured.ReferenceSha256, captured.CurrentSha256,
                SemanticRolesProved = false, RuntimeBinding = false, GameplayActivated = false,
                InputsWritten = false, OutputFilesWritten = false,
            }));
            return 0;
        }
        catch (Exception)
        {
            // Exception messages, source paths and parser row values are private.
            return ItemAlignmentRefusal("InspectionFailed");
        }
    }

    private static bool TryItemAlignmentPin(string text, out byte[] pin)
    {
        pin = [];
        if (text.Length != SHA256.HashSizeInBytes * 2 || !text.All(Uri.IsHexDigit)) return false;
        pin = Convert.FromHexString(text); return true;
    }

    private static int ItemAlignmentRefusal(string failure)
    {
        Console.Error.WriteLine(JsonSerializer.Serialize(new
        {
            FormatVersion = 1, ClientVersion = 7937, Status = "refused", Failure = failure,
            SemanticRolesProved = false, RuntimeBinding = false, GameplayActivated = false,
            InputsWritten = false, OutputFilesWritten = false,
        }));
        return 1;
    }
}
