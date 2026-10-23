using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativePrestigeRankingTests
{
    private sealed class State : IDisposable
    {
        private readonly LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank> prior = Ranking.Ranks;
        private readonly object gate = Ranking.SynRoot;
        private readonly LegacyNativeSafeRandom priorRandom = LegacyNativePoolRandom.GetRandom;
        private readonly CultureInfo culture = CultureInfo.CurrentCulture, ui = CultureInfo.CurrentUICulture;
        private readonly TextWriter output = Console.Out;
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);

        internal State()
        {
            Ranking.Ranks = new();
            Ranking.SynRoot = new();
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Console.SetOut(log);
        }

        public void Dispose()
        {
            Ranking.Ranks = prior;
            Ranking.SynRoot = gate;
            LegacyNativePoolRandom.GetRandom = priorRandom;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = ui;
            Console.SetOut(output);
            log.Dispose();
        }
    }

    private const string FullRow = "41/Hero/4/505/206/707/30004/808/909/1010/1111/1212/1313/1414/1515/1616/1717/1818/1919/2020/Guild/2/201/202/";

    private static Ranking.Entry Entry(uint uid = 41, uint points = 30004) => new()
    {
        UID = uid, Name = "Hero", type = Ranking.Type.Ninja, TotalPoints = points,
        Class = 505, Level = 206, Mesh = 707, HairStyle = 808, Head = 909, Garment = 1010,
        LeftWeapon = 1111, LefttWeaponAccessory = 1212, RightWeapon = 1313,
        RightWeaponAccessory = 1414, MountArmor = 1515, Armor = 1616, Wing = 1717,
        WingPlus = 1818, Title = 1919, Flag = 2020, GuildName = "Guild", Points = [201,202],
    };

    private static object[] Values(Ranking.Entry entry) =>
        [entry.UID, entry.Name, (uint)entry.type, entry.Class, entry.Level, entry.Mesh, entry.TotalPoints,
         entry.HairStyle, entry.Head, entry.Garment, entry.LeftWeapon, entry.LefttWeaponAccessory,
         entry.RightWeapon, entry.RightWeaponAccessory, entry.MountArmor, entry.Armor, entry.Wing,
         entry.WingPlus, entry.Title, entry.Flag, entry.GuildName];

    private static Ranking.Rank Place(Ranking.Entry entry, Ranking.Type type = Ranking.Type.Ninja)
    {
        var rank = new Ranking.Rank(type) { MaxItems = type == Ranking.Type.World ? 100 : 30 };
        rank.Items.Add(entry.UID, entry);
        Ranking.Ranks.Add(type, rank);
        return rank;
    }

    private static string Row(uint uid, uint points = 30000, string type = "4", string name = "N", int count = 0)
    {
        string[] values = Enumerable.Repeat("0", 22).ToArray();
        values[0] = uid.ToString(CultureInfo.InvariantCulture);
        values[1] = name;
        values[2] = type;
        values[6] = points.ToString(CultureInfo.InvariantCulture);
        values[20] = "G";
        values[21] = count.ToString(CultureInfo.InvariantCulture);
        return string.Join('/', values) + "/";
    }
}
