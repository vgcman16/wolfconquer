using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyPlayerStatusCalculator
{
    private static void ApplyLegacyAuraAttributes(uint[] fields, CharacterSnapshot snapshot)
    {
        if (!WorldPacketHandlers.TryGetLegacyAuraStatEffect(
                snapshot,
                out var statusFlag,
                out var damage))
        {
            return;
        }

        switch (statusFlag)
        {
            case WorldPacketHandlers.LegacyTyrantAuraStatusFlag:
                Add(fields, LegacyPlayerStatusPacket.CriticalStrikeIndex, damage * 100u);
                break;

            case WorldPacketHandlers.LegacyFendAuraStatusFlag:
                Add(fields, LegacyPlayerStatusPacket.ImmunityIndex, damage * 100u);
                break;

            case WorldPacketHandlers.LegacyMetalAuraStatusFlag:
                Add(fields, LegacyPlayerStatusPacket.MetalResistanceIndex, damage);
                break;

            case WorldPacketHandlers.LegacyWoodAuraStatusFlag:
                Add(fields, LegacyPlayerStatusPacket.WoodResistanceIndex, damage);
                break;

            case WorldPacketHandlers.LegacyWaterAuraStatusFlag:
                Add(fields, LegacyPlayerStatusPacket.WaterResistanceIndex, damage);
                break;

            case WorldPacketHandlers.LegacyAurasFireStatusFlag:
                Add(fields, LegacyPlayerStatusPacket.FireResistanceIndex, damage);
                break;

            case WorldPacketHandlers.LegacyEarthAuraStatusFlag:
                Add(fields, LegacyPlayerStatusPacket.EarthResistanceIndex, damage);
                break;
        }
    }
}
