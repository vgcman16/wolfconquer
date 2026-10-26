using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void ValidateMailboxGearImage(byte[] image)
    {
        if (image.Length is 0 or > MaxTargetBytes) throw new ArgumentException("CombatGear image size is invalid.");
        var utf8 = new UTF8Encoding(false, true);
        var text = utf8.GetString(image);
        var sections = IniDocument.Parse(text).Sections;
        if (!sections.TryGetValue("CombatGear", out var main) || !sections.TryGetValue("BlackJadeBag", out var bag) ||
            !TryMailboxGearCount(main, out var items) || !TryMailboxGearCount(bag, out var jades) ||
            items != sections.Keys.Count(key => key.StartsWith("Item", StringComparison.OrdinalIgnoreCase)) ||
            jades > bag.Count - 1)
            throw new ArgumentException("CombatGear counts do not match their complete image.");
        // Bound allocation by actual present sections/keys BEFORE the legacy parser,
        // whose permissive fallback and unbounded count allocation are not admission.
        for (var index = 0; index < items; index++)
            if (!sections.ContainsKey("Item" + index.ToString(CultureInfo.InvariantCulture)))
                throw new ArgumentException("CombatGear item section is missing.");
        for (var index = 0; index < jades; index++)
            if (!bag.ContainsKey("BlackJade" + index.ToString(CultureInfo.InvariantCulture)))
                throw new ArgumentException("CombatGear BlackJade entry is missing.");
        var parsed = CombatGearSnapshotLoader.Parse(text);
        var canonical = utf8.GetBytes(CombatGearSnapshotLoader.ToLegacyText(parsed));
        if (!image.AsSpan().SequenceEqual(canonical))
            throw new ArgumentException("CombatGear image is not a complete canonical loader/writer image.");
    }

    private static bool TryMailboxGearCount(IReadOnlyDictionary<string, string> fields, out int count)
    {
        count = 0;
        return fields.TryGetValue("Count", out var raw) &&
            int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out count) && count >= 0;
    }
}
