using System.Buffers.Binary;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Buy = WolfConquer.Core.World.LegacyNativeCMsgExchangeShopBuy;
using Goods = WolfConquer.Core.World.LegacyNativeCMsgExchangeShopGoods;
using Info = WolfConquer.Core.World.LegacyNativeCMsgExchangeShopGoodsInfo;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeContractTests
{
    [Theory]
    [InlineData("goods-mixed", "081110A801187B20D72328093206080710031804320A08FFFFFFFF0F10001800", 2317)]
    [InlineData("buy-mixed", "0801101118A80120022809320408071003320408081004", 2109)]
    [InlineData("goods-max", "08FFFFFFFF0F10FFFFFFFF0F18FFFFFFFF0F20FFFFFFFF0F28FFFFFFFF0F321208FFFFFFFF0F10FFFFFFFF0F18FFFFFFFF0F", 2317)]
    [InlineData("buy-max", "08FFFFFFFF0F10FFFFFFFF0F18FFFFFFFF0F20FFFFFFFF0F28FFFFFFFF0F320C08FFFFFFFF0F10FFFFFFFF0F", 2109)]
    public Task BothCreateCodecsUseActualObjectAndExactFrameCursor(string name, string payload, int opcode) =>
        NativePacketTestThread.Run(() =>
        {
            using var fixture = new NativeSpellTestFixture();
            Fill(fixture.Packet);
            var query = Query(name);
            var packet = query is Goods goods ? fixture.Packet.CreateExchangeShopGoods(goods)
                : fixture.Packet.CreateExchangeShopBuy((Buy)query!);
            Assert.Same(fixture.Packet, packet);
            Assert.Equal(Frame(payload, opcode), NativeSpellTestFixture.Bytes(packet));
            Assert.Equal(4, packet.Position);
            Tail(packet);
            Assert.Empty(fixture.Queue); // A primitive codec itself is not a sender.
        });

    [Theory]
    [InlineData("goods-mixed", "081110A801187B20D72328093206080710031804320A08FFFFFFFF0F10001800")]
    [InlineData("buy-mixed", "0801101118A80120022809320408071003320408081004")]
    [InlineData("info-mixed", "088202101118A801")]
    [InlineData("goods-max", "08FFFFFFFF0F10FFFFFFFF0F18FFFFFFFF0F20FFFFFFFF0F28FFFFFFFF0F321208FFFFFFFF0F10FFFFFFFF0F18FFFFFFFF0F")]
    [InlineData("buy-max", "08FFFFFFFF0F10FFFFFFFF0F18FFFFFFFF0F20FFFFFFFF0F28FFFFFFFF0F320C08FFFFFFFF0F10FFFFFFFF0F")]
    [InlineData("info-max", "08FFFFFFFF0F10FFFFFFFF0F18FFFFFFFF0F")]
    public Task AllThreeGetCodecsSeekHeaderConsumeDeclaredBodyAndKeepFullWidthValues(string name, string payload) =>
        NativePacketTestThread.Run(() =>
    {
        // Native decoder reads but does not validate opcode, and reads the body
        // from the declared length rather than all supplied trailing bytes.
        byte[] frame = Frame(payload, 0x7AAA);
        using var packet = new LegacyNativePacket(frame.Concat(new byte[] { 0xA5, 0x5A }).ToArray());
        int physicalSize = packet.Size;
        packet.Seek(3);
        object actual;
        if (name.StartsWith("goods-", StringComparison.Ordinal))
        {
            packet.GetExchangeShopGoods(out Goods goods); actual = goods;
        }
        else if (name.StartsWith("buy-", StringComparison.Ordinal))
        {
            packet.GetExchangeShopBuy(out Buy buy); actual = buy;
        }
        else
        {
            packet.GetExchangeShopGoodsInfo(out Info info); actual = info;
        }
        Assert.Equal(payload, Convert.ToHexString(Payload(actual)));
        Assert.Equal(Query(name)!.GetType(), actual.GetType());
        Assert.Equal(frame.Length, packet.Position);
        Assert.Equal(physicalSize, packet.Size);
        if (actual is Info value && name == "info-mixed") Assert.Equal(258u, value.Type);
    });

    [Fact]
    public void ExplicitShopConstructorSlotReplacesOnlyTheMemberWithAGenuineFreshOwner()
    {
        var client = new LegacyNativeGameClientRuntime();
        var old = new LegacyNativeExchangeShopRuntime(client);
        old.Load("1/168/7/9/");
        client.MyExchangeShop = old;
        client.Player.UID = 77;
        LegacyNativeExchangeShopMemberPhases.AssignExchangeShopAtNativeConstructorSlot(client);
        var assigned = Assert.IsType<LegacyNativeExchangeShopRuntime>(client.MyExchangeShop);
        Assert.NotSame(old, assigned);
        Assert.Equal("0/", assigned.ToString());
        Assert.Equal("1/168/7/9/", old.ToString());
        Assert.Equal(77u, client.Player.UID);
    }

    private static byte[] Frame(string payload, int opcode)
    {
        byte[] bytes = Convert.FromHexString(payload);
        var result = new byte[bytes.Length + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(result, (ushort)result.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(2), (ushort)opcode);
        bytes.CopyTo(result, 4);
        return result;
    }

    private static unsafe void Fill(LegacyNativePacket packet)
    {
        Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
            0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
        packet.Size = 321; packet.Seek(237);
    }

    private static unsafe void Tail(LegacyNativePacket packet) => Assert.Equal((byte)0xA5, packet.Memory[packet.Size]);
}
