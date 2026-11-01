namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Selected physical native fields in source-relative order; no appearance DTO is adopted.
    public uint SpecialTitleID = 0u;

    public uint SpecialWingID = 0u;

#pragma warning disable CS0649
    internal LegacyNativeGuildRuntime.Member? MyGuildMember;

    internal LegacyNativeGuildRuntime? MyGuild;

    // Source-width numeric storage; ordinary attached writes use the Store plan.
    private ushort _agility;
    public ushort Agility
    {
        get => _agility;
        set => SetUnboundActorScalar(ActorScalar.Agility, value);
    }

    private ushort _vitality, _spirit, _strength;
    public ushort Vitality { get => _vitality; set => SetUnboundActorScalar(ActorScalar.Vitality, value); }
    public ushort Spirit { get => _spirit; set => SetUnboundActorScalar(ActorScalar.Spirit, value); }
    public ushort Strength { get => _strength; set => SetUnboundActorScalar(ActorScalar.Strength, value); }

    // Native Player1115/1127/1165; saved identity hydration belongs to ExistingPrefixLoad.
    // the whole Hair property is owned by its dedicated partial.
    private uint _class;

    private ushort _level;

    private byte _reborn;
    public byte Reborn
    {
        get => _reborn;
        set => SetUnboundActorScalar(ActorScalar.Reborn, value);
    }

    private uint _hair;
#pragma warning restore CS0649

    public uint GarmentId = 0u;

    public uint ArmorId = 0u;

    public uint LeftWeaponAccessoryId = 0u;

    public uint RightWeaponAccessoryId = 0u;

    public uint MountArmorId = 0u;

    public uint WingId = 0u;

    public byte WingPlus = 0;

    // Genuine read accessors. Committed cold loading uses ExistingPrefixLoad; public live setters remain absent.
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

}
