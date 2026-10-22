namespace WolfConquer.Core.World;

// Complete native physical query declaration, not a host packet or alternate DTO.
internal struct LegacyNativeNpcServerQuery
{
    public int Stamp;
    public LegacyNativeNpcID ID;
    public ushort Mesh;
    public byte OptionID;
    public byte InteractType;
    public LegacyNativeNpcServerReplay.Mode Action;
    public LegacyNativeNpcType NpcType;
}
