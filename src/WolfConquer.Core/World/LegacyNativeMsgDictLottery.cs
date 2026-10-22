#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Complete native MsgDictLottery state/process. Its typed wire projection is not
// the missing RuntimeTypeModel registration or a host receive binding.
internal class LegacyNativeMsgDictLottery
{
    public class Lottery
    {
        public bool Exp;
        public uint Type = 0u;
    }

    public List<Lottery> Items2 = new List<Lottery>();
    public uint Type = 0u;
    public uint Done;

    public static implicit operator byte[](LegacyNativeMsgDictLottery obj)
    {
        return LegacyNativeCombatGearWire.Encode(obj, 2465);
    }

    public static List<byte[]> Process(LegacyNativeMsgDictLottery Info, LegacyNativeCombatGear user, uint userid, uint count)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
        {
            return null;
        }
        List<byte[]> list = new List<byte[]>();
        uint[] array = new uint[0];
        switch (Info.Type)
        {
        case 0u:
            array = new uint[1];
            break;
        case 1u:
            array = new uint[10];
            break;
        default:
            Console.WriteLine("MsgDictLottery Type->" + unchecked((byte)Info.Type));
            break;
        }
        uint num = (uint)(405 * array.Length);
        if (num >= 4050 && !user.ContainsFlag(LegacyNativeCombatGear.LotteryEffect.Free10Sail))
        {
            num -= 4050;
            list.Add(user.AddFlag(LegacyNativeCombatGear.LotteryEffect.Free10Sail));
        }
        if (num >= 405 && !user.ContainsFlag(LegacyNativeCombatGear.LotteryEffect.Free1Sail))
        {
            num -= 405;
            list.Add(user.AddFlag(LegacyNativeCombatGear.LotteryEffect.Free1Sail));
        }
        if (user.AirPowers.Points >= num || count != 0)
        {
            if (count == 0)
            {
                user.AirPowers.Points -= num;
            }
            list.Add(user.AirPowers.AddPoints(0u, userid));
            Random random = new Random();
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] != 0)
                {
                    continue;
                }
                LegacyNativeDictLotteryTable.CQ[] array2 = LegacyNativeDictLotteryTable.Info.Values.Where(
                    (LegacyNativeDictLotteryTable.CQ p) => p.Type == 1 || p.Type == 2 ||
                        (p.Type == 3 && LegacyNativeCombatGear.Rate(1, 10000))).ToArray();
                LegacyNativeDictLotteryTable.CQ cQ = array2[random.Next(0, array2.Length)];
                bool exp = false;
                if (cQ != null)
                {
                    switch (cQ.Type)
                    {
                    case 1u:
                        user.PirateExp = unchecked(user.PirateExp + cQ.Value2);
                        list.Add(new LegacyNativeMsgDaoQi
                        {
                            Type = LegacyNativeMsgDaoQi.Types.PIRATE_POINTS,
                            PirateExp = user.PirateExp
                        });
                        break;
                    case 2u:
                    {
                        byte[] array4 = user.AddBlackJade(cQ.Value2, 0u);
                        if (array4 == null)
                        {
                            user.PirateExp = unchecked(user.PirateExp + 10u);
                            exp = true;
                            list.Add(new LegacyNativeMsgDaoQi
                            {
                                Type = LegacyNativeMsgDaoQi.Types.PIRATE_POINTS,
                                PirateExp = user.PirateExp
                            });
                        }
                        else
                        {
                            list.Add(array4);
                        }
                        break;
                    }
                    case 3u:
                    {
                        byte[] array3 = user.AddItem(userid, (LegacyNativeCombatGear.TypeID)cQ.Value2, 1u, 0u, 0u, 0u);
                        if (array3 == null)
                        {
                            user.PirateExp = unchecked(user.PirateExp + 50u);
                            list.Add(new LegacyNativeMsgDaoQi
                            {
                                Type = LegacyNativeMsgDaoQi.Types.PIRATE_POINTS,
                                PirateExp = user.PirateExp
                            });
                        }
                        else
                        {
                            exp = true;
                            list.Add(array3);
                        }
                        break;
                    }
                    }
                }
                array[i] = cQ.Id;
                Info.Items2.Add(new Lottery
                {
                    Type = array[i],
                    Exp = exp
                });
            }
            Info.Done = 1u;
            list.Add(Info);
        }
        return list;
    }
}
