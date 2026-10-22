using System.Collections.Concurrent;
using System.Collections.Generic;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

// Literal native Equip declaration-order state, constructor, AllItems and GetItem only.
// This is the genuine same-item storage prerequisite, NOT QueryEquipment/UpdateStats/Add/Remove.
internal sealed class LegacyNativeEquipItemStorage
{
    public int Rank;
    public int Chance;
    public string? Name;
    public uint ID;
    public byte Color;
    public byte Sockets;
    public byte Plus;
    public ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> ClientItems = new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>();
    public uint SoulsPotency = 0u;
    public int WeaponsMinAttack = 0;
    public uint ArmorID;
    public bool CreateSpawn = true;
    public bool SuperArmor = false;
    public Flags.ItemEffect RightWeaponEffect = Flags.ItemEffect.None;
    public Flags.ItemEffect RingEffect = Flags.ItemEffect.None;
    public Flags.ItemEffect NecklaceEffect = Flags.ItemEffect.None;
    public bool UseMonkEpicWeapon = false;
    public uint ShieldID = 0u;
    public uint RidingCrop = 0u;
    public uint HeadID;
    public uint RightWeapon = 0u;
    public uint LeftWeapon = 0u;
    public uint SteedPlusPorgres = 0u;
    public int rangeR = 0;
    public int rangeL = 0;
    public int SizeAdd = 0;
    public int SpeedR = 0;
    public int SpeedL = 0;
    public int SpeedRing = 0;
    public bool SuperDragonGem = false;
    public bool SuperPheonixGem = false;
    public bool SuperVioletGem = false;
    public bool SuperRaibowGem = false;
    public bool SuperMoonGem = false;
    public bool SuprtTortoiseGem = false;
    public bool SuperKylinGem = false;
    public bool SuperFuryGem = false;
    public bool HaveBless = false;
    public uint DashRate;
    public bool Alternante = false;
    private LegacyNativeGameClientRuntime? Owner;
    public LegacyNativeGameItemRuntime[] CurentEquip = new LegacyNativeGameItemRuntime[0];
    public int BattlePower = 0;

    public ICollection<LegacyNativeGameItemRuntime> AllItems => ClientItems.Values;

    public LegacyNativeGameItemRuntime? GetItem(uint UID)
    {
        LegacyNativeGameItemRuntime? value = null;
        ClientItems.TryGetValue(UID, out value);
        return value;
    }

    public LegacyNativeEquipItemStorage(LegacyNativeGameClientRuntime? client)
    {
        Owner = client;
    }
}
