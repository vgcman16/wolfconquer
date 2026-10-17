using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Client;

public sealed record ClientTextCjkDebtReport(
    string RootPath,
    int CandidateFileCount,
    int TextFileCount,
    int PackedOrBinaryFileCount,
    int FilesWithCjk,
    int TotalCjkCharacterCount,
    int TotalCjkLineCount,
    IReadOnlyList<ClientTextCjkDebtFile> Files)
{
    public int ActionableFilesWithCjk { get; init; }

    public int TotalActionableCjkCharacterCount { get; init; }

    public int TotalActionableCjkLineCount { get; init; }

    public IReadOnlyList<ClientTextCjkDebtCategorySummary> Categories { get; init; } =
        Array.Empty<ClientTextCjkDebtCategorySummary>();
}

public sealed record ClientTextCjkDebtFile(
    string RelativePath,
    ClientTextEncodingKind EncodingKind,
    int CjkCharacterCount,
    int CjkLineCount,
    IReadOnlyList<ClientTextCjkDebtFamily> Families,
    IReadOnlyList<string> Samples)
{
    public int ActionableCjkCharacterCount { get; init; }

    public int ActionableCjkLineCount { get; init; }

    public IReadOnlyList<ClientTextCjkDebtCategorySummary> Categories { get; init; } =
        Array.Empty<ClientTextCjkDebtCategorySummary>();
}

public sealed record ClientTextCjkDebtFamily(
    string Name,
    int CjkCharacterCount,
    int CjkLineCount,
    string Sample);

public sealed record ClientTextCjkDebtCategorySummary(
    ClientTextCjkDebtCategory Category,
    int CjkCharacterCount,
    int CjkLineCount);

public enum ClientTextCjkDebtCategory
{
    VisibleText,
    Comment,
    SupportData,
    PlayerState,
}

public static class ClientTextCjkDebtReporter
{
    private const int MaximumSampleLength = 160;
    private static readonly string[] CandidateExtensions = [".ini", ".lua", ".txt", ".dat", ".cs"];

    public static ClientTextCjkDebtReport Create(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var fullRootPath = Path.GetFullPath(rootPath);
        var files = Directory.EnumerateFiles(fullRootPath, "*", SearchOption.AllDirectories)
            .Where(IsCandidateFile)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var textFileCount = 0;
        var packedOrBinaryFileCount = 0;
        var debtFiles = new List<ClientTextCjkDebtFile>();

        foreach (var path in files)
        {
            var bytes = ReadAllBytesShared(path);
            var classification = ClientTextFileClassifier.Classify(bytes);
            if (classification.IsLikelyPackedOrBinary)
            {
                packedOrBinaryFileCount++;
                continue;
            }

            textFileCount++;
            var decoded = DecodeLegacyText(bytes);
            var debt = AnalyzeFile(fullRootPath, path, decoded);
            if (debt.CjkCharacterCount > 0)
            {
                debtFiles.Add(debt);
            }
        }

        var orderedDebtFiles = debtFiles
            .OrderByDescending(file => file.CjkCharacterCount)
            .ThenBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ClientTextCjkDebtReport(
            fullRootPath,
            files.Length,
            textFileCount,
            packedOrBinaryFileCount,
            orderedDebtFiles.Length,
            orderedDebtFiles.Sum(file => file.CjkCharacterCount),
            orderedDebtFiles.Sum(file => file.CjkLineCount),
            orderedDebtFiles)
        {
            ActionableFilesWithCjk = orderedDebtFiles.Count(file => file.ActionableCjkLineCount > 0),
            TotalActionableCjkCharacterCount = orderedDebtFiles.Sum(file => file.ActionableCjkCharacterCount),
            TotalActionableCjkLineCount = orderedDebtFiles.Sum(file => file.ActionableCjkLineCount),
            Categories = orderedDebtFiles
                .SelectMany(file => file.Categories)
                .GroupBy(summary => summary.Category)
                .Select(group => new ClientTextCjkDebtCategorySummary(
                    group.Key,
                    group.Sum(summary => summary.CjkCharacterCount),
                    group.Sum(summary => summary.CjkLineCount)))
                .OrderBy(summary => summary.Category)
                .ToArray(),
        };
    }

