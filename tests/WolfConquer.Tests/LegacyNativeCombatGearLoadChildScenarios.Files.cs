using System.Globalization;
using System.Text;
using Collection = WolfConquer.Core.Data.LegacyNativeDictCollectionTable;
using Jade = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;
using Lottery = WolfConquer.Core.Data.LegacyNativeDictLotteryTable;
using Rank = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Config = WolfConquer.Core.World.LegacyNativeServerConfig;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    private sealed class Scope : IDisposable
    {
        private readonly TextWriter console = Console.Out;
        private readonly CultureInfo culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        private readonly bool priorLoad = Table.m_Load;
        private readonly string priorLocation = Table.Location;
        private readonly string priorDbLocation = Config.DbLocation;
        private readonly Dictionary<uint, Dictionary<uint, Table.CQ>> priorInfo = Table.Info;
        private readonly Dictionary<uint, Jade.CQ> priorJade = Jade.Info;
        private readonly Dictionary<uint, Lottery.CQ> priorLottery = Lottery.Info;
        private readonly Dictionary<uint, Collection.CQ> priorCollection = Collection.Info;
        private readonly Dictionary<Rank.Type, Rank.Rank> priorRanks = Rank.Ranks;
        private readonly Dictionary<string, byte[]> files = new(StringComparer.OrdinalIgnoreCase);
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-gear-loader-" + Guid.NewGuid().ToString("N"));
        internal string Prefix => Path.Combine(Root, "db-");
        internal string RankPrefix => Path.Combine(Root, "rank-");
        internal string GearFile => Prefix + "combat_gear.txt";
        internal StringWriter Log { get; } = new(CultureInfo.InvariantCulture);
        internal Dictionary<uint, Dictionary<uint, Table.CQ>> OldInfo { get; } = new()
            { [999] = new() { [999] = new() { ID = 999 } } };
        internal Dictionary<uint, Jade.CQ> OldJade { get; } = new() { [999] = new() };
        internal Dictionary<uint, Lottery.CQ> OldLottery { get; } = new() { [999] = new() };
        internal Dictionary<uint, Collection.CQ> OldCollection { get; } = new() { [999] = new() };

        internal Scope(bool seedGate = true)
        {
            Require(!File.Exists(Root) && !Directory.Exists(Root), "Fresh owned root required.");
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(Root);
            Directory.CreateDirectory(Root);
            Console.SetOut(Log);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            if (seedGate) Table.m_Load = false;
            Table.Location = "old-location";
            Table.Info = OldInfo;
            Jade.Info = OldJade;
            Lottery.Info = OldLottery;
            Collection.Info = OldCollection;
            Rank.Ranks = new();
            Config.DbLocation = RankPrefix;
            Write("db-daoqi_dict_type.txt", "");
            Write("db-dict_lottery.txt", "");
            Write("db-dict_collection.txt", "");
            Write("db-combat_gear.txt", GearRow());
            Write("rank-CombatGearRank.txt", "Count=0\n");
        }

        internal void Write(string leaf, string contents)
        {
            string path = Leaf(leaf);
            if (files.TryGetValue(path, out var before)) Require(File.ReadAllBytes(path).SequenceEqual(before), "Seed changed before explicit rewrite.");
            else Require(!File.Exists(path) && !Directory.Exists(path), "Fresh owned seed required.");
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(contents);
            using var stream = File.Open(path, files.ContainsKey(path) ? FileMode.Truncate : FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
            stream.Flush(true);
            files[path] = bytes;
        }

        internal void RemoveGear()
        {
            Require(files.ContainsKey(GearFile) && File.ReadAllBytes(GearFile).SequenceEqual(files[GearFile]), "Owned gear deletion guard.");
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(GearFile);
            File.Delete(GearFile);
            files.Remove(GearFile);
        }

        private string Leaf(string name)
        {
            Require(name == Path.GetFileName(name) && name is ("db-daoqi_dict_type.txt" or "db-dict_lottery.txt" or
                "db-dict_collection.txt" or "db-combat_gear.txt" or "rank-CombatGearRank.txt" or "db-CombatGearRank.txt"),
                "Unregistered owned seed leaf.");
            string path = Path.GetFullPath(Path.Combine(Root, name));
            Require(Path.GetDirectoryName(path) == Root, "Owned leaf containment.");
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
            return path;
        }

        public void Dispose()
        {
            Console.SetOut(console);
            Log.Dispose();
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
            Table.m_Load = priorLoad;
            Table.Location = priorLocation;
            Config.DbLocation = priorDbLocation;
            Table.Info = priorInfo;
            Jade.Info = priorJade;
            Lottery.Info = priorLottery;
            Collection.Info = priorCollection;
            Rank.Ranks = priorRanks;
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(Root);
            Require(Path.GetDirectoryName(Root) == Path.TrimEndingDirectorySeparator(Path.GetTempPath()) &&
                System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(Root), "^wolf-gear-loader-[0-9a-f]{32}$"), "Cleanup root containment.");
            Require(Directory.GetFileSystemEntries(Root).Length == files.Count, "Unexpected child seed preserved.");
            foreach (var pair in files)
            {
                LegacyNativeDbReadOwnedFiles.OrdinaryChain(pair.Key);
                Require(File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value), "Changed child seed preserved.");
            }
            foreach (var path in files.Keys) File.Delete(path);
            Directory.Delete(Root, recursive: false);
        }
    }

    private static string GearRow(uint id = 1, uint type = 2, uint level = 3)
    {
        var values = Enumerable.Range(1, 19).Select(i => (uint)i).ToArray();
        values[0] = id; values[1] = type; values[2] = level;
        return string.Join("@@", values) + "\n";
    }

    private static uint[] Values(Table.CQ row) => new[] { row.ID, row.Type, row.Level, row.Exp,
        row.MaxHP, row.PAttack, row.PDefense, row.MDefense }.Concat(row.Skills).Append(row.Score).ToArray();

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
