using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using WolfConquer.Core.World;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.Data;

// Entire NumberInformation/Member and table state/constructor; no round worker is installed.
internal partial class LegacyNativeRoulettes
{
    public class NumberInformation
    {
        public List<byte> Red = new List<byte>
        {
            1, 3, 5, 7, 9, 12, 14, 16, 18, 19,
            21, 23, 25, 27, 30, 32, 34, 36
        };

        public List<byte> Black = new List<byte>
        {
            2, 4, 6, 8, 10, 11, 13, 15, 17, 20,
            22, 24, 26, 28, 29, 31, 33, 35
        };

        public List<byte> Line1 = new List<byte>
        {
            1, 4, 7, 10, 13, 16, 19, 22, 25, 28,
            31, 34
        };

        public List<byte> Line2 = new List<byte>
        {
            2, 5, 8, 11, 14, 17, 20, 23, 26, 29,
            32, 35
        };

        public List<byte> Line3 = new List<byte>
        {
            3, 6, 9, 12, 15, 18, 21, 24, 27, 30,
            33, 36
        };

        public bool IsOdd(byte Number)
        {
            if (Number == 0 || Number >= 37)
            {
                return false;
            }
            return Number % 2 != 0;
        }

        public bool IsEven(byte Number)
        {
            if (Number == 0 || Number >= 37)
            {
                return false;
            }
            return Number % 2 == 0;
        }

        public bool IsSmall(byte Number)
        {
            return Number >= 1 && Number <= 18;
        }

        public bool IsBig(byte Number)
        {
            return Number >= 19 && Number <= 36;
        }

        public bool IsFront(byte Number)
        {
            return Number >= 1 && Number <= 12;
        }

        public bool IsMiddle(byte Number)
        {
            return Number >= 13 && Number <= 24;
        }

        public bool IsBack(byte Number)
        {
            return Number >= 25 && Number <= 36;
        }
    }
    public partial class RouletteTable : NumberInformation
    {
        public class Member
        {
            public LegacyNativeGameClientRuntime Owner = null!;

            public LegacyNativeMsgRouletteOpenGui.Color Color;

            public uint Betting = 0u;

            public uint Winning = 0u;

            public uint smalwin = 0u;

            public uint Noumber = 0u;

            public ConcurrentDictionary<byte, LegacyNativeMsgRouletteCheck.Item> MyLuckNumber = new ConcurrentDictionary<byte, LegacyNativeMsgRouletteCheck.Item>();

            public ConcurrentDictionary<byte, LegacyNativeMsgRouletteCheck.Item> MyLuckExtra = new ConcurrentDictionary<byte, LegacyNativeMsgRouletteCheck.Item>();

            public uint MyCost
            {
                get
                {
                    long num = 0L;
                    num += MyLuckNumber.Values.Sum((LegacyNativeMsgRouletteCheck.Item p) => p.BetPrice);
                    num += MyLuckExtra.Values.Sum((LegacyNativeMsgRouletteCheck.Item p) => p.BetPrice);
                    return (uint)num;
                }
            }

            public void ShareBetting(RouletteTable table)
            {
                using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
                LegacyNativePacket stream = recycledPacket.GetStream();
                table.SendPacketTable(stream.RouletteShareBettingCreate(this, Color));
            }
        }
        public Random Rand;

        public const uint MapID = 3053u;

        public const uint MaxPlayerOnTable = 5u;

        public const uint MaxTimerStamp = 30u;

        public DateTime TimerStamp = default(DateTime);

        public DateTime StampRound = default(DateTime);

        public byte LuckyNumber = 1;

        public ConcurrentDictionary<uint, Member> RegistredPlayers;

        public ConcurrentDictionary<uint, LegacyNativeGameClientRuntime> ClientsWatch;

        public LegacyNativeMsgRouletteTable SpawnPacket;

#pragma warning disable CS0414 // Read by the whole held work() round lifecycle.
        private bool Reset = false;
#pragma warning restore CS0414

        public int GetTimerStamp
        {
            get
            {
                int num = TimerStamp.AllSeconds() - DateTime.Now.AllSeconds();
                if (num < 0)
                {
                    return 0;
                }
                return num;
            }
        }

        public RouletteTable()
        {
            // Framework Random() seeded from TickCount; no clock/result adapter.
            Rand = new Random(Environment.TickCount);
            TimerStamp = DateTime.Now.AddSeconds(30.0);
            ClientsWatch = new ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>();
            RegistredPlayers = new ConcurrentDictionary<uint, Member>();
            SpawnPacket = new LegacyNativeMsgRouletteTable();
        }


    }
    public static Dictionary<uint, RouletteTable> RoulettesPoll = new Dictionary<uint, RouletteTable>();
    internal static void Load()
    {
        string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "Roulettes.txt");
        if (array.Length <= 1)
        {
            return;
        }
        for (int i = 0; i < array.Length; i++)
        {
            if (!array[i].StartsWith(";"))
            {
                LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(array[i], ',');
                RouletteTable rouletteTable = new RouletteTable();
                rouletteTable.SpawnPacket.UID = readLine.Read(0u);
                rouletteTable.SpawnPacket.TableNumber = readLine.Read(0u);
                rouletteTable.SpawnPacket.MoneyType = (LegacyNativeMsgRouletteTable.TableType)readLine.Read(0u);
                ushort num = readLine.Read((ushort)0);
                rouletteTable.SpawnPacket.X = readLine.Read((ushort)0);
                rouletteTable.SpawnPacket.Y = readLine.Read((ushort)0);
                rouletteTable.SpawnPacket.Mesh = readLine.Read((ushort)0);
                if (!RoulettesPoll.ContainsKey(rouletteTable.SpawnPacket.UID))
                {
                    RoulettesPoll.Add(rouletteTable.SpawnPacket.UID, rouletteTable);
                }
            }
        }
    }
}
