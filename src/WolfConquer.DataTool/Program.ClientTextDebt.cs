using System.Globalization;
using System.Text;
using WolfConquer.Core.Client;

internal static partial class Program
{
    private const string DefaultCjkDebtRootPath = @"client";

    static int ScanCjkTextDebt(string[] args)
    {
        CjkTextDebtOptions options;
        try
        {
            options = ResolveCjkTextDebtOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        if (!Directory.Exists(options.RootPath))
        {
            Console.Error.WriteLine($"CJK text debt root not found: {options.RootPath}");
            return 1;
        }

        try
        {
            var report = ClientTextCjkDebtReporter.Create(options.RootPath);
            PrintCjkTextDebtReport(report, options.TopCount);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            Console.Error.WriteLine($"CJK text debt scan failed: {ex.Message}");
            return 1;
        }
    }

    static CjkTextDebtOptions ResolveCjkTextDebtOptions(string[] args)
    {
        var rootPath = DefaultCjkDebtRootPath;
        var topCount = 25;
        var rootPathSet = false;

        for (var index = 1; index < args.Length; index++)
        {
            var option = args[index];
            if (string.Equals(option, "--top", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length)
                {
                    throw new ArgumentException("Missing value for --top.");
                }

                topCount = ParseCjkTextDebtTopCount(args[index]);
                continue;
            }

            const string topPrefix = "--top=";
            if (option.StartsWith(topPrefix, StringComparison.OrdinalIgnoreCase))
            {
                topCount = ParseCjkTextDebtTopCount(option[topPrefix.Length..]);
                continue;
            }

            if (option.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown scan-cjk-text-debt option: {option}");
            }

            if (rootPathSet)
            {
                throw new ArgumentException(
                    "Usage: WolfConquer.DataTool scan-cjk-text-debt [rootPath] [--top N]");
            }

            rootPath = option;
            rootPathSet = true;
        }

        return new CjkTextDebtOptions(Path.GetFullPath(rootPath), topCount);
    }

    static int ParseCjkTextDebtTopCount(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var topCount) ||
            topCount < 0)
        {
            throw new FormatException("--top requires a zero or positive integer.");
        }

        return topCount;
    }

    static void PrintCjkTextDebtReport(ClientTextCjkDebtReport report, int topCount)
    {
        Console.WriteLine($"CJK text debt root: {report.RootPath}");
        Console.WriteLine($"Candidate files scanned: {report.CandidateFileCount}");
        Console.WriteLine($"Editable text files scanned: {report.TextFileCount}");
        Console.WriteLine($"Skipped packed/binary files: {report.PackedOrBinaryFileCount}");
        Console.WriteLine($"Files with CJK: {report.FilesWithCjk}");
        Console.WriteLine($"CJK lines: {report.TotalCjkLineCount}");
        Console.WriteLine($"CJK characters: {report.TotalCjkCharacterCount}");
        Console.WriteLine($"Actionable files with CJK: {report.ActionableFilesWithCjk}");
        Console.WriteLine($"Actionable CJK lines: {report.TotalActionableCjkLineCount}");
        Console.WriteLine($"Actionable CJK characters: {report.TotalActionableCjkCharacterCount}");

        if (report.Categories.Count > 0)
        {
            Console.WriteLine("CJK categories:");
            foreach (var category in report.Categories)
            {
                Console.WriteLine(
                    $"  {category.Category}\tlines={category.CjkLineCount}\tcjkChars={category.CjkCharacterCount}");
            }
        }

        var actionableFiles = report.Files
            .Where(file => file.ActionableCjkLineCount > 0)
            .OrderByDescending(file => file.ActionableCjkCharacterCount)
            .ThenByDescending(file => file.CjkCharacterCount)
            .ThenBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(topCount)
            .ToArray();
        if (actionableFiles.Length > 0)
        {
            Console.WriteLine("Top actionable CJK files:");
            foreach (var file in actionableFiles)
            {
                Console.WriteLine(
                    $"{file.RelativePath}\tencoding={file.EncodingKind}\tactionableLines={file.ActionableCjkLineCount}\tactionableChars={file.ActionableCjkCharacterCount}\tcjkLines={file.CjkLineCount}\tcjkChars={file.CjkCharacterCount}\tcategories={FormatCjkDebtCategories(file.Categories)}");
            }
        }

        foreach (var file in report.Files.Take(topCount))
        {
            Console.WriteLine(
                $"{file.RelativePath}\tencoding={file.EncodingKind}\tcjkLines={file.CjkLineCount}\tcjkChars={file.CjkCharacterCount}\tactionableLines={file.ActionableCjkLineCount}\tactionableChars={file.ActionableCjkCharacterCount}\tcategories={FormatCjkDebtCategories(file.Categories)}");
            foreach (var family in file.Families.Take(10))
            {
                Console.WriteLine(
                    $"  {family.Name}\tlines={family.CjkLineCount}\tcjkChars={family.CjkCharacterCount}\tsample={family.Sample}");
            }
        }
    }

    static string FormatCjkDebtCategories(IReadOnlyList<ClientTextCjkDebtCategorySummary> categories)
    {
        return categories.Count == 0
            ? "<none>"
            : string.Join(
                ",",
                categories.Select(category =>
                    $"{category.Category}:{category.CjkLineCount}/{category.CjkCharacterCount}"));
    }
}

internal sealed record CjkTextDebtOptions(string RootPath, int TopCount);
