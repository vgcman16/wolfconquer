using System.Text.Json;
using WolfConquer.Core.Client;

internal static partial class Program
{
    private const string DefaultLegacyDbShopPath = "data/DBServer/shops/Shop.dat";
    private const string DefaultClientShopPath = "client/ini/Shop.dat";
    private const string DefaultLegacyDbSobNpcPath = "data/DBServer/SobNpcs.txt";
    private const string DefaultLegacyDbSobNpcBackupPath = "data/DBServer/SobNpcs.txt.bak";

    static int PatchDbShopNames(string[] args)
    {
        var targetPath = ResolveDbShopPath(args, 1, DefaultLegacyDbShopPath);
        var referencePath = ResolveDbShopPath(args, 2, DefaultClientShopPath);

        if (!File.Exists(targetPath))
        {
            Console.Error.WriteLine($"DB shop file not found: {targetPath}");
            return 1;
        }

        if (!File.Exists(referencePath))
        {
            Console.Error.WriteLine($"Reference shop file not found: {referencePath}");
            return 1;
        }

        var result = LegacyShopNameTranslator.PatchFile(targetPath, referencePath);
        Console.WriteLine($"DB shop file: {targetPath}");
        Console.WriteLine($"Reference shop file: {referencePath}");
        Console.WriteLine($"Reference shops scanned: {result.ReferenceShopsScanned}");
        Console.WriteLine($"Target shops scanned: {result.TargetShopsScanned}");
        Console.WriteLine($"Matched target shops: {result.MatchedTargetShops}");
        Console.WriteLine($"Translated names: {result.TranslatedNames}");
        Console.WriteLine($"Changed names: {result.ChangedNames}");
        Console.WriteLine($"Unmatched names: {result.UnmatchedNames}");
        Console.WriteLine($"Changed file: {result.Changed}");
        return result.TranslatedNames > 0 ? 0 : 2;
    }

    static int PatchDbSobNpcNames(string[] args)
    {
        var targetPath = ResolveDbTextPath(args, 1, DefaultLegacyDbSobNpcPath, "SobNpcs.txt");
        var referencePath = ResolveDbTextPath(args, 2, DefaultLegacyDbSobNpcBackupPath, "SobNpcs.txt.bak");

        if (!File.Exists(targetPath))
        {
            Console.Error.WriteLine($"DB SobNpc file not found: {targetPath}");
            return 1;
        }

        if (!File.Exists(referencePath))
        {
            Console.Error.WriteLine($"Reference SobNpc backup file not found: {referencePath}");
            return 1;
        }

        var result = LegacySobNpcNameTranslator.PatchFile(targetPath, referencePath);
        Console.WriteLine($"DB SobNpc file: {targetPath}");
        Console.WriteLine($"Reference SobNpc file: {referencePath}");
        Console.WriteLine($"Reference rows scanned: {result.ReferenceRowsScanned}");
        Console.WriteLine($"Reference exact rows: {result.ReferenceExactRows}");
        Console.WriteLine($"Reference unique IDs: {result.ReferenceUniqueIds}");
        Console.WriteLine($"Target rows scanned: {result.TargetRowsScanned}");
        Console.WriteLine($"Translatable names: {result.TranslatableNames}");
        Console.WriteLine($"Exact row matches: {result.ExactRowMatches}");
        Console.WriteLine($"Unique-ID fallback matches: {result.UniqueIdMatches}");
        Console.WriteLine($"Known-name fallback matches: {result.KnownNameMatches}");
        Console.WriteLine($"Translated names: {result.TranslatedNames}");
        Console.WriteLine($"Changed names: {result.ChangedNames}");
        Console.WriteLine($"Unmatched names: {result.UnmatchedNames}");
        Console.WriteLine($"Changed file: {result.Changed}");
        return result.TranslatedNames > 0 ? 0 : 2;
    }

    static int PatchDbVisibleText(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("Usage: WolfConquer.DataTool patch-db-visible-text <DBServerPath> <catalogJsonPath>");
            return 1;
        }

        var dbServerPath = Path.GetFullPath(args[1]);
        if (!Directory.Exists(dbServerPath))
        {
            Console.Error.WriteLine($"DBServer path not found: {dbServerPath}");
            return 1;
        }

