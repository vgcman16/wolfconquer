using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCharacterReadTests
{
    private static LegacyNativeMsgDetainedItem Detained(uint id = 2, uint owner = 17, uint gainer = 33) => new()
    {
        UID = id, ItemUID = 71, ItemID = 410009, OwnerUID = owner, OwnerName = "Owner", GainerUID = gainer, GainerName = "Gainer",
        Date = LegacyNativeConfiscatorRuntime.CreateTimer(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day),
        Action = LegacyNativeMsgDetainedItem.ContainerType.DetainPage, Signature = "Signature", PerfectionOwnerName = "Smith"
    };

    [Fact]
    public void DetainedPhysicalRecordHasSource322ByteLayoutAndOneByteBoolWithoutMarshallerProjection()
    {
        Assert.Equal(322, Unsafe.SizeOf<LegacyNativeMsgDetainedItem>());
        foreach (var field in new (string Name, int Offset)[] { ("UID",0),("ItemUID",4),("Action",16),("Bound",32),("Enchant",33),
            ("OwnerUID",42),("szOwnerName",46),("GainerUID",78),("Date",114),("_Signature",170),("Purification",206),("Refinary",246),("Attribute1",294),("AnimaItemID",318) })
            Assert.Equal(field.Offset, (int)Marshal.OffsetOf<LegacyNativeMsgDetainedItem>(field.Name));
        var row = Detained(); row.Bound = true; row.Enchant = 0x7F;
        byte[] raw = Raw(row); Assert.Equal(1, raw[32]); Assert.Equal(127, raw[33]);
        Assert.Equal("Owner", row.OwnerName); Assert.Equal("Gainer", row.GainerName); Assert.Equal("Smith", row.PerfectionOwnerName); Assert.Equal("Signature", row.Signature);
    }

    [Fact]
    public Task EntireCreateAndCopyMethodsKeepSourceAsymmetryInsteadOfCloningUnsupportedFields() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = scope.Item();
        item.Bound = 2; item.Locked = 7; item.Suspicious = 3; item.OwnerUID = 99; item.OwnerName = "Smith"; item.Signature = "Mark";
        item.PerfectionLevel = 8; item.PerfectionProgress = 9; item.RuneEXP = 10; item.AnimaItemID = 11;
        var row = LegacyNativeMsgDetainedItem.Create(item);
        Assert.False(row.Bound); Assert.Equal((ushort)7, row.Lock); Assert.Equal(0u, row.OwnerUID);
        Assert.Equal("Smith", row.PerfectionOwnerName); Assert.Equal("Mark", row.Signature);
        Assert.Equal(0u, row.Purification.ItemUID); Assert.Equal(0u, row.Refinary.ItemUID);
        row.OwnerUID = 17; row.Bound = true;
        var copy = LegacyNativeMsgDetainedItem.CopyTo(row);
        Assert.Equal(71u, copy.Uid); Assert.Equal(410009u, copy.ITEM_ID); Assert.Equal((byte)1, copy.Bound);
        Assert.Equal((byte)0, copy.Locked); Assert.Equal(17u, copy.OwnerUID); Assert.Equal("Smith", copy.OwnerName);
        Assert.Equal(8u, copy.PerfectionLevel); Assert.Equal(9u, copy.PerfectionProgress); Assert.Equal(10u, copy.RuneEXP); Assert.Equal(11u, copy.AnimaItemID);
        item.RelicAttributes = null!; Assert.Throws<NullReferenceException>(() => LegacyNativeMsgDetainedItem.Create(item));
        item.RelicAttributes = new LegacyNativeRelicAttribute[1]; Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeMsgDetainedItem.Create(item));
    });

    [Fact]
    public Task DetainedWireUsesCompleteLiteral248ByteLayoutAndRepeatedSourceOwnerFields() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Detained(); row.Bound = true; row.Enchant = 9; row.PerfectionLevel = 6;
        row.ConquerPointsCost = 31; row.DaysLeft = 5;
        byte[] bytes = NativeSpellTestFixture.Bytes(scope.Packet.DetainedItemCreate(row));
        Assert.Equal(248, bytes.Length); Assert.Equal((ushort)2262, Opcode(bytes));
        Assert.Equal(2u, U32(bytes, 4)); Assert.Equal(71u, U32(bytes, 8)); Assert.Equal(410009u, U32(bytes, 12));
        Assert.Equal((byte)1, bytes[39]); Assert.Equal(9u, U32(bytes, 40)); Assert.Equal(6u, U32(bytes, 52));
        Assert.Equal(17u, U32(bytes, 60)); Assert.Equal(17u, U32(bytes, 160)); Assert.Equal(33u, U32(bytes, 196));
        Assert.Equal(bytes.AsSpan(64,32).ToArray(), bytes.AsSpan(164,32).ToArray());
        Assert.Equal("Owner", System.Text.Encoding.ASCII.GetString(bytes, 64, 5));
        Assert.All(bytes.AsSpan(96,4).ToArray(), b => Assert.Equal(0, b));
        Assert.Equal(31u, U32(bytes, 240)); Assert.Equal(5u, U32(bytes, 244));
    });

    [Fact]
    public Task RewardSendMutatesItsActualValueAndReturnsRealInventoryWithoutExtraPacket() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Detained(); row.Action = LegacyNativeMsgDetainedItem.ContainerType.RewardCps;
        row.Plus = 8; row.Bound = true; row.Lock = 9; row.Enchant = 9; row.Purification.ItemUID = 71;
        Assert.Same(scope.Client.Inventory, row.Send(scope.Client, scope.Packet));
        Assert.Equal(500u, row.OwnerUID); Assert.Equal(0u, row.ItemID); Assert.Equal((byte)0, row.Plus); Assert.False(row.Bound); Assert.Equal((ushort)0, row.Lock);
        Assert.Equal(71u, row.Purification.ItemUID); Assert.Equal((ushort)2262, Opcode(Assert.Single(scope.Queue)));
    });

    [Fact]
    public Task NormalSendIncludesGenuinePermanentExtrasAndMutatesRealAvailabilityAndTypes() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Detained();
        row.Purification.ItemUID = 71; row.Purification.PurificationItemID = 1001;
        row.Refinary.ItemUID = 71; row.Refinary.EffectID = 1001;
        Assert.Same(scope.Client.Inventory, row.Send(scope.Client, scope.Packet));
        Assert.Equal(new ushort[] { 2262, 2134 }, scope.Queue.Select(Opcode));
        Assert.Equal(LegacyNativeItemExtra.Typing.PurificationAdding, row.Purification.Typ);
        Assert.Equal(LegacyNativeItemExtra.Typing.PermanentRefinery, row.Refinary.Typ); Assert.True(row.Refinary.Available);
    });
}
