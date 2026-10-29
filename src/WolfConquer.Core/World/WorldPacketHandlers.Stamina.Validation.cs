using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Presence and validity are distinct. Only genuine absence retains the
    // existing blessing defaults; malformed input never grants default funds.
    private static bool TryReadLegacyRuntimeStamina(CharacterSnapshot character, out ulong stamina)
    {
        stamina = 0;
        if (character?.Attributes is not { } attributes) return false;
        if (attributes.TryGetValue("Stamina", out var raw))
            return ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out stamina);
        stamina = TryReadUInt64(attributes, out var blessing, "HeavenBlessing", "HeavensBlessing") && blessing > 0
            ? 150UL : 100UL;
        return true;
    }
}
