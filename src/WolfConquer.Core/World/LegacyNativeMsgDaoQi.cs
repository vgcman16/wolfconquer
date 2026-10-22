#nullable disable

namespace WolfConquer.Core.World;

// Complete native MsgDaoQi mutable state and its fixed registered-contract wire conversion.
// This is not generic metadata registration, a handler or host adoption.
internal partial class LegacyNativeMsgDaoQi
{
    [Flags]
    public enum Types
    {
        INFO = 0,
        LOAD_ITEM = 1,
        BUY = 2,
        TAOIST_REIKI = 3,
        DRAWS = 4,
        EQUIP = 5,
        UN_EQUIP = 6,
        REPLACE = 8,
        ADDOBJ = 7,
        PIRATE_UPGRADE = 9,
        PIRATE_ITEM = 0xA,
        PIRATE_POINTS = 0xB,
        LOTTERY = 0xC
    }

    public class Item
    {
        public uint Id = 0u;
    }

    public Types Type = Types.INFO;
    public uint ItemID = 0u;
    public uint TaoistReiki = 0u;
    public uint LOTTERY;
    public uint Data5;
    public uint Data6;
    public uint MasteryPoints = 0u;
    public uint Data8;
    public uint PirateExp;
    public uint Data10;
    public uint Position = 0u;
    public uint Done = 0u;
    public List<Item> Items = new List<Item>();
    public uint[] Positions;

    public static implicit operator byte[](LegacyNativeMsgDaoQi obj)
    {
        return LegacyNativeCombatGearWire.Encode(obj, 2451);
    }
}
