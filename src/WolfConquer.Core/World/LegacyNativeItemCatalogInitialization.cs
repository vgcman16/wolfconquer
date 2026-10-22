namespace WolfConquer.Core.World;

// Only the contiguous native MrRayzo/Server.cs198..201 catalog phase.
// No host/login caller, DbLocation producer or surrounding startup is installed.
internal static class LegacyNativeItemCatalogInitialization
{
    internal static void InitializeAtNativeCatalogPhase()
    {
        // Native globals retain every completed publication if a later
        // constructor or Loading faults; static catalog maps are not reset.
        LegacyNativePoolItems.ItemsBase = new LegacyNativeItemTypeRuneMetadata();
        LegacyNativePoolItems.RifineryItems = new LegacyNativeRifinery();
        LegacyNativePoolItems.DBRerinaryBoxes = new LegacyNativeRefinaryBoxes();
        LegacyNativePoolItems.ItemsBase.Loading();
    }
}
