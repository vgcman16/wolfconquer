using System.Buffers;
using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicDecomposePreparationTests
{
    private const uint First = 7, Second = 8, Kept = 90, Crystal = 3_306_885;

    private static PlayerItemSnapshotItem Item(uint uid, uint itemId = 4_100_001, byte bound = 0) => new()
    {
        Uid = uid, ItemId = itemId, Bound = bound, StackSize = 1,
        Durability = 10_000, MaximumDurability = 10_000,
    };

    private static PlayerItemSnapshot Owner() => PlayerItemSnapshot.Empty with
    { InventoryItems = new[] { Item(First), Item(Second, 4_100_002, bound: 1), Item(Kept, 1_000_000) } };

    private static ItemType Type(uint id, string durability = "17")
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "DecomposeFixture";
        fields[21] = durability; fields[46] = "1";
        return new(checked((int)id), fields[1], fields);
    }

    private static ItemType[] Types() =>
        [Type(4_100_001), Type(4_100_002), Type(4_100_003), Type(4_100_004), Type(4_100_005), Type(Crystal)];
    private static WorldDataCatalog World() => LegacyItemWorldDataFixture.Create(Types());

    private static LegacyRelicDecomposeRequest Request(params uint[] ids)
    {
        var writer = new PacketWriter(2137, checked(87 + ids.Length * 4));
        writer.WriteUInt16(12, 67); writer.WriteUInt32(18, checked((uint)ids.Length));
        for (var index = 0; index < ids.Length; index++) writer.WriteUInt32(87 + index * 4, ids[index]);
        var sequence = new ReadOnlySequence<byte>(writer.ToArray());
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        Assert.True(MsgItemUsagePacket.TryParse(frame, out var packet));
        Assert.True(LegacyRelicDecomposeRequest.TryParse(packet, frame, out var request, out var reason), reason);
        return Assert.IsType<LegacyRelicDecomposeRequest>(request);
    }

    private static LegacyRelicDecomposePreparation Prepare(PlayerItemSnapshot owner,
        LegacyRelicDecomposeRequest request, WorldDataCatalog? world = null)
    {
        Assert.True(LegacyRelicDecomposePreparation.TryPrepare(world ?? World(), owner, request,
            out var prepared, out var reason), reason);
        return Assert.IsType<LegacyRelicDecomposePreparation>(prepared);
    }

    [Fact]
    public void ConstructorOwnsOnlyRequiredRelicAndRewardScalarsWithoutComposeOrEchoPrerequisites()
    {
        var rows = Types(); var world = LegacyItemWorldDataFixture.Create(rows);
        Assert.Empty(world.XuanbaoComposeAttributeLimits); Assert.Empty(world.XuanbaoRandomAttributes);
        var owner = Owner(); var prepared = Prepare(owner, Request(Second, First), world);
        ((string[])rows[^1].Fields)[21] = "65536";
        ((string[])rows[0].Fields)[0] = "1";
        rows[1] = Type(1);
        Assert.True(prepared.IsCurrent(owner));
        Assert.Equal((ushort)17, prepared.Recipe.RewardDurability);
        Assert.Equal(new uint[] { Second, First }, prepared.Recipe.Selections.ToArray().Select(row => row.Uid));
        Assert.Equal(new byte[] { 1, 0 }, prepared.Recipe.Selections.ToArray().Select(row => row.Bound));
        Assert.Equal((ushort)17, Prepare(owner, Request(First), world).Recipe.RewardDurability);
    }

    [Theory]
    [InlineData("missing-reward")] [InlineData("bad-durability")] [InlineData("missing-selected-relic")]
    public void MissingRequiredMetadataNeverCreatesAnAcceptingPreparation(string defect)
    {
        var rows = Types().ToList();
        if (defect == "missing-reward") rows.RemoveAll(row => row.Id == Crystal);
        else if (defect == "missing-selected-relic") rows.RemoveAll(row => row.Id == 4_100_001);
        else rows[^1] = Type(Crystal, "65536");
        Refuses(Owner(), Request(First), LegacyItemWorldDataFixture.Create(rows.ToArray()));
    }

    private static void Refuses(PlayerItemSnapshot owner, LegacyRelicDecomposeRequest request, WorldDataCatalog? world = null)
    {
        // JSON observes the complete object graph, including deliberately
        // unpersistable tails which the legacy serializer would truncate/refuse.
        var image = JsonSerializer.Serialize(owner);
        var rows = owner.InventoryItems.ToArray();
        Assert.False(LegacyRelicDecomposePreparation.TryPrepare(world ?? World(), owner, request, out var result, out var reason));
        Assert.Null(result); Assert.NotEmpty(reason);
        Assert.Equal(image, JsonSerializer.Serialize(owner));
        Assert.Equal(rows.Length, owner.InventoryItems.Count);
        for (var index = 0; index < rows.Length; index++) Assert.Same(rows[index], owner.InventoryItems[index]);
    }
}
