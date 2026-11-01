using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static class LegacyEquipmentQueryBounds
{
    internal static int Hitpoints(long value) => value < 0
        ? throw new InvalidDataException("equipmentHitpointsNegative") : checked((int)value);

    internal static int AddHitpoints(long current, uint addition) =>
        checked(Hitpoints(current) + checked((int)addition));

    internal static int ClampHitpoints(long current, uint maximum) =>
        Math.Min(Hitpoints(current), checked((int)maximum));

    internal static ushort ClampVigor(uint current, ushort maximum) =>
        checked((ushort)Math.Min(current, (uint)maximum));

    internal static uint ReadVigor(CharacterSnapshot character)
    {
        var proof = LegacyNativeAttributeMapProof.CaptureRaw(character.Attributes);
        var rows = proof.Fields.Where(pair => pair.Key.Equals("Vigor", StringComparison.OrdinalIgnoreCase)).ToArray();
        uint value = 0;
        if (rows.Length > 1 || rows.Length == 1 && !uint.TryParse(rows[0].Value,
                NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            throw new InvalidDataException("equipmentVigorInvalidOrAmbiguous");
        if (!proof.IsCurrentPlain()) throw new InvalidDataException("equipmentVigorChanged");
        return value;
    }

    internal static CharacterSnapshot WithHitpoints(CharacterSnapshot character, int value)
    {
        _ = Hitpoints(value);
        var proof = LegacyNativeAttributeMapProof.CaptureRaw(character.Attributes);
        var keys = proof.Fields.Keys.Where(key => key.Equals("MinHitPoints", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (keys.Length > 1) throw new InvalidDataException("equipmentHitpointsAmbiguous");
        var fields = new Dictionary<string, string>(proof.Fields, proof.CapturedComparer)
        { [keys.Length == 0 ? "MinHitPoints" : keys[0]] = value.ToString(CultureInfo.InvariantCulture) };
        if (!proof.IsCurrentPlain()) throw new InvalidDataException("equipmentHitpointsChanged");
        return character with { MinHitPoints = value, Attributes = fields };
    }

    internal static uint ReduceHitpoints(uint maximum, uint percent)
    {
        if (percent > 100) throw new InvalidDataException("equipmentSacrificePercentOutOfRange");
        return checked(maximum - (uint)((ulong)maximum * percent / 100));
    }
}
