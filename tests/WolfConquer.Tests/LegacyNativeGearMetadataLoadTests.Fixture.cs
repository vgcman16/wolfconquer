using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GearTable = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Jade = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;
using Collection = WolfConquer.Core.Data.LegacyNativeDictCollectionTable;
using Lottery = WolfConquer.Core.Data.LegacyNativeDictLotteryTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeGearMetadataLoadTests
{
    private sealed class LoadFixture : IDisposable
    {
        private readonly bool priorLoad = GearTable.m_Load;
        private readonly Dictionary<uint, Jade.CQ> priorJade = Jade.Info;
        private readonly Dictionary<uint, Collection.CQ> priorCollection = Collection.Info;
        private readonly Dictionary<uint, Lottery.CQ> priorLottery = Lottery.Info;
        private readonly CultureInfo priorCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo priorUiCulture = CultureInfo.CurrentUICulture;
        private readonly TextWriter priorConsole = Console.Out;
        private readonly StringWriter output = new(CultureInfo.InvariantCulture);
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-native-gear-load-" + Guid.NewGuid().ToString("N"));
        internal string DataDirectory => Path.Combine(Root, "db");
        internal string Prefix => DataDirectory + Path.DirectorySeparatorChar;
        internal string Log => output.ToString();

        internal LoadFixture(bool loaded = true)
        {
            Directory.CreateDirectory(DataDirectory);
            GearTable.m_Load = loaded;
            Jade.Info = new Dictionary<uint, Jade.CQ>();
            Collection.Info = new Dictionary<uint, Collection.CQ>();
            Lottery.Info = new Dictionary<uint, Lottery.CQ>();
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Console.SetOut(output);
        }

        internal static string FileName(int kind) => kind switch
        {
            0 => "daoqi_dict_type.txt", 1 => "dict_collection.txt", 2 => "dict_lottery.txt",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        internal static int Columns(int kind) => kind switch { 0 => 15, 1 => 7, 2 => 6, _ => throw new ArgumentOutOfRangeException(nameof(kind)) };
        internal string PathFor(int kind) => Path.Combine(DataDirectory, FileName(kind));
        internal void Write(int kind, params string[] rows) => File.WriteAllLines(PathFor(kind), rows, new UTF8Encoding(false));
        internal void Load(int kind) => Load(kind, Prefix);
        internal void Load(int kind, string prefix)
        {
            switch (kind)
            {
                case 0: Jade.Load(prefix); break;
                case 1: Collection.Load(prefix); break;
                case 2: Lottery.Load(prefix); break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        internal static IDictionary Current(int kind) => kind switch
        {
            0 => Jade.Info, 1 => Collection.Info, 2 => Lottery.Info,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        internal static object Seed(int kind, uint id)
        {
            object row = kind switch
            {
                0 => new Jade.CQ { ID = id }, 1 => new Collection.CQ { Id = id }, 2 => new Lottery.CQ { Id = id },
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            Current(kind).Add(id, row);
            return row;
        }

        internal static string Row(int kind, uint id)
        {
            var fields = Enumerable.Range(1, Columns(kind)).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray();
            fields[0] = id.ToString(CultureInfo.InvariantCulture);
            return string.Join("@@", fields);
        }

        internal static uint[] Values(int kind, object row) => kind switch
        {
            0 => JadeValues(Assert.IsType<Jade.CQ>(row)),
            1 => CollectionValues(Assert.IsType<Collection.CQ>(row)),
            2 => LotteryValues(Assert.IsType<Lottery.CQ>(row)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        private static uint[] JadeValues(Jade.CQ row) =>
            [row.ID, (uint)row.Type, row.attr_type1, row.attr_value1, row.attr_type2, row.attr_value2,
             row.score, row.points, row.rate, row.TypeRune, row.Level, row.Exp, row.u12, row.u13, row.TypeCombatGear];
        private static uint[] CollectionValues(Collection.CQ row) =>
            [row.Id, row.Type, row.Min, row.Mix, (uint)row.attr_type1, row.attr_value1, row.value2];
        private static uint[] LotteryValues(Lottery.CQ row) =>
            [row.Id, row.Type, row.Value1, row.Value2, row.Value3, row.rate];

        public void Dispose()
        {
            Jade.Info = priorJade;
            Collection.Info = priorCollection;
            Lottery.Info = priorLottery;
            GearTable.m_Load = priorLoad;
            CultureInfo.CurrentCulture = priorCulture;
            CultureInfo.CurrentUICulture = priorUiCulture;
            Console.SetOut(priorConsole);
            output.Dispose();
            var full = Path.GetFullPath(Root);
            var expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full), expectedParent) ||
                !Regex.IsMatch(Path.GetFileName(full), "^wolf-native-gear-load-[0-9a-f]{32}$"))
                throw new InvalidOperationException("Owned gear-metadata fixture cleanup containment failed.");
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
    }
}
