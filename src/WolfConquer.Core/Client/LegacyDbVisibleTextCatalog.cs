using System.Text.Json;
using System.Text.Json.Serialization;

namespace WolfConquer.Core.Client;

public sealed record LegacyDbVisibleTextReplacement(
    string RelativePath,
    string Section,
    string Key,
    string SourceValue,
    string ReplacementValue,
    string SourceEvidence);

/// <summary>Loads explicit, caller-owned DB text replacements; no game text is bundled.</summary>
public static class LegacyDbVisibleTextCatalog
{
    public static IReadOnlyList<LegacyDbVisibleTextReplacement> Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var rows = JsonSerializer.Deserialize<LegacyDbVisibleTextReplacement[]>(File.ReadAllText(path),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            }) ?? throw new InvalidDataException("A JSON array of explicit DB text replacements is required.");
        if (rows.Length == 0)
        {
            throw new ArgumentException("The DB text replacement catalog cannot be empty.", nameof(path));
        }
        foreach (var row in rows)
        {
            ArgumentNullException.ThrowIfNull(row);
            EnglishClientPatch.ValidateRelativePath(row.RelativePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(row.Section);
            ArgumentException.ThrowIfNullOrWhiteSpace(row.Key);
            ArgumentNullException.ThrowIfNull(row.SourceValue);
            ArgumentNullException.ThrowIfNull(row.ReplacementValue);
            ArgumentException.ThrowIfNullOrWhiteSpace(row.SourceEvidence);
        }
        return rows;
    }
}
