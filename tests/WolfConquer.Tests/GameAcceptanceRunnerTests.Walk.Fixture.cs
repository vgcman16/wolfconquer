using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceRunnerTests
{
    private sealed class WalkFixture : IDisposable
    {
        internal const uint Account = 2749500;
        internal string Parent { get; } = Path.Combine(Path.GetTempPath(), "wolf-walk-tests-" + Guid.NewGuid().ToString("N"));
        internal string Root { get; }
        internal string Evidence { get; }
        internal LegacyOwnedAdmissionFixtureResult Owned { get; }
        internal LegacyOwnedAdmissionOptions Routes { get; }
        private readonly Process tool;
        internal bool Complete;
        internal WalkFixture()
        {
            Directory.CreateDirectory(Parent);
            Root = Path.Combine(Parent, "wolf-owned-admission-" + Guid.NewGuid().ToString("N"));
            Evidence = Path.Combine(Parent, "wolf-game-acceptance-" + Guid.NewGuid().ToString("N"));
            Assert.True(LegacyOwnedAdmissionFixture.TryCreate(Root, Account, Account, 8013201,
                ConquerProtocolProfile.VisibleDx8D13, out var owned, out var reason), reason);
            Owned = Assert.IsType<LegacyOwnedAdmissionFixtureResult>(owned);
            Assert.True(LegacyOwnedAdmissionOptions.TryReadFile(Owned.ConfigurationPath, out var routes, out reason), reason);
            Routes = Assert.IsType<LegacyOwnedAdmissionOptions>(routes);
            tool = TestProjectProcess.Create(RepositoryRoot(), "WolfConquer.DataTool");
        }
        internal string[] Arguments => ["-Scenario", "login-walk-save", "-DataToolDll", tool.StartInfo.ArgumentList[0],
            "-OwnedAdmissionConfig", Owned.ConfigurationPath, "-AccountId", Account.ToString(), "-EvidenceRoot", Evidence,
            "-DotNetPath", FindApplication(tool.StartInfo.FileName, "DOTNET_HOST_PATH"),
            "-ExpectedMap", "1002", "-ExpectedX", "411", "-ExpectedY", "355"];
        internal async Task<JsonNode> Invoke(bool resume = false, params string[] extra)
        {
            var files = Files(Root);
            var args = Arguments.Concat(resume ? new[] { "-Resume" } : []).Concat(extra).ToArray();
            var result = await RunAsync(args);
            Assert.True(string.IsNullOrWhiteSpace(result.Error), result.Error);
            AssertFiles(Root, files); // Actual inspector/runner never writes the saved test data.
            var json = JsonNode.Parse(result.Output)!;
            json.AsObject()["actualExitCode"] = result.ExitCode;
            if (resume && result.ExitCode == 2)
            {
                var receipt = Read(json["receipt"]!.GetValue<string>());
                var step = Assert.Single(receipt["steps"]!.AsArray());
                Assert.Equal("inspect-owned-player", step!["name"]!.GetValue<string>());
                Assert.DoesNotContain(Directory.GetFiles(Evidence), path => Path.GetFileName(path) is "request.json" or "delivery-result.json");
            }
            return json;
        }
        internal JsonNode Read(string leaf) => JsonNode.Parse(File.ReadAllBytes(Path.Combine(Evidence, leaf)))!;
        internal void Write(string leaf, JsonNode json) => File.WriteAllText(Path.Combine(Evidence, leaf), json.ToJsonString());
        internal string Hash(string leaf) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Evidence, leaf))));
        internal void Character(params (string Key, string Value)[] fields)
        {
            Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(Path.Combine(Routes.UsersRoot, Account + ".ini"),
                Account, fields.ToDictionary(pair => pair.Key, pair => pair.Value), true, out var reason), reason);
        }
        internal void Destination() => Character(("Map", "1002"), ("X", "411"), ("Y", "355"));
        internal void RefreshRetainedHashes()
        {
            var initial = Read("receipt.json"); var intent = Read("walk-intent.json");
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Evidence, "before.json"))));
            intent["beforeSha256"] = hash; Write("walk-intent.json", intent);
            initial["state"]!["beforeSha256"] = hash;
            initial["state"]!["intentSha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Evidence, "walk-intent.json"))));
            Write("receipt.json", initial);
        }
        internal void AddRealReservedRows(int count)
        {
            Assert.True(LegacyItemUidRegistry.TryOpen(Routes.RegistryOptions, Routes.ExpectedDomain, out var registry, out var reason), reason);
            using var actual = Assert.IsType<LegacyItemUidRegistry>(registry);
            Assert.True(actual.TryReserve(count, out var reservation, out reason), reason);
            Assert.True(Assert.IsType<LegacyItemUidRegistry.Reservation>(reservation).TryTake(out var ids));
            var loader = new PlayerItemSnapshotLoader(Routes.ItemsRoot); var prior = loader.LoadByCharacterUid(Account);
            loader.SaveByCharacterUid(Account, prior with { InventoryItems = prior.InventoryItems.Concat(ids.Select(uid =>
                new PlayerItemSnapshotItem { Uid = uid, ItemId = 1001000, StackSize = 1, Durability = 1, MaximumDurability = 1 })).ToArray() });
        }
        internal void AddRealContainerRows()
        {
            Assert.True(LegacyItemUidRegistry.TryOpen(Routes.RegistryOptions, Routes.ExpectedDomain, out var registry, out var reason), reason);
            using var actual = Assert.IsType<LegacyItemUidRegistry>(registry);
            Assert.True(actual.TryReserve(14, out var reservation, out reason), reason);
            Assert.True(Assert.IsType<LegacyItemUidRegistry.Reservation>(reservation).TryTake(out var ids));
            PlayerItemSnapshotItem Row(int index, ushort position = 0, uint warehouse = 0) => new()
            {
                Uid = ids[index * 2], ItemId = 1001000, Position = position, WarehouseId = warehouse,
                StackSize = 1, Durability = 1, MaximumDurability = 1,
                DepositedItems = [new() { Uid = ids[index * 2 + 1], ItemId = 1001000, StackSize = 1, Durability = 1, MaximumDurability = 1 }]
            };
            var loader = new PlayerItemSnapshotLoader(Routes.ItemsRoot); var prior = loader.LoadByCharacterUid(Account);
            loader.SaveByCharacterUid(Account, prior with
            {
                InventoryItems = prior.InventoryItems.Concat([Row(0)]).ToArray(),
                EquipmentItems = prior.EquipmentItems.Concat([Row(1, 4)]).ToArray(),
                WarehouseItems = prior.WarehouseItems.Concat([Row(2, warehouse: 8)]).ToArray(),
                RelicFuseItems = prior.RelicFuseItems.Concat([Row(3, 213)]).ToArray(),
                MythsoulBagItems = prior.MythsoulBagItems.Concat([Row(4, 217)]).ToArray(),
                RuneItems = prior.RuneItems.Concat([Row(5, 211)]).ToArray(),
                WardrobeItems = prior.WardrobeItems.Concat([Row(6, warehouse: 100)]).ToArray()
            });
        }
        public void Dispose()
        {
            tool.Dispose();
            if (Complete) DeleteOwned(Parent, "wolf-walk-tests-");
        }
    }

    private static void WalkResult(JsonNode json, string status, int exit = 2)
    {
        Assert.Equal(exit, json["actualExitCode"]!.GetValue<int>());
        Assert.Equal(status, json["status"]!.GetValue<string>());
        Assert.Equal("LiveClientVisualPending", json["visualAcceptance"]!.GetValue<string>());
        if (exit == 2)
        {
            Assert.False(json["state"]!["liveStoreSynchronized"]!.GetValue<bool>());
            Assert.Equal("WirePending", json["state"]!["wireStatus"]!.GetValue<string>());
            Assert.Equal("SourceGatePending", json["state"]!["sourceGateStatus"]!.GetValue<string>());
        }
    }
}