    private static ClientTextCjkDebtFile AnalyzeFile(
        string rootPath,
        string path,
        DecodedClientTextFile decoded)
    {
        var relativePath = Path.GetRelativePath(rootPath, path);
        var families = new Dictionary<string, MutableCjkDebtFamily>(StringComparer.Ordinal);
        var categories = new Dictionary<ClientTextCjkDebtCategory, MutableCjkDebtCategory>();
        var samples = new List<string>();
        var cjkCharacterCount = 0;
        var cjkLineCount = 0;
        var actionableCjkCharacterCount = 0;
        var actionableCjkLineCount = 0;

        using var reader = new StringReader(decoded.Text);
        while (reader.ReadLine() is { } line)
        {
            var lineCjkCharacterCount = CountCjkCharacters(line);
            if (lineCjkCharacterCount == 0)
            {
                continue;
            }

            cjkCharacterCount += lineCjkCharacterCount;
            cjkLineCount++;

            var category = ClientTextCjkDebtClassifier.Classify(relativePath, line);
            if (!categories.TryGetValue(category, out var categoryDebt))
            {
                categoryDebt = new MutableCjkDebtCategory();
                categories[category] = categoryDebt;
            }

            categoryDebt.CjkCharacterCount += lineCjkCharacterCount;
            categoryDebt.CjkLineCount++;

            if (ClientTextCjkDebtClassifier.IsActionable(category))
            {
                actionableCjkCharacterCount += lineCjkCharacterCount;
                actionableCjkLineCount++;
            }

            var sample = NormalizeSample(line);
            if (samples.Count < 3)
            {
                samples.Add(sample);
            }

            var familyName = GetLineFamily(line);
            if (!families.TryGetValue(familyName, out var family))
            {
                family = new MutableCjkDebtFamily(familyName, sample);
                families[familyName] = family;
            }

            family.CjkCharacterCount += lineCjkCharacterCount;
            family.CjkLineCount++;
        }

        return new ClientTextCjkDebtFile(
            relativePath,
            decoded.EncodingKind,
            cjkCharacterCount,
            cjkLineCount,
            families.Values
                .Select(family => new ClientTextCjkDebtFamily(
                    family.Name,
                    family.CjkCharacterCount,
                    family.CjkLineCount,
                    family.Sample))
                .OrderByDescending(family => family.CjkCharacterCount)
                .ThenBy(family => family.Name, StringComparer.Ordinal)
                .ToArray(),
            samples)
        {
            ActionableCjkCharacterCount = actionableCjkCharacterCount,
            ActionableCjkLineCount = actionableCjkLineCount,
            Categories = categories
                .Select(pair => new ClientTextCjkDebtCategorySummary(
                    pair.Key,
                    pair.Value.CjkCharacterCount,
                    pair.Value.CjkLineCount))
                .OrderBy(summary => summary.Category)
                .ToArray(),
        };
    }

