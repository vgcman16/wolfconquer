using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using WolfConquer.Core.World;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.Data;

internal class LegacyNativePokerDatabase
{
    public static Dictionary<uint, LegacyNativePokerTableRuntime>? Tables;

    public static void Load()
    {
        Tables = new Dictionary<uint, LegacyNativePokerTableRuntime>();
        string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "PokerTables.txt");
        string[] array2 = array;
        foreach (string text in array2)
        {
            string[] array3 = text.Split(' ');
            LegacyNativePokerTableRuntime pokerTable = new LegacyNativePokerTableRuntime(Convert.ToUInt32(array3[0]));
            if (!Tables!.ContainsKey(pokerTable.Id))
            {
                pokerTable.X = Convert.ToUInt16(array3[1]);
                pokerTable.Y = Convert.ToUInt16(array3[2]);
                pokerTable.Mesh = Convert.ToUInt32(array3[3]);
                pokerTable.Number = Convert.ToUInt32(array3[4]);
                int num = Convert.ToByte(array3[5]);
                if (num == 1)
                {
                    pokerTable.UnLimited = true;
                }
                num = Convert.ToByte(array3[6]);
                if (num == 1)
                {
                    pokerTable.IsCPs = true;
                    pokerTable.MapId = 1860;
                }
                pokerTable.MinBet = Convert.ToUInt32(array3[7]);
                pokerTable.TableType = ((pokerTable.Mesh != 7578087) ? LegacyNativePokerFlags.TableType.TexasHoldem : LegacyNativePokerFlags.TableType.ShowHand);
                Tables!.Add(pokerTable.Id, pokerTable);
            }
        }
    }
}
