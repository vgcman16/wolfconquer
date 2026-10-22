#nullable disable

namespace WolfConquer.Core.World;

// Native mutable MsgCombatGearOpt model; the concrete wire projection does not
// install the RuntimeTypeModel registration or adopt a host receive path.
internal partial class LegacyNativeMsgCombatGearOpt
{
    [Flags]
    public enum Types
    {
        UPGRADE = 0,
        OPEN = 1,
        CLOSE = 2,
        TRANSFORM = 3,
        CANCEL = 4,
        ADD = 5,
        REMOVE = 6,
        VIEW = 7,
        UPGRADE_TAOIST = 8,
        MASTERY_SCORE_TAOIST = 9,
        UPGRADE_TAOIST_ALL = 0xA,
        UPGRADE_PIRATE = 0xB,
        MASTERY_SCORE_PIRATE = 0xE
    }

    public class Items
    {
        public uint Size = 0u;
        public uint UID = 0u;
    }

    public uint Transform = 0u;
    public LegacyNativeCombatGear.TypeID ItemID = LegacyNativeCombatGear.TypeID.None;
    public Types Type = Types.UPGRADE;
    public Items[] SelectMaterial;
    public uint Exp = 0u;
    public uint UID = 0u;
    public uint[] U;

    public static implicit operator byte[](LegacyNativeMsgCombatGearOpt obj)
    {
        return LegacyNativeCombatGearWire.Encode(obj, 2421);
    }
}
