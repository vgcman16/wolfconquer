using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDragonSkinTransactionTests
{
    public static IEnumerable<object[]> UnlockRows()
    {
        foreach (var skin in new uint[] { 1, 2, 3 })
            foreach (var stack in new ushort[] { 1, 2 }) yield return [skin, stack];
    }

    [Theory]
    [MemberData(nameof(UnlockRows))]
    public Task RegisteredUnlockCommitsBothFilesBeforeConsumeAndAcknowledgement(uint skin, ushort stack) => RunAsync(async f =>
    {
        f.Seed("0/1/77/", [Item(9, 720_001), Item(10, Mapped(skin), stack)]);
        var before = Capture(f.Original);
        var cost = f.Original.PlayerItems.InventoryItems[1];
        await f.InvokeAsync(LegacyDragonSkinType.Unlock, skin);
        AssertCommitted(f, before, $"0/2/77/{skin}/");
        Assert.Same(f.Original.PlayerItems.InventoryItems[0], f.Current.PlayerItems.InventoryItems[0]);
        var consume = stack == 1
            ? new MsgItemUsagePacket(10, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes()
            : ItemGolden(10, Mapped(skin), 3, (ushort)(stack - 1));
        AssertWireAndLogOrder(f, LegacyDragonSkinType.Unlock, skin, consume);
        if (stack == 1) Assert.DoesNotContain(f.Current.PlayerItems.InventoryItems, item => item.Uid == cost.Uid);
        else
        {
            var remaining = Assert.Single(f.Current.PlayerItems.InventoryItems, item => item.Uid == cost.Uid);
            Assert.Equal((byte)0, remaining.Bound);
            Assert.Equal((ushort)1, remaining.StackSize);
            Assert.NotSame(cost, remaining);
        }
    });

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public Task RegisteredRetrievePreservesMappingsUnknownOwnedIdsEncodingAndFullWire(uint skin, bool utf8Bom) => RunAsync(async f =>
    {
        f.Seed($"{skin}/2/77/{skin}/", utf8Bom: utf8Bom);
        f.Catalog = LegacyItemWorldDataFixture.Create(Metadata(Mapped(skin)));
        var before = Capture(f.Original);
        await f.InvokeAsync(LegacyDragonSkinType.Retrieve, skin);
        AssertCommitted(f, before, "0/1/77/");
        var added = Assert.Single(f.Current.PlayerItems.InventoryItems, item => item.ItemId == Mapped(skin));
        Assert.Equal(10U, added.Uid);
        Assert.Equal((byte)0, added.Bound);
        Assert.Equal((ushort)1, added.StackSize);
        Assert.Same(f.Original.PlayerItems.InventoryItems[0], f.Current.PlayerItems.InventoryItems[0]);
        AssertWireAndLogOrder(f, LegacyDragonSkinType.Retrieve, skin, ItemGolden(10, Mapped(skin), 1, 1, 23));
    });

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public Task RegisteredRetrieveOptionalAbsentMetadataKeepsDurabilityZero(uint skin, bool emptyCatalog) => RunAsync(async f =>
    {
        f.Seed($"0/1/{skin}/");
        if (emptyCatalog) f.Catalog = LegacyItemWorldDataFixture.Create();
        var before = Capture(f.Original);
        await f.InvokeAsync(LegacyDragonSkinType.Retrieve, skin);
        AssertCommitted(f, before, "0/0/");
        AssertWireAndLogOrder(f, LegacyDragonSkinType.Retrieve, skin, ItemGolden(10, Mapped(skin), 1, 1));
    });

    [Fact]
    public Task UnlockSkipsBoundAndZeroStackRowsThenDebitsFirstEligibleUnboundPositiveRow() => RunAsync(async f =>
    {
        f.Seed("0/0/", [Item(9, 720_001), Item(10, Mapped(1), 0), Item(11, Mapped(1), 2, 1),
            Item(12, Mapped(1), 3), Item(13, Mapped(1), 4)]);
        var before = Capture(f.Original);
        await f.InvokeAsync(LegacyDragonSkinType.Unlock);
        AssertCommitted(f, before, "0/1/1/");
        Assert.Same(f.Original.PlayerItems.InventoryItems[1], f.Current.PlayerItems.InventoryItems[1]);
        Assert.NotSame(f.Original.PlayerItems.InventoryItems[3], f.Current.PlayerItems.InventoryItems[3]);
        Assert.Equal((ushort)0, f.Current.PlayerItems.InventoryItems[1].StackSize);
        Assert.Same(f.Original.PlayerItems.InventoryItems[2], f.Current.PlayerItems.InventoryItems[2]);
        Assert.Same(f.Original.PlayerItems.InventoryItems[4], f.Current.PlayerItems.InventoryItems[4]);
        Assert.Equal((ushort)2, f.Current.PlayerItems.InventoryItems[2].StackSize);
        Assert.Equal((ushort)2, f.Current.PlayerItems.InventoryItems[3].StackSize);
        AssertWireAndLogOrder(f, LegacyDragonSkinType.Unlock, 1, ItemGolden(12, Mapped(1), 3, 2));
    });

    [Theory]
    [InlineData("zero-cost", 2U)]
    [InlineData("not-owned", 3U)]
    [InlineData("unknown", 3U)]
    [InlineData("full", 3U)]
    public Task OrdinaryNoGrantCasesRetainTheirFalseAcknowledgementWithoutMutation(string kind, uint type) => RunAsync(async f =>
    {
        var state = kind == "unknown" ? "0/1/77/" : kind == "not-owned" || kind == "zero-cost" ? "0/0/" : "1/1/1/";
        var rows = kind == "full" ? Enumerable.Range(1, 40).Select(value => Item((uint)value, 720_001)).ToArray()
            : new[] { Item(9, kind == "zero-cost" ? Mapped(1) : 720_001, kind == "zero-cost" ? (ushort)0 : (ushort)1) };
        f.Seed(state, rows);
        var before = Capture(f.Original);
        var skin = kind == "unknown" ? 77U : 1U;
        await f.InvokeAsync((LegacyDragonSkinType)type, skin);
        Assert.Same(before.Session, f.Current);
        AssertModelUnchanged(before); AssertFilesUnchanged(f);
        Assert.Equal(SkinGolden((LegacyDragonSkinType)type, skin, false), Assert.Single(f.Sent));
        Assert.DoesNotContain("WorldServer persisted LegacyDragonSkin", f.Log.ToString());
    });

    [Fact]
    public Task AlreadyOwnedUnlockRemainsAQuietNoOpWithoutDebitingAnotherItem() => RunAsync(async f =>
    {
        f.Seed("1/1/1/", [Item(10, Mapped(1))]);
        var before = Capture(f.Original);
        await f.InvokeAsync(LegacyDragonSkinType.Unlock);
        Assert.Same(before.Session, f.Current); AssertModelUnchanged(before); AssertFilesUnchanged(f);
        Assert.Empty(f.Sent); Assert.Contains("skin is already owned", f.Log.ToString());
    });

    [Fact]
    public Task SelectThenRetrieveAcceptsLegitimateEquippedOnlyDiskDivergence() => RunAsync(async f =>
    {
        f.Seed("0/1/1/");
        await f.InvokeAsync(LegacyDragonSkinType.Select);
        Assert.Equal("1/1/1/", f.Current.Character.Attributes["AnimaSkin"]);
        Assert.Equal("0/1/1/", f.ReloadCharacter().Attributes["AnimaSkin"]);
        f.Sent.Clear(); f.Trace.Clear();
        var selected = Capture(f.Current);
        await f.InvokeAsync(LegacyDragonSkinType.Retrieve);
        Assert.Equal("0/0/", f.Current.Character.Attributes["AnimaSkin"]);
        Assert.Equal("0/0/", f.ReloadCharacter().Attributes["AnimaSkin"]);
        AssertModelUnchanged(selected);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems), File.ReadAllBytes(f.ItemPath));
        AssertWireAndLogOrder(f, LegacyDragonSkinType.Retrieve, 1, ItemGolden(10, Mapped(1), 1, 1));
    });
}
