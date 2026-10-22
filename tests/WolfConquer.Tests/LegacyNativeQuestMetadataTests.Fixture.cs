using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Quest = WolfConquer.Core.Data.LegacyNativeQuestInfo;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeQuestMetadataTests
{
    private sealed class Scope : IDisposable
    {
        private readonly Dictionary<uint, Quest.DBQuest> oldAll = Quest.AllQuests;
        private readonly Dictionary<uint, Quest.ListNpcQuests> oldNpcs = Quest.NpcQuests;
        private readonly List<uint> oldMissions = Quest.KingDomMissions;
        private readonly string oldLocation = LegacyNativeServerConfig.DbLocation;
        private readonly CultureInfo oldCulture = CultureInfo.CurrentCulture, oldUi = CultureInfo.CurrentUICulture;
        private readonly Dictionary<string, byte[]> seeds = new(StringComparer.OrdinalIgnoreCase);
        private readonly string root = Path.Combine(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(), "native-quest-meta-" + Guid.NewGuid().ToString("N"));
        internal string Root => root;

        internal Scope()
        {
            Assert.True(OperatingSystem.IsWindows());
            Guard(); Assert.False(Directory.Exists(root)); Assert.False(File.Exists(root)); Directory.CreateDirectory(root);
            Quest.AllQuests = new(); Quest.NpcQuests = new(); Quest.KingDomMissions = new();
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            LegacyNativeServerConfig.DbLocation = root;
        }
        internal string Seed(string text, bool prefix = false)
        {
            string parent = prefix ? Path.Combine(root, "prefix") : root;
            Directory.CreateDirectory(parent);
            string path = Path.Combine(parent, "Questinfo.ini");
            Assert.False(File.Exists(path)); LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
            byte[] bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray();
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { file.Write(bytes); file.Flush(true); }
            seeds.Add(path, bytes); return path;
        }
        private void Guard()
        {
            Assert.Equal(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(), Path.GetDirectoryName(Path.GetFullPath(root)));
            Assert.Matches("^native-quest-meta-[0-9a-f]{32}$", Path.GetFileName(root));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(root);
        }
        public void Dispose()
        {
            Quest.AllQuests = oldAll; Quest.NpcQuests = oldNpcs; Quest.KingDomMissions = oldMissions;
            LegacyNativeServerConfig.DbLocation = oldLocation;
            CultureInfo.CurrentCulture = oldCulture; CultureInfo.CurrentUICulture = oldUi;
            Guard();
            string prefix = Path.Combine(root, "prefix");
            foreach (string entry in Directory.GetFileSystemEntries(root))
                Assert.True(seeds.ContainsKey(entry) || entry == prefix && Directory.Exists(entry), "Unexpected fixture entry preserved.");
            if (Directory.Exists(prefix))
                foreach (string entry in Directory.GetFileSystemEntries(prefix)) Assert.True(seeds.ContainsKey(entry));
            foreach (var (path, bytes) in seeds)
            { LegacyNativeDbReadOwnedFiles.OrdinaryChain(path); Assert.Equal(bytes, File.ReadAllBytes(path)); }
            foreach (string path in seeds.Keys) File.Delete(path);
            if (Directory.Exists(prefix)) Directory.Delete(prefix, false);
            Directory.Delete(root, false);
        }
    }

    private static Quest.DBQuest Row(ushort id, params uint[] profession) => new() { MissionId = id, Profession = profession };
    private static void Npc(uint id, params Quest.DBQuest[] rows) => Quest.NpcQuests.Add(id,
        new Quest.ListNpcQuests { Quests = rows.ToList() });
    private static string Ini(int total, params string[] sections) => "[TotalMission]\r\nTotalMission=" + total + "\r\n" + string.Join("\r\n", sections);
}
