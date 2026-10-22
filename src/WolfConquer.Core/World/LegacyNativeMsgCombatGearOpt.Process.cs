#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgCombatGearOpt
{
    public static List<byte[]> Process(LegacyNativeMsgCombatGearOpt Info, LegacyNativeCombatGear user, uint userid, uint Class)
    {
        unchecked
        {
            if (!LegacyNativeCombatGearTable.m_Load)
            {
                return null;
            }
            List<byte[]> list = new List<byte[]>();
            switch (Info.Type)
            {
            case Types.OPEN:
            {
                Info.UID = 1u;
                list.Add(Info);
                LegacyNativeCombatGear.Weapon weapon = user.isEquip(Class);
                if (weapon != null)
                {
                    Info.Type = Types.VIEW;
                    Info.UID = userid;
                    Info.Exp = weapon.Exp;
                    Info.Transform = weapon.Transform;
                    list.Add(Info);
                }
                break;
            }
            case Types.TRANSFORM:
            {
                if (user.Items.TryGetValue(Info.ItemID, out var value5) && value5.Level == 50)
                {
                    value5.Transform = Info.Transform;
                    Info.UID = 1u;
                    list.Add(LegacyNativeMsgCombatGear.Create(userid, value5, LegacyNativeMsgCombatGear.TypeID.Update));
                    list.Add(Info);
                }
                break;
            }
            case Types.CANCEL:
            {
                if (user.Items.TryGetValue(Info.ItemID, out var value2) && value2.Level == 50)
                {
                    value2.Transform = 0u;
                    Info.UID = 1u;
                    list.Add(LegacyNativeMsgCombatGear.Create(userid, value2, LegacyNativeMsgCombatGear.TypeID.Update));
                    list.Add(Info);
                }
                break;
            }
            case Types.UPGRADE_TAOIST:
            {
                if (user.Items.TryGetValue(Info.ItemID, out var value6) && user.TaoistReiki + value6.Exp >= value6.DBItem.Exp)
                {
                    user.TaoistReiki -= value6.DBItem.Exp - value6.Exp;
                    list.Add(user.UpdateTaoistReiki());
                    Info.UID = 1u;
                    value6.Exp = value6.DBItem.Exp;
                    if (value6.Exp >= value6.DBItem.Exp && value6.Level < 60)
                    {
                        value6.Level++;
                    }
                    list.Add(LegacyNativeMsgCombatGear.Create(userid, value6, LegacyNativeMsgCombatGear.TypeID.Update));
                    list.Add(Info);
                }
                break;
            }
            case Types.MASTERY_SCORE_TAOIST:
            {
                if (user.Items.TryGetValue(Info.ItemID, out var value7) && user.MasteryPoints >= Info.Transform)
                {
                    user.MasteryPoints -= Info.Transform;
                    value7.MasteryScore += Info.Transform;
                    Info.UID = 1u;
                    list.Add(LegacyNativeMsgCombatGear.Create(userid, value7, LegacyNativeMsgCombatGear.TypeID.Update));
                    list.Add(Info);
                }
                break;
            }
            case Types.UPGRADE_TAOIST_ALL:
            {
                if (!user.Items.TryGetValue(Info.ItemID, out var value3))
                {
                    break;
                }
                Info.UID = 1u;
                while (user.TaoistReiki + value3.Exp >= value3.DBItem.Exp && value3.Level < 60)
                {
                    if (user.TaoistReiki + value3.Exp >= value3.DBItem.Exp)
                    {
                        user.TaoistReiki -= value3.DBItem.Exp - value3.Exp;
                        list.Add(user.UpdateTaoistReiki());
                        value3.Exp = value3.DBItem.Exp;
                        value3.Level++;
                    }
                }
                list.Add(LegacyNativeMsgCombatGear.Create(userid, value3, LegacyNativeMsgCombatGear.TypeID.Update));
                list.Add(Info);
                break;
            }
            case Types.UPGRADE_PIRATE:
            {
                if (user.Items.TryGetValue(Info.ItemID, out var value4))
                {
                    LegacyNativeCombatGearTable.TryGetValue((uint)value4.ItemID, value4.Level - 1, out var type);
                    uint num = value4.DBItem.Exp;
                    if (type != null)
                    {
                        num -= type.Exp;
                    }
                    Info.UID = 1u;
                    if (user.PirateExp >= num && value4.Level < 4)
                    {
                        user.PirateExp -= num;
                        value4.Level++;
                        list.Add(new LegacyNativeMsgDaoQi
                        {
                            Type = LegacyNativeMsgDaoQi.Types.PIRATE_POINTS,
                            PirateExp = user.PirateExp
                        });
                        list.Add(LegacyNativeMsgCombatGear.Create(userid, value4, LegacyNativeMsgCombatGear.TypeID.Update));
                        list.Add(Info);
                    }
                }
                break;
            }
            case Types.MASTERY_SCORE_PIRATE:
            {
                if (!user.Items.TryGetValue(Info.ItemID, out var value))
                {
                    break;
                }
                switch (Info.Exp)
                {
                case 0u:
                    if (user.PirateExp != 0)
                    {
                        user.PirateExp--;
                        value.MasteryScore += 15u;
                    }
                    break;
                case 1u:
                    value.MasteryScore += user.PirateExp;
                    user.PirateExp = 0u;
                    break;
                }
                Info.UID = 1u;
                list.Add(new LegacyNativeMsgDaoQi
                {
                    Type = LegacyNativeMsgDaoQi.Types.PIRATE_POINTS,
                    PirateExp = user.PirateExp
                });
                list.Add(LegacyNativeMsgCombatGear.Create(userid, value, LegacyNativeMsgCombatGear.TypeID.Update));
                list.Add(Info);
                break;
            }
            default:
                Console.WriteLine("MsgCombatGearOpt Type->" + (byte)Info.Type);
                break;
            }
            return list;
        }
    }
}
