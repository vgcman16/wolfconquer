using System.Buffers.Binary;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneBagTests
{
    [Theory]
    [InlineData("in", "reborn")] [InlineData("in", "level")] [InlineData("in", "overcap")]
    [InlineData("in", "bag-full")] [InlineData("out-full", "")]
    [InlineData("out-rich-full", "")] [InlineData("out-child-receiver-full", "")]
    [InlineData("in", "malformed-cap")] [InlineData("in-fragment", "missing-cap")]
    public Task ActualPreflightRefusalsPreserveAllFilesNativeGraphAndUidNamespace(string mode, string fault) => Run(async f =>
    {
        if (mode.Contains("full", StringComparison.Ordinal)) Assert.Equal(40, f.Current.PlayerItems.InventoryItems.Count);
        if (mode == "out-rich-full") Assert.Equal(BagFixture.Child, Assert.Single(f.Selected().DepositedItems).Uid);
        if (mode == "out-child-receiver-full")
            Assert.Equal(BagFixture.Child, Assert.Single(Assert.Single(f.Current.PlayerItems.InventoryItems,
                row => row.Uid == BagFixture.Receiver).DepositedItems).Uid);
        if (fault == "overcap") Assert.Equal((ushort)2, f.Selected().StackSize);
        await Refuse(f, BagFixture.Request(f.IntoBag));
    }, mode, fault);

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public Task MissingSelectedUidDoesNotCreateIntentQueryReplyOrRankingChange(bool intoBag) => Run(async f =>
    {
        await Refuse(f, BagFixture.Request(intoBag, uint.MaxValue));
    }, intoBag ? "in" : "out");

    [Fact]
    public Task IncompleteAppendedArgumentIsRejectedBeforeAnyMutation() => Run(async f =>
    {
        var bytes = BagFixture.Request(true).Concat(new byte[] { 0xA5 }).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)bytes.Length));
        await Refuse(f, bytes);
    });

    [Theory]
    [InlineData(ItemType.StackSizeFieldIndex)] [InlineData(ItemType.RuneSpellIdFieldIndex)]
    public Task ActualAdoptedRowDriftCannotAuthorizeABagMutation(int field) => Run(async f =>
    {
        var row = f.Catalog.Items.Single(item => item.Id == BagFixture.RuneType); var fields = Assert.IsType<string[]>(row.Fields);
        string before = fields[field]; fields[field] = field == ItemType.StackSizeFieldIndex ? "invalid" : "1416017";
        try { await Refuse(f, BagFixture.Request(true), requireCurrent: false); } finally { fields[field] = before; }
    });

    private static async Task Refuse(BagFixture f, byte[] request, bool requireCurrent = true)
    {
        var files = f.Data.Files(); var current = f.Current; int first = f.Sent.Count;
        var binding = f.Native.OrdinarySpellBinding!; var image = binding.Items.Image;
        var spells = f.Native.MySpells!.ClientSpells; var ranks = f.Rankings.Snapshot();
        if (requireCurrent) Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        await f.SendAsync(request);
        Assert.Equal(first, f.Sent.Count); Assert.Same(current, f.Current); f.Data.AssertFiles(files);
        Assert.Same(image, binding.Items.Image); Assert.Same(spells, f.Native.MySpells.ClientSpells);
        Assert.Equal(ranks, f.Rankings.Snapshot()); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        Assert.False(File.Exists(Path.Combine(f.Data.Registry, "pending.json")));
        if (requireCurrent) Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
    }
}
