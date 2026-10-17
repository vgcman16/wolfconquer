using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyDirectPlayerPhysicalFuryStrikeRange = 7;
    private const long LegacyDirectPlayerPhysicalDragonhowlRange = 9;
    private const long LegacyDirectPlayerPhysicalArcherCombatGearRange = 18;

    private static bool IsLegacyDirectPlayerPhysicalTargetInRange(
        WorldSession session,
        WorldDataCatalog? worldData,
        long distance)
    {
        // MsgAttackPacket.cs:635 leaves NeptuneCurse as an outer caller rejection.
        if (distance <= LegacyDirectPlayerPhysicalRange ||
            (distance <= LegacyDirectPlayerPhysicalFuryStrikeRange &&
                HasLegacyDirectPlayerPhysicalFuryStrike(session, worldData)))
        {
            return true;
        }

        // MyCombatGear.GetDistance delegates to the class-selected equipped gear:
        // TQCombatGear.CombatGear.cs:1420-1442 uses distance9/18, not spell-row range.
        var combatGear = session.CombatGear ?? CombatGearSnapshot.Empty;
        var classCode = session.Character.Class;
        if (distance <= LegacyDirectPlayerPhysicalDragonhowlRange &&
            HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearDragonhowlItemId))
        {
            return true;
        }

        return distance <= LegacyDirectPlayerPhysicalArcherCombatGearRange &&
            (HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearStoneCrackerItemId) ||
                HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearColdMoonItemId) ||
                HasLegacyCombatGearEquippedForClass(combatGear, classCode, LegacyCombatGearTharnCutterItemId));
    }
}
