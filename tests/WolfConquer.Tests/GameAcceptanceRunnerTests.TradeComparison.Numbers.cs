namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceRunnerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TradePlanAndIntentRefuseExponentTokensEvenWhenTheirValuesAppearIntegral(bool intent)
    {
        var f = new TradeComparisonFixture();
        var source = intent ? f.Intent.ToJsonString() : f.Plan().ToJsonString();
        var changed = source.Replace("\"goldDelta\":-125", "\"goldDelta\":-125e0", StringComparison.Ordinal);
        Assert.NotEqual(source, changed);
        var result = intent ? await CompareTradeAsync(f, intentText: changed, planText: f.Plan().ToJsonString())
            : await CompareTradeAsync(f, planText: changed);
        Assert.NotNull(result["readerError"]); Assert.Null(result["state"]);
    }

    [Fact]
    public async Task ExactIntegerWalletsAboveDoublePrecisionRemainAccepted()
    {
        var f = new TradeComparisonFixture();
        f.BeforeA["character"]!["gold"] = 9007199254740993L;
        f.AfterA["character"]!["gold"] = 9007199254740868L;
        Assert.Empty((await CompareTradeAsync(f))["issues"]!.AsArray());
    }

    [Fact]
    public async Task RoundedLargeExponentCannotMasqueradeAsTheExpectedSavedWallet()
    {
        var f = new TradeComparisonFixture();
        f.BeforeA["character"]!["gold"] = 9007199254741117L; // Expected exact after is2^53.
        f.AfterA["character"]!["gold"] = "__trade_report_number__";
        var result = await CompareTradeAsync(f, rawReportNumber: "9007199254740993e0");
        Assert.Contains("integerJsonRequired:character.gold", result["issues"]!.AsArray().Select(row => row!.GetValue<string>()));
        Assert.Equal("ClientActionPendingOrStateMismatch", result["state"]!["status"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("schemaVersion", "1e0", "integerJsonRequired:schemaVersion")]
    [InlineData("accountId", "2749601e0", "integerJsonRequired:accountId")]
    [InlineData("uid", "2749601e0", "integerJsonRequired:character.uid")]
    [InlineData("count", "5e0", "integerJsonRequired:physicalItemCount")]
    [InlineData("vital", "493e0", "integerJsonRequired:character.minMana")]
    [InlineData("itemUid", "21e0", "integerJsonRequired:item.uid")]
    [InlineData("stack", "4e0", "integerJsonRequired:item.stackSize")]
    [InlineData("socket", "0e0", "integerJsonRequired:item.socketOne")]
    [InlineData("mail", "7e0", "integerJsonRequired:mail.gold")]
    [InlineData("length", "100e0", "integerJsonRequired:file.length")]
    public async Task NewTradeReportFieldsRequireIntegerJsonRepresentations(string field, string literal, string issue)
    {
        var f = new TradeComparisonFixture(); const string marker = "__trade_report_number__";
        switch (field)
        {
            case "schemaVersion": f.AfterA["schemaVersion"] = marker; break;
            case "accountId": f.AfterA["accountId"] = marker; break;
            case "uid": f.AfterA["character"]!["uid"] = marker; break;
            case "count": f.AfterA["physicalItemCount"] = marker; break;
            case "vital": f.AfterA["character"]!["minMana"] = marker; break;
            case "itemUid": TradeComparisonFixture.Row(f.AfterA, 21)["uid"] = marker; break;
            case "stack": TradeComparisonFixture.Row(f.AfterA, 21)["stackSize"] = marker; break;
            case "socket": TradeComparisonFixture.Row(f.AfterA, 21)["socketOne"] = marker; break;
            case "mail": f.AfterA["mails"]![0]!["gold"] = marker; break;
            case "length": TradeComparisonFixture.File(f.AfterA, "Items")["length"] = marker; break;
        }
        var result = await CompareTradeAsync(f, rawReportNumber: literal);
        Assert.Contains(issue, result["issues"]!.AsArray().Select(row => row!.GetValue<string>()));
        Assert.Equal("ClientActionPendingOrStateMismatch", result["state"]!["status"]!.GetValue<string>());
    }
}
