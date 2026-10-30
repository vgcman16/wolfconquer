using System.Globalization;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

internal static partial class Program
{
    private static int CreateGmPolicy(string[] args)
    {
        try
        {
            if (args.Length is not (5 or 6) ||
                !uint.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var account) || account == 0 ||
                !uint.TryParse(args[4], NumberStyles.None, CultureInfo.InvariantCulture, out var uid) || uid == 0)
                throw new ArgumentException("Explicit numeric account and character required.");
            var capabilities = (args.Length == 5 ? "both" : args[5]) switch
            {
                "inspect" => new[] { "inspect.self" },
                "mail" => new[] { "mail.testkit.self" },
                "both" => new[] { "inspect.self", "mail.testkit.self" },
                _ => throw new ArgumentException("A closed GM capability selection is required.")
            };
            var output = RequireGmPolicyOutput(args[1]);
            var configuration = RequireGmOrdinaryFile(args[2]);
            if (!LegacyOwnedAdmissionOptions.TryReadFile(configuration, out var options, out _) || options is null)
                throw new InvalidDataException("Existing owned admission configuration required.");
            _ = RequireGmOrdinaryFile(configuration);
            var source = Path.Combine(options.UsersRoot, account.ToString(CultureInfo.InvariantCulture) + ".ini");
            var before = ReadGmCharacterBytes(source);
            var text = DecodeGmCharacter(before);
            RequireGmCharacterFields(text);
            if (CharacterSnapshotParser.ParseText(text, Path.GetFileName(source)).Uid != uid)
                throw new InvalidDataException("Requested saved character identity does not match.");
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1, expectedDomain = options.ExpectedDomain.ToString("D"),
                grants = new[] { new { accountId = account, characterUid = uid, capabilities } }
            }, new JsonSerializerOptions { WriteIndented = true });
            _ = LegacyGmPolicy.Parse(bytes);
            if (!LegacyOwnedAdmissionOptions.TryReadFile(configuration, out var latest, out _) || latest != options ||
                !before.AsSpan().SequenceEqual(ReadGmCharacterBytes(source)))
                throw new IOException("Selected source changed during policy preparation.");
            _ = RequireGmOrdinaryFile(configuration);
            PublishGmPolicy(output, bytes);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                expectedDomain = options.ExpectedDomain, accountId = account, characterUid = uid, capabilities,
                policyCreated = true, activated = false,
                note = "Review the file. No live permissions, environment, registry or server process changed."
            }));
            return 0;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
            InvalidOperationException or JsonException or FormatException or KeyNotFoundException or
            NotSupportedException or System.Security.SecurityException)
        {
            Console.Error.WriteLine("GM policy creation failed; any created output remains inert. Existing files were not overwritten; no live permissions changed.");
            return 1;
        }
    }

    private static string RequireGmLocalPath(string supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied) || supplied.Length > 4096 || !Path.IsPathFullyQualified(supplied) ||
            supplied.StartsWith("\\\\", StringComparison.Ordinal) || supplied.StartsWith("//", StringComparison.Ordinal) ||
            supplied.Any(char.IsControl) || OperatingSystem.IsWindows() &&
            (supplied.Length < 3 || supplied[1] != ':' || supplied.IndexOf(':', 2) >= 0))
            throw new ArgumentException("An absolute ordinary local path is required.");
        return Path.GetFullPath(supplied);
    }

    private static void RequireGmOrdinaryParents(string path)
    {
        for (var parent = new FileInfo(path).Directory; parent is not null; parent = parent.Parent)
        {
            parent.Refresh();
            if (!parent.Exists || (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("An ordinary existing parent is required.");
        }
    }

    private static string RequireGmOrdinaryFile(string supplied)
    {
        var path = RequireGmLocalPath(supplied);
        var file = new FileInfo(path); file.Refresh();
        if (!file.Exists || (file.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new IOException("An ordinary existing file is required.");
        RequireGmOrdinaryParents(path);
        return path;
    }

    private static string RequireGmPolicyOutput(string supplied)
    {
        var output = RequireGmLocalPath(supplied);
        if (!string.Equals(Path.GetExtension(output), ".json", StringComparison.OrdinalIgnoreCase) ||
            File.Exists(output) || Directory.Exists(output) || new FileInfo(output).LinkTarget is not null)
            throw new IOException("A new JSON output is required.");
        RequireGmOrdinaryParents(output);
        return output;
    }

    private static byte[] ReadGmCharacterBytes(string supplied)
    {
        var path = RequireGmOrdinaryFile(supplied);
        using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (source.Length is < 1 or > 16 * 1024 * 1024) throw new InvalidDataException("Character image length invalid.");
        var bytes = new byte[checked((int)source.Length)]; source.ReadExactly(bytes);
        if (source.ReadByte() != -1 || source.Length != bytes.Length) throw new IOException("Character image changed.");
        _ = RequireGmOrdinaryFile(path);
        return bytes;
    }

    private static string DecodeGmCharacter(byte[] bytes)
    {
        var offset = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0;
        try { return new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset); }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    private static void RequireGmCharacterFields(string text)
    {
        var selected = false; var sections = 0;
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } raw)
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                selected = line[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                if (selected && ++sections != 1) throw new InvalidDataException("Character section ambiguous.");
                continue;
            }
            if (!selected || line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            var equals = line.IndexOf('=');
            if (equals <= 0 || !keys.Add(line[..equals].Trim()))
                throw new InvalidDataException("Character fields ambiguous or invalid.");
        }
        if (sections != 1) throw new InvalidDataException("Character section missing.");
    }

    private static void PublishGmPolicy(string path, byte[] bytes)
    {
        _ = RequireGmPolicyOutput(path);
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        output.Write(bytes); output.Flush(flushToDisk: true);
        _ = RequireGmOrdinaryFile(path);
        output.Position = 0;
        var actual = new byte[bytes.Length]; output.ReadExactly(actual);
        if (output.ReadByte() != -1 || output.Length != bytes.Length || !actual.AsSpan().SequenceEqual(bytes))
            throw new IOException("Policy output image changed.");
        _ = LegacyGmPolicy.Parse(actual);
        _ = RequireGmOrdinaryFile(path);
    }
}
