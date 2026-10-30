using System.Collections.ObjectModel;
using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// This records only absent spells actually created by the owned Rune producer.
// A legacy/unmarked row is never retroactively claimed as a temporary ability.
internal static class LegacyRuneSpellOrigins
{
    internal const string Key = "ManagedRuneSpellOrigins";

    internal static Dictionary<ushort, byte> Read(CharacterSnapshot character)
    {
        string? encoded = null;
        foreach (var pair in character.Attributes)
            if (pair.Key.Equals(Key, StringComparison.OrdinalIgnoreCase))
            {
                if (encoded is not null || pair.Value is null) throw Invalid();
                encoded = pair.Value;
            }
        var result = new Dictionary<ushort, byte>();
        if (encoded is null or "") return result;
        ushort previous = 0;
        foreach (var entry in encoded.Split(','))
        {
            var fields = entry.Split(':');
            if (fields.Length != 2 || !ushort.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id) ||
                !byte.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var level) || id <= previous || level > 99 ||
                fields[0] != id.ToString(CultureInfo.InvariantCulture) || fields[1] != level.ToString(CultureInfo.InvariantCulture)) throw Invalid();
            result.Add(id, level); previous = id;
        }
        return result;
    }

    internal static PlayerSpellSnapshotEntry Granted(ushort id, byte level) => new(id, level, 0, 0, 0, 0);

    internal static void RequireRows(CharacterSnapshot character, PlayerSpellSnapshot spells)
    {
        var rows = LegacyNativeSpellStorage.FreezeSessionSnapshot(spells).Spells.ToDictionary(row => row.Id);
        foreach (var (id, level) in Read(character))
            if (!rows.TryGetValue(id, out var row) || row != Granted(id, level)) throw Invalid();
    }

    internal static void RequireSavedRows(CharacterSnapshot character, byte[]? image)
    {
        var origins = Read(character);
        if (origins.Count == 0) return;
        if (image is null) throw Invalid();
        var rows = PlayerSpellSnapshotLoader.Parse(image).Spells;
        RequireRows(character, new(rows));
        for (var index = 0; index < rows.Count; index++)
            if (origins.ContainsKey(rows[index].Id) && image[sizeof(int) + index * PlayerSpellSnapshotLoader.LegacySpellSize + 11] != 0)
                throw Invalid();
    }

    internal static CharacterSnapshot WithOrigins(CharacterSnapshot character, IReadOnlyDictionary<ushort, byte> origins)
    {
        bool hadKey = character.Attributes.Keys.Any(key => key.Equals(Key, StringComparison.OrdinalIgnoreCase));
        var encoded = string.Join(',', origins.OrderBy(pair => pair.Key).Select(pair =>
            pair.Key.ToString(CultureInfo.InvariantCulture) + ":" + pair.Value.ToString(CultureInfo.InvariantCulture)));
        if (!hadKey && encoded.Length == 0) return character;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in character.Attributes)
            if (!pair.Key.Equals(Key, StringComparison.OrdinalIgnoreCase)) values.Add(pair.Key, pair.Value);
        values.Add(Key, encoded);
        var result = character with { Attributes = new ReadOnlyDictionary<string, string>(values) };
        _ = Read(result);
        return result;
    }

    internal static byte[] ProjectCharacterImage(byte[] before, CharacterSnapshot target)
    {
        var text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(before)) throw Invalid();
        var original = CharacterSnapshotParser.ParseText(text);
        _ = Read(original); _ = Read(target);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (original.MinHitPoints != target.MinHitPoints)
            values.Add("MinHitPoints", target.MinHitPoints.ToString(CultureInfo.InvariantCulture));
        foreach (var pair in target.Attributes)
            if (pair.Key.Equals(Key, StringComparison.OrdinalIgnoreCase)) values.Add(Key, pair.Value);
        var projected = LegacyOrdinaryAdmissionCharacterText.Project(text, values, null);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(projected)).ToArray();
        if (bytes.Length > CharacterSnapshotFileBaseline.MaximumBytes) throw Invalid();
        return bytes;
    }

    internal static byte[] ProjectSpellImage(byte[]? before, PlayerSpellSnapshot target)
    {
        var frozen = LegacyNativeSpellStorage.FreezeSessionSnapshot(target);
        var result = PlayerSpellSnapshotLoader.ToLegacyBytes(frozen);
        if (before is null) return result;
        var rows = PlayerSpellSnapshotLoader.Parse(before).Spells;
        var offsets = rows.Select((row, index) => (row.Id, Offset: sizeof(int) + index * PlayerSpellSnapshotLoader.LegacySpellSize))
            .ToDictionary(row => row.Id, row => row.Offset);
        for (var index = 0; index < frozen.Spells.Count; index++)
            if (offsets.TryGetValue(frozen.Spells[index].Id, out var offset))
                result[sizeof(int) + index * PlayerSpellSnapshotLoader.LegacySpellSize + 11] = before[offset + 11];
        return result;
    }

    private static InvalidDataException Invalid() => new("Rune spell origins require exact canonical unchanged producer-owned grant rows.");
}
