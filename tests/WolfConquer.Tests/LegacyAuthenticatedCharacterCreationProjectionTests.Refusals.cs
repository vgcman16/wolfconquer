using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedCharacterCreationProjectionTests
{
    [Theory]
    [InlineData("hp")] [InlineData("durability")] [InlineData("zero-durability")] [InlineData("wide-durability")]
    public void MalformedRequiredAdoptedItemFieldsNeverUseTheLegacyFallbackOrClamp(string fault)
    {
        var catalog = Catalog(6000); var fields = Assert.IsType<string[]>(catalog.Items[0].Fields);
        fields[fault == "hp" ? 18 : 21] = fault switch { "zero-durability" => "0", "wide-durability" => "65536", _ => "invalid" };
        Assert.Throws<InvalidDataException>(() => Capture(catalog));
    }

    [Theory]
    [InlineData("item")] [InlineData("profession")] [InlineData("profession-values")] [InlineData("rune")] [InlineData("index")]
    public void ARealCapturedMetadataWitnessRejectsLaterMutableSourceDrift(string fault)
    {
        var catalog = Catalog(6000); var captured = Capture(catalog); Assert.True(captured.IsCurrent());
        switch (fault)
        {
            case "item": Assert.IsType<string[]>(catalog.Items[0].Fields)[18] = "999"; break;
            case "profession": Assert.IsType<string[]>(catalog.ProfessionLevelBenefits[0].Fields)[3] = "60099"; break;
            case "profession-values": Assert.IsType<int[]>(catalog.ProfessionLevelBenefits[0].BonusValues)[0]++; break;
            case "rune": Assert.IsType<string[]>(catalog.RuneStorageAttributes[0].Fields)[3] = "999"; break;
            case "index": Assert.IsType<Dictionary<int, IReadOnlyList<ItemType>>>(catalog.ItemsById).Remove(catalog.Items[0].Id); break;
        }
        Assert.False(captured.IsCurrent());
    }

    [Fact]
    public void ActualPriorInnerPowerAtTheFreshUidRefusesInsteadOfSilentlyLoweringFullCalculatedHp()
    {
        var source = Catalog(6000);
        var prior = RankingSnapshotParser.ParseInnerPowerRankRow("owned-prior-inner-power", 1, "PriorOwner/2399701/0/0/0/0/");
        var catalog = NativeCallerWorldDataFixture.Create(source.Items.ToArray(), [], innerPowerRanks: new[] { prior });
        Assert.Throws<InvalidDataException>(() => Capture(catalog));
    }

    [Fact]
    public void ValidUintItemHpThatOverflowsTheCombinedFreshHpFieldIsRefusedRatherThanWrappedOrInvented()
    {
        var catalog = Catalog(6000);
        Assert.IsType<string[]>(catalog.Items[0].Fields)[18] = uint.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<InvalidDataException>(() => Capture(catalog));
    }

    [Theory]
    [InlineData("unknown")] [InlineData("missing")] [InlineData("inner-power")] [InlineData("profession-count")]
    public void HistoricalMetadataRejectsUnknownMissingOrUnsupportedSchema(string fault)
    {
        var metadata = Capture(Catalog(6000)).CopyMetadata(); var root = JsonNode.Parse(JsonSerializer.SerializeToUtf8Bytes(metadata))!.AsObject();
        switch (fault)
        {
            case "unknown": root["AfterHp"] = 123; break;
            case "missing": root.Remove("Profession"); break;
            case "inner-power": root["InnerPowerRows"]!.AsArray().Add(new JsonArray()); break;
            case "profession-count": root["Profession"]!.AsArray().RemoveAt(0); break;
        }
        using var document = JsonDocument.Parse(root.ToJsonString());
        Assert.Throws<JsonException>(() => LegacyAuthenticatedCharacterCreationMetadata.Read(document.RootElement));
    }

    [Fact]
    public void HistoricalMetadataRejectsDuplicateJsonProperties()
    {
        var text = JsonSerializer.Serialize(Capture(Catalog(6000)).CopyMetadata());
        using var document = JsonDocument.Parse(text[..^1] + ",\"InnerPowerRows\":[]}");
        Assert.Throws<JsonException>(() => LegacyAuthenticatedCharacterCreationMetadata.Read(document.RootElement));
    }
}
