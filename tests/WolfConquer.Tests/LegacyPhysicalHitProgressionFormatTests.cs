using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Closed operation DATA only; these bytes are not a journal or an owner.
public sealed class LegacyPhysicalHitProgressionFormatTests
{
    private static byte[] Items() => PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([], [], [],
        [new PlayerItemSnapshotItem { Uid = 11, ItemId = 410009, Position = 4, Durability = 100, MaximumDurability = 100, StackSize = 1 }], []));
    private static LegacyPhysicalHitProgressionOperation Operation(bool death = false)
    {
        var targetItems = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty);
        var source = death ? new LegacyPhysicalDeathOperation(202, true, true, targetItems,
            [new LegacyPhysicalDeathRankInput(202, ["202", "RankData", "42", "1"])], [], []) : null;
        return new(9001, 101, 9002, 202, 123, 100, true, Items(), source);
    }
    private static byte[] Write(bool death = false) => LegacyTradeJournal.SerializePhysicalHitProgressionData(Operation(death));
    private static LegacyPhysicalHitProgressionOperation Read(string value) =>
        LegacyTradeJournal.ParsePhysicalHitProgressionData(Encoding.UTF8.GetBytes(value));
    private static string Text(bool death = false) => Encoding.UTF8.GetString(Write(death));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ClosedDataCodecRoundTripsRecordedInputsAndOwnsItsCopies(bool death)
    {
        byte[] bytes = Write(death); var original = bytes.ToArray();
        var actual = LegacyTradeJournal.ParsePhysicalHitProgressionData(bytes);
        Assert.Equal((9001L, 101U, 9002L, 202U, 123U, 100U, true),
            (actual.ActorAccountId, actual.ActorUid, actual.TargetAccountId, actual.TargetUid, actual.RawExperience, actual.AppliedDamage, actual.ActorAlternate));
        Assert.Equal(Items(), actual.ActorItems); Assert.Equal(death, actual.Death is not null);
        if (death)
        {
            Assert.True(actual.Death!.QueryAdmitted); Assert.True(actual.Death.Alternate);
            Assert.Equal(new[] { "202", "RankData", "42", "1" }, Assert.Single(actual.Death.RankingRows).Fields);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty), actual.Death.ItemsBefore);
        }
        Assert.Equal(original, bytes); Assert.Equal(bytes, LegacyTradeJournal.SerializePhysicalHitProgressionData(actual));
        var leaked = actual.ActorItems; leaked[0] ^= 1; Assert.Equal(Items(), actual.ActorItems);
        if (death) { var target = actual.Death!.ItemsBefore; target[0] ^= 1; Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty), actual.Death.ItemsBefore); }
    }

    [Fact]
    public void ExistingDeathGrantRowsAndAllDerivedSpellFieldsRoundTrip()
    {
        const string row = "21930@@21930@@6@@Dark@@0@@0@@0@@0@@1@@0@@1@@0@@200@@1@@0@@16@@417@@0@@0@@0@@0@@0@@0@@0@@48@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@416@@1@@50@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@";
        var magic = MagicTypeParser.ParseLine(row);
        var death = new LegacyPhysicalDeathOperation(202, true, false,
            PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty),
            [new LegacyPhysicalDeathRankInput(202, ["202", "RankData", "42", "1"])],
            [new WorldPacketHandlers.OwnedPhysicalDeathDerivedSpellChange(21930, new(21930, 1, 0, 0, 0, 0))], [magic]);
        var original = new LegacyPhysicalHitProgressionOperation(9001, 101, 9002, 202, 123, 100, false, Items(), death);
        var bytes = LegacyTradeJournal.SerializePhysicalHitProgressionData(original);
        var decoded = LegacyTradeJournal.ParsePhysicalHitProgressionData(bytes);
        var change = Assert.Single(decoded.Death!.DerivedSpells);
        Assert.Equal((ushort)21930, change.Id);
        Assert.Equal(new PlayerSpellSnapshotEntry(21930, 1, 0, 0, 0, 0), change.After);
        var grant = Assert.Single(decoded.Death.GrantedRows);
        Assert.Equal((magic.Id, magic.TypeId, magic.Name), (grant.Id, grant.TypeId, grant.Name));
        Assert.Equal(magic.Fields, grant.Fields);
        Assert.Equal(bytes, LegacyTradeJournal.SerializePhysicalHitProgressionData(decoded));
    }

    [Theory]
    [InlineData("ActorAccountId")] [InlineData("ActorUid")] [InlineData("TargetAccountId")]
    [InlineData("TargetUid")] [InlineData("RawExperience")] [InlineData("AppliedDamage")]
    public void NumbersRequireExactUnsignedIntegerKind(string name)
    {
        var root = JsonNode.Parse(Write())!.AsObject();
        foreach (string bad in new[] { "1.0", "1e0", "\"1\"", "-1", "4294967296" })
        {
            string json = root.ToJsonString();
            string token = "\"" + name + "\":" + root[name]!.ToJsonString();
            Assert.Contains(token, json, StringComparison.Ordinal);
            Assert.Throws<JsonException>(() => Read(json.Replace(token, "\"" + name + "\":" + bad, StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void ExactTopLevelPropertiesAndFieldKindsAreRequired()
    {
        string json = Text();
        Assert.Throws<JsonException>(() => Read(json.Insert(1, "\"ActorUid\":101,")));
        Assert.Throws<JsonException>(() => Read(json.Insert(1, "\"Accepted\":true,")));
        Assert.Throws<JsonException>(() => Read(json.Replace("\"ActorUid\"", "\"actorUid\"", StringComparison.Ordinal)));
        var root = JsonNode.Parse(json)!.AsObject(); Assert.True(root.Remove("ActorUid"));
        Assert.Throws<JsonException>(() => Read(root.ToJsonString()));
        Assert.Throws<JsonException>(() => Read(json.Replace("\"ActorAlternate\":true", "\"ActorAlternate\":1", StringComparison.Ordinal)));
        Assert.Throws<JsonException>(() => Read(json.Replace("\"Death\":null", "\"Death\":false", StringComparison.Ordinal)));
        root = JsonNode.Parse(json)!.AsObject(); root["ActorItems"] = "not base64!";
        Assert.Throws<JsonException>(() => Read(root.ToJsonString()));
    }

    [Fact]
    public void NestedDeathRequiresExactExisting22Schema()
    {
        string json = Text(death: true);
        Assert.Throws<JsonException>(() => Read(json.Replace("\"QueryAdmitted\":true", "\"QueryAdmitted\":\"true\"", StringComparison.Ordinal)));
        Assert.Throws<JsonException>(() => Read(json.Replace("\"RankingRows\":[", "\"Unknown\":1,\"RankingRows\":[", StringComparison.Ordinal)));
        Assert.Throws<JsonException>(() => Read(json.Replace("\"TargetUid\":202,\"QueryAdmitted\"", "\"TargetUid\":202,\"TargetUid\":202,\"QueryAdmitted\"", StringComparison.Ordinal)));
        Assert.Throws<JsonException>(() => Read(json.Replace("\"CharacterId\":202", "\"CharacterId\":\"202\"", StringComparison.Ordinal)));
        var root = JsonNode.Parse(json)!.AsObject(); Assert.True(root["Death"]!.AsObject().Remove("ItemsBefore"));
        Assert.Throws<JsonException>(() => Read(root.ToJsonString()));
        root = JsonNode.Parse(json)!.AsObject();
        root["Death"]!["RankingRows"]![0]!["Fields"]![1] = new string('x', 4097);
        Assert.Throws<JsonException>(() => Read(root.ToJsonString()));
    }
}
