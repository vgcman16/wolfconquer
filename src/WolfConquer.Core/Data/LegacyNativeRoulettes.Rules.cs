using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using WolfConquer.Core.World;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeRoulettes
{
    public partial class RouletteTable
    {
        public void ResetRoulette()
        {
            foreach (Member value in RegistredPlayers.Values)
            {
                value.MyLuckExtra.Clear();
                value.MyLuckNumber.Clear();
            }
            Reset = false;
            TimerStamp = DateTime.Now.AddSeconds(30.0);
        }

        public bool Rate(int value)
        {
            return value > Rand.Next(0, 100);
        }

        public void GenerateLuckyNumber()
        {
            Member[] array = RegistredPlayers.Values.ToArray();
            Member[] array2 = array;
            foreach (Member member in array2)
            {
                member.smalwin = 0u;
            }
            LuckyNumber = (byte)LegacyNativePoolRandom.GetRandom.Next(0, 38);
        }

        public bool CountainNo(byte noumber)
        {
            Member[] array = RegistredPlayers.Values.ToArray();
            bool result = false;
            Member[] array2 = array;
            foreach (Member member in array2)
            {
                if (member.MyLuckNumber.ContainsKey(noumber))
                {
                    result = true;
                }
                if (CheckLines(member))
                {
                    result = true;
                }
            }
            return result;
        }

        public bool CheckLines(Member amember)
        {
            foreach (LegacyNativeMsgRouletteCheck.Item value in amember.MyLuckExtra.Values)
            {
                switch (value.Number)
                {
                    case 152:
                    if (Black.Contains(LuckyNumber))
                    {
                        return true;
                    }
                    break;
                    case 151:
                    if (Red.Contains(LuckyNumber))
                    {
                        return true;
                    }
                    break;
                    case 112:
                    if (IsOdd(LuckyNumber))
                    {
                        return true;
                    }
                    break;
                    case 111:
                    if (IsEven(LuckyNumber))
                    {
                        return true;
                    }
                    break;
                    case 141:
                    if (IsSmall(LuckyNumber))
                    {
                        return true;
                    }
                    break;
                    case 142:
                    if (IsBig(LuckyNumber))
                    {
                        return true;
                    }
                    break;
                    case 121:
                    if (IsFront(LuckyNumber))
                    {
                        return true;
                    }
                    break;
                    case 122:
                    if (IsMiddle(LuckyNumber))
                    {
                        return true;
                    }
                    break;
                    case 123:
                    if (IsBack(LuckyNumber))
                    {
                        return true;
                    }
                    break;
                    case 131:
                    if (Line1.Contains(LuckyNumber))
                    {
                        return true;
                    }
                    break;
                    case 132:
                    if (Line2.Contains(LuckyNumber))
                    {
                        return true;
                    }
                    break;
                    case 133:
                    if (Line3.Contains(LuckyNumber))
                    {
                        return true;
                    }
                    break;
                }
            }
            return false;
        }
    }
}
