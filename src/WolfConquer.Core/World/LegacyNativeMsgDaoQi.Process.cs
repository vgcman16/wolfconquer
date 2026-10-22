#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgDaoQi
{
    public static List<byte[]> Process(LegacyNativeMsgDaoQi Info, LegacyNativeCombatGear user)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
        {
            return null;
        }
        List<byte[]> list = new List<byte[]>();
        switch (Info.Type)
        {
        case Types.INFO:
            Info.MasteryPoints = user.MasteryPoints;
            Info.TaoistReiki = user.TaoistReiki;
            Info.LOTTERY = user.Lottery;
            Info.Data5 = 6u;
            Info.Data6 = 9u;
            Info.Data8 = 1u;
            Info.PirateExp = user.PirateExp;
            Info.Data10 = 3u;
            list.Add(Info);
            break;
        case Types.LOAD_ITEM:
            foreach (LegacyNativeCombatGear.BlackJade value4 in user.BlackJadeBag.Values)
            {
                Info.Items.Add(new Item
                {
                    Id = value4.Type
                });
            }
            list.Add(Info);
            break;
        case Types.BUY:
            user.TaoistReiki += Info.Position * 15;
            list.Add(user.UpdateTaoistReiki());
            Info.Done = 1u;
            list.Add(Info);
            break;
        case Types.DRAWS:
        {
            uint[] array = new uint[0];
            switch (Info.Position)
            {
            case 0u:
                array = new uint[1];
                break;
            case 1u:
                array = new uint[10];
                break;
            }
            uint num = (uint)(array.Length * 1500);
            if (Info.Position == 1)
            {
                num -= num * 10 / 100;
            }
            if (!user.ContainsFlag(LegacyNativeCombatGear.LotteryEffect.Free10Draws))
            {
                num = 0u;
                list.Add(user.AddFlag(LegacyNativeCombatGear.LotteryEffect.Free10Draws));
            }
            if (user.TaoistReiki < num)
            {
                break;
            }
            user.TaoistReiki -= num;
            list.Add(user.UpdateTaoistReiki());
            uint num2 = 0u;
            for (int num3 = 0; num3 < array.Length; num3++)
            {
                while (array[num3] == 0)
                {
                    if (!user.BlackJadeBag.ContainsKey(113u))
                    {
                        user.AddBlackJade(113u, 0u);
                        array[num3] = 113u;
                        Info.Items.Add(new Item
                        {
                            Id = 113u
                        });
                        break;
                    }
                    LegacyNativeDaoqiDictTypeTable.CQ cQ = LegacyNativeDaoqiDictTypeTable.Info.Values.ToArray()[
                        LegacyNativeCombatGear.GetRandom.Next(0, LegacyNativeDaoqiDictTypeTable.Info.Values.Count)];
                    if (cQ.Type == LegacyNativeDaoqiDictTypeTable.Type.Sacred)
                    {
                        if (LegacyNativeCombatGear.Rate((int)cQ.rate / 4, 10000))
                        {
                            byte[] array2 = user.AddBlackJade(cQ.ID, 0u);
                            if (array2 == null)
                            {
                                num2 += cQ.points;
                            }
                            else
                            {
                                list.Add(array2);
                            }
                            array[num3] = cQ.ID;
                            Info.Items.Add(new Item
                            {
                                Id = cQ.ID
                            });
                            break;
                        }
                    }
                    else if (LegacyNativeCombatGear.Rate((int)cQ.rate, 10000))
                    {
                        byte[] array3 = user.AddBlackJade(cQ.ID, 0u);
                        if (array3 == null)
                        {
                            num2 += cQ.points;
                        }
                        else
                        {
                            list.Add(array3);
                        }
                        array[num3] = cQ.ID;
                        Info.Items.Add(new Item
                        {
                            Id = cQ.ID
                        });
                        break;
                    }
                }
            }
            user.MasteryPoints += num2;
            Info.Position = num2;
            list.Add(Info);
            break;
        }
        case Types.EQUIP:
        {
            if (user.Items.TryGetValue((LegacyNativeCombatGear.TypeID)Info.ItemID, out var value))
            {
                LegacyNativeCombatGear.BlackJade blackJade2 = user.BlackJadeBag.Values.FirstOrDefault(
                    (LegacyNativeCombatGear.BlackJade p) => p.Type == Info.Items[0].Id);
                if (blackJade2 != null)
                {
                    Info.Done = 1u;
                    value.BlackJades[Info.Position - 1] = blackJade2;
                    list.Add(Info);
                }
            }
            break;
        }
        case Types.UN_EQUIP:
        {
            if (user.Items.TryGetValue((LegacyNativeCombatGear.TypeID)Info.ItemID, out var value3))
            {
                Info.Done = 1u;
                value3.BlackJades[Info.Position - 1] = new LegacyNativeCombatGear.BlackJade();
                list.Add(Info);
            }
            break;
        }
        case Types.REPLACE:
        {
            if (!user.Items.TryGetValue((LegacyNativeCombatGear.TypeID)Info.ItemID, out var value2))
            {
                break;
            }
            int i;
            for (i = 0; i < Info.Items.Count; i++)
            {
                LegacyNativeCombatGear.BlackJade blackJade3 = user.BlackJadeBag.Values.FirstOrDefault(
                    (LegacyNativeCombatGear.BlackJade p) => p.Type == Info.Items[i].Id);
                if (blackJade3 != null)
                {
                    Info.Done = 1u;
                    value2.BlackJades[i] = blackJade3;
                    list.Add(Info);
                }
            }
            break;
        }
        case Types.PIRATE_UPGRADE:
        {
            LegacyNativeCombatGear.BlackJade blackJade = user.BlackJadeBag.Values.FirstOrDefault(
                (LegacyNativeCombatGear.BlackJade p) => p.Type == Info.ItemID);
            if (blackJade != null && blackJade.Type % 1000 < 900 && user.PirateExp >= blackJade.DBItem.Exp)
            {
                user.PirateExp -= blackJade.DBItem.Exp;
                blackJade.Type += 100u;
                Info.PirateExp = blackJade.Type;
                Info.Position = 1u;
                list.Add(new LegacyNativeMsgDaoQi
                {
                    Type = Types.PIRATE_POINTS,
                    PirateExp = user.PirateExp
                });
                list.Add(Info);
            }
            break;
        }
        default:
            Console.WriteLine("MsgDaoQi Type->" + (byte)Info.Type);
            break;
        case Types.TAOIST_REIKI:
            break;
        }
        return list;
    }
}
