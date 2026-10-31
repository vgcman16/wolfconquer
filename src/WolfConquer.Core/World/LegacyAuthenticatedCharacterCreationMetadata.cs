using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyAuthenticatedCharacterCreationItemMetadata(uint ItemId, string RawHitpoints, string RawDurability);

// Historical deterministic inputs, not authentication, a grant or a live catalog owner.
internal sealed partial class LegacyAuthenticatedCharacterCreationMetadata
{
    private readonly LegacyAuthenticatedCharacterCreationItemMetadata[] items;
    private readonly string[]? profession;
    private readonly string[][] runeStorage;
    public LegacyAuthenticatedCharacterCreationItemMetadata[] Items => items.ToArray();
    public string[]? Profession => profession?.ToArray();
    public string[][] RuneStorageZeroScore => runeStorage.Select(row => row.ToArray()).ToArray();
    public string[][] InnerPowerRows => [];

    internal LegacyAuthenticatedCharacterCreationMetadata(LegacyAuthenticatedCharacterCreationItemMetadata[] items,
        string[]? profession, string[][] runeStorageZeroScore, string[][] innerPowerRows)
    {
        ArgumentNullException.ThrowIfNull(items); ArgumentNullException.ThrowIfNull(runeStorageZeroScore);
        ArgumentNullException.ThrowIfNull(innerPowerRows);
        if (innerPowerRows.Length != 0) throw Invalid("Fresh creation cannot adopt prior InnerPower state.");
        this.items = items.ToArray(); this.profession = profession?.ToArray();
        runeStorage = runeStorageZeroScore.Select(row => row?.ToArray() ?? throw Invalid("Missing Rune row.")).ToArray();
        Validate();
    }

    internal LegacyAuthenticatedCharacterCreationMetadata Copy() => new(items, profession, runeStorage, []);

    internal void Validate()
    {
        if (items.Length is < 1 or > 4 || items.Any(row => row is null || row.ItemId is 0 or > int.MaxValue) ||
            !items.Select(row => row.ItemId).SequenceEqual(items.Select(row => row.ItemId).Distinct().Order()))
            throw Invalid("Creation item dependencies are ambiguous or unordered.");
        foreach (var item in items)
        {
            _ = ReadUInt(item.RawHitpoints);
            if (ReadUInt(item.RawDurability) is 0 or > ushort.MaxValue) throw Invalid("Invalid actual starter durability.");
        }
        if (profession is not null) _ = ParseProfession(profession);
        if (runeStorage.Length > 128) throw Invalid("Too many applicable Rune storage rows.");
        foreach (var fields in runeStorage)
            if (ParseRune(fields).RequiredStorageScore != 0) throw Invalid("Creation Rune dependency is not a score-zero row.");
    }

    internal uint Hitpoints(uint baseHitpoints, long classCode, IEnumerable<LegacyOrdinaryStarterTemplate> templates)
    {
        ulong total = baseHitpoints;
        foreach (var template in templates) total = checked(total + ReadUInt(items.Single(row => row.ItemId == template.ItemId).RawHitpoints));
        if (profession is not null)
        {
            var row = ParseProfession(profession);
            if (row.ProfessionCode != classCode) throw Invalid("Profession dependency belongs to a different class.");
            foreach (int encoded in row.BonusValues)
                if (encoded > 0 && encoded / 10_000 == (int)LegacyChiAttributeType.HPAdd)
                    total = checked(total + (uint)(encoded % 10_000));
        }
        foreach (var fields in runeStorage)
            foreach (var bonus in ParseRune(fields).Bonuses)
                if (bonus.AttributeType == 1 && bonus.Value > 0) total = checked(total + (uint)bonus.Value);
        return total <= uint.MaxValue ? (uint)total : throw Invalid("Fresh character HP overflows its source field.");
    }

    internal static uint ReadUInt(string text)
    {
        if (text is null || text.Length is < 1 or > 64 ||
            !uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint value))
            throw Invalid("Malformed required creation item field.");
        return value;
    }
    internal static ProfessionLevelBenefit ParseProfession(string[] fields)
    {
        RequireFields(fields);
        return ProfessionParser.ParseLevelBenefitRow("captured-creation-profession", 1, string.Join("@@", fields));
    }
    internal static RuneStorageAttribute ParseRune(string[] fields)
    {
        RequireFields(fields);
        return RuneParser.ParseStorageAttributeRow("captured-creation-rune-storage", 1, string.Join("@@", fields));
    }
    private static void RequireFields(string[] fields)
    {
        if (fields.Length != 9 || fields[^1] != "" || fields.Any(value => value is null || value.Length > 64 || value.Contains("@@", StringComparison.Ordinal)))
            throw Invalid("Invalid captured creation table row.");
    }
    private static InvalidDataException Invalid(string message) => new(message);
}
