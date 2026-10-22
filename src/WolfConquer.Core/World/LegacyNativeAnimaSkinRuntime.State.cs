using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Complete native AnimaSkin physical declarations, getter and constructor.
// No automatic member birth, profile caller or packet receiver is installed.
internal partial class LegacyNativeAnimaSkinRuntime
{
    public class Item
    {
        public uint ID;

        public bool isEquip;
    }

    public ConcurrentDictionary<uint, Item> Items;

    public LegacyNativeGameClientRuntime user;

    public bool Load = false;

    public uint isEquip => Items.Values.Where((Item p) => p.isEquip).FirstOrDefault()?.ID ?? 0;

    public LegacyNativeAnimaSkinRuntime(LegacyNativeGameClientRuntime _user)
    {
        user = _user;
        Items = new ConcurrentDictionary<uint, Item>();
    }
}
