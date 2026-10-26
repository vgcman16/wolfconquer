using System.Globalization;
using System.Text;
using WolfConquer.Core.World;
using Metadata = WolfConquer.Core.World.LegacyNativeNinjaFile;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeNinjaMetadataLoadTests
{
    private sealed class OwnedFiles : IDisposable
    {
        private readonly Dictionary<uint, Metadata.NinjaSprintItem>? priorTypes = Metadata.gouyu_type;
        private readonly Dictionary<uint, Metadata.Item>? priorImmortals = Metadata.gouyu_immortals;
        private readonly string priorLocation = LegacyNativeServerConfig.DbLocation;
        private readonly TextWriter priorConsole = Console.Out;
        private readonly CultureInfo priorCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo priorUiCulture = CultureInfo.CurrentUICulture;
        private readonly Dictionary<string, byte[]?> expected = new(StringComparer.OrdinalIgnoreCase);
        internal string Root { get; }
        internal StringWriter Output { get; } = new(CultureInfo.InvariantCulture);
        internal Metadata.NinjaSprintItem OldTypeRow { get; }
        internal Metadata.Item OldImmortalRow { get; }
        internal Dictionary<uint, Metadata.NinjaSprintItem> OldTypes { get; } = new()
        {
            [9001] = new() { ID = 9001, ItemID = 999, Score = 123 },
        };
        internal Dictionary<uint, Metadata.Item> OldImmortals { get; } = new()
        {
            [9002] = new() { Level = 9002, MaxHP = 456 },
        };

        internal OwnedFiles()
        {
            Root = Path.Combine(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(),
                "native-ninja-metadata-" + Guid.NewGuid().ToString("N"));
            GuardRoot();
            Assert.False(File.Exists(Root) || Directory.Exists(Root));
            Directory.CreateDirectory(Root);
            foreach (bool immortals in new[] { false, true })
                foreach (bool prefix in new[] { false, true })
                    expected.Add(PathName(immortals, prefix), null);
            OldTypeRow = OldTypes[9001];
            OldImmortalRow = OldImmortals[9002];
            Metadata.gouyu_type = OldTypes;
            Metadata.gouyu_immortals = OldImmortals;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Console.SetOut(Output);
            UseLocation();
        }

        internal string PathName(bool immortals, bool prefix = false) => Path.Combine(Root,
            (prefix ? "prefix" : "") + (immortals ? "gouyu_immortal.txt" : "gouyu_type.txt"));

        internal void UseLocation(bool prefix = false) =>
            LegacyNativeServerConfig.DbLocation = Root + Path.DirectorySeparatorChar + (prefix ? "prefix" : "");

        internal void Seed(bool immortals, string text, bool prefix = false)
            => SeedBytes(immortals, new UTF8Encoding(false, true).GetBytes(text), prefix);

        internal void SeedBytes(bool immortals, byte[] bytes, bool prefix = false)
        {
            string path = PathName(immortals, prefix);
            Guard(path);
            Assert.Null(expected[path]);
            Assert.False(File.Exists(path) || Directory.Exists(path));
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(bytes);
                file.Flush(true);
            }
            expected[path] = bytes.ToArray();
        }

        internal void Rewrite(bool immortals, string text)
        {
            string path = PathName(immortals);
            Guard(path);
            Assert.NotNull(expected[path]);
            Assert.Equal(expected[path], File.ReadAllBytes(path));
            var bytes = new UTF8Encoding(false, true).GetBytes(text);
            using (var file = new FileStream(path, FileMode.Truncate, FileAccess.Write, FileShare.None))
            { file.Write(bytes); file.Flush(true); }
            expected[path] = bytes;
        }

        private void GuardRoot()
        {
            Assert.Equal(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(), Path.GetDirectoryName(Path.GetFullPath(Root)));
            Assert.Matches("^native-ninja-metadata-[0-9a-f]{32}$", Path.GetFileName(Root));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(Root);
        }

        private void Guard(string path)
        {
            GuardRoot();
            Assert.True(expected.ContainsKey(path));
            Assert.Equal(Root, Path.GetDirectoryName(Path.GetFullPath(path)));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
        }

        public void Dispose()
        {
            Metadata.gouyu_type = priorTypes!;
            Metadata.gouyu_immortals = priorImmortals!;
            LegacyNativeServerConfig.DbLocation = priorLocation;
            CultureInfo.CurrentCulture = priorCulture;
            CultureInfo.CurrentUICulture = priorUiCulture;
            Console.SetOut(priorConsole);
            Output.Dispose();
            GuardRoot();
            foreach (string path in Directory.GetFileSystemEntries(Root))
                Assert.True(expected.ContainsKey(path) && !Directory.Exists(path), "Unexpected owned entry is preserved.");
            // Validate every known leaf before deleting any; no recursive deletion.
            foreach (var pair in expected)
            {
                Guard(pair.Key);
                if (pair.Value is null) Assert.False(File.Exists(pair.Key) || Directory.Exists(pair.Key));
                else Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            }
            foreach (var pair in expected)
                if (pair.Value is not null) File.Delete(pair.Key);
            Directory.Delete(Root, recursive: false);
        }
    }

    private static string[] TypeTokens(uint id = 17) =>
        [id.ToString(CultureInfo.InvariantCulture), "100", "3", "4", "5", "6", "7", "8", "-9", "10", "ignored-column-ten", "12"];

    private static string[] ImmortalTokens(uint level = 21) =>
        [level.ToString(CultureInfo.InvariantCulture), "22", "23", "24", "25", "26", "27", "28", "29",
         "ignored-nine", "ignored-ten", "ignored-eleven", "33", "34", "35", "36"];

    private static string Row(bool immortals, uint id = 17) =>
        string.Join("@@", immortals ? ImmortalTokens(id) : TypeTokens(id));

    private static void AssertType(uint key)
    {
        var row = Metadata.gouyu_type[key];
        Assert.Equal(new uint[] { key, 100, 3, 4, 5, 6, 7, 8, 10, 12, 12 },
            new[] { row.ID, row.ItemID, row.Level, row.Exp, row.UK1, row.UK2,
                row.magic1, row.magic2, row.Damage, row.Score, row.UK5 });
        Assert.Equal(-9, row.Power);
    }

    private static void AssertImmortal(uint key)
    {
        var row = Metadata.gouyu_immortals[key];
        Assert.Equal(new uint[] { key, 22, 23, 24, 25, 26, 27, 28, 29, 33, 34, 35, 36 },
            new[] { row.Level, row.U1, row.MaxHP, row.PAttack, row.PDefense, row.MDefense,
                row.Chance, row.Seconds, row.Rate, row.MovementSpeed, row.U13, row.U14, row.P });
    }
}
