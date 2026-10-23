using ProtoBuf;

namespace WolfConquer.Core.World;

// Complete native InteractQuery attributes, mutable state and ShallowCopy.
// Generic component reads do not install the native combat dispatcher or host.
[ProtoContract]
internal class LegacyNativeInteractQuery
{
    [ProtoMember(4, IsRequired = true)]
    public uint UID;
    [ProtoMember(5, IsRequired = true)]
    public uint OpponentUID;
    [ProtoMember(7, IsRequired = true)]
    public ushort X;
    [ProtoMember(8, IsRequired = true)]
    public ushort Y;
    [ProtoMember(13, IsRequired = true)]
    public ushort AtkType;
    [ProtoMember(15, IsRequired = true)]
    public int Damage;
    [ProtoMember(16)]
    public ulong CombatGear;
    [ProtoMember(17, IsRequired = true)]
    public int KilledMonster;
    [ProtoMember(18)]
    public ushort SpellLevel;
    [ProtoMember(19)]
    public ushort SpellID;
    [ProtoMember(21, IsRequired = true)]
    public uint ResponseDamage;
    [ProtoMember(25, IsRequired = true)]
    public uint Effect;
    [ProtoMember(26, IsRequired = true)]
    public uint Unknown;

    public static LegacyNativeInteractQuery ShallowCopy(LegacyNativeInteractQuery item)
    {
        return (LegacyNativeInteractQuery)item.MemberwiseClone();
    }
}
