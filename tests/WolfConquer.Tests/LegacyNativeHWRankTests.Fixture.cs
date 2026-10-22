using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativeHWRank;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeHWRankTests : IDisposable
{
    private readonly LegacyNativeSafeRandom previousRandom = LegacyNativePoolRandom.GetRandom;

    public LegacyNativeHWRankTests()
    {
        // Genuine Player birth draws a shared seed; don't advance another test's owner.
        LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
    }

    public void Dispose() => LegacyNativePoolRandom.GetRandom = previousRandom;

    private sealed class State : IDisposable
    {
        private readonly LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank> prior = Ranking.Ranks;
        private readonly CultureInfo culture = CultureInfo.CurrentCulture, ui = CultureInfo.CurrentUICulture;
        private readonly TextWriter output = Console.Out;
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);

        internal State()
        {
            Ranking.Ranks = new();
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Console.SetOut(log);
        }

        public void Dispose()
        {
            Ranking.Ranks = prior;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = ui;
            Console.SetOut(output);
            log.Dispose();
        }
    }

    private const string FullRow = "41/Hero/2/404/505/206/707/808/909/1010/1111/1212/1313/1414/1515/1616/1717/1818/1919/2020/Guild/";

    private static Ranking.Entry Entry(uint uid = 41, uint score = 404) => new()
    {
        UID = uid, Name = "Hero", Type = Ranking.Type.Judge, TotalPoints = score,
        Class = 505, Level = 206, Mesh = 707, HairStyle = 808, Head = 909, Garment = 1010,
        LeftWeapon = 1111, LefttWeaponAccessory = 1212, RightWeapon = 1313,
        RightWeaponAccessory = 1414, MountArmor = 1515, Armor = 1616, Wing = 1717,
        WingPlus = 1818, Title = 1919, Flag = 2020, GuildName = "Guild",
    };

    private static object[] Values(Ranking.Entry entry) =>
        [entry.UID, entry.Name, (uint)entry.Type, entry.TotalPoints, entry.Class, entry.Level,
         entry.Mesh, entry.HairStyle, entry.Head, entry.Garment, entry.LeftWeapon,
         entry.LefttWeaponAccessory, entry.RightWeapon, entry.RightWeaponAccessory,
         entry.MountArmor, entry.Armor, entry.Wing, entry.WingPlus, entry.Title, entry.Flag, entry.GuildName];

    private static Ranking.Rank Place(Ranking.Entry entry, Ranking.Type key = Ranking.Type.Judge,
        Ranking.Type physicalType = Ranking.Type.Judge)
    {
        var rank = new Ranking.Rank(physicalType) { MaxItems = 10 };
        rank.RankingList.Add(entry.UID, entry);
        Ranking.Ranks.Add(key, rank);
        return rank;
    }

    private static string Row(uint uid, uint score = 1, string type = "0", string name = "N")
    {
        string[] values = Enumerable.Repeat("0", 21).ToArray();
        values[0] = uid.ToString(CultureInfo.InvariantCulture); values[1] = name;
        values[2] = type; values[3] = score.ToString(CultureInfo.InvariantCulture); values[20] = "G";
        return string.Join('/', values) + "/";
    }
}
