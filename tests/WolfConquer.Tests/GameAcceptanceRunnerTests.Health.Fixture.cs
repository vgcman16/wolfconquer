using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceRunnerTests
{
    // Reuses the genuine issued UID/domain fixture, never a live runtime HP seed.
    // Owned file writes below prepare simulated saved inputs between read-only runs.
    private sealed class HealthFixture : IDisposable
    {
        internal const uint Uid = 8013201, Type = 1000000;
        private readonly WalkFixture data = new();
        internal string Parent => data.Parent;
        internal string Root => data.Root;
        internal string Evidence => data.Evidence;
        internal LegacyOwnedAdmissionOptions Routes => data.Routes;
        internal bool Complete;
        internal HealthFixture(ushort stack = 3, bool includeMaximumMana = true)
        {
            var loader = new PlayerItemSnapshotLoader(Routes.ItemsRoot); var rows = loader.LoadByCharacterUid(WalkFixture.Account);
            Assert.Equal(Uid, Assert.Single(rows.InventoryItems).Uid);
            loader.SaveByCharacterUid(WalkFixture.Account, rows with { InventoryItems = [new()
            { Uid = Uid, ItemId = Type, StackSize = stack, Durability = 1, MaximumDurability = 1 }] });
            data.Character(("MinHitPoints", "10"), ("MinMana", "20"));
            if (includeMaximumMana) data.Character(("MaxMana", "30"));
        }
        internal string[] Arguments
        {
            get
            {
                var prior = data.Arguments;
                return prior.Take(Array.IndexOf(prior, "-ExpectedMap")).Select(arg => arg == "login-walk-save" ? "consume-health" : arg)
                    .Concat(["-ExpectedItemUid", Uid.ToString(), "-ExpectedItemId", Type.ToString(), "-ExpectedHitpoints", "80"]).ToArray();
            }
        }
        internal async Task<JsonNode> Invoke(bool resume = false, params string[] extra)
        {
            var before = Files(Root);
            var result = await RunAsync(Arguments.Concat(resume ? new[] { "-Resume" } : []).Concat(extra).ToArray());
            Assert.Empty(result.Error); AssertFiles(Root, before);
            var json = JsonNode.Parse(result.Output)!; json.AsObject()["actualExitCode"] = result.ExitCode;
            if (result.ExitCode == 2)
            {
                var receipt = Read(json["receipt"]!.GetValue<string>());
                var step = Assert.Single(receipt["steps"]!.AsArray());
                Assert.Equal("inspect-owned-player", step!["name"]!.GetValue<string>());
                Assert.Equal("NotRequested", receipt["sourceChecks"]!["status"]!.GetValue<string>());
                Assert.DoesNotContain(Directory.GetFiles(Evidence), path => Path.GetFileName(path) is "request.json" or "delivery-result.json");
            }
            return json;
        }
        internal void SavedConsumption(long hp = 80, uint type = Type)
        {
            var loader = new PlayerItemSnapshotLoader(Routes.ItemsRoot); var rows = loader.LoadByCharacterUid(WalkFixture.Account);
            var selected = Assert.Single(rows.InventoryItems, row => row.Uid == Uid);
            var inventory = rows.InventoryItems.Where(row => row.Uid != Uid).ToList();
            if (selected.StackSize > 1) inventory.Insert(0, new PlayerItemSnapshotItem
            { Uid = Uid, ItemId = type, StackSize = (ushort)(selected.StackSize - 1), Durability = 1, MaximumDurability = 1 });
            loader.SaveByCharacterUid(WalkFixture.Account, rows with { InventoryItems = inventory });
            data.Character(("MinHitPoints", hp.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        internal void Character(params (string Key, string Value)[] fields) => data.Character(fields);
        internal void AddRows(int count) => data.AddRealReservedRows(count);
        internal void AddContainers() => data.AddRealContainerRows();
        internal JsonNode Read(string leaf) => data.Read(leaf);
        internal void Write(string leaf, JsonNode json) => data.Write(leaf, json);
        internal string Hash(string leaf) => data.Hash(leaf);
        internal void RefreshHashes()
        {
            var initial = Read("receipt.json"); var intent = Read("health-intent.json");
            intent["beforeSha256"] = Hash("before.json"); Write("health-intent.json", intent);
            initial["state"]!["beforeSha256"] = Hash("before.json"); initial["state"]!["intentSha256"] = Hash("health-intent.json");
            Write("receipt.json", initial);
        }
        public void Dispose() { data.Complete = Complete; data.Dispose(); }
    }
}
