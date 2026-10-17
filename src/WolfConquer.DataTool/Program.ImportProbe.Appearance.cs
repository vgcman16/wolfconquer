using WolfConquer.Core.Data;

internal static partial class Program
{
    static void ProbeFurniture(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"furniture: {ReadFurniture(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"furniture: {ex.Message}");
        }
    }

    static IReadOnlyList<Furniture> ReadFurniture(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<Furniture>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                rows.Add(FurnitureParser.ParseRow(Path.GetFileName(path), lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }

    static void ProbeHairColorTypes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"hairColorTypes: {ReadHairColorTypes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"hairColorTypes: {ex.Message}");
        }
    }

    static IReadOnlyList<HairColorType> ReadHairColorTypes(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<HairColorType>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                rows.Add(CosmeticParser.ParseHairColorTypeRow(Path.GetFileName(path), lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }

    static void ProbeHairFaceStorageTypes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"hairFaceStorageTypes: {ReadHairFaceStorageTypes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"hairFaceStorageTypes: {ex.Message}");
        }
    }

    static IReadOnlyList<HairFaceStorageType> ReadHairFaceStorageTypes(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<HairFaceStorageType>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                rows.Add(CosmeticParser.ParseHairFaceStorageTypeRow(Path.GetFileName(path), lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }

    static void ProbeCoatStorageTypes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"coatStorageTypes: {ReadCoatStorageTypes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"coatStorageTypes: {ex.Message}");
        }
    }

    static IReadOnlyList<CoatStorageType> ReadCoatStorageTypes(string path)
    {
        return ReadRequiredRows(path, CosmeticParser.ParseCoatStorageTypeRow);
    }

    static void ProbeCoatStorageAttributes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"coatStorageAttributes: {ReadCoatStorageAttributes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"coatStorageAttributes: {ex.Message}");
        }
    }

    static IReadOnlyList<CoatStorageAttribute> ReadCoatStorageAttributes(string path)
    {
        return ReadRequiredRows(path, CosmeticParser.ParseCoatStorageAttributeRow);
    }

    static void ProbeCoatColorRules(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"coatColorRules: {ReadCoatColorRules(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"coatColorRules: {ex.Message}");
        }
    }

    static IReadOnlyList<CoatColorRule> ReadCoatColorRules(string path)
    {
        return ReadRequiredRows(path, CosmeticParser.ParseCoatColorRuleRow);
    }
}
