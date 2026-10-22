namespace WolfConquer.Core.World;

// Complete native InteractQuery mutable state/ShallowCopy. Concrete InteractionCreate
// is in MsgAttackPacket; generic protobuf/read dispatch and native attacks remain absent.
internal class LegacyNativeInteractQuery
{
    public uint UID;
    public uint OpponentUID;
    public ushort X;
    public ushort Y;
    public ushort AtkType;
    public int Damage;
    public ulong CombatGear;
    public int KilledMonster;
    public ushort SpellLevel;
    public ushort SpellID;
    public uint ResponseDamage;
    public uint Effect;
    public uint Unknown;

    public static LegacyNativeInteractQuery ShallowCopy(LegacyNativeInteractQuery item)
    {
        return (LegacyNativeInteractQuery)item.MemberwiseClone();
    }
}
