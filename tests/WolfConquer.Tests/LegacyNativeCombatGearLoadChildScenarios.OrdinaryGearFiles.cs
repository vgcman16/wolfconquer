using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    // Copies the exact declared cold-loader inputs into an owned directory for
    // the actual configured runtime service. No metadata owner is synthesized.
    private sealed class OrdinaryGearRuntimeFiles : IDisposable
    {
        private readonly Dictionary<string, byte[]> files = new(StringComparer.OrdinalIgnoreCase);
        internal string DirectoryPath { get; }
        internal string RankPath { get; }
        internal CombatGear[] GearRows { get; }
        internal DaoqiDictType[] JadeRows { get; }

        internal OrdinaryGearRuntimeFiles(Scope scope, bool extendedSchema)
        {
            DirectoryPath = Path.Combine(scope.Root, "ordinary-runtime-metadata");
            Require(!Directory.Exists(DirectoryPath) && !File.Exists(DirectoryPath), "Fresh metadata directory required.");
            Directory.CreateDirectory(DirectoryPath);
            foreach (var leaf in new[] { "combat_gear.txt", "daoqi_dict_type.txt", "dict_lottery.txt", "dict_collection.txt" })
            {
                var path = Path.Combine(DirectoryPath, leaf);
                var content = File.ReadAllBytes(scope.Prefix + leaf);
                if (extendedSchema && leaf == "combat_gear.txt")
                {
                    var lines = System.Text.Encoding.UTF8.GetString(content).Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    content = System.Text.Encoding.UTF8.GetBytes(string.Join("\n", lines.Select(line => line.TrimEnd('\r') + "@@0@@0@@0")) + "\n");
                }
                File.WriteAllBytes(path, content);
                files.Add(path, content);
            }
            RankPath = scope.RankPrefix + "CombatGearRank.txt";
            // This fixture models the explicit native legacy19 layout only.
            // It is not the 7937/22-column managed parser or schema evidence.
            GearRows = File.ReadAllLines(scope.Prefix + "combat_gear.txt")
                .Where(row => !string.IsNullOrWhiteSpace(row))
                .Select((row, index) => ParseLegacy19(row, index + 1))
                .ToArray();
            JadeRows = File.ReadAllLines(Path.Combine(DirectoryPath, "daoqi_dict_type.txt"))
                .Where(row => !string.IsNullOrWhiteSpace(row))
                .Select((row, index) => DaoqiDictTypeParser.ParseRow("owned-ordinary-daoqi_dict_type.txt", index + 1, row + "@@"))
                .ToArray();
        }

        private static CombatGear ParseLegacy19(string row, int line)
        {
            var fields = row.Split("@@", StringSplitOptions.None);
            Require(fields.Length == 19, "Expected the complete owned legacy19 row.");
            var values = fields.Select(value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            return new CombatGear(values[0], values[1], values[2], values[3], values[4], values[5], values[6],
                values[7], values[7], values.Skip(8).Take(10).ToArray(), values[18], 0, 0, 0,
                "owned-ordinary-legacy19", line, row, fields);
        }

        public void Dispose()
        {
            Require(Directory.GetFileSystemEntries(DirectoryPath).Length == files.Count, "Unexpected metadata fixture entry retained.");
            foreach (var (path, content) in files)
                Require(File.ReadAllBytes(path).SequenceEqual(content), "Changed metadata fixture file retained.");
            foreach (var path in files.Keys) File.Delete(path);
            Directory.Delete(DirectoryPath);
        }
    }
}
