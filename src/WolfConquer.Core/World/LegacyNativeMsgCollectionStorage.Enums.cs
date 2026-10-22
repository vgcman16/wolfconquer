namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgCollectionStorage
{
    [Flags]
    public enum Types : uint
    {
        STORAGE_LOAD = 0u,
        STORAGE_ACTIVATE = 1u,
        STORAGE_GEAR = 2u,
        STORAGE_TAKEOFF = 3u,
        STORAGE_TAKEOUT = 4u,
        STORAGE_EQUIP = 5u,
        STORAGE_ITEM = 6u,
        STORAGE_GIVE_AWAY = 8u
    }
}
