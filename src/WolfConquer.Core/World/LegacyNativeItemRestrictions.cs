namespace WolfConquer.Core.World;

internal static class LegacyNativeItemRestrictions
{
    // Native MrRayzo.Database/ItemType.cs:328..333, unabletradeitem.
    // Exact IDs only; neither Trade nor Drop may infer neighboring categories.
    internal static bool IsUnableTradeItem(uint itemId) => itemId is
        750000 or 722700 or 3306885 or 3307142 or 3600025 or 3200000 or
        729549 or 3008994 or 3200005 or 3600029 or 3007108 or 3007109 or
        3007110 or 3200004 or 3600031 or 3600027 or 3600024;

    // Native MrRayzo.Game.MsgServer/MsgItemUsuagePacket.cs:1351..1353.
    // This protected-drop branch is distinct from unabletradeitem at 1361.
    internal static bool IsDropProtectedItem(uint itemId) => itemId is
        2168735 or 2168725 or 2168715 or 2168705;
}
