using System.Reflection;
using ProtoBuf;
using WolfConquer.Core.World;
using ItemQuery = WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaItem;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Fact]
    public void ItemContractRetainsRequiredMutableNativeFieldsNullArrayAndFlagsEnum()
    {
        Assert.NotNull(typeof(ItemQuery).GetCustomAttribute<ProtoContractAttribute>());
        Assert.NotNull(typeof(ItemQuery.Item).GetCustomAttribute<ProtoContractAttribute>());
        Assert.NotNull(typeof(ItemQuery.TypeID).GetCustomAttribute<FlagsAttribute>());
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(ItemQuery.TypeID)));
        Assert.Equal(new[] { "Load", "View", "Add", "Update" }, Enum.GetNames<ItemQuery.TypeID>());
        Assert.Equal(new[] { 0, 1, 2, 3 }, Enum.GetValues<ItemQuery.TypeID>().Select(value => (int)value));
        var fields = typeof(ItemQuery).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "Type", "UID", "Items" }, fields.Select(field => field.Name));
        Assert.Equal(new[] { typeof(ItemQuery.TypeID), typeof(uint), typeof(ItemQuery.Item[]) }, fields.Select(field => field.FieldType));
        Assert.Equal(new[] { 1, 2, 3 }, fields.Select(field => field.GetCustomAttribute<ProtoMemberAttribute>()!.Tag));
        Assert.All(fields, field => { Assert.False(field.IsInitOnly); Assert.True(field.GetCustomAttribute<ProtoMemberAttribute>()!.IsRequired); });
        var value = new ItemQuery();
        Assert.Equal(ItemQuery.TypeID.Load, value.Type); Assert.Equal(0u, value.UID); Assert.Null(value.Items);
        var nested = typeof(ItemQuery.Item).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "ID", "Level", "Position", "Points", "UK" }, nested.Select(field => field.Name));
        Assert.Equal(Enumerable.Range(1, 5), nested.Select(field => field.GetCustomAttribute<ProtoMemberAttribute>()!.Tag));
        var item = new ItemQuery.Item();
        Assert.All(nested, field =>
        {
            Assert.Equal(typeof(uint), field.FieldType); Assert.False(field.IsInitOnly);
            Assert.True(field.GetCustomAttribute<ProtoMemberAttribute>()!.IsRequired);
            Assert.Equal(0u, field.GetValue(item));
        });
        Assert.Equal((ushort)2393, LegacyNativeMsgNinja.MsgNinjaItem_ID);
        Assert.DoesNotContain(typeof(ItemQuery).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
            method => method.Name == "Process");
    }

    [Fact]
    public Task BulkItemProducerUsesSuppliedUidPhysicalValuesAndAliasesIncludingUkWithoutMutatingOwners() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Client.Player.UID = 777;
            var item = scope.Item(20, 123, 2, 3, 4, 5);
            Assert.True(scope.Ninja.Items.TryAdd(30, item));
            scope.Ninja.user = null!;
            var unrelated = new LegacyNativeNinja(); scope.Client.MyNinja = unrelated;
            ItemQuery.Create(scope.Client, scope.Ninja, ItemQuery.TypeID.View, 99);
            byte[] first = ItemFrame(ItemQuery.TypeID.View, 99, (123, 2, 3, 4, 5), (123, 2, 3, 4, 5));
            scope.Serialized(0, first);
            item.UK = 66; item.Exp = 77;
            ItemQuery.Create(scope.Client, scope.Ninja, ItemQuery.TypeID.Update, 100);
            byte[] second = ItemFrame(ItemQuery.TypeID.Update, 100, (123, 2, 3, 77, 66), (123, 2, 3, 77, 66));
            scope.Sent(first, second); scope.Serialized(1, second);
            Assert.Same(item, scope.Ninja.Items[20]); Assert.Same(item, scope.Ninja.Items[30]);
            Assert.Same(unrelated, scope.Client.MyNinja); Assert.Null(scope.Ninja.user);
            Assert.Equal(777u, scope.Client.Player.UID);
            Assert.Equal(new uint[] { 123, 2, 3, 77, 66 }, new[] { item.ItemID, item.Level, item.Position, item.Exp, item.UK });
            Assert.Equal(10, scope.Available); Assert.Empty(scope.Metadata);
        });

    [Fact]
    public Task SingleItemProducerUsesRecipientUidKeepsFullWidthsAndIntentionallyLeavesUkZero() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Client.Player.UID = uint.MaxValue;
            var item = scope.Item(1, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, 123);
            ItemQuery.Create(scope.Client, item, ItemQuery.TypeID.Update);
            byte[] expected = ItemFrame(ItemQuery.TypeID.Update, uint.MaxValue,
                (uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, 0));
            scope.Sent(expected); scope.Serialized(0, expected);
            Assert.Equal(123u, item.UK); Assert.Equal(uint.MaxValue, item.Exp);
            Assert.Same(item, Assert.Single(scope.Ninja.Items).Value); Assert.Equal(11, scope.Available);
        });

    [Fact]
    public Task EmptyBulkProducerStillSendsItsRequiredTypeAndSuppliedUid() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            ItemQuery.Create(scope.Client, scope.Ninja, ItemQuery.TypeID.Load, 0);
            byte[] expected = ItemFrame(ItemQuery.TypeID.Load, 0);
            scope.Sent(expected); scope.Serialized(0, expected);
            Assert.Empty(scope.Ninja.Items); Assert.Equal(11, scope.Available);
        });

    [Theory]
    [InlineData("owner")]
    [InlineData("items")]
    [InlineData("row")]
    public Task BulkProducerSourceFaultsAfterTakeAndBeforeEncodingEvenForFakeRecipient(string missing) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Client.Fake = true;
            if (missing == "items") scope.Ninja.Items = null!;
            if (missing == "row") Assert.True(scope.Ninja.Items.TryAdd(1, null!));
            Assert.Throws<NullReferenceException>(() => ItemQuery.Create(scope.Client,
                missing == "owner" ? null! : scope.Ninja, ItemQuery.TypeID.Load, 17));
            Assert.Empty(scope.Queue); scope.UnserializedDisposed(0); Assert.Equal(11, scope.Available);
        });

    [Fact]
    public Task BulkNullRecipientFaultsOnlyAfterCopyingAndSerializingTheWholeItemArray() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Item(1, 10, 20, 30, 40, 50);
            Assert.Throws<NullReferenceException>(() => ItemQuery.Create(null!, scope.Ninja, ItemQuery.TypeID.View, 456));
            scope.Serialized(0, ItemFrame(ItemQuery.TypeID.View, 456, (10, 20, 30, 40, 50)));
            Assert.Empty(scope.Queue); Assert.Equal(11, scope.Available);
        });

    [Fact]
    public Task SingleNullRecipientFaultsBeforeItemReadOrEncodingButStillDisposesTheTakenPacket() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Assert.Throws<NullReferenceException>(() => ItemQuery.Create(null!, (LegacyNativeNinja.Item)null!, ItemQuery.TypeID.Add));
            Assert.Empty(scope.Queue); scope.UnserializedDisposed(0); Assert.Equal(11, scope.Available);
        });

    [Fact]
    public Task SingleNullItemFaultsAfterRecipientIdentityReadWithoutEncoding() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Assert.Throws<NullReferenceException>(() => ItemQuery.Create(scope.Client, (LegacyNativeNinja.Item)null!, ItemQuery.TypeID.Add));
            Assert.Empty(scope.Queue); scope.UnserializedDisposed(0); Assert.Equal(11, scope.Available);
        });

    [Fact]
    public Task FakeRecipientSuppressesBulkSendOnlyAfterCompleteItemEncodingAndDisposal() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Client.Fake = true; scope.Client.Socket = null!;
            scope.Item(1, 0, 0, 0, 0, uint.MaxValue);
            ItemQuery.Create(scope.Client, scope.Ninja, ItemQuery.TypeID.Add, 987);
            scope.Serialized(0, ItemFrame(ItemQuery.TypeID.Add, 987, (0, 0, 0, 0, uint.MaxValue)));
            Assert.Empty(scope.Queue); Assert.Equal(11, scope.Available); Assert.Empty(scope.Log);
        });
}
