using System.Reflection;
using ProtoBuf;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeleportLifecycleTests
{
    [Fact]
    public Task TradeAllRequiredDefaultFieldsHaveLiteralWireEncodingAndReaderRewinds()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Packet.CreateTradeInfo(new LegacyNativeMsgTrade.Trade());
            byte[] expected = { 16,0,71,8,8,0,16,0,24,0,32,0,40,0,48,0 };
            Assert.Equal(expected, Bytes(scope.Packet));
            scope.Packet.Seek(13);
            scope.Packet.GetTradeInfo(out var request);
            Assert.Equal(16, scope.Packet.Position);
            Assert.Equal(0uL, request.Action); Assert.Equal(0L, request.Money);
            Assert.Equal(0uL, request.ItemUID); Assert.Equal(0u, request.CPS);
            Assert.Equal(0uL, request.Unknown); Assert.Equal(0uL, request.UID);
        });
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(127L)]
    [InlineData(128L)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public Task TradeSignedMoneyAndFullWidthIdsRoundTripNativeGenericCodec(long money)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var input = new LegacyNativeMsgTrade.Trade { Action = ulong.MaxValue, Money = money,
                ItemUID = 0x1122334455667788, CPS = uint.MaxValue, Unknown = 128, UID = ulong.MaxValue };
            scope.Packet.CreateTradeInfo(input);
            Assert.Equal(TradeBytes(input.Action, money, input.ItemUID, input.CPS, input.Unknown, input.UID), Bytes(scope.Packet));
            scope.Packet.GetTradeInfo(out var decoded);
            Assert.NotSame(input, decoded);
            Assert.Equal(input.Action, decoded.Action); Assert.Equal(money, decoded.Money);
            Assert.Equal(input.ItemUID, decoded.ItemUID); Assert.Equal(input.CPS, decoded.CPS);
            Assert.Equal(input.Unknown, decoded.Unknown); Assert.Equal(input.UID, decoded.UID);
        });
    }

    [Fact]
    public void TradeContractsRetainEveryRequiredTagAndNativeEnumOrdinal()
    {
        foreach (var row in new[] { (typeof(LegacyNativeMsgTrade.Trade), 6), (typeof(LegacyNativeMsgTrade.TradeInfo), 20) })
        {
            var fields = row.Item1.GetFields(BindingFlags.Instance | BindingFlags.Public);
            Assert.Equal(row.Item2, fields.Length);
            var members = fields.Select(field => field.GetCustomAttribute<ProtoMemberAttribute>()!).OrderBy(member => member.Tag).ToArray();
            Assert.Equal(Enumerable.Range(1, row.Item2), members.Select(member => member.Tag));
            Assert.All(members, member => Assert.True(member.IsRequired));
        }
        Assert.Equal(Enumerable.Range(0, 13).Select(x => (uint)x),
            Enum.GetValues<LegacyNativeMsgTrade.TradeID>().Select(value => (uint)value));
    }

    [Fact]
    public Task TradeInfoToArrayPreservesAllTwentyRequiredFieldsInAnOwnedPacket()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var info = new LegacyNativeMsgTrade.TradeInfo { Name = "A" };
            using var packet = info.ToArray();
            byte[] expected = {50,0,92,9,8,0,16,0,24,0,34,1,65,40,0,48,0,56,0,64,0,72,0,
                80,0,88,0,96,0,104,0,112,0,120,0,128,1,0,136,1,0,144,1,0,152,1,0,160,1,0};
            Assert.Equal(expected, Bytes(packet));
            var decoded = ReadProto<LegacyNativeMsgTrade.TradeInfo>(expected);
            Assert.Equal("A", decoded.Name); Assert.False(decoded.Spouse); Assert.Equal(0L, decoded.un20);
        });
    }

    [Theory]
    [InlineData(0u, 0u, 0uL)]
    [InlineData(1002u, 1000u, 19568946643047uL)]
    [InlineData(uint.MaxValue, uint.MaxValue, ulong.MaxValue)]
    public Task MapStatusUsesRequiredProtoFieldsAndOmitsOptionalZeroWeather(uint id, uint baseId, ulong status)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Assert.Same(scope.Packet, scope.Packet.MapStatusCreate(id, baseId, status));
            Assert.Equal(ProtoFrame(2213, (1, id), (2, baseId), (3, status)), Bytes(scope.Packet));
            var parsed = ReadProto<LegacyNativeMapStatusPacket.MapStatusProto>(Bytes(scope.Packet));
            Assert.Equal(id, parsed.ID); Assert.Equal(baseId, parsed.BaseID); Assert.Equal(status, parsed.Status);
            Assert.Equal(0u, parsed.Weather);
        });
    }

    [Fact]
    public void MapStatusOptionalWeatherRemainsARealFourthProtoField()
    {
        var data = new LegacyNativeMapStatusPacket.MapStatusProto { ID = 1, BaseID = 2, Status = 3, Weather = 4 };
        using var body = new MemoryStream();
        Serializer.Serialize(body, data);
        Assert.Equal(new byte[] { 8,1,16,2,24,3,32,4 }, body.ToArray());
        Assert.False(typeof(LegacyNativeMapStatusPacket.MapStatusProto).GetField("Weather")!
            .GetCustomAttribute<ProtoMemberAttribute>()!.IsRequired);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(7u)]
    [InlineData(8u)]
    [InlineData(10u)]
    [InlineData(0u)]
    [InlineData(uint.MaxValue)]
    public Task WeatherWritesAllFiveRawWordsWithoutRangeNormalization(uint type)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Packet.WeatherCreate((LegacyNativeMsgWeather.WeatherType)type, uint.MaxValue, 2, 3, 4);
            var expected = new byte[24];
            BitConverter.GetBytes((ushort)24).CopyTo(expected, 0);
            BitConverter.GetBytes((ushort)2306).CopyTo(expected, 2);
            BitConverter.GetBytes(type).CopyTo(expected, 4);
            BitConverter.GetBytes(uint.MaxValue).CopyTo(expected, 8);
            BitConverter.GetBytes(2u).CopyTo(expected, 12); BitConverter.GetBytes(3u).CopyTo(expected, 16);
            BitConverter.GetBytes(4u).CopyTo(expected, 20);
            Assert.Equal(expected, Bytes(scope.Packet));
        });
    }

    [Fact]
    public void WeatherEnumKeepsNativeGapAndTeleportListsAreMutableNativeLists()
    {
        Assert.Equal(new uint[] { 1,2,3,4,5,7,8,9,10 }, Enum.GetValues<LegacyNativeMsgWeather.WeatherType>().Select(x => (uint)x));
        Assert.Equal(new uint[] { 1005,6000 }, LegacyNativeConstants.MapCounterHits);
        Assert.Equal(new uint[] { 601,6000,5040,6001,1005,700,1858,1860,3852,2068,1768 }, LegacyNativeConstants.BlockTeleportMap);
        LegacyNativeConstants.MapCounterHits.Add(0xF0000001);
        try { Assert.Contains(0xF0000001u, LegacyNativeConstants.MapCounterHits); }
        finally { Assert.True(LegacyNativeConstants.MapCounterHits.Remove(0xF0000001)); }
    }
}
