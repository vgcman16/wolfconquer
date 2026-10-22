using System.Globalization;
using WolfConquer.Core.World;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeItemSendTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("ar-SA")]
    public Task ToLogUsesExactNativeLabelsPhysicalFieldsAndEnumFormatting(string culture) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            scope.Items[1_000_000] = new() { ID = 42, Name = "狼 Pearl" };
            var item = Item();
            item.Uid = uint.MaxValue;
            item.Plus = 12;
            item.Bless = 7;
            item.AnimaItemID = 1234567890;
            item.PerfectionLevel = uint.MaxValue;
            item.SocketOne = Flags.Gem.SuperDragonGem;
            item.SocketTwo = (Flags.Gem)254;

            Assert.Equal("Name: 狼 Pearl: UID: 4294967295 | ID: 1000000 | Plus: 12 | Bless: 7" +
                " | Anima: 1234567890 | Perfection Level: 4294967295 | S1: SuperDragonGem | S2: 254", item.ToLog());
            Assert.Equal(42u, scope.Items[1_000_000].ID);
            Assert.Empty(scope.Queue);
            Assert.Equal(237, scope.Packet.Position);
        });

    [Fact]
    public Task ToLogReReadsCurrentMetadataNameAndReplacementTableOnEveryCall() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var row = new LegacyNativeItemRuneMetadata { Name = "first" };
            scope.Items[1_000_000] = row;
            var item = Item();
            const string suffix = ": UID: 17 | ID: 1000000 | Plus: 0 | Bless: 0 | Anima: 0" +
                " | Perfection Level: 0 | S1: NoSocket | S2: NoSocket";
            Assert.Equal("Name: first" + suffix, item.ToLog());
            row.Name = "second";
            Assert.Equal("Name: second" + suffix, item.ToLog());
            LegacyNativePoolItems.ItemsBase = new() { [1_000_000] = new() { Name = "third" } };
            Assert.Equal("Name: third" + suffix, item.ToLog());
            Assert.Same(row, scope.Items[1_000_000]);
            Assert.Equal("second", row.Name);
        });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public Task ToLogConcatenatesNullOrEmptyMetadataNameWithoutFallback(string? name) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Items[1_000_000] = new() { Name = name };
            var item = Item();
            item.SocketOne = Flags.Gem.EmptySocket;
            Assert.Equal("Name: : UID: 17 | ID: 1000000 | Plus: 0 | Bless: 0 | Anima: 0" +
                " | Perfection Level: 0 | S1: EmptySocket | S2: NoSocket", item.ToLog());
            Assert.Same(scope.Items, LegacyNativePoolItems.ItemsBase);
        });

    [Theory]
    [InlineData("table")]
    [InlineData("key")]
    [InlineData("row")]
    public Task ToLogRetainsNativeMissingMetadataFaultWithoutMutatingItem(string missing) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var item = Item();
            item.Plus = 9;
            item.MaximDurability = 55;
            if (missing == "table") LegacyNativePoolItems.ItemsBase = null;
            if (missing == "row") scope.Items[1_000_000] = null!;
            if (missing == "key") Assert.Throws<KeyNotFoundException>(() => item.ToLog());
            else Assert.Throws<NullReferenceException>(() => item.ToLog());
            Assert.Equal(17u, item.Uid);
            Assert.Equal(1_000_000u, item.ITEM_ID);
            Assert.Equal((byte)9, item.Plus);
            Assert.Equal((ushort)55, item.MaximDurability);
            Assert.Empty(scope.Queue);
            AssertTail(scope, 0);
        });
}