    private static byte[] ReadAllBytesShared(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static DecodedClientTextFile DecodeLegacyText(byte[] bytes)
    {
        if (HasUtf8Bom(bytes))
        {
            var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8Bom);
            return new DecodedClientTextFile(TrimUtf8Bom(encoding.GetString(bytes)), ClientTextEncodingKind.Utf8Bom);
        }

        var utf8Text = TryDecode(bytes, ClientTextEncodingKind.Utf8);
        var codePage936Text = TryDecode(bytes, ClientTextEncodingKind.CodePage936);

        if (utf8Text is not null && codePage936Text is not null)
        {
            return GetMojibakePenalty(utf8Text) <= GetMojibakePenalty(codePage936Text)
                ? new DecodedClientTextFile(utf8Text, ClientTextEncodingKind.Utf8)
                : new DecodedClientTextFile(codePage936Text, ClientTextEncodingKind.CodePage936);
        }

        if (utf8Text is not null)
        {
            return new DecodedClientTextFile(utf8Text, ClientTextEncodingKind.Utf8);
        }

        if (codePage936Text is not null)
        {
            return new DecodedClientTextFile(codePage936Text, ClientTextEncodingKind.CodePage936);
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var fallbackEncoding = Encoding.GetEncoding(
            936,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ReplacementFallback);
        return new DecodedClientTextFile(fallbackEncoding.GetString(bytes), ClientTextEncodingKind.CodePage936);
    }

    private static string? TryDecode(byte[] bytes, ClientTextEncodingKind encodingKind)
    {
        try
        {
            return ClientTextEncodings.GetEncoding(encodingKind).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static bool HasUtf8Bom(byte[] bytes)
    {
        return bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF;
    }

    private static string TrimUtf8Bom(string text)
    {
        return text.Length > 0 && text[0] == '\uFEFF'
            ? text[1..]
            : text;
    }

    private static int GetMojibakePenalty(string text)
    {
        var penalty = 0;
        foreach (var character in text)
        {
            if (character is '\uFFFD')
            {
                penalty += 20;
            }
            else if (character is >= '\uE000' and <= '\uF8FF')
            {
                penalty += 10;
            }
            else if (character is 'Ã' or 'Â' or '�')
            {
                penalty += 5;
            }
        }

        return penalty;
    }

    private static string GetLineFamily(string line)
    {
        if (TryGetServerKeyName(line, out var serverKeyName))
        {
            return CollapseServerKeyFamily(serverKeyName);
        }

        if (TryGetAssignmentKey(line, out var assignmentKey))
        {
            return assignmentKey;
        }

        if (line.Contains("@@", StringComparison.Ordinal))
        {
            return "<record>";
        }

        return "<no-key>";
    }

    private static bool TryGetServerKeyName(string line, out string keyName)
    {
        const string prefix = "[\"STR_";
        var prefixIndex = line.IndexOf(prefix, StringComparison.Ordinal);
        if (prefixIndex < 0)
        {
            keyName = string.Empty;
            return false;
        }

        var start = prefixIndex + 2;
        var end = line.IndexOf('"', start);
        if (end <= start)
        {
            keyName = string.Empty;
            return false;
        }

        keyName = line[start..end];
        return true;
    }

    private static string CollapseServerKeyFamily(string keyName)
    {
        var parts = keyName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var count = parts.Length;
        while (count > 0 && int.TryParse(parts[count - 1], NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            count--;
        }

        return count > 1 && count < parts.Length
            ? string.Join('_', parts.Take(count))
            : keyName;
    }

    private static bool TryGetAssignmentKey(string line, out string assignmentKey)
    {
        var trimmed = line.TrimStart();
        var equalsIndex = trimmed.IndexOf('=');
        if (equalsIndex is <= 0 or > 64)
        {
            assignmentKey = string.Empty;
            return false;
        }

        var candidate = trimmed[..equalsIndex].Trim();
        if (candidate.Length == 0)
        {
            assignmentKey = string.Empty;
            return false;
        }

        foreach (var character in candidate)
        {
            if (!char.IsLetterOrDigit(character) &&
                character is not '_' and not '-' and not '.')
            {
                assignmentKey = string.Empty;
                return false;
            }
        }

        assignmentKey = candidate;
        return true;
    }

    private static int CountCjkCharacters(string value)
    {
        var count = 0;
        foreach (var character in value)
        {
            if (IsCjkCharacter(character))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsCjkCharacter(char value)
    {
        return value is >= '\u3400' and <= '\u9FFF';
    }

    private static string NormalizeSample(string line)
    {
        var sample = line.Trim();
        if (sample.Length <= MaximumSampleLength)
        {
            return sample;
        }

        return sample[..MaximumSampleLength] + "...";
    }

    private static bool IsCandidateFile(string path)
    {
        var extension = Path.GetExtension(path);
        return CandidateExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record DecodedClientTextFile(string Text, ClientTextEncodingKind EncodingKind);

    private sealed class MutableCjkDebtFamily(string name, string sample)
    {
        public string Name { get; } = name;

        public string Sample { get; } = sample;

        public int CjkCharacterCount { get; set; }

        public int CjkLineCount { get; set; }
    }

    private sealed class MutableCjkDebtCategory
    {
        public int CjkCharacterCount { get; set; }

        public int CjkLineCount { get; set; }
    }
}
