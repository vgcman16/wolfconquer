using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceRunnerTests
{
    // Synthetic inspector-shaped data only. It grants no saved-file/runtime authority.
    private sealed class TradeComparisonFixture
    {
        internal const uint First = 2749601, Second = 2749602;
        internal string EvidenceId { get; } = "wolf-game-acceptance-" + Guid.NewGuid().ToString("N");
        internal string Domain { get; } = Guid.NewGuid().ToString("N");
        internal JsonObject BeforeA { get; }
        internal JsonObject BeforeB { get; }
        internal JsonObject AfterA { get; }
        internal JsonObject AfterB { get; }
        internal JsonObject Intent { get; }
        internal TradeComparisonFixture()
        {
            BeforeA = Report(First, 1000, 250, [Item(11, 410003, 0), Item(12, 118003, 1, "equipment", 1),
                Item(13, 1000000, 2, "warehouse", warehouse: 8), Item(14, 2000000, 1, "wardrobe"),
                Item(15, 2000001, 1, "warehouse.deposited")]);
            BeforeB = Report(Second, 5000, 750, [Item(21, 1001000, 4), Item(22, 118003, 1, "equipment", 1)]);
            AfterA = (JsonObject)BeforeA.DeepClone(); AfterB = (JsonObject)BeforeB.DeepClone();
            Move(AfterA, AfterB, 11); Move(AfterB, AfterA, 21);
            AfterA["character"]!["gold"] = 875; AfterA["character"]!["conquerPoints"] = 300;
            AfterB["character"]!["gold"] = 5125; AfterB["character"]!["conquerPoints"] = 700;
            foreach (var report in new[] { AfterA, AfterB })
            {
                File(report, "Character")["contentSha256"] = Hash("changed-character-" + report["accountId"]);
                File(report, "Items")["contentSha256"] = Hash("changed-items-" + report["accountId"]);
            }
            Intent = new()
            {
                ["schemaVersion"] = 1, ["evidenceId"] = EvidenceId, ["domain"] = Domain,
                ["participants"] = new JsonArray(Party(First, -125, 50), Party(Second, 125, -50)),
                ["items"] = new JsonArray(Selection(11, 410003, 0, First, Second), Selection(21, 1001000, 4, Second, First)),
                ["beforeSha256"] = new JsonArray(Hash(BeforeA.ToJsonString()), Hash(BeforeB.ToJsonString()))
            };
        }
        internal JsonObject Plan()
        {
            var parties = (JsonArray)Intent["participants"]!.DeepClone();
            foreach (var party in parties) party!.AsObject().Remove("characterUid");
            return new() { ["schemaVersion"] = 1, ["participants"] = parties, ["items"] = Intent["items"]!.DeepClone() };
        }
        private JsonObject Report(uint account, long gold, long cp, JsonObject[] items) => new()
        {
            ["schemaVersion"] = 1, ["domain"] = Domain, ["accountId"] = account,
            ["character"] = new JsonObject { ["uid"] = account, ["map"] = 1002, ["x"] = 400, ["y"] = 350,
                ["body"] = 1003, ["class"] = 10, ["level"] = 120, ["gold"] = gold, ["conquerPoints"] = cp,
                ["hitpoints"] = 2176, ["minMana"] = 493, ["maxMana"] = 500 },
            ["inventoryCount"] = 1, ["equipmentCount"] = 1, ["warehouseCount"] = account == First ? 1 : 0,
            ["physicalItemCount"] = items.Length, ["spellCount"] = 2, ["proficiencyCount"] = 1, ["activeMailCount"] = 1,
            ["items"] = new JsonArray(items.Select(row => (JsonNode)row).ToArray()),
            ["mails"] = new JsonArray(new JsonObject { ["mailId"] = 1, ["attachedItemUid"] = 0,
                ["attachmentPresentInInbox"] = false, ["gold"] = 7, ["conquerPoints"] = 0, ["packageFlag"] = 0 }),
            ["summariesTruncated"] = false, ["liveStoreSynchronized"] = false,
            ["files"] = new JsonArray(new[] { "Configuration", "Character", "Items", "Spells", "Proficiencies", "Mailbox" }
                .Select(kind => (JsonNode)new JsonObject { ["kind"] = kind, ["present"] = true, ["length"] = 100,
                    ["bytesAndMetadataUnchanged"] = true, ["contentSha256"] = Hash(kind == "Configuration" ? Domain : kind + account) }).ToArray())
        };
        private static JsonObject Party(uint account, long gold, long cp) => new()
        { ["accountId"] = account, ["characterUid"] = account, ["goldDelta"] = gold, ["conquerPointsDelta"] = cp };
        private static JsonObject Selection(uint uid, uint item, int stack, uint from, uint to) => new()
        { ["uid"] = uid, ["itemId"] = item, ["stackSize"] = stack, ["fromAccountId"] = from, ["toAccountId"] = to };
        internal static JsonObject Item(uint uid, uint item, int stack, string container = "inventory", int position = 0, int warehouse = 0) => new()
        {
            ["container"] = container, ["uid"] = uid, ["itemId"] = item, ["position"] = position, ["warehouseId"] = warehouse,
            ["plus"] = 0, ["stackSize"] = stack, ["durability"] = 100, ["maximumDurability"] = 100,
            ["socketOne"] = 0, ["socketTwo"] = 0, ["bound"] = 0, ["locked"] = 0
        };
        private static void Move(JsonObject source, JsonObject target, uint uid)
        {
            var row = Row(source, uid); source["items"]!.AsArray().Remove(row); target["items"]!.AsArray().Add(row);
        }
        internal static JsonObject Row(JsonObject report, uint uid) => report["items"]!.AsArray().Single(row => row!["uid"]!.GetValue<uint>() == uid)!.AsObject();
        internal static JsonObject File(JsonObject report, string kind) => report["files"]!.AsArray().Single(row => row!["kind"]!.GetValue<string>() == kind)!.AsObject();
        internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
