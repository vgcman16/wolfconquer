#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Native MsgAirPowerUp model and Process. Encoding projects its source-registered
// concrete contract; no CQ.Load registration lifecycle or host adoption is installed.
internal class LegacyNativeMsgAirPowerUp
{
    [Flags]
    public enum Types
    {
        INFO = 0,
        VIEW = 1,
        UPGRADE = 3,
        PRACTICE = 4,
        ADDPOINTS = 5
    }

    public class Power
    {
        public uint Id = 0u;
        public uint SenseLevel = 0u;
        public uint SenseAipPower = 0u;
        public uint ArmedLevel = 0u;
        public uint ArmedAipPower = 0u;
        public uint OverLoadLevel = 0u;
        public uint OverLoadAipPower = 0u;
        public uint Points;
    }

    public uint PowerType;

    public uint UserId;

    public List<Power> Powers = new List<Power>();

    public Types Type = Types.INFO;

    public static implicit operator byte[](LegacyNativeMsgAirPowerUp obj)
    {
        return LegacyNativeCombatGearWire.Encode(obj, 2464);
    }

    public static List<byte[]> Process(LegacyNativeMsgAirPowerUp Info,
        LegacyNativeCombatGear user, uint userid)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
        {
            return null;
        }
        List<byte[]> list = new List<byte[]>();
        Random random = new Random();
        uint[] array = new uint[8] { 40000u, 120000u, 200000u, 280000u, 360000u, 480000u, 640000u, 828000u };
        Types type = Info.Type;
        Types types = type;
        if (types == Types.PRACTICE)
        {
            switch (Info.PowerType)
            {
                case 701u:
                    if (user.AirPowers.SenseLevel != 0 && user.AirPowers.SenseLevel < 9)
                    {
                        if (user.AirPowers.Points >= 1000 * Info.UserId)
                        {
                            user.AirPowers.Points -= 1000 * Info.UserId;
                            user.AirPowers.SenseAipPower += (uint)random.Next((int)(750 * Info.UserId), (int)(1251 * Info.UserId));
                            if (user.AirPowers.SenseAipPower >= array[user.AirPowers.SenseLevel - 1])
                            {
                                user.AirPowers.SenseLevel++;
                            }
                            if (user.AirPowers.SenseLevel > 1 && user.AirPowers.ArmedLevel == 0)
                            {
                                user.AirPowers.ArmedLevel = 1u;
                            }
                            if (user.AirPowers.SenseLevel > 2 && user.AirPowers.OverLoadLevel == 0)
                            {
                                user.AirPowers.OverLoadLevel = 1u;
                            }
                        }
                    }
                    else if (user.AirPowers.Points >= 1000 * Info.UserId)
                    {
                        user.AirPowers.Points -= 1000 * Info.UserId;
                        user.AirPowers.SenseAipPower += (uint)random.Next((int)(750 * Info.UserId), (int)(1251 * Info.UserId));
                    }
                    break;
                case 702u:
                    if (user.AirPowers.ArmedLevel != 0 && user.AirPowers.ArmedLevel < 9)
                    {
                        if (user.AirPowers.Points >= 1000 * Info.UserId)
                        {
                            user.AirPowers.Points -= 1000 * Info.UserId;
                            user.AirPowers.ArmedAipPower += (uint)random.Next((int)(750 * Info.UserId), (int)(1251 * Info.UserId));
                            if (user.AirPowers.ArmedAipPower >= array[user.AirPowers.ArmedLevel - 1])
                            {
                                user.AirPowers.ArmedLevel++;
                            }
                        }
                    }
                    else if (user.AirPowers.Points >= 1000 * Info.UserId)
                    {
                        user.AirPowers.Points -= 1000 * Info.UserId;
                        user.AirPowers.ArmedAipPower += (uint)random.Next((int)(750 * Info.UserId), (int)(1251 * Info.UserId));
                    }
                    break;
                case 703u:
                    if (user.AirPowers.OverLoadLevel != 0 && user.AirPowers.OverLoadLevel < 9)
                    {
                        if (user.AirPowers.Points >= 1000 * Info.UserId)
                        {
                            user.AirPowers.Points -= 1000 * Info.UserId;
                            user.AirPowers.OverLoadAipPower += (uint)random.Next((int)(750 * Info.UserId), (int)(1251 * Info.UserId));
                            if (user.AirPowers.OverLoadAipPower >= array[user.AirPowers.OverLoadLevel - 1])
                            {
                                user.AirPowers.OverLoadLevel++;
                            }
                        }
                    }
                    else if (user.AirPowers.Points >= 1000 * Info.UserId)
                    {
                        user.AirPowers.Points -= 1000 * Info.UserId;
                        user.AirPowers.OverLoadAipPower += (uint)random.Next((int)(750 * Info.UserId), (int)(1251 * Info.UserId));
                    }
                    break;
            }
            list.Add(user.AirPowers.Send(Types.UPGRADE, userid));
        }
        return list;
    }
}
