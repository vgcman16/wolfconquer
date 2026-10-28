using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceStatusTests
{
    private sealed class EvidenceFixture : IDisposable
    {
        internal const uint Account = 2749100, ItemUid = 8013201, ItemId = 410339;
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        internal string Parent { get; } = Path.Combine(Path.GetTempPath(), "wolf-game-status-tests-" + Guid.NewGuid().ToString("N"));
        private string OwnedRoot { get; } = Path.Combine(Path.GetTempPath(), "wolf-owned-admission-" + Guid.NewGuid().ToString("N"));
        internal LegacyOwnedAdmissionFixtureResult Owned { get; }
        private readonly LegacyOwnedAdmissionOptions options;
        private int stamp;
        internal EvidenceFixture()
        {
            Directory.CreateDirectory(Parent);
            Assert.True(LegacyOwnedAdmissionFixture.TryCreate(OwnedRoot, Account, Account, ItemUid,
                ConquerProtocolProfile.VisibleDx8D13, out var fixture, out var reason), reason);
            Owned = Assert.IsType<LegacyOwnedAdmissionFixtureResult>(fixture);
            Assert.True(LegacyOwnedAdmissionOptions.TryReadFile(Owned.ConfigurationPath, out var parsed, out reason), reason);
            options = Assert.IsType<LegacyOwnedAdmissionOptions>(parsed);
            // Quiescent saved-schema fixture, not a claim of gameplay equip acceptance.
            var loader = new PlayerItemSnapshotLoader(options.ItemsRoot);
            var rows = loader.LoadByCharacterUid(Account);
            var gear = new PlayerItemSnapshotItem { Uid = ItemUid, ItemId = ItemId, StackSize = 1, Durability = 321, MaximumDurability = 321 };
            loader.SaveByCharacterUid(Account, rows with { InventoryItems = new[] { gear } });
        }
        internal string Evidence(string scenario, bool equipped = false)
        {
            var root = Path.Combine(Parent, "wolf-game-acceptance-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            using var tool = TestProjectProcess.Create(RepositoryRoot(), "WolfConquer.DataTool");
            var owner = new JsonObject
            {
                ["schemaVersion"] = 1, ["evidenceId"] = Path.GetFileName(root), ["scenario"] = scenario,
                ["accountId"] = Account, ["configPath"] = Owned.ConfigurationPath,
                ["configSha256"] = Hash(File.ReadAllBytes(Owned.ConfigurationPath)),
                ["dataToolPath"] = tool.StartInfo.ArgumentList[0],
                ["dataToolSha256"] = Hash(File.ReadAllBytes(tool.StartInfo.ArgumentList[0]))
            };
            if (scenario == "equip-unequip")
            {
                owner["expectedItemUid"] = ItemUid; owner["expectedItemId"] = ItemId;
                owner["expectedEquipmentPosition"] = 4;
            }
            Write(root, "owner.json", owner);
            var before = Inspection(); Write(root, "before.json", before);
            var initial = Receipt(scenario, scenario == "equip-unequip" ? "WaitingForEquip" : "GuidedActionsPending");
            if (scenario == "equip-unequip")
            {
                Pin(initial); initial["state"]!["beforeSha256"] = Hash(File.ReadAllBytes(Path.Combine(root, "before.json")));
            }
            Write(root, "receipt.json", initial);
            if (equipped)
            {
                var loader = new PlayerItemSnapshotLoader(options.ItemsRoot);
                var rows = loader.LoadByCharacterUid(Account);
                loader.SaveByCharacterUid(Account, rows with { InventoryItems = [], EquipmentItems = new[] { rows.InventoryItems.Single().WithPosition(4) } });
                var afterLeaf = "resume-" + Guid.NewGuid().ToString("N") + "-after.json";
                Write(root, afterLeaf, Inspection());
                var final = Receipt(scenario, "SavedEquipObserved"); Pin(final);
                final["after"] = afterLeaf; final["state"]!["assertions"] = new JsonArray();
                final["state"]!["afterSha256"] = Hash(File.ReadAllBytes(Path.Combine(root, afterLeaf)));
                final["state"]!["liveStoreSynchronized"] = false;
                Write(root, ResumeLeaf(), final);
            }
            return root;
        }
        private JsonNode Inspection()
        {
            Assert.True(LegacyOwnedPlayerInspection.TryInspect(Owned.ConfigurationPath, Account, out var report, out var reason), reason);
            return JsonSerializer.SerializeToNode(new { success = true, report }, Json)!;
        }
        internal static JsonObject Receipt(string scenario, string status) => new()
        {
            ["schemaVersion"] = 1, ["scenario"] = scenario, ["accountId"] = Account,
            ["observedUtc"] = DateTime.UtcNow.ToString("O"), ["before"] = "before.json", ["after"] = null,
            ["state"] = new JsonObject { ["status"] = status, ["liveClientVisualStatus"] = "LiveClientVisualPending" },
            ["steps"] = new JsonArray(), ["sourceChecks"] = new JsonObject { ["status"] = "NotRequested" },
            ["visualAcceptance"] = "LiveClientVisualPending", ["bound"] = "Recorded saved schema fixture only."
        };
        private static void Pin(JsonObject receipt)
        {
            receipt["state"]!["expectedItemUid"] = ItemUid; receipt["state"]!["expectedItemId"] = ItemId;
            receipt["state"]!["expectedPosition"] = 4;
        }
        internal void Write(string root, string leaf, JsonNode value) => Raw(root, leaf, value.ToJsonString(Json));
        internal void Raw(string root, string leaf, string text)
        {
            var path = Path.Combine(root, leaf); File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
            File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(++stamp));
        }
        internal static JsonNode Read(string root, string leaf) => JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, leaf)))!;
        internal static string ResumeLeaf() => "resume-" + Guid.NewGuid().ToString("N") + "-receipt.json";
        internal Dictionary<string, (byte[] Bytes, long Mtime)> Capture() => new[] { Parent, OwnedRoot }
            .SelectMany(root => Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            .ToDictionary(path => path, path => (File.ReadAllBytes(path), File.GetLastWriteTimeUtc(path).Ticks));
        internal void AssertUnchanged(Dictionary<string, (byte[] Bytes, long Mtime)> before)
        {
            var after = Capture(); Assert.Equal(before.Keys.Order(), after.Keys.Order());
            foreach (var (path, value) in before) { Assert.Equal(value.Bytes, after[path].Bytes); Assert.Equal(value.Mtime, after[path].Mtime); }
        }
        private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
        public void Dispose() { DeleteOwned(Parent, "wolf-game-status-tests-"); DeleteOwned(OwnedRoot, "wolf-owned-admission-"); }
        private static void DeleteOwned(string root, string prefix)
        {
            var full = Path.GetFullPath(root); var leaf = Path.GetFileName(full);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.GetDirectoryName(full), ignoreCase: true);
            Assert.StartsWith(prefix, leaf, StringComparison.Ordinal);
            Assert.True(Guid.TryParseExact(leaf[prefix.Length..], "N", out var id) && id != Guid.Empty);
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
    }
}
