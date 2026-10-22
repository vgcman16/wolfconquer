using System.Text.Json;
using System.Text.Json.Serialization;
using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.Install;

internal sealed record InstalledFile(string Path, long Length, string Sha256);
internal sealed record InstalledState(string ProductId, string Channel, long Sequence,
    string PayloadSha256, string EnvelopeSha256, long EnvelopeLength, List<InstalledFile> Files);
internal sealed record JournalFile(string Path, long Length, string Sha256, string? OriginalSha256,
    int OriginalAttributes, long OriginalWriteTicks);
internal sealed record InstallJournal(int Version, string Id, string Phase, string ProductId,
    string Channel, long Sequence, string PayloadSha256, string? BeforeStateSha256,
    string NextStateSha256, List<JournalFile> Files, List<string> CreatedDirectories);

internal static class InstallMetadata
{
    internal const int HardMaxFiles = 200000;
    internal const long MaximumBytes = 256L * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 16
    };

    internal static byte[] Bytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Json);
    internal static T Parse<T>(byte[] bytes) where T : class
    {
        try
        {
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 16 });
            var objects = new Stack<HashSet<string>>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject) objects.Push(new(StringComparer.Ordinal));
                else if (reader.TokenType == JsonTokenType.EndObject) objects.Pop();
                else if (reader.TokenType == JsonTokenType.PropertyName && !objects.Peek().Add(reader.GetString()!))
                    throw new InvalidDataException("Duplicate updater metadata member.");
            }
            return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new InvalidDataException("Null updater metadata.");
        }
        catch (JsonException error) { throw new InvalidDataException("Malformed updater metadata.", error); }
    }

    internal static void Validate(InstalledState state)
    {
        Identity(state.ProductId, state.Channel, state.Sequence, state.PayloadSha256);
        if (!InstallFileSystem.IsHash(state.EnvelopeSha256) || state.EnvelopeLength is < 1 or > (96L << 20))
            throw new InvalidDataException("Invalid installed envelope identity.");
        if (state.Files is null || state.Files.Count > HardMaxFiles) throw new InvalidDataException("Invalid installed file count.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in state.Files)
        {
            if (file is null) throw new InvalidDataException("Null installed file.");
            File(file.Path, file.Length, file.Sha256, names);
        }
        foreach (var file in state.Files)
            for (var slash = file.Path.LastIndexOf('/'); slash >= 0; slash = file.Path.LastIndexOf('/', slash - 1))
                if (names.Contains(file.Path[..slash])) throw new InvalidDataException("An installed file is also a directory.");
    }

    internal static void Validate(InstallJournal journal, string directoryName)
    {
        Identity(journal.ProductId, journal.Channel, journal.Sequence, journal.PayloadSha256);
        if (journal.Version != 1 || journal.Id != directoryName || !Guid.TryParseExact(journal.Id, "N", out _) ||
            journal.Phase is not ("prepared" or "committed" or "rolledback") ||
            !InstallFileSystem.IsHash(journal.NextStateSha256) ||
            (journal.BeforeStateSha256 is not null && !InstallFileSystem.IsHash(journal.BeforeStateSha256)) ||
            journal.Files is null || journal.Files.Count > HardMaxFiles || journal.CreatedDirectories is null ||
            journal.CreatedDirectories.Count > HardMaxFiles * 32)
            throw new InvalidDataException("Invalid recovery journal.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in journal.Files)
        {
            if (file is null) throw new InvalidDataException("Null journal file.");
            File(file.Path, file.Length, file.Sha256, names);
            if (file.OriginalSha256 is not null && !InstallFileSystem.IsHash(file.OriginalSha256))
                throw new InvalidDataException("Invalid original hash.");
            if (file.OriginalWriteTicks < DateTime.MinValue.Ticks || file.OriginalWriteTicks > DateTime.MaxValue.Ticks ||
                (file.OriginalAttributes & (int)(FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                throw new InvalidDataException("Invalid original file metadata.");
        }
        var ancestors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in journal.Files)
            for (var slash = file.Path.LastIndexOf('/'); slash >= 0; slash = file.Path.LastIndexOf('/', slash - 1))
                ancestors.Add(file.Path[..slash]);
        if (ancestors.Any(names.Contains)) throw new InvalidDataException("A recovery file is also a directory.");
        names.Clear();
        foreach (var directory in journal.CreatedDirectories)
        {
            UpdatePathPolicy.ValidateRelativePath(directory);
            if (!names.Add(directory)) throw new InvalidDataException("Duplicate recovery directory.");
            if (!ancestors.Contains(directory))
                throw new InvalidDataException("Recovery directory does not own a target ancestor.");
        }
    }

    private static void Identity(string product, string channel, long sequence, string hash)
    {
        if (string.IsNullOrWhiteSpace(product) || string.IsNullOrWhiteSpace(channel) || sequence < 1 || !InstallFileSystem.IsHash(hash))
            throw new InvalidDataException("Invalid update identity.");
    }
    private static void File(string path, long length, string hash, HashSet<string> names)
    {
        UpdatePathPolicy.ValidateRelativePath(path);
        if (length is < 0 or > (64L << 30) || !InstallFileSystem.IsHash(hash) || !names.Add(path))
            throw new InvalidDataException("Invalid or case-colliding updater file.");
    }
}
