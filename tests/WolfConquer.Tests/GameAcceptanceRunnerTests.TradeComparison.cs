using System.Text.Json.Nodes;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceRunnerTests
{
    [Fact]
    public async Task SavedTradeComparisonObservesTwoDirectionsRawZeroStackAndConservedWalletsOnly()
    {
        var f = new TradeComparisonFixture();
        var result = await CompareTradeAsync(f);
        Assert.Empty(result["selection"]!.AsArray()); Assert.Empty(result["issues"]!.AsArray());
        Assert.Equal(2, result["planParticipants"]!.GetValue<int>());
        var state = result["state"]!;
        Assert.Equal("SavedTradeObserved", state["status"]!.GetValue<string>());
        Assert.Equal("AtomicityPending", state["atomicityStatus"]!.GetValue<string>());
        Assert.Equal("LiveClientVisualPending", state["liveClientVisualStatus"]!.GetValue<string>());
        Assert.Equal("WirePending", state["wireStatus"]!.GetValue<string>());
        Assert.Equal("SourceGatePending", state["sourceGateStatus"]!.GetValue<string>());
        Assert.False(state["liveStoreSynchronized"]!.GetValue<bool>());
    }

    [Fact]
    public async Task SavedCurrencyOnlyTradeRequiresUnchangedCompleteItemImages()
    {
        var f = new TradeComparisonFixture(); f.Intent["items"] = new JsonArray();
        foreach (var pair in new[] { (f.BeforeA, f.AfterA), (f.BeforeB, f.AfterB) })
        {
            pair.Item2["items"] = pair.Item1["items"]!.DeepClone();
            TradeComparisonFixture.File(pair.Item2, "Items")["contentSha256"] = TradeComparisonFixture.File(pair.Item1, "Items")["contentSha256"]!.DeepClone();
        }
        Assert.Empty((await CompareTradeAsync(f))["issues"]!.AsArray());
    }

    [Fact]
    public async Task SavedItemOnlyTradeRequiresUnchangedCharacterImagesAndWallets()
    {
        var f = new TradeComparisonFixture();
        foreach (var pair in new[] { (f.BeforeA, f.AfterA), (f.BeforeB, f.AfterB) })
        {
            pair.Item2["character"] = pair.Item1["character"]!.DeepClone();
            TradeComparisonFixture.File(pair.Item2, "Character")["contentSha256"] = TradeComparisonFixture.File(pair.Item1, "Character")["contentSha256"]!.DeepClone();
        }
        foreach (var party in f.Intent["participants"]!.AsArray()) { party!["goldDelta"] = 0; party["conquerPointsDelta"] = 0; }
        Assert.Empty((await CompareTradeAsync(f))["issues"]!.AsArray());
    }

    [Fact]
    public async Task SavedOneWayTradeCountsPhysicalRowsAndKeepsUntouchedAbsentSidecars()
    {
        var f = new TradeComparisonFixture(); f.Intent["items"]!.AsArray().RemoveAt(1);
        f.AfterA["items"] = f.BeforeA["items"]!.DeepClone();
        f.AfterA["items"]!.AsArray().Remove(TradeComparisonFixture.Row(f.AfterA, 11));
        f.AfterB["items"] = f.BeforeB["items"]!.DeepClone();
        f.AfterB["items"]!.AsArray().Add(TradeComparisonFixture.Row(f.BeforeA, 11).DeepClone());
        f.AfterA["inventoryCount"] = 0; f.AfterA["physicalItemCount"] = 4;
        f.AfterB["inventoryCount"] = 2; f.AfterB["physicalItemCount"] = 3;
        foreach (var report in new[] { f.BeforeA, f.BeforeB, f.AfterA, f.AfterB })
        {
            var file = TradeComparisonFixture.File(report, "Spells");
            file["present"] = false; file["length"] = 0; file["contentSha256"] = null; report["spellCount"] = 0;
        }
        Assert.Empty((await CompareTradeAsync(f))["issues"]!.AsArray());
    }

    [Theory]
    [InlineData("duplicate", "duplicateUidAcrossPlayers")]
    [InlineData("lost", "itemMissingOrWrongSavedOwner")]
    [InlineData("wrongType", "transferredOrUnrelatedItemChanged:itemId")]
    [InlineData("wrongStack", "transferredOrUnrelatedItemChanged:stackSize")]
    [InlineData("gold", "expectedCurrencyDeltaNotObserved:gold")]
    [InlineData("cp", "pairCurrencyNotConserved:conquerPoints")]
    [InlineData("unrelated", "transferredOrUnrelatedItemChanged:durability")]
    [InlineData("mail", "unrelatedRowChanged:mails:gold")]
    [InlineData("sidecar", "untouchedSavedImageChanged:Proficiencies")]
    [InlineData("foreign", "sameExplicitSavedIdentityRequired")]
    [InlineData("vital", "savedVitalRequired:maxMana")]
    [InlineData("manaChanged", "characterProjectionChanged:minMana")]
    [InlineData("truncated", "completeSavedProjectionRequired")]
    [InlineData("digest", "completeSidecarDigestRequired:Mailbox")]
    public async Task SavedTradeComparisonRefusesConcreteItemWalletAndEvidenceDrift(string fault, string issue)
    {
        var f = new TradeComparisonFixture();
        switch (fault)
        {
            case "duplicate": f.AfterA["items"]!.AsArray().Add(TradeComparisonFixture.Row(f.AfterB, 11).DeepClone()); f.AfterA["inventoryCount"] = 2; f.AfterA["physicalItemCount"] = 6; break;
            case "lost": f.AfterB["items"]!.AsArray().Remove(TradeComparisonFixture.Row(f.AfterB, 11)); f.AfterB["inventoryCount"] = 0; f.AfterB["physicalItemCount"] = 1; break;
            case "wrongType": TradeComparisonFixture.Row(f.AfterB, 11)["itemId"] = 410004; break;
            case "wrongStack": TradeComparisonFixture.Row(f.AfterA, 21)["stackSize"] = 3; break;
            case "gold": f.AfterA["character"]!["gold"] = 876; break;
            case "cp": f.AfterA["character"]!["conquerPoints"] = 301; break;
            case "unrelated": TradeComparisonFixture.Row(f.AfterA, 13)["durability"] = 99; break;
            case "mail": f.AfterA["mails"]![0]!["gold"] = 8; break;
            case "sidecar": TradeComparisonFixture.File(f.AfterA, "Proficiencies")["contentSha256"] = TradeComparisonFixture.Hash("changed-profs"); break;
            case "foreign": f.AfterA["domain"] = Guid.NewGuid().ToString(); break;
            case "vital": f.AfterA["character"]!.AsObject().Remove("maxMana"); break;
            case "manaChanged": f.AfterA["character"]!["minMana"] = 492; break;
            case "truncated": f.AfterB["summariesTruncated"] = true; break;
            case "digest": TradeComparisonFixture.File(f.AfterA, "Mailbox").Remove("contentSha256"); break;
        }
        var result = await CompareTradeAsync(f);
        Assert.Equal("ClientActionPendingOrStateMismatch", result["state"]!["status"]!.GetValue<string>());
        Assert.Contains(issue, result["issues"]!.AsArray().Select(row => row!.GetValue<string>()));
    }
}
