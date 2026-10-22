#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Complete native Role.Instance.HundredWeaponInfo; all metadata lookups remain live.
internal class LegacyNativeHundredWeaponInfo
{
    public LegacyNativeMagicType.WeaponsType WeaponSubtype;
    public byte Level;
    public uint Progress;
    public Dictionary<LegacyNativeHundredWeaponsTable.AttributeType, int> Attributes;
    public LegacyNativeBitVector32 BitVector;
    public byte AppearancePosition;

    public uint Score => unchecked((uint)(Level * 1000 +
        (Attributes.Values.Sum((int i) => i) + DBInfo.Attributes.Values.Sum((int i) => i)) * 5));

    public LegacyNativeHundredWeaponsTable.HundredWeapon DBInfo => LegacyNativeHundredWeaponsTable.HundredWeaponsList.Values
        .Where((LegacyNativeHundredWeaponsTable.HundredWeapon x) => x.WeaponSubtype == WeaponSubtype && x.Level == Level).FirstOrDefault();

    public LegacyNativeMagicType.Magic DBSpell => LegacyNativePoolMagic.Magic[DBInfo.MagicType][unchecked((ushort)(Level + 1))];
}
