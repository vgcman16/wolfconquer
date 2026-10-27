using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using ProtoBuf;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Show = WolfConquer.Core.World.LegacyNativeMsgRankMemberShow.MsgRankMemberShowProto;
using Item = WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaItem;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeNinjaWireContractTests
{
    // Observed with complete source-shaped contracts and the object Serialize API
    // in an owned Framework/x86 probe, held protobuf-net2.0.0.668 SHA66653D78....
    [Theory]
    [InlineData("rank-default", "0800")]
    [InlineData("rank-empty", "0800")]
    [InlineData("rank-zero-entry", "0800123008001000180022002A003000380040004800500058006000680070007800800100880100900100980100A00100A80100")]
    [InlineData("rank-mixed-unicode", "08FFFFFFFF0F123B08021001187B220857C3B66C66E78BBC2A03E78BBC3006380740084809500A580B600C680D700E780F800110880111900112980113A00114A80115")]
    [InlineData("rank-null-strings", "0800122C0800100018003000380040004800500058006000680070007800800100880100900100980100A00100A80100")]
    [InlineData("rank-null-entry", "0800")]
    [InlineData("rank-null", "")]
    [InlineData("item-default", "08001000")]
    [InlineData("item-empty", "08001000")]
    [InlineData("item-zero", "080010001A0A08001000180020002800")]
    [InlineData("item-mixed", "0803107B1A0A08011002180320042805")]
    [InlineData("item-max", "08FFFFFFFF0710FFFFFFFF0F1A1E08FFFFFFFF0F10FFFFFFFF0F18FFFFFFFF0F20FFFFFFFF0F28FFFFFFFF0F")]
    [InlineData("item-unknown-four", "08041000")]
    [InlineData("item-negative", "08FFFFFFFFFFFFFFFFFF011000")]
    [InlineData("item-null-entry", "08001000")]
    [InlineData("item-null", "")]
    public Task CompletePrimitiveEncodersMatchHeldLiteralControls(string name, string hex) =>
        NativePacketTestThread.Run(() =>
        {
            using var fixture = new NativeSpellTestFixture();
            Fill(fixture.Packet);
            if (name == "item-null")
            {
                // Required null envelope rejects before touching native storage;
                // the historical successful empty frame is not acceptance.
                var before = ReadStorage(fixture.Packet);
                Assert.Throws<ArgumentNullException>(() => fixture.Packet.CreateNinjaItem(null!));
                Assert.Equal(before, ReadStorage(fixture.Packet));
                Assert.Equal(321, fixture.Packet.Size);
                Assert.Equal(237, fixture.Packet.Position);
                Assert.Empty(fixture.Queue);
                return;
            }
            bool rank = name.StartsWith("rank-", StringComparison.Ordinal);
            var returned = rank ? fixture.Packet.CreateMsgRankMemberShow(ShowValue(name)!) :
                fixture.Packet.CreateNinjaItem(ItemValue(name)!);
            Assert.Same(fixture.Packet, returned);
            byte[] payload = Convert.FromHexString(hex);
            var expected = new byte[payload.Length + 4];
            BinaryPrimitives.WriteUInt16LittleEndian(expected, (ushort)expected.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2), (ushort)(rank ? 2054 : 2393));
            payload.CopyTo(expected, 4);
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(fixture.Packet));
            Assert.Equal(4, fixture.Packet.Position);
            Assert.Equal(expected.Length, fixture.Packet.Size);
            Tail(fixture.Packet);
            Assert.Empty(fixture.Queue);
        });

    [Fact]
    public void CompleteRankContractHasAllMutableRequiredFieldsAndNativeDefaults()
    {
        Check(typeof(Show), new[] { "Type", "Items" }, typeof(Show.Entry[]));
        Check(typeof(Show.Entry), new[] { "Type", "Rank", "EntityUID", "Name", "GuildName", "Mesh",
            "HairStyle", "Head", "Garment", "LeftWeapon", "LefttWeaponAccessory", "RightWeapon",
            "RightWeaponAccessory", "MountArmor", "Armor", "Wing", "WingPlus", "Title", "Flag",
            "Profession", "WeaponWar" });
        Assert.Null(new Show().Items);
        Assert.Equal("", new Show.Entry().Name);
        Assert.Equal("", new Show.Entry().GuildName);
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(LegacyNativeMsgRankMemberShow.Mode)));
        Assert.NotNull(typeof(LegacyNativeMsgRankMemberShow.Mode).GetCustomAttribute<FlagsAttribute>());
        Assert.Equal(new uint[] { 0, 1 }, Enum.GetValues<LegacyNativeMsgRankMemberShow.Mode>().Select(x => (uint)x));
    }

    [Fact]
    public void CompleteItemContractHasAllMutableRequiredFieldsAndNativeDefaults()
    {
        Check(typeof(Item), new[] { "Type", "UID", "Items" }, typeof(Item.Item[]), typeof(Item.TypeID));
        Check(typeof(Item.Item), new[] { "ID", "Level", "Position", "Points", "UK" });
        Assert.Null(new Item().Items);
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(Item.TypeID)));
        Assert.NotNull(typeof(Item.TypeID).GetCustomAttribute<FlagsAttribute>());
        Assert.Equal(new[] { "Load", "View", "Add", "Update" }, Enum.GetNames<Item.TypeID>());
        Assert.Equal(new[] { 0, 1, 2, 3 }, Enum.GetValues<Item.TypeID>().Select(x => (int)x));
        Assert.Equal((ushort)2393, LegacyNativeMsgNinja.MsgNinjaItem_ID);
    }

    private static void Check(Type type, string[] names, Type? arrayType = null, Type? enumType = null)
    {
        Assert.NotNull(type.GetCustomAttribute<ProtoContractAttribute>());
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public).OrderBy(x => x.MetadataToken).ToArray();
        Assert.Equal(names, fields.Select(x => x.Name));
        Assert.Equal(Enumerable.Range(1, names.Length), fields.Select(x => x.GetCustomAttribute<ProtoMemberAttribute>()!.Tag));
        Assert.All(fields, field =>
        {
            Assert.False(field.IsInitOnly);
            Assert.True(field.GetCustomAttribute<ProtoMemberAttribute>()!.IsRequired);
            Type expected = field.Name == "Items" ? arrayType! :
                field.Name is "Name" or "GuildName" ? typeof(string) :
                field.Name == "Type" && enumType != null ? enumType : typeof(uint);
            Assert.Equal(expected, field.FieldType);
        });
    }

    private static Show? ShowValue(string name) => name switch
    {
        "rank-default" => new(),
        "rank-empty" => new() { Items = Array.Empty<Show.Entry>() },
        "rank-zero-entry" => new() { Items = new[] { new Show.Entry() } },
        "rank-mixed-unicode" => new() { Type = uint.MaxValue, Items = new[] {
            new Show.Entry { Type = 2, Rank = 1, EntityUID = 123, Name = "W\u00f6lf\u72fc", GuildName = "\u72fc",
                Mesh = 6, HairStyle = 7, Head = 8, Garment = 9, LeftWeapon = 10, LefttWeaponAccessory = 11,
                RightWeapon = 12, RightWeaponAccessory = 13, MountArmor = 14, Armor = 15, Wing = 16,
                WingPlus = 17, Title = 18, Flag = 19, Profession = 20, WeaponWar = 21 } } },
        "rank-null-strings" => new() { Items = new[] { new Show.Entry { Name = null!, GuildName = null! } } },
        "rank-null-entry" => new() { Items = new Show.Entry[] { null! } },
        "rank-null" => null,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static Item? ItemValue(string name) => name switch
    {
        "item-default" => new(),
        "item-empty" => new() { Items = Array.Empty<Item.Item>() },
        "item-zero" => new() { Items = new[] { new Item.Item() } },
        "item-mixed" => new() { Type = Item.TypeID.Update, UID = 123, Items = new[] {
            new Item.Item { ID = 1, Level = 2, Position = 3, Points = 4, UK = 5 } } },
        "item-max" => new() { Type = (Item.TypeID)int.MaxValue, UID = uint.MaxValue, Items = new[] {
            new Item.Item { ID = uint.MaxValue, Level = uint.MaxValue, Position = uint.MaxValue,
                Points = uint.MaxValue, UK = uint.MaxValue } } },
        "item-unknown-four" => new() { Type = (Item.TypeID)4 },
        "item-negative" => new() { Type = (Item.TypeID)(-1) },
        "item-null-entry" => new() { Items = new Item.Item[] { null! } },
        "item-null" => null,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static unsafe byte[] ReadStorage(LegacyNativePacket packet)
    {
        var bytes = new byte[packet.Capacity];
        Marshal.Copy((IntPtr)packet.Memory, bytes, 0, bytes.Length);
        return bytes;
    }

    private static unsafe void Fill(LegacyNativePacket packet)
    {
        Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
            0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
        packet.Size = 321; packet.Seek(237);
    }

    private static unsafe void Tail(LegacyNativePacket packet) =>
        Assert.Equal((byte)0xA5, packet.Memory[packet.Size]);
}
