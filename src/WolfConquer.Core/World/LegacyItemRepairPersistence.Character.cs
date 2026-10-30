using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyItemRepairPersistence
{
    private static bool CharactersHaveOnlyPermittedDelta(CharacterSnapshot before, CharacterSnapshot after,
        LegacyItemRepairPayment payment)
    {
        if (before.Money < 0 || after.Money < 0 ||
            (payment == LegacyItemRepairPayment.Money ? after.Money >= before.Money : after.Money != before.Money) ||
            !WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(before, after with { Money = before.Money }) ||
            before.SourceFile != after.SourceFile || !ReferenceEquals(before.PlayerQuests, after.PlayerQuests) ||
            !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(before, after) || before.Attributes.Count != after.Attributes.Count) return false;
        foreach (var pair in before.Attributes)
        {
            if (!after.Attributes.TryGetValue(pair.Key, out var value)) return false;
            if (payment == LegacyItemRepairPayment.Money && string.Equals(pair.Key, "Money", StringComparison.OrdinalIgnoreCase))
            {
                if (value != pair.Value && value != after.Money.ToString(CultureInfo.InvariantCulture)) return false;
            }
            else if (!string.Equals(pair.Value, value, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static bool CharacterBaselineMatches(CharacterSnapshot disk, CharacterSnapshot current) =>
        disk.Uid == current.Uid && disk.Body == current.Body && disk.Class == current.Class &&
        disk.Reborn == current.Reborn && disk.Level == current.Level && disk.Strength == current.Strength &&
        disk.Agility == current.Agility && disk.Money == current.Money && disk.ConquerPoints == current.ConquerPoints &&
        LegacyTradeEscrowClaims.TryRead(disk.Attributes, out var storedClaim, out _) && storedClaim is null &&
        LegacyTradeEscrowClaims.TryRead(current.Attributes, out var claim, out _) && claim is null &&
        LegacySecondaryPasswordState.TryRead(disk.Attributes, out var storedPin, out _) &&
        LegacySecondaryPasswordState.TryRead(current.Attributes, out var pin, out _) && storedPin == pin &&
        disk.Attributes.TryGetValue("SecurityPass", out var storedRaw) == current.Attributes.TryGetValue("SecurityPass", out var currentRaw) &&
        string.Equals(storedRaw, currentRaw, StringComparison.Ordinal);

    private static byte[] PrepareCharacterImage(byte[] raw, CharacterSnapshot before, CharacterSnapshot after,
        LegacyItemRepairPayment payment)
    {
        var text = CharacterSnapshotFileEncoding.Decode(raw, out var encoding);
        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(raw)) throw Invalid("characterEncodingUnproven");
        var disk = CharacterSnapshotParser.ParseText(text);
        if (!CharacterBaselineMatches(disk, before)) throw Invalid("characterBaselineStale");
        if (payment == LegacyItemRepairPayment.Meteors) return raw.ToArray();
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { ["Money"] = after.Money.ToString(CultureInfo.InvariantCulture) };
        var projected = LegacyOrdinaryAdmissionCharacterText.Project(text, values, paid: null);
        var parsed = CharacterSnapshotParser.ParseText(projected);
        if (!WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(parsed, disk with { Money = after.Money }) ||
            !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(parsed, disk) ||
            parsed.Attributes.Count != disk.Attributes.Count ||
            parsed.Attributes.Any(pair => !disk.Attributes.TryGetValue(pair.Key, out var old) ||
                (!string.Equals(pair.Key, "Money", StringComparison.OrdinalIgnoreCase) && old != pair.Value)))
            throw Invalid("characterProjectionChangedOtherFields");
        var result = encoding.GetPreamble().Concat(encoding.GetBytes(projected)).ToArray();
        if (result.Length > CharacterSnapshotFileBaseline.MaximumBytes) throw Invalid("characterImageTooLarge");
        return result;
    }
}
