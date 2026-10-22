#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCombatGear
{
    // Complete native CombatGear969..991; caller Exp is intentionally unused.
    public byte[] AddItem(uint UserID, TypeID ID, uint Level, uint Exp, uint Equip, uint Transform)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
        {
            return null;
        }
        if (!Items.ContainsKey(ID) && LegacyNativeCombatGearTable.TryGetValue((uint)ID, (Level > 1) ? (Level - 1) : Level, out var type))
        {
            Weapon weapon = new Weapon();
            weapon.ItemID = ID;
            weapon.Level = Level;
            if (Level != 1)
            {
                weapon.Exp = type.Exp;
            }
            weapon.Equip = Equip;
            weapon.Transform = Transform;
            Items.TryAdd(ID, weapon);
            return LegacyNativeMsgCombatGear.Create(UserID, weapon, LegacyNativeMsgCombatGear.TypeID.Add);
        }
        return null;
    }
}
