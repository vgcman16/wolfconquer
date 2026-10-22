#nullable disable

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeCombatGearWire
{
    // CQ_COMBAT_GEAR110..123: field order is registration order, not declaration order.
    private static void Serialize(Stream output, LegacyNativeMsgAirPowerUp message)
    {
        if (message is null)
            return;
        Field(output, 1, unchecked((ulong)(long)(int)message.Type));
        Field(output, 2, message.UserId);
        Field(output, 3, message.PowerType);
        if (message.Powers is null)
            return;
        foreach (LegacyNativeMsgAirPowerUp.Power item in message.Powers)
        {
            using MemoryStream nested = new MemoryStream();
            // Pinned2.0.0.668 emits empty submessages for null LIST entries here,
            // unlike the skipped null ARRAY entries in Gear/Skin contracts.
            if (item is not null)
            {
                Field(nested, 1, item.Id);
                Field(nested, 2, item.SenseLevel);
                Field(nested, 3, item.SenseAipPower);
                Field(nested, 4, item.ArmedLevel);
                Field(nested, 5, item.ArmedAipPower);
                Field(nested, 6, item.OverLoadLevel);
                Field(nested, 7, item.OverLoadAipPower);
                Field(nested, 8, item.Points);
            }
            Nested(output, 4, nested);
        }
    }
}
