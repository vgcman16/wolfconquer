using System.Globalization;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceRunnerTests
{
    // Actual owned file/JSON inputs between genuine read-only PowerShell and
    // DataTool runs. This fixture grants no runtime resource/transaction proof.
    private sealed class ResourceFixture : IDisposable
    {
        internal const uint Uid = 8013201;
        private readonly WalkFixture data = new();
        internal string Scenario { get; }
        internal uint Type { get; }
        internal long ExpectedHp { get; }
        internal long ExpectedMana { get; }
        internal bool Full => Scenario == "consume-full-restore";
        internal string Intent => Full ? "full-restore-intent.json" : "mana-intent.json";
        internal string Waiting => Full ? "WaitingForSavedFullRestoreConsumption" : "WaitingForSavedManaConsumption";
        internal string Observed => Full ? "SavedFullRestoreConsumptionObserved" : "SavedManaConsumptionObserved";
        internal string Parent => data.Parent;
        internal string Root => data.Root;
        internal string Evidence => data.Evidence;
        internal LegacyOwnedAdmissionOptions Routes => data.Routes;
        internal bool Complete;
        internal ResourceFixture(bool full, ushort stack = 3, long expectedHp = 80, long expectedMana = 30,
            bool includeMaximumMana = true, uint? type = null)
        {
            Scenario = full ? "consume-full-restore" : "consume-mana"; Type = type ?? (full ? 723726U : 1001000U);
            ExpectedHp = expectedHp; ExpectedMana = expectedMana;
            var loader = new PlayerItemSnapshotLoader(Routes.ItemsRoot); var rows = loader.LoadByCharacterUid(WalkFixture.Account);
            Assert.Equal(Uid, Assert.Single(rows.InventoryItems).Uid);
            loader.SaveByCharacterUid(WalkFixture.Account, rows with { InventoryItems = [new()
                { Uid = Uid, ItemId = Type, StackSize = stack, Durability = 1, MaximumDurability = 1 }] });
            Character(("MinHitPoints", "10"), ("MinMana", "20"));
            if (includeMaximumMana) Character(("MaxMana", "30"));
        }
        internal string[] Arguments
        {
            get
            {
                var prior = data.Arguments;
                var args = prior.Take(Array.IndexOf(prior, "-ExpectedMap"))
                    .Select(arg => arg == "login-walk-save" ? Scenario : arg)
                    .Concat(["-ExpectedItemUid", Uid.ToString(CultureInfo.InvariantCulture), "-ExpectedItemId", Type.ToString(CultureInfo.InvariantCulture),
                        "-ExpectedMana", ExpectedMana.ToString(CultureInfo.InvariantCulture)]);
                return args.Concat(Full ? new[] { "-ExpectedHitpoints", ExpectedHp.ToString(CultureInfo.InvariantCulture) } : []).ToArray();
            }
        }
        internal async Task<JsonNode> Invoke(bool resume = false, params string[] extra)
        {
            var before = Files(Root);
            var result = await RunAsync(Arguments.Concat(resume ? new[] { "-Resume" } : []).Concat(extra).ToArray());
            Assert.Empty(result.Error); AssertFiles(Root, before);
            var json = JsonNode.Parse(result.Output)!; json["actualExitCode"] = result.ExitCode;
            if (result.ExitCode == 2)
            {
                var receipt = Read(json["receipt"]!.GetValue<string>());
                Assert.Equal("inspect-owned-player", Assert.Single(receipt["steps"]!.AsArray())!["name"]!.GetValue<string>());
                Assert.Equal("NotRequested", receipt["sourceChecks"]!["status"]!.GetValue<string>());
                Assert.DoesNotContain(Directory.GetFiles(Evidence), path => Path.GetFileName(path) is "request.json" or "delivery-result.json");
            }
            return json;
        }
        internal void SavedConsumption(long? hp = null, long? mana = null, uint? type = null)
        {
            var loader = new PlayerItemSnapshotLoader(Routes.ItemsRoot); var rows = loader.LoadByCharacterUid(WalkFixture.Account);
            var selected = Assert.Single(rows.InventoryItems, row => row.Uid == Uid);
            var inventory = rows.InventoryItems.Where(row => row.Uid != Uid).ToList();
            if (selected.StackSize > 1) inventory.Insert(0, new PlayerItemSnapshotItem
                { Uid = Uid, ItemId = type ?? Type, StackSize = (ushort)(selected.StackSize - 1), Durability = 1, MaximumDurability = 1 });
            loader.SaveByCharacterUid(WalkFixture.Account, rows with { InventoryItems = inventory });
            Character(("MinMana", (mana ?? ExpectedMana).ToString(CultureInfo.InvariantCulture)));
            if (Full || hp.HasValue) Character(("MinHitPoints", (hp ?? ExpectedHp).ToString(CultureInfo.InvariantCulture)));
        }
        internal void Character(params (string Key, string Value)[] fields) => data.Character(fields);
        internal void AddRows(int count) => data.AddRealReservedRows(count);
        internal void AddContainers() => data.AddRealContainerRows();
        internal JsonNode Read(string leaf) => data.Read(leaf);
        internal void Write(string leaf, JsonNode value) => data.Write(leaf, value);
        internal string Hash(string leaf) => data.Hash(leaf);
        internal void RefreshHashes()
        {
            var initial = Read("receipt.json"); var intent = Read(Intent);
            intent["beforeSha256"] = Hash("before.json"); Write(Intent, intent);
            initial["state"]!["beforeSha256"] = Hash("before.json"); initial["state"]!["intentSha256"] = Hash(Intent);
            Write("receipt.json", initial);
        }
        public void Dispose() { data.Complete = Complete; data.Dispose(); }
    }
}
