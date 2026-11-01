using System.Text;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    private sealed class RuntimeMetadataFiles : IDisposable
    {
        private readonly Dictionary<string, byte[]> files = new(StringComparer.OrdinalIgnoreCase);
        internal string DirectoryPath { get; }
        internal string RankPath { get; }
        internal RuntimeMetadataFiles(Scope scope)
        {
            DirectoryPath = Path.Combine(scope.Root, "runtime-metadata");
            Require(!Directory.Exists(DirectoryPath) && !File.Exists(DirectoryPath), "Fresh runtime metadata root required.");
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(DirectoryPath);
            Directory.CreateDirectory(DirectoryPath);
            foreach (var leaf in new[] { "combat_gear.txt", "daoqi_dict_type.txt", "dict_lottery.txt", "dict_collection.txt" })
            {
                var path = Path.Combine(DirectoryPath, leaf);
                var bytes = File.ReadAllBytes(scope.Prefix + leaf);
                using var stream = File.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(bytes); stream.Flush(true); files.Add(path, bytes);
            }
            RankPath = scope.RankPrefix + "CombatGearRank.txt";
        }
        internal void RewriteGear(string contents)
        {
            var path = Path.Combine(DirectoryPath, "combat_gear.txt");
            Require(File.ReadAllBytes(path).SequenceEqual(files[path]), "Runtime fixture source changed unexpectedly.");
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
            var bytes = new UTF8Encoding(false, true).GetBytes(contents);
            using var stream = File.Open(path, FileMode.Truncate, FileAccess.Write, FileShare.None);
            stream.Write(bytes); stream.Flush(true); files[path] = bytes;
        }
        public void Dispose()
        {
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(DirectoryPath);
            Require(Directory.GetFileSystemEntries(DirectoryPath).Length == files.Count, "Unexpected runtime fixture entry retained.");
            foreach (var pair in files)
            {
                LegacyNativeDbReadOwnedFiles.OrdinaryChain(pair.Key);
                Require(Path.GetDirectoryName(pair.Key) == DirectoryPath &&
                    File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value), "Changed runtime fixture retained.");
            }
            foreach (var path in files.Keys) File.Delete(path);
            Directory.Delete(DirectoryPath, recursive: false);
        }
    }
}
