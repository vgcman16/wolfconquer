using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal static CharacterSnapshot ProjectLegacyRuntimeStamina(CharacterSnapshot character, ushort stamina)
    {
        var edit = new RuntimeTickAttributeEdit(character, ["Stamina"]);
        edit.Set("Stamina", stamina.ToString(CultureInfo.InvariantCulture));
        return edit.Complete();
    }

    internal static CharacterSnapshot ProjectLegacyRuntimeXpState(CharacterSnapshot character,
        ulong? xpCount, bool? xpListActive)
    {
        var owned = new List<string>();
        if (xpCount is not null) owned.Add("XPCount");
        var registration = LegacyStatusFlagDeathRegistration.Key(LegacyRuntimeUpdateTicker.XpListFlag);
        if (xpListActive is not null) owned.AddRange(["StatusFlags", "BitVector", registration]);
        var edit = new RuntimeTickAttributeEdit(character, owned);
        if (xpCount is { } count) edit.Set("XPCount", count.ToString(CultureInfo.InvariantCulture));
        if (xpListActive is { } active)
        {
            var attributes = edit.Owned;
            var key = attributes.ContainsKey("BitVector") && !attributes.ContainsKey("StatusFlags") ? "BitVector" : "StatusFlags";
            var flags = LegacyPlayerDeathState.CreateStatusFlagVector(attributes, LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength);
            var wordIndex = LegacyRuntimeUpdateTicker.XpListFlag / 32;
            var bitIndex = LegacyRuntimeUpdateTicker.XpListFlag % 32;
            if (active)
            {
                LegacyStatusFlagDeathRegistration.PrepareAdd(attributes, LegacyRuntimeUpdateTicker.XpListFlag);
                if (!attributes.ContainsKey(registration)) edit.Remove(registration);
                flags[wordIndex] |= 1UL << bitIndex;
            }
            else
            {
                edit.Remove(registration);
                flags[wordIndex] &= ~(1UL << bitIndex);
            }
            edit.Set(key, string.Join('/', flags) + "/");
        }
        return edit.Complete();
    }
}
