using System.Collections.ObjectModel;
using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Saved DATA only. An ID or matching row never issues a producer capability.
internal static partial class LegacyDerivedSpellOrigins
{
    internal const string Key = "ManagedDerivedSpellOrigins";

    internal static IReadOnlyDictionary<ushort, ushort> Read(CharacterSnapshot character)
    {
        var value = Encoded(character);
        var result = new Dictionary<ushort, ushort>();
        if (value is null or "") return new ReadOnlyDictionary<ushort, ushort>(result);
        if (value.Length > 31) throw Invalid();
        ushort previous = 0;
        foreach (var token in value.Split(','))
        {
            var fields = token.Split(':');
            if (fields.Length != 2 || !ushort.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id) ||
                !ushort.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var level) ||
                id <= previous || !IsDomain(id) || level is 0 or > byte.MaxValue ||
                fields[0] != id.ToString(CultureInfo.InvariantCulture) || fields[1] != level.ToString(CultureInfo.InvariantCulture))
                throw Invalid();
            result.Add(id, level); previous = id;
        }
        if (result.Count > 2) throw Invalid();
        RequireNoRuneOverlap(character, result);
        return new ReadOnlyDictionary<ushort, ushort>(result);
    }

    internal static void RequireSavedRows(CharacterSnapshot character, byte[]? image)
    {
        var origins = Read(character);
        if (origins.Count == 0) return;
        if (image is null) throw Invalid();
        var rows = Freeze(PlayerSpellSnapshotLoader.Parse(image)).Spells;
        RequireRows(origins, rows);
        for (int index = 0; index < rows.Count; index++)
            if (origins.ContainsKey(rows[index].Id) && image[sizeof(int) + index * PlayerSpellSnapshotLoader.LegacySpellSize + 11] != 0)
                throw Invalid();
    }

    internal static byte[] ProjectCharacterImage(byte[] before, CharacterSnapshot proposed)
    {
        if (before is null || before.Length is 0 or > CharacterSnapshotFileBaseline.MaximumBytes) throw Invalid();
        var text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(before)) throw Invalid();
        var original = CharacterSnapshotParser.ParseText(text);
        if (original.Uid != proposed.Uid) throw Invalid();
        _ = Read(original); _ = Read(proposed);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Encoded(proposed) is { } encoded) values.Add(Key, encoded);
        else if (Encoded(original) is not null) throw Invalid();
        var projected = LegacyOrdinaryAdmissionCharacterText.Project(text, values, null);
        var after = encoding.GetPreamble().Concat(encoding.GetBytes(projected)).ToArray();
        if (after.Length > CharacterSnapshotFileBaseline.MaximumBytes) throw Invalid();
        return after;
    }

    internal static CharacterSnapshot WithOrigins(CharacterSnapshot character, IReadOnlyDictionary<ushort, ushort> origins)
    {
        _ = Read(character);
        if (origins.Count > 2 || origins.Any(pair => !IsDomain(pair.Key) || pair.Value is 0 or > byte.MaxValue)) throw Invalid();
        var encoded = string.Join(',', origins.OrderBy(pair => pair.Key).Select(pair =>
            pair.Key.ToString(CultureInfo.InvariantCulture) + ":" + pair.Value.ToString(CultureInfo.InvariantCulture)));
        var source = CaptureMap(character.Attributes);
        var values = new Dictionary<string, string>(source.CapturedComparer);
        foreach (var pair in source.Fields)
            if (!pair.Key.Equals(Key, StringComparison.OrdinalIgnoreCase)) values.Add(pair.Key, pair.Value);
        if (encoded.Length != 0 || Encoded(character) is not null) values.Add(Key, encoded);
        if (!source.IsCurrentPlain()) throw Invalid();
        var result = character with { Attributes = new ReadOnlyDictionary<string, string>(values) };
        _ = Read(result); return result;
    }

    internal static PlayerSpellSnapshotEntry Granted(ushort id, ushort level) => new(id, level, 0, 0, 0, 0);
    internal static bool IsDomain(ushort id) => id is 21_930 or 20_000;
    internal static InvalidDataException Invalid() => new("derivedSpellOriginOrReconciliationInvalid");

    internal static PlayerSpellSnapshot Freeze(PlayerSpellSnapshot spells)
    {
        try { return LegacyNativeSpellStorage.FreezeSessionSnapshot(spells); }
        catch (InvalidOperationException error) { throw new InvalidDataException("derivedSpellMembershipInvalid", error); }
    }

    internal static CharacterSnapshot CopyCharacter(CharacterSnapshot character) => character with
    {
        Attributes = Map(character.Attributes), PaidNobility = Map(character.PaidNobility),
    };

    private static LegacyNativeAttributeMapProof CaptureMap(IReadOnlyDictionary<string, string> source)
    {
        // Only the owned marker is case-insensitive. Unrelated saved fields may
        // legally differ by case; retain their comparer and complete raw witness.
        try { return LegacyNativeAttributeMapProof.CaptureRaw(source); }
        catch (InvalidOperationException error) { throw new InvalidDataException("derivedSpellAttributesInvalid", error); }
    }

    private static IReadOnlyDictionary<string, string> Map(IReadOnlyDictionary<string, string> source)
    {
        var captured = CaptureMap(source);
        if (!captured.IsCurrentPlain()) throw Invalid();
        return captured.Fields;
    }

    private static string? Encoded(CharacterSnapshot character)
    {
        var fields = Map(character.Attributes);
        string? encoded = null;
        foreach (var pair in fields)
            if (pair.Key.Equals(Key, StringComparison.OrdinalIgnoreCase))
            {
                if (encoded is not null) throw Invalid();
                encoded = pair.Value;
            }
        return encoded;
    }

    private static void RequireNoRuneOverlap(CharacterSnapshot character, IReadOnlyDictionary<ushort, ushort> origins)
    {
        if (origins.Count != 0 && LegacyRuneSpellOrigins.Read(character).Keys.Any(origins.ContainsKey)) throw Invalid();
    }

    private static void RequireRows(IReadOnlyDictionary<ushort, ushort> origins, IReadOnlyList<PlayerSpellSnapshotEntry> rows)
    {
        var indexed = rows.ToDictionary(row => row.Id);
        foreach (var pair in origins)
            if (!indexed.TryGetValue(pair.Key, out var row) || row != Granted(pair.Key, pair.Value)) throw Invalid();
    }
}
