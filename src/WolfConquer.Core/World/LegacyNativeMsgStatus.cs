namespace WolfConquer.Core.World;

// Complete native MsgStatus fields and implicit zero-state construction only.
// The status request receiver/StatusCreate and host binding remain separate.
internal class LegacyNativeMsgStatus
{
    public int Stamp;
    public uint UID;
    public uint MaxHitpoints;
    public uint MaxMana;
    public uint MaxAttack;
    public uint MinAttack;
    public uint Defence;
    public uint MagicAttack;
    public uint MagicDefence;
    public uint Dodge;
    public uint AgilityAtack;
    public uint Accuracy;
    public uint PhysicalPercent;
    public uint MagicPercent;
    public uint Damage;
    public uint ItemBless;
    public uint CriticalStrike;
    public uint SkillCStrike;
    public uint Immunity;
    public uint Penetration;
    public uint Block;
    public uint Breakthrough;
    public uint Counteraction;
    public uint Detoxication;
    public uint PhysicalDamageIncrease;
    public uint MagicDamageIncrease;
    public uint PhysicalDamageDecrease;
    public uint MagicDamageDecrease;
    public uint MetalResistance;
    public uint WoodResistance;
    public uint WaterResistance;
    public uint FireResistance;
    public uint EarthResistance;
    public uint MDefence;
    public ushort MaxVigor;
    public uint PrestigeLevel;
    public ushort TotalAnimaLevel;
    public uint RuneScore;
    public uint DashRate;
    public double LukyStrike;
    public double Parry;
    public uint DodgeRate;
    public byte BloodthirstLevel;
    public byte EtherealLevel;
    public byte ElanLevel;
    public byte SolidLevel;
    public byte VenomLevel;
}
