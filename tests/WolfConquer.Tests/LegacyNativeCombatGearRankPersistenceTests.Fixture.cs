using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using RankTable = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

// Shared only by the rank persistence and native writer tests. No native console
// error branch, server file, process, or host is used by this owned-file lease.
internal sealed class NativeRankFileScope : IDisposable
{
    private readonly string previousLocation = LegacyNativeServerConfig.DbLocation;
    private readonly bool previousLoad = LegacyNativeCombatGearTable.m_Load;
    private readonly Dictionary<RankTable.Type, RankTable.Rank> previousRanks = RankTable.Ranks;
    private readonly CultureInfo previousCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;

    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-native-rank-db-" + Guid.NewGuid().ToString("N"));
    internal string Database => Path.Combine(Root, "db");
    internal string RankPath => Path.Combine(Database, "CombatGearRank.txt");
    internal string PathFor(string name) => Path.Combine(Database, name);

    internal NativeRankFileScope()
    {
        Directory.CreateDirectory(Database);
        LegacyNativeServerConfig.DbLocation = Database + Path.DirectorySeparatorChar;
        LegacyNativeCombatGearTable.m_Load = true;
        RankTable.Ranks = new();
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    internal void Rows(params string[] rows) =>
        File.WriteAllText(RankPath, Text("Count=" + rows.Length.ToString(CultureInfo.InvariantCulture), rows), new UTF8Encoding(false));

    internal static string Text(string header, params string[] rows) =>
        header + Environment.NewLine + string.Concat(rows.Select(row => row + Environment.NewLine));

    public void Dispose()
    {
        LegacyNativeServerConfig.DbLocation = previousLocation;
        LegacyNativeCombatGearTable.m_Load = previousLoad;
        RankTable.Ranks = previousRanks;
        CultureInfo.CurrentCulture = previousCulture;
        CultureInfo.CurrentUICulture = previousUiCulture;
        string full = Path.GetFullPath(Root);
        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full), temp) ||
            !Regex.IsMatch(Path.GetFileName(full), "^wolf-native-rank-db-[0-9a-f]{32}$"))
            throw new InvalidOperationException("Rank/writer fixture cleanup containment failed.");
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Linked rank/writer fixture retained; recursive cleanup refused.");
        }
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
}

public sealed partial class LegacyNativeCombatGearRankPersistenceTests
{
    private const string FullRow = "41/Hero/19/404/505/206/707/808/909/1010/1111/1212/1313/1414/1515/1616/1717/1818/1919/2020/Guild/2222/";

    private static RankTable.Entry Entry(uint uid = 41) => new()
    {
        UID = uid, Name = "Hero", Type = (RankTable.Type)19, TotalPoints = 404,
        Class = 505, Level = 206, Mesh = 707, HairStyle = 808, Head = 909,
        Garment = 1010, LeftWeapon = 1111, LefttWeaponAccessory = 1212,
        RightWeapon = 1313, RightWeaponAccessory = 1414, MountArmor = 1515,
        Armor = 1616, Wing = 1717, WingPlus = 1818, Title = 1919,
        Flag = 2020, GuildName = "Guild", WeaponWar = 2222
    };

    private static RankTable.Rank Place(RankTable.Entry entry, uint mapKey = 41, uint rankKey = 19)
    {
        var rank = new RankTable.Rank((RankTable.Type)rankKey) { MaxItems = 10 };
        rank.RankingList.Add(mapKey, entry);
        RankTable.Ranks.Add((RankTable.Type)rankKey, rank);
        return rank;
    }

    private static string Row(uint uid, uint points = 1, string type = "19", string? name = null)
    {
        string[] fields = Enumerable.Repeat("0", 22).ToArray();
        fields[0] = uid.ToString(CultureInfo.InvariantCulture);
        fields[1] = name ?? "N" + fields[0];
        fields[2] = type;
        fields[3] = points.ToString(CultureInfo.InvariantCulture);
        fields[20] = "";
        return string.Join('/', fields) + "/";
    }

    private static object[] Values(RankTable.Entry entry) => new object[]
    {
        entry.UID, entry.Name, (uint)entry.Type, entry.TotalPoints, entry.Class,
        entry.Level, entry.Mesh, entry.HairStyle, entry.Head, entry.Garment,
        entry.LeftWeapon, entry.LefttWeaponAccessory, entry.RightWeapon,
        entry.RightWeaponAccessory, entry.MountArmor, entry.Armor, entry.Wing,
        entry.WingPlus, entry.Title, entry.Flag, entry.GuildName, entry.WeaponWar
    };
}
