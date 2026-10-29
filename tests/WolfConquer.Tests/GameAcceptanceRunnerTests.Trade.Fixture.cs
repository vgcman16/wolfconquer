using System.Globalization;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceRunnerTests
{
    // Actual owned saved inputs and real inspector child calls. These are not
    // gameplay trade execution or evidence of a cross-player atomic snapshot.
    private sealed class TradeFixture : IDisposable
    {
        internal const uint A = WalkFixture.Account, B = A + 1, ItemA = ResourceFixture.Uid;
        private readonly WalkFixture data = new();
        internal string Parent => data.Parent;
        internal string Root => data.Root;
        internal string Evidence => data.Evidence;
        internal string PlanPath => Path.Combine(Parent, "trade-plan-input.json");
        internal uint ItemB { get; }
        internal bool Complete;
        internal TradeFixture()
        {
            data.Character(("MaxMana", "100"));
            var character = File.ReadAllText(Path.Combine(data.Routes.UsersRoot, A + ".ini"));
            Assert.Contains("UID=" + A, character, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(data.Routes.UsersRoot, B + ".ini"),
                character.Replace("UID=" + A, "UID=" + B, StringComparison.Ordinal));
            Assert.True(LegacyItemUidRegistry.TryOpen(data.Routes.RegistryOptions, data.Routes.ExpectedDomain,
                out var registry, out var reason), reason);
            using (var owned = Assert.IsType<LegacyItemUidRegistry>(registry))
            {
                Assert.True(owned.TryReserve(3, out var reservation, out reason), reason);
                Assert.True(Assert.IsType<LegacyItemUidRegistry.Reservation>(reservation).TryTake(out var ids));
                ItemB = ids[0];
                var loader = new PlayerItemSnapshotLoader(data.Routes.ItemsRoot);
                var prior = loader.LoadByCharacterUid(A);
                loader.SaveByCharacterUid(A, prior with { InventoryItems = [
                    new() { Uid = ItemA, ItemId = 1001000, StackSize = 0, Durability = 1, MaximumDurability = 1 },
                    new() { Uid = ids[1], ItemId = 410339, StackSize = 1, Durability = 25, MaximumDurability = 30 }] });
                loader.SaveByCharacterUid(B, prior with { InventoryItems = [
                    new() { Uid = ItemB, ItemId = 1000000, StackSize = 3, Durability = 1, MaximumDurability = 1 },
                    new() { Uid = ids[2], ItemId = 410339, StackSize = 1, Durability = 20, MaximumDurability = 30 }] });
            }
            new PlayerSpellSnapshotLoader(data.Routes.SpellsRoot).SaveByCharacterUid(B, PlayerSpellSnapshot.Empty);
            new PlayerProficiencySnapshotLoader(data.Routes.ProficienciesRoot).SaveByCharacterUid(B, PlayerProficiencySnapshot.Empty);
            var plan = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["participants"] = new JsonArray(
                    new JsonObject { ["accountId"] = A, ["goldDelta"] = -7, ["conquerPointsDelta"] = 3 },
                    new JsonObject { ["accountId"] = B, ["goldDelta"] = 7, ["conquerPointsDelta"] = -3 }),
                ["items"] = new JsonArray(
                    new JsonObject { ["uid"] = ItemA, ["itemId"] = 1001000, ["stackSize"] = 0, ["fromAccountId"] = A, ["toAccountId"] = B },
                    new JsonObject { ["uid"] = ItemB, ["itemId"] = 1000000, ["stackSize"] = 3, ["fromAccountId"] = B, ["toAccountId"] = A })
            };
            File.WriteAllText(PlanPath, plan.ToJsonString());
        }
        internal string[] Arguments => data.Arguments.Take(Array.IndexOf(data.Arguments, "-ExpectedMap"))
            .Select(arg => arg == "login-walk-save" ? "trade-two-clients" : arg)
            .Concat(["-CounterpartyAccountId", B.ToString(CultureInfo.InvariantCulture), "-TradePlan", PlanPath]).ToArray();
        internal async Task<JsonNode> Invoke(bool resume = false, string[]? arguments = null)
        {
            var before = Files(Root);
            var result = await RunAsync((arguments ?? Arguments).Concat(resume ? ["-Resume"] : []).ToArray());
            Assert.Empty(result.Error); AssertFiles(Root, before);
            var output = JsonNode.Parse(result.Output)!; output["actualExitCode"] = result.ExitCode;
            if (result.ExitCode == 2)
            {
                var receipt = Read(output["receipt"]!.GetValue<string>());
                Assert.Equal(2, receipt["steps"]!.AsArray().Count);
                Assert.All(receipt["steps"]!.AsArray(), step => Assert.Equal("inspect-owned-player", step!["name"]!.GetValue<string>()));
                Assert.Equal("NotRequested", receipt["sourceChecks"]!["status"]!.GetValue<string>());
                Assert.DoesNotContain(Directory.GetFiles(Evidence), path => Path.GetFileName(path) is "request.json" or "delivery-result.json");
            }
            return output;
        }
        internal void SavedTransfer()
        {
            var loader = new PlayerItemSnapshotLoader(data.Routes.ItemsRoot);
            var a = loader.LoadByCharacterUid(A); var b = loader.LoadByCharacterUid(B);
            var fromA = Assert.Single(a.InventoryItems, row => row.Uid == ItemA);
            var fromB = Assert.Single(b.InventoryItems, row => row.Uid == ItemB);
            loader.SaveByCharacterUid(A, a with { InventoryItems = a.InventoryItems.Where(row => row.Uid != ItemA).Append(fromB).ToArray() });
            loader.SaveByCharacterUid(B, b with { InventoryItems = b.InventoryItems.Where(row => row.Uid != ItemB).Append(fromA).ToArray() });
            Character(A, ("Money", "93"), ("ConquerPoints", "53"));
            Character(B, ("Money", "107"), ("ConquerPoints", "47"));
        }
        internal void Character(uint account, params (string Key, string Value)[] fields)
        {
            Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(Path.Combine(data.Routes.UsersRoot, account + ".ini"),
                account, fields.ToDictionary(pair => pair.Key, pair => pair.Value), true, out var reason), reason);
        }
        internal void ChangeSecondSidecar() => new PlayerSpellSnapshotLoader(data.Routes.SpellsRoot)
            .SaveByCharacterUid(B, new([new(6001, 1, 7, 0, 0, 0)]));
        internal JsonNode Read(string leaf) => data.Read(leaf);
        internal void Write(string leaf, JsonNode value) => data.Write(leaf, value);
        internal string Hash(string leaf) => data.Hash(leaf);
        internal async Task<JsonNode> Status(uint account = 0)
        {
            var before = Files(Parent);
            var result = await RunScriptAsync("Get-GameAcceptanceStatus.ps1", "-EvidenceParent", Parent,
                "-AccountId", account.ToString(CultureInfo.InvariantCulture));
            Assert.Empty(result.Error); AssertFiles(Parent, before);
            var output = JsonNode.Parse(result.Output)!; output["actualExitCode"] = result.ExitCode;
            return output;
        }
        internal static JsonNode Row(JsonNode summary) => Assert.Single(summary["rows"]!.AsArray(),
            row => row!["scenario"]!.GetValue<string>() == "trade-two-clients")!;
        public void Dispose() { data.Complete = Complete; data.Dispose(); }
    }
}
