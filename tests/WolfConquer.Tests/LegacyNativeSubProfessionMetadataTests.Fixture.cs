using System.Globalization;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeSubProfessionMetadataTests
{
    private static readonly byte[] Classes = [1, 2, 3, 4, 5, 6, 9];

    private sealed class OwnedIni : IDisposable
    {
        private readonly string priorLocation = LegacyNativeServerConfig.DbLocation;
        private readonly CultureInfo priorCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo priorUiCulture = CultureInfo.CurrentUICulture;
        private readonly string root;
        private readonly Dictionary<string, byte[]?> expected = new(StringComparer.OrdinalIgnoreCase);
        internal string FilePath => Path.Combine(root, "SubProfessionInfo.ini");
        internal string PrefixPath => Path.Combine(root, "prefixSubProfessionInfo.ini");

        internal OwnedIni()
        {
            Assert.True(OperatingSystem.IsWindows(), "Actual INI loading requires Windows Kernel32.");
            root = Path.Combine(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(),
                "native-subprofession-meta-" + Guid.NewGuid().ToString("N"));
            GuardRoot();
            Assert.False(Directory.Exists(root));
            Assert.False(File.Exists(root));
            Directory.CreateDirectory(root);
            expected.Add(FilePath, null);
            expected.Add(PrefixPath, null);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            UseNormalLocation();
        }

        internal void UseNormalLocation() =>
            LegacyNativeServerConfig.DbLocation = root + Path.DirectorySeparatorChar;

        internal void UseLiteralPrefix() =>
            LegacyNativeServerConfig.DbLocation = root + Path.DirectorySeparatorChar + "prefix";

        internal void Seed(string text, bool prefix = false)
        {
            string path = prefix ? PrefixPath : FilePath;
            Guard(path);
            Assert.Null(expected[path]);
            Assert.False(File.Exists(path));
            byte[] bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray();
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(bytes);
                file.Flush(true);
            }
            expected[path] = bytes;
            Assert.Equal(new byte[] { 255, 254 }, File.ReadAllBytes(path)[..2]);
        }

        private void GuardRoot()
        {
            Assert.Equal(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(), Path.GetDirectoryName(Path.GetFullPath(root)));
            Assert.Matches("^native-subprofession-meta-[0-9a-f]{32}$", Path.GetFileName(root));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(root);
        }

        private void Guard(string path)
        {
            GuardRoot();
            Assert.True(expected.ContainsKey(path));
            Assert.Equal(root, Path.GetDirectoryName(Path.GetFullPath(path)));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
        }

        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation = priorLocation;
            CultureInfo.CurrentCulture = priorCulture;
            CultureInfo.CurrentUICulture = priorUiCulture;
            GuardRoot();
            foreach (string path in Directory.GetFileSystemEntries(root))
                Assert.True(expected.ContainsKey(path) && !Directory.Exists(path), "Unexpected owned entry is preserved.");
            // Validate every owned leaf before deleting any; no recursive cleanup.
            foreach (var pair in expected)
            {
                Guard(pair.Key);
                if (pair.Value is null) Assert.False(File.Exists(pair.Key) || Directory.Exists(pair.Key));
                else Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            }
            foreach (var pair in expected)
                if (pair.Value is not null) File.Delete(pair.Key);
            Directory.Delete(root, recursive: false);
        }
    }

    private static string Section(byte id, byte phase) =>
        phase == 0 ? id.ToString(CultureInfo.InvariantCulture) :
            id.ToString(CultureInfo.InvariantCulture) + phase.ToString(CultureInfo.InvariantCulture);

    private static uint Damage(byte id, byte phase, uint offset = 0) => offset + (uint)(id * 1000 + phase);

    private static string CompleteIni(Dictionary<string, string?>? overrides = null, uint offset = 0)
    {
        var text = new StringBuilder();
        foreach (byte id in Classes)
        {
            for (byte phase = 0; phase < 10; phase++)
            {
                string section = Section(id, phase);
                text.Append('[').Append(section).Append("]\r\n");
                string intro = id == 6
                    ? "魔攻+" + Damage(id, phase, offset).ToString(CultureInfo.InvariantCulture) +
                        ";物攻+" + (offset + (uint)(id * 2000 + phase)).ToString(CultureInfo.InvariantCulture)
                    : "特效+" + Damage(id, phase, offset).ToString(CultureInfo.InvariantCulture) + "%附加";
                Add("intro", intro);
                Add("req_lv", (id == 6 ? 250 : id * 20 + phase).ToString(CultureInfo.InvariantCulture));
                Add("req_meto", (id == 6 ? 255 : phase % 3).ToString(CultureInfo.InvariantCulture));
                text.Append("\r\n");

                void Add(string key, string fallback)
                {
                    string? value = fallback;
                    if (overrides is not null && overrides.TryGetValue(section + "/" + key, out var replacement))
                        value = replacement;
                    if (value is not null) text.Append(key).Append('=').Append(value).Append("\r\n");
                }
            }
        }
        return text.ToString();
    }

    private static void AssertRow(LegacyNativeSubProfessionInfo.ProfessionInfo row,
        byte id, byte phase, uint offset = 0)
    {
        Assert.Equal(id == 6
            ? new[] { Damage(id, phase, offset), offset + (uint)(id * 2000 + phase) }
            : new[] { Damage(id, phase, offset) }, row.Damage);
        Assert.Equal(id == 6 ? 0u : (uint)(id * 20 + phase), row.NeedLevel);
        Assert.Equal(id == 6 ? (byte)0 : (byte)(phase % 3), row.NeedReborn);
    }

    private static void AssertComplete(LegacyNativeSubProfessionInfo metadata, uint offset = 0)
    {
        Assert.Equal(7, metadata.Count);
        Assert.Equal(70, metadata.Values.Sum(rows => rows.Count));
        Assert.Equal(Classes.Select(id => (LegacyNativeDbLevExp.Sort)id).ToArray(), metadata.Keys.OrderBy(id => id).ToArray());
        var arrays = new HashSet<uint[]>();
        var records = new HashSet<LegacyNativeSubProfessionInfo.ProfessionInfo>();
        foreach (byte id in Classes)
        {
            var rows = metadata[(LegacyNativeDbLevExp.Sort)id];
            Assert.Equal(10, rows.Count);
            for (byte phase = 0; phase < 10; phase++)
            {
                var row = rows[phase];
                Assert.True(records.Add(row));
                Assert.True(arrays.Add(row.Damage));
                AssertRow(row, id, phase, offset);
            }
        }
    }

    private static LegacyNativeGameClientRuntime Client(ushort level, byte reborn)
    {
        var client = new LegacyNativeGameClientRuntime();
        typeof(LegacyNativePlayerRuntime).GetField("_level", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(client.Player, level);
        client.Player.Reborn = reborn;
        return client;
    }
}
