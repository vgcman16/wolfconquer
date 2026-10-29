using System.Text.Json.Nodes;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceRunnerTests
{
    [Theory]
    [InlineData("malformed")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("version")]
    [InlineData("changed")]
    [InlineData("foreignEvidence")]
    [InlineData("fractionalDelta")]
    [InlineData("nonconserving")]
    [InlineData("duplicateItem")]
    [InlineData("samePlayer")]
    [InlineData("width")]
    [InlineData("noop")]
    public async Task StrictRetainedTradeIntentRejectsMalformedChangedOrUnsupportedInput(string fault)
    {
        var f = new TradeComparisonFixture(); var plan = f.Plan().ToJsonString();
        var original = f.Intent.ToJsonString(); string? expectedHash = null;
        var text = original;
        switch (fault)
        {
            case "malformed": text = "{bad"; break;
            case "duplicate": text = original.Insert(1, "\"schemaVersion\":1,"); break;
            case "unknown": f.Intent["sendCommand"] = "never-executed"; break;
            case "version": f.Intent["schemaVersion"] = 2; break;
            case "changed": f.Intent["items"]![0]!["itemId"] = 410004; expectedHash = TradeComparisonFixture.Hash(original); break;
            case "foreignEvidence": f.Intent["evidenceId"] = "wolf-game-acceptance-" + Guid.NewGuid().ToString("N"); break;
            case "fractionalDelta": f.Intent["participants"]![0]!["goldDelta"] = -125.5; break;
            case "nonconserving": f.Intent["participants"]![0]!["goldDelta"] = -124; break;
            case "duplicateItem": f.Intent["items"]![1]!["uid"] = 11; break;
            case "samePlayer": f.Intent["participants"]![1]!["accountId"] = TradeComparisonFixture.First; break;
            case "width": f.Intent["items"]![0]!["stackSize"] = 65536; break;
            case "noop":
                f.Intent["items"] = new JsonArray();
                foreach (var party in f.Intent["participants"]!.AsArray()) { party!["goldDelta"] = 0; party["conquerPointsDelta"] = 0; }
                break;
        }
        if (fault is not ("malformed" or "duplicate")) text = f.Intent.ToJsonString();
        var result = await CompareTradeAsync(f, text, expectedHash, plan);
        Assert.NotNull(result["readerError"]); Assert.Null(result["state"]);
        if (fault == "changed") Assert.Equal("tradeIntentChanged", result["readerError"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("route")]
    public async Task TradeSelectionPlanUsesStrictSchemaWithoutBecomingACommand(string fault)
    {
        var f = new TradeComparisonFixture(); var plan = f.Plan();
        if (fault == "unknown") plan["command"] = "never-executed";
        if (fault == "route") plan["items"]![0]!["toAccountId"] = 999;
        var text = plan.ToJsonString();
        if (fault == "duplicate") text = text.Insert(1, "\"schemaVersion\":1,");
        var result = await CompareTradeAsync(f, planText: text);
        Assert.NotNull(result["readerError"]); Assert.Null(result["state"]);
    }

    [Fact]
    public async Task PairWalletTotalsAboveInt64DoNotOverflowComparisonArithmetic()
    {
        var f = new TradeComparisonFixture();
        f.BeforeA["character"]!["gold"] = long.MaxValue - 10; f.BeforeB["character"]!["gold"] = 20;
        f.AfterA["character"]!["gold"] = long.MaxValue - 5; f.AfterB["character"]!["gold"] = 15;
        f.Intent["participants"]![0]!["goldDelta"] = 5; f.Intent["participants"]![1]!["goldDelta"] = -5;
        Assert.Empty((await CompareTradeAsync(f))["issues"]!.AsArray());
    }

    [Fact]
    public async Task MissingTouchedImageChangeAndUnrelatedConfigurationChangeCannotQualify()
    {
        foreach (var kind in new[] { "Items", "Character", "Configuration" })
        {
            var f = new TradeComparisonFixture();
            if (kind == "Configuration") TradeComparisonFixture.File(f.AfterA, kind)["contentSha256"] = TradeComparisonFixture.Hash("foreign-config");
            else TradeComparisonFixture.File(f.AfterA, kind)["contentSha256"] = TradeComparisonFixture.File(f.BeforeA, kind)["contentSha256"]!.DeepClone();
            var result = await CompareTradeAsync(f);
            Assert.Equal("ClientActionPendingOrStateMismatch", result["state"]!["status"]!.GetValue<string>());
            Assert.NotEmpty(result["issues"]!.AsArray());
        }
    }

    [Theory]
    [InlineData("duplicate", "duplicateUidAcrossPlayers")]
    [InlineData("foreign", "sameExplicitSavedIdentityRequired")]
    [InlineData("container", "unknownItemContainer")]
    [InlineData("equipped", "exactSourceInventoryRowRequired")]
    [InlineData("deposited", "selectedOwnershipAmbiguous")]
    [InlineData("attachment", "selectedOwnershipAmbiguous")]
    public async Task InitialTradeSelectionRefusesUnownedOrUnreportedItemAmbiguity(string fault, string issue)
    {
        var f = new TradeComparisonFixture();
        switch (fault)
        {
            case "duplicate": f.BeforeA["items"]!.AsArray().Add(TradeComparisonFixture.Row(f.BeforeB, 21).DeepClone()); f.BeforeA["inventoryCount"] = 2; f.BeforeA["physicalItemCount"] = 6; break;
            case "foreign": f.BeforeA["character"]!["uid"] = 900; break;
            case "container": TradeComparisonFixture.Row(f.BeforeA, 14)["container"] = "unknown"; break;
            case "equipped": TradeComparisonFixture.Row(f.BeforeA, 11)["container"] = "equipment"; TradeComparisonFixture.Row(f.BeforeA, 11)["position"] = 4; f.BeforeA["inventoryCount"] = 0; f.BeforeA["equipmentCount"] = 2; break;
            case "deposited": f.BeforeA["items"]!.AsArray().Add(TradeComparisonFixture.Item(16, 1000000, 1, "inventory.deposited")); f.BeforeA["physicalItemCount"] = 6; break;
            case "attachment": f.BeforeA["mails"]![0]!["attachedItemUid"] = 11; break;
        }
        var result = await CompareTradeAsync(f);
        Assert.Contains(issue, result["selection"]!.AsArray().Select(row => row!.GetValue<string>()));
        Assert.Equal("ClientActionPendingOrStateMismatch", result["state"]!["status"]!.GetValue<string>());
    }
}
