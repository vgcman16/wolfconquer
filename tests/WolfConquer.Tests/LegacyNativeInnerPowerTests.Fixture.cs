using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Inner = WolfConquer.Core.World.LegacyNativeInnerPowerRuntime;
using Table = WolfConquer.Core.Data.LegacyNativeInnerPowerTable;
using Attr = WolfConquer.Core.Data.LegacyNativeInnerPowerTable.AtributeType;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeInnerPowerTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Ranks = typeof(Inner.InnerPowerRank).GetField("InnerPowerRanks", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly FieldInfo Ranking = typeof(Inner.InnerPowerRank).GetField("Rankings", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly FieldInfo Sync = typeof(Inner.InnerPowerRank).GetField("synroot", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly uint oldCount = Table.Count;
        private readonly Table.Stage[] oldStages = Table.Stages;
        private readonly ConcurrentDictionary<uint, Inner> oldPool = Inner.InnerPowerPolle;
        private readonly object? oldRanks = Ranks.GetValue(null), oldRanking = Ranking.GetValue(null), oldSync = Sync.GetValue(null);
        private readonly string oldLocation = LegacyNativeServerConfig.DbLocation;
        private readonly CultureInfo oldCulture = CultureInfo.CurrentCulture, oldUiCulture = CultureInfo.CurrentUICulture;
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        internal string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "wolf-inner-native-" + Guid.NewGuid().ToString("N"));
        internal string FilePath => Path.Combine(DirectoryPath, "InnerPower.txt");

        internal Scope(params Table.Stage[] definitions)
        {
            Directory.CreateDirectory(DirectoryPath);
            LegacyNativeServerConfig.DbLocation = DirectoryPath + Path.DirectorySeparatorChar;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            Table.Count = (uint)definitions.Length;
            Table.Stages = definitions;
            Inner.InnerPowerPolle = new();
            Ranks.SetValue(null, new ConcurrentDictionary<uint, Inner>());
            Ranking.SetValue(null, null);
            Sync.SetValue(null, new object());
        }

        internal void Record(string record) => File.WriteAllText(FilePath, "Count=1\n" + record + "\n");

        public void Dispose()
        {
            Table.Count = oldCount; Table.Stages = oldStages; Inner.InnerPowerPolle = oldPool;
            Ranks.SetValue(null, oldRanks); Ranking.SetValue(null, oldRanking); Sync.SetValue(null, oldSync);
            LegacyNativeServerConfig.DbLocation = oldLocation;
            LegacyNativePoolRandom.GetRandom = oldRandom;
            CultureInfo.CurrentCulture = oldCulture; CultureInfo.CurrentUICulture = oldUiCulture;
            string exact = Path.GetFullPath(DirectoryPath);
            string prefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar + "wolf-inner-native-";
            if (!exact.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Owned InnerPower fixture escaped its temporary root.");
            Directory.Delete(exact, recursive: true);
        }
    }

    private static Table.Stage Definition(ushort id = 1, params byte[] ids) => new()
    {
        ID = id, Name = "Stage" + id, NeiGongNum = (byte)ids.Length,
        SpecialAtributesType = [], AtributesValues = [],
        NeiGongAtributes = ids.Select(value => new Table.Stage.NeiGong
        {
            ID = value, MaxLevel = 3, AtributesType = [], AtributesValues = [], ProgressNeiGongValue = [],
        }).ToArray(),
    };

    private static uint[] Values(Inner value) =>
    [value.MaxLife, value.AddAttack, value.AddMagicAttack, value.Defence, value.AddMagicDefense,
     value.FinalAttack, value.FinalMagicAttack, value.FinalDefense, value.FinalMagicDefense,
     value.CriticalStrike, value.SkillCriticalStrike, value.Immunity, value.Breakthrough, value.Counteraction];

    private static Inner WithScore(uint uid, byte score)
    {
        var inner = new Inner("Rank" + uid, uid);
        inner.Stages = [new Inner.Stage { ID = 1, NeiGongs = [new Inner.Stage.NeiGong { Score = score }] }];
        return inner;
    }

    public static IEnumerable<object[]> Attributes => Enumerable.Range(1, 14).Select(value => new object[] { (byte)value });
}
