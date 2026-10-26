using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static class LegacyOrdinaryAdmissionImages
{
    private static readonly string[] AllowedCharacterChanges =
        ["DonationNobility", "LastNobilityDonation", "NobilityRank", "NobilityPosition", "SecurityPass"];

    internal static CharacterSnapshot PrepareSnapshot(CharacterSnapshot source, DateTime localNow,
        out LegacySecondaryPasswordState pin)
    {
        var result = CharacterSnapshotLoader.PrepareExpiredPaidNobility(source, localNow);
        if (!LegacySecondaryPasswordState.TryRead(result.Attributes, out pin, out var reason))
            throw new InvalidDataException(reason);
        pin = pin.LoadAt(localNow, out var expired);
        if (!expired) return result;
        return result with { Attributes = new Dictionary<string, string>(result.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["SecurityPass"] = pin.ToLegacyText() } };
    }

    internal static byte[] Character(byte[] before, CharacterSnapshot prefix, CharacterSnapshot normalized)
    {
        var empty = new Dictionary<string, string>();
        if ((prefix with { Attributes = empty, PaidNobility = empty, PlayerQuests = PlayerQuestSnapshot.Empty }) !=
            (normalized with { Attributes = empty, PaidNobility = empty, PlayerQuests = PlayerQuestSnapshot.Empty }))
            throw new InvalidDataException("Ordinary admission cannot replace saved business scalars.");
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        { ["Online"] = prefix.Online.ToString(CultureInfo.InvariantCulture) };
        foreach (var key in prefix.Attributes.Keys.Concat(normalized.Attributes.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var had = prefix.Attributes.TryGetValue(key, out var old);
            var has = normalized.Attributes.TryGetValue(key, out var value);
            if (had == has && old == value) continue;
            if (!has || !AllowedCharacterChanges.Contains(key, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("Ordinary admission cannot replace unrelated Character fields.");
            values[key] = value!;
        }
        var paid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in normalized.PaidNobility)
            if (!prefix.PaidNobility.TryGetValue(pair.Key, out var old) || old != pair.Value) paid[pair.Key] = pair.Value;
        if (prefix.PaidNobility.Keys.Any(key => !normalized.PaidNobility.ContainsKey(key)))
            throw new InvalidDataException("Ordinary admission cannot remove PaidNobility fields.");
        var text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
        var projected = LegacyOrdinaryAdmissionCharacterText.Project(text, values, paid.Count == 0 ? null : paid);
        var result = encoding.GetPreamble().Concat(encoding.GetBytes(projected)).ToArray();
        if (result.Length > CharacterSnapshotFileBaseline.MaximumBytes) throw new InvalidDataException("Character image is too large.");
        return result;
    }

    internal static byte[] ProjectCharacter(byte[] before, DateTime localNow)
    {
        var snapshot = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(before, out _));
        var attrs = new Dictionary<string, string>(snapshot.Attributes, StringComparer.OrdinalIgnoreCase) { ["Online"] = "1" };
        var source = snapshot with { Online = 1, Attributes = attrs };
        var normalized = PrepareSnapshot(source, localNow, out _);
        return Character(before, source, normalized);
    }
}
