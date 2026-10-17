using System.Text.Json;
using System.Text.Json.Serialization;

namespace WolfConquer.Core.Client;

/// <summary>Loads a caller-owned JSON array of text patch definitions.</summary>
public static class ClientPatchCatalog
{
    public static IReadOnlyList<ClientPatchFile> Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new JsonStringEnumConverter<ClientTextEncodingKind>(allowIntegerValues: false));
        var entries = JsonSerializer.Deserialize<CatalogFile[]>(File.ReadAllText(path), options)
            ?? throw new InvalidDataException("A JSON array of patch definitions is required.");
        var files = entries.Select(entry =>
        {
            if (entry is null)
            {
                throw new InvalidDataException("Catalog entries cannot be null.");
            }
            var replacements = (entry.Replacements ?? []).Select(replacement =>
            {
                if (replacement is null)
                {
                    throw new InvalidDataException("Catalog replacements cannot be null.");
                }
                return new ClientTextReplacement(replacement.Key, replacement.Value)
                {
                    Occurrence = replacement.Occurrence,
                    ReplaceLine = replacement.ReplaceLine,
                };
            }).ToArray();
            return new ClientPatchFile(entry.RelativePath, entry.EncodingKind, replacements,
                entry.CommentPrefixes, entry.FullTextPatch);
        }).ToArray();
        // A file-selection catalog may omit replacements; Apply requires actual patch operations.
        EnglishClientPatch.ValidateFiles(files, requireReplacements: false);
        return files;
    }

    private sealed class CatalogFile
    {
        public CatalogFile() { }
        public required string RelativePath { get; init; }
        public required ClientTextEncodingKind EncodingKind { get; init; }
        public CatalogReplacement[]? Replacements { get; init; }
        public string[]? CommentPrefixes { get; init; }
        public ClientFullTextPatch? FullTextPatch { get; init; }
    }

    private sealed class CatalogReplacement
    {
        public CatalogReplacement() { }
        public required string Key { get; init; }
        public required string Value { get; init; }
        public int Occurrence { get; init; } = 1;
        public bool ReplaceLine { get; init; }
    }
}
