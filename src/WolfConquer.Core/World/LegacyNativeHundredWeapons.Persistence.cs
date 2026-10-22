#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHundredWeapons
{
    public void Load(string line)
    {
        LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(line, '/');
        Unlocked = readLine.Read((byte)0) == 1;
        if (Unlocked)
        {
            int num = readLine.Read(0);
            for (int i = 0; i < num; i++)
            {
                LegacyNativeHundredWeaponInfo hundredWeaponInfo = new LegacyNativeHundredWeaponInfo
                {
                    WeaponSubtype = (LegacyNativeMagicType.WeaponsType)readLine.Read((ushort)0),
                    Level = readLine.Read((byte)0),
                    Progress = readLine.Read(0u),
                    BitVector = new LegacyNativeBitVector32(32)
                };
                hundredWeaponInfo.BitVector.bits[0] = readLine.Read(0u);
                hundredWeaponInfo.AppearancePosition = readLine.Read((byte)0);
                hundredWeaponInfo.Attributes = new Dictionary<LegacyNativeHundredWeaponsTable.AttributeType, int>();
                int num2 = readLine.Read(0);
                for (int j = 0; j < num2; j++)
                {
                    LegacyNativeHundredWeaponsTable.AttributeType key = (LegacyNativeHundredWeaponsTable.AttributeType)readLine.Read((byte)0);
                    int value = readLine.Read(0);
                    if (!hundredWeaponInfo.Attributes.ContainsKey(key))
                    {
                        hundredWeaponInfo.Attributes.Add(key, value);
                    }
                }
                AddWeapon(hundredWeaponInfo);
            }
        }
        ValidateSpells();
    }

    public override string ToString()
    {
        LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/').Add((byte)(Unlocked ? 1 : 0));
        if (Unlocked)
        {
            writeLine.Add(Objects.Count);
            foreach (LegacyNativeHundredWeaponInfo value in Objects.Values)
            {
                writeLine.Add(unchecked((ushort)value.WeaponSubtype));
                writeLine.Add(value.Level);
                writeLine.Add(value.Progress);
                writeLine.Add(value.BitVector.bits[0]);
                writeLine.Add(value.AppearancePosition);
                writeLine.Add(value.Attributes.Count);
                foreach (KeyValuePair<LegacyNativeHundredWeaponsTable.AttributeType, int> attribute in value.Attributes)
                {
                    writeLine.Add(unchecked((byte)attribute.Key));
                    writeLine.Add(attribute.Value);
                }
            }
        }
        return writeLine.Close();
    }
}
