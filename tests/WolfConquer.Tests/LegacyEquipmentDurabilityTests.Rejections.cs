using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyEquipmentDurabilityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ActualFrameStaleCharacterOrItemsRefusesBeforeStoreNativeOrSuccessOutput(bool unequip, bool character) => Run(async f =>
    {
        var current = f.Current;
        if (character)
        {
            var original = File.ReadAllBytes(f.CharacterPath);
            var text = Encoding.UTF8.GetString(original);
            var before = "Money=" + current.Character.Money + "\r\n";
            var changed = text.Replace(before, "Money=" + (current.Character.Money + 1) + "\r\n", StringComparison.Ordinal);
            Assert.NotEqual(text, changed);
            File.WriteAllBytes(f.CharacterPath, Encoding.UTF8.GetBytes(changed));
        }
        else
        {
            var saved = f.ReadItems();
            var changed = unequip
                ? saved with { EquipmentItems = saved.EquipmentItems.Select(row => row.Uid == EquipmentFixture.GearUid ? row.WithDurability(320) : row).ToArray() }
                : saved with { InventoryItems = saved.InventoryItems.Select(row => row.Uid == EquipmentFixture.GearUid ? row.WithDurability(320) : row).ToArray() };
            var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(changed);
            bytes[EquipmentFixture.RecordOffset(bytes, EquipmentFixture.GearUid) + 14] = 0xA5;
            File.WriteAllBytes(f.ItemsPath, bytes);
        }
        var files = f.Data.Files();
        var native = CaptureNative(f);
        await f.Dispatch(unequip);
        Assert.Same(current, f.Current);
        Assert.Empty(f.Sent);
        AssertNativeUnchanged(f, native);
        f.Data.AssertFiles(files);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        Assert.Contains("equipmentMove", f.Log.ToString(), StringComparison.Ordinal);
    }, equipped: unequip);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualInvalidatedPinCannotMoveAnOwnedItem(bool unequip) => Run(async f =>
    {
        var current = f.Current;
        LegacySecondaryPasswordRuntime.Invalidate(f.Connection, current);
        Assert.False(LegacySecondaryPasswordRuntime.IsAdmitted(f.Connection, current));
        var files = f.Data.Files(); var native = CaptureNative(f);
        await f.Dispatch(unequip);
        Assert.Same(current, f.Current);
        Assert.Empty(f.Sent);
        AssertNativeUnchanged(f, native);
        f.Data.AssertFiles(files);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }, equipped: unequip);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualRetiredStoreIssuanceCannotFallBackToAUidMatchedItemOwner(bool unequip) => Run(async f =>
    {
        Assert.True(f.Store.TryRemove(f.Connection, out _));
        Assert.Null(f.CurrentOrNull);
        var files = f.Data.Files(); var native = CaptureNative(f);
        await f.Dispatch(unequip);
        Assert.Null(f.CurrentOrNull);
        Assert.DoesNotContain(f.Sent, bytes => Frame(bytes).Header.Opcode is MsgItemUsagePacket.Opcode or LegacyGameItemPacket.Opcode);
        AssertNativeUnchanged(f, native);
        f.Data.AssertFiles(files);
    }, equipped: unequip);

    [Fact]
    public Task FullFortySlotInventoryRejectsUnequipWithoutAnyPartialMove() => Run(async f =>
    {
        Assert.Equal(40, f.Current.PlayerItems.InventoryItems.Count);
        var current = f.Current; var files = f.Data.Files(); var native = CaptureNative(f);
        await f.Dispatch(unequip: true);
        Assert.Same(current, f.Current); Assert.Empty(f.Sent);
        AssertNativeUnchanged(f, native); f.Data.AssertFiles(files);
        Assert.Contains("equipmentMoveInventoryFull", f.Log.ToString(), StringComparison.Ordinal);
    }, equipped: true, inventoryFillers: 40);

    [Fact]
    public Task TwoHandReplacementCountsBothDisplacedRowsBeforeRejectingFortyOneSlots() => Run(async f =>
    {
        Assert.Equal(40, f.Current.PlayerItems.InventoryItems.Count);
        Assert.Equal(2, f.Current.PlayerItems.EquipmentItems.Count);
        var current = f.Current; var files = f.Data.Files(); var native = CaptureNative(f);
        await f.Dispatch(position: 4);
        Assert.Same(current, f.Current); Assert.Empty(f.Sent);
        AssertNativeUnchanged(f, native); f.Data.AssertFiles(files);
        Assert.Contains("equipmentMoveInventoryFull", f.Log.ToString(), StringComparison.Ordinal);
    }, inventoryFillers: 39, twoHandReplacement: true);
}