        IReadOnlyList<LegacyDbVisibleTextReplacement> catalog;
        try
        {
            catalog = LegacyDbVisibleTextCatalog.Load(args[2]);
            foreach (var row in catalog)
            {
                var target = EnglishClientPatch.ResolveTargetPath(dbServerPath, row.RelativePath);
                if (!File.Exists(target))
                {
                    throw new FileNotFoundException("DB text catalog target was not found.", target);
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            Console.Error.WriteLine($"Invalid DB text catalog: {ex.Message}");
            return 1;
        }

        var totalReplacementRows = 0;
        var totalVisibleTextRowsScanned = 0;
        var totalMatchedSourceRows = 0;
        var totalAlreadyTranslatedRows = 0;
        var totalChangedRows = 0;
        var totalUnmatchedCjkRows = 0;

        Console.WriteLine($"DBServer: {dbServerPath}");
        foreach (var group in catalog
            .GroupBy(replacement => replacement.RelativePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var targetPath = EnglishClientPatch.ResolveTargetPath(dbServerPath, group.Key);
            if (!File.Exists(targetPath))
            {
                Console.Error.WriteLine($"DB visible-text file not found: {targetPath}");
                return 1;
            }

            var groupRows = group.ToArray();
            var result = LegacyDbVisibleTextTranslator.PatchFile(targetPath, groupRows);
            totalReplacementRows += result.ReplacementRows;
            totalVisibleTextRowsScanned += result.VisibleTextRowsScanned;
            totalMatchedSourceRows += result.MatchedSourceRows;
            totalAlreadyTranslatedRows += result.AlreadyTranslatedRows;
            totalChangedRows += result.ChangedRows;
            totalUnmatchedCjkRows += result.UnmatchedCjkRows;

            Console.WriteLine(
                $"{group.Key}: sourceRows={result.ReplacementRows} visibleRows={result.VisibleTextRowsScanned} " +
                $"matched={result.MatchedSourceRows} alreadyTranslated={result.AlreadyTranslatedRows} " +
                $"changed={result.ChangedRows} unmatchedCjk={result.UnmatchedCjkRows}");
        }

        Console.WriteLine($"Source-backed rows: {totalReplacementRows}");
        Console.WriteLine($"Visible text rows scanned: {totalVisibleTextRowsScanned}");
        Console.WriteLine($"Matched source rows: {totalMatchedSourceRows}");
        Console.WriteLine($"Already translated rows: {totalAlreadyTranslatedRows}");
        Console.WriteLine($"Changed rows: {totalChangedRows}");
        Console.WriteLine($"Unmatched CJK visible rows in patched files: {totalUnmatchedCjkRows}");
        return totalMatchedSourceRows + totalAlreadyTranslatedRows > 0 ? 0 : 2;
    }

    static int PatchDbQuestInfoMojibake(string[] args)
    {
        if (args.Length <= 1)
        {
            Console.Error.WriteLine("DBServer path or Questinfo.ini path is required for patch-db-quest-info-mojibake.");
            return 1;
        }

        var targetPath = ResolveDbTextPath(args, 1, string.Empty, "Questinfo.ini");
        if (!File.Exists(targetPath))
        {
            Console.Error.WriteLine($"DB Questinfo file not found: {targetPath}");
            return 1;
        }

        var result = LegacyDbQuestInfoMojibakeNormalizer.PatchFile(targetPath);
        Console.WriteLine($"DB Questinfo file: {targetPath}");
        Console.WriteLine($"Lines scanned: {result.LinesScanned}");
        Console.WriteLine($"Visible text fields scanned: {result.VisibleTextFieldsScanned}");
        Console.WriteLine($"Normalized fields: {result.NormalizedFields}");
        Console.WriteLine($"Changed file: {result.Changed}");
        return 0;
    }

    private static string ResolveDbShopPath(string[] args, int index, string defaultPath)
    {
        if (args.Length <= index)
        {
            return Path.GetFullPath(defaultPath);
        }

        var path = Path.GetFullPath(args[index]);
        if (!Directory.Exists(path))
        {
            return path;
        }

        var dbServerShopPath = Path.Combine(path, "shops", "Shop.dat");
        if (File.Exists(dbServerShopPath))
        {
            return dbServerShopPath;
        }

        return Path.Combine(path, "ini", "Shop.dat");
    }

    private static string ResolveDbTextPath(string[] args, int index, string defaultPath, string fileName)
    {
        if (args.Length <= index)
        {
            return Path.GetFullPath(defaultPath);
        }

        var path = Path.GetFullPath(args[index]);
        return Directory.Exists(path)
            ? Path.Combine(path, fileName)
            : path;
    }
}
