#nullable disable
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgHundredWeaponsInfo
{
    public static LegacyNativePacket CreateHundredWeaponsInfo(this LegacyNativePacket stream,
        LegacyNativeGameClientRuntime client, ActionID type)
    {
        unchecked
        {
            MsgHundredWeaponsInfoProto msgHundredWeaponsInfoProto = new MsgHundredWeaponsInfoProto
            {
                Type = type
            };
            if (client != null)
            {
                msgHundredWeaponsInfoProto.OwnerUID = client.Player.UID;
                msgHundredWeaponsInfoProto.Weapons = new WeaponInfo[client.HundredWeapons.Objects.Count];
                for (int i = 0; i < msgHundredWeaponsInfoProto.Weapons.Length; i++)
                {
                    LegacyNativeHundredWeaponInfo hundredWeaponInfo = client.HundredWeapons.Objects.Values
                        .OrderBy((LegacyNativeHundredWeaponInfo w) => (ushort)w.WeaponSubtype).ToArray()[i];
                    msgHundredWeaponsInfoProto.Weapons[i] = new WeaponInfo
                    {
                        SubType = hundredWeaponInfo.WeaponSubtype,
                        Progress = hundredWeaponInfo.Progress,
                        Level = hundredWeaponInfo.Level,
                        Hitpoints = 10000 + (hundredWeaponInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.Hitpoints] - hundredWeaponInfo.DBInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.Hitpoints]),
                        PhysicalAttack = 20000 + (hundredWeaponInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.PhysicalAttack] - hundredWeaponInfo.DBInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.PhysicalAttack]),
                        PhysicalDefense = 30000 + (hundredWeaponInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.PhysicalDefense] - hundredWeaponInfo.DBInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.PhysicalDefense]),
                        MagicAttack = 40000 + (hundredWeaponInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.MagicAttack] - hundredWeaponInfo.DBInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.MagicAttack]),
                        MagicDefense = 50000 + (hundredWeaponInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.MagicDefense] - hundredWeaponInfo.DBInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.MagicDefense]),
                        PositionFlags = hundredWeaponInfo.BitVector.bits[0],
                        AppearancePosition = hundredWeaponInfo.AppearancePosition,
                        Score = hundredWeaponInfo.Score
                    };
                }
            }
            stream.InitWriter();
            stream.ProtoBufferSerialize(msgHundredWeaponsInfoProto);
            stream.Finalize(2352);
            return stream;
        }
    }
}
