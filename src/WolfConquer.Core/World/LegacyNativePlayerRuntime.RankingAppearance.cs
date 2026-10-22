namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Selected physical native fields in source-relative order; no appearance DTO is adopted.
    public uint SpecialTitleID = 0u;

    public uint SpecialWingID = 0u;

#pragma warning disable CS0649
    internal LegacyNativeGuildRuntime.Member? MyGuildMember;

    internal LegacyNativeGuildRuntime? MyGuild;

    // Native Player1103..1109 plain stat fields, before the native _class slot.
    public ushort Agility;

    public ushort Vitality;

    public ushort Spirit;

    public ushort Strength;

    // Native Player1115/1127/1165, implicit zero. Their full setters remain absent.
    private uint _class;

    private ushort _level;

    // Native Player1129: plain byte storage, between _level and the later _hair slot.
    public byte Reborn;

    private uint _hair;
#pragma warning restore CS0649

    public uint GarmentId = 0u;

    public uint ArmorId = 0u;

    public uint LeftWeaponAccessoryId = 0u;

    public uint RightWeaponAccessoryId = 0u;

    public uint MountArmorId = 0u;

    public uint WingId = 0u;

    public byte WingPlus = 0;

    // Genuine read accessors only; no writable cold path or partial live setter is installed.
    public uint Class
    {
        get
        {
            return _class;
        }
    }

    public ushort Level
    {
        get
        {
            return _level;
        }
    }

    public uint Hair
    {
        get
        {
            return _hair;
        }
    }
}
