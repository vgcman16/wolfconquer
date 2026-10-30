using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeItemSpellCallerFixture;
using Item = WolfConquer.Core.Data.PlayerItemSnapshotItem;

namespace WolfConquer.Tests;

public sealed partial class OwnedItemRepairTests
{
    [Theory]
    [InlineData("funds")]
    [InlineData("materials")]
    [InlineData("material-target")]
    [InlineData("material-children")]
    [InlineData("raw-tail")]
    [InlineData("missing-items")]
    [InlineData("saved-money")]
    [InlineData("native-drift")]
    [InlineData("foreign-player")]
    public async Task ActualRepairRefusesWithoutPublishingOrUndoingExternallyChangedInputs(string fault)
    {
        bool meteor = fault is "materials" or "material-target" or "material-children";
        var initial = Saved(meteor);
        if (fault == "materials") initial = initial with
            { InventoryItems = new[] { Target(true), Material(0, count: 4) } };
        if (fault == "material-target") initial = initial with
        {
            InventoryItems = new[] { new Item { Uid = Fixture.ItemUid, ItemId = MeteorId,
                Durability = 0, MaximumDurability = 10, StackSize = 5 } },
        };
        if (fault == "material-children")
        {
            var material = Material(0, count: 5, children: new[] { new Item { Uid = Fixture.ItemUid + 99,
                ItemId = TargetId, Durability = 1, MaximumDurability = 10, StackSize = 1 } });
            initial = initial with { InventoryItems = new[] { Target(true), material } };
        }
        await WithRepair(meteor, false, async f =>
        {
            await f.Login(); var before = f.Current; var binding = AssertGraph(f); var image = binding.Items.Image;
            var nativeTarget = f.Native.Inventory!.ClientItems[Fixture.ItemUid];
            if (fault == "raw-tail") File.WriteAllBytes(f.ItemsPath, File.ReadAllBytes(f.ItemsPath).Append((byte)0xA5).ToArray());
            if (fault == "missing-items") File.Move(f.ItemsPath, f.ItemsPath + ".owned-held");
            if (fault == "saved-money")
            {
                byte[] raw = File.ReadAllBytes(f.CharacterPath);
                string text = CharacterSnapshotFileEncoding.Decode(raw, out var encoding);
                Assert.Contains("Money=1000", text, StringComparison.Ordinal);
                File.WriteAllBytes(f.CharacterPath, encoding.GetPreamble().Concat(encoding.GetBytes(
                    text.Replace("Money=1000", "Money=999", StringComparison.Ordinal))).ToArray());
            }
            if (fault == "native-drift") nativeTarget.Plus = 7;
            if (fault == "foreign-player") f.Native.Player = new LegacyNativePlayerRuntime(f.Native);
            var files = f.Files(); int first = f.Sent.Count;
            try
            {
                await f.Send(Request());

                Assert.Same(before, f.Current); Assert.Same(image, binding.Items.Image);
                Assert.Same(nativeTarget, f.Native.Inventory.ClientItems[Fixture.ItemUid]);
                Assert.Equal(meteor ? (ushort)0 : (ushort)5, nativeTarget.Durability);
                Assert.Equal(fault == "native-drift" ? (byte)7 : (byte)0, nativeTarget.Plus);
                Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
                Assert.False(LegacyTradeJournal.HasPending(f.Users));
            }
            finally
            {
                if (fault == "missing-items") File.Move(f.ItemsPath + ".owned-held", f.ItemsPath);
            }
        }, saved: initial, goldWorth: fault == "funds" ? 1_000_000u : 100u);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("-1")]
    [InlineData("4294967296")]
    public async Task PaidRepairRequiresStrictActualGoldWorthBeforeCharging(string invalid)
    {
        await WithRepair(false, false, async f =>
        {
            await f.Login(); var before = f.Current; var binding = AssertGraph(f); var image = binding.Items.Image;
            var row = Assert.Single(f.Catalog.Items, item => item.Id == TargetId);
            Assert.IsType<string[]>(row.Fields)[ItemType.GoldWorthFieldIndex] = invalid;
            var files = f.Files(); int first = f.Sent.Count;

            await f.Send(Request());

            Assert.Same(before, f.Current); Assert.Same(image, binding.Items.Image);
            Assert.Equal(1000, f.Current.Character.Money); Assert.Equal((ushort)5, RepairedTarget(f.Current).Durability);
            Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
        });
    }

    [Fact]
    public async Task RetiredActualHolderCannotRepairOrReviveItsNativeGraph()
    {
        await WithRepair(false, false, async f =>
        {
            await f.Login(); var before = f.Current; var binding = AssertGraph(f);
            Assert.True(f.Store.TryRemove(f.Connection, out var removed)); Assert.Same(before, removed);
            Assert.True(binding.IsRetired); var files = f.Files(); int first = f.Sent.Count;

            await f.Send(Request());

            Assert.False(f.Store.TryGet(f.Connection, out _)); Assert.True(binding.IsRetired);
            Assert.False(binding.Items.PhysicalOwnerIsCurrentPlain());
            Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
        });
    }
}
