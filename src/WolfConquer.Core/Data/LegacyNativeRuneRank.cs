using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native Database.RuneRank; original null publication and query order remain.
internal class LegacyNativeRuneRank
{
    public class Entry
    {
        public uint TotalPoints = 0u;

        public string Name = "";
    }

    public const int MaxPlayers = 100;

    public static Dictionary<uint, Entry>? RankingList;

    public static object SynRoot = new object();

    public static void Load()
    {
        RankingList = new Dictionary<uint, Entry>();
        using (LegacyNativeDbRead read = new LegacyNativeDbRead("RuneRanking.txt"))
        {
            if (read.Reader())
            {
                int count = read.Count;
                for (int i = 0; i < count; i++)
                {
                    LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(read.ReadString("/"), '/');
                    Entry entry = new Entry();
                    entry.Name = readLine.Read("")!;
                    entry.TotalPoints = readLine.Read(0u);
                    RankingList!.Add(readLine.Read(0u), entry);
                }
            }
        }
        RankingList = (from keyValuePair in RankingList
            where keyValuePair.Value.TotalPoints >= 10000
            orderby keyValuePair.Value.TotalPoints descending
            select keyValuePair).Take(100).ToDictionary((KeyValuePair<uint, Entry> pair) => pair.Key, (KeyValuePair<uint, Entry> pair) => pair.Value);
    }

    public static void Update(LegacyNativeGameClientRuntime client)
    {
        if (RankingList!.ContainsKey(client.Player.UID))
        {
            RankingList![client.Player.UID].TotalPoints = client.Rune!.Score;
            RankingList![client.Player.UID].Name = client.Player.Name;
        }
        else
        {
            RankingList!.Add(client.Player.UID, new Entry
            {
                Name = client.Player.Name,
                TotalPoints = client.Rune!.Score
            });
        }
        RankingList = (from i in RankingList
            where i.Value.TotalPoints >= 10000
            orderby i.Value.TotalPoints descending
            select i).Take(100).ToDictionary((KeyValuePair<uint, Entry> pair) => pair.Key, (KeyValuePair<uint, Entry> pair) => pair.Value);
    }

    public static void Save()
    {
        RankingList = (from i in RankingList!
            where i.Value.TotalPoints >= 10000
            orderby i.Value.TotalPoints descending
            select i).Take(100).ToDictionary((KeyValuePair<uint, Entry> pair) => pair.Key, (KeyValuePair<uint, Entry> pair) => pair.Value);
        using LegacyNativeDbWrite write = new LegacyNativeDbWrite("RuneRanking.txt");
        foreach (KeyValuePair<uint, Entry> ranking in RankingList)
        {
            LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
            writeLine.Add(ranking.Value.Name).Add(ranking.Value.TotalPoints).Add(ranking.Key);
            write.Add(writeLine.Close());
        }
        write.Execute(LegacyNativeDbWriteMode.Open);
    }
}
