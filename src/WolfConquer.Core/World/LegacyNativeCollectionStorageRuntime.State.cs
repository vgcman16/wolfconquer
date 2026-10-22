using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Complete native CollectionStorage storage declarations and constructor.
// No snapshot DTO, automatic member birth or host persistence caller is installed.
internal partial class LegacyNativeCollectionStorageRuntime
{
    public class Scrap
    {
        public uint Type;

        public uint Amount;
    }

    public class Item
    {
        public uint Type = 0u;

        public uint Activate = 0u;

        public uint Amount = 0u;

        public uint RemainingTime = 0u;

        public uint F5 = 0u;
    }

    public ConcurrentDictionary<uint, Item> Items;

    public ConcurrentDictionary<uint, Scrap> Scraps;

    public LegacyNativeGameClientRuntime user;

    public LegacyNativeCollectionStorageRuntime(LegacyNativeGameClientRuntime _user)
    {
        user = _user;
        Items = new ConcurrentDictionary<uint, Item>();
        Scraps = new ConcurrentDictionary<uint, Scrap>();
    }
}
