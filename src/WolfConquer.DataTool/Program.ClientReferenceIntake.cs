using System.Globalization;
using System.Text;
using WolfConquer.Core.Client;

internal static partial class Program
{
    private const string ClientReferenceIntakeUsage =
        "Usage: WolfConquer.DataTool client-reference-intake <absolute-reference-root> <expected-version> [--output-new <absolute-findings.md>]";

    private static int ClientReferenceIntake(string[] args)
    {
        try
        {
            if ((args.Length != 3 && args.Length != 5) ||
                !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var expected) ||
                args[2].Length != 4 || expected == 0 ||
                (args.Length == 5 && args[3] != "--output-new"))
                throw new ArgumentException("Expected explicit reference/version and optional create-only Markdown output.");
            string? output = null;
            if (args.Length == 5)
            {
                if (!Path.IsPathFullyQualified(args[4])) throw new ArgumentException("Absolute output path required.");
                output = Path.GetFullPath(args[4]);
                if (output.StartsWith("\\\\", StringComparison.Ordinal) || output.StartsWith("//", StringComparison.Ordinal) ||
                    !string.Equals(Path.GetExtension(output), ".md", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("A local Markdown output path is required.");
                ValidateClientItemCatalogPath(output, mustBeNew: true);
                var relative = Path.GetRelativePath(Path.GetFullPath(args[1]), output);
                if (!Path.IsPathFullyQualified(relative) && relative != ".." &&
                    !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new ArgumentException("Findings must be outside the reference directory.");
            }
            var snapshot = ChineseFullClientCatalogIntake.ReadReferenceDirectory(args[1], expected);
            var findings = ChineseFullClientCatalogIntake.WriteFindingsMarkdown(snapshot);
            if (output is not null)
            {
                ValidateClientItemCatalogPath(output, mustBeNew: true);
                using var destination = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                destination.Write(new UTF8Encoding(false).GetBytes(findings));
            }
            Console.Write(findings);
            return snapshot.HasUnsupportedItems ? 2 : 0;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or OverflowException or System.Security.SecurityException or AggregateException)
        {
            // Parser details may contain proprietary text; emit only bounded static classification.
            Console.Error.WriteLine("Selected client intake refused: " + error.GetType().Name);
            Console.Error.WriteLine(ClientReferenceIntakeUsage);
            return 1;
        }
    }
}
