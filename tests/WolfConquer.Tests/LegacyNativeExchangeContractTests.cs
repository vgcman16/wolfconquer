using System.Reflection;
using ProtoBuf;
using Buy = WolfConquer.Core.World.LegacyNativeCMsgExchangeShopBuy;
using Goods = WolfConquer.Core.World.LegacyNativeCMsgExchangeShopGoods;
using Info = WolfConquer.Core.World.LegacyNativeCMsgExchangeShopGoodsInfo;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeExchangeContractTests
{
    // Independently executed Framework/x86 source-shaped controls, preserved
    // protobuf-net2.0.0.668 SHA66653D78..., using the real object Serialize API.
    [Theory]
    [InlineData("goods-zero", "08001000180020002800")]
    [InlineData("goods-empty", "08001000180020002800")]
    [InlineData("goods-item-zero", "080010001800200028003206080010001800")]
    [InlineData("goods-null-element", "08001000180020002800")]
    [InlineData("goods-mixed", "081110A801187B20D72328093206080710031804320A08FFFFFFFF0F10001800")]
    [InlineData("goods-max", "08FFFFFFFF0F10FFFFFFFF0F18FFFFFFFF0F20FFFFFFFF0F28FFFFFFFF0F321208FFFFFFFF0F10FFFFFFFF0F18FFFFFFFF0F")]
    [InlineData("buy-zero", "08001000180020002800")]
    [InlineData("buy-item-zero", "08001000180020002800320408001000")]
    [InlineData("buy-mixed", "0801101118A80120022809320408071003320408081004")]
    [InlineData("buy-max", "08FFFFFFFF0F10FFFFFFFF0F18FFFFFFFF0F20FFFFFFFF0F28FFFFFFFF0F320C08FFFFFFFF0F10FFFFFFFF0F")]
    [InlineData("info-zero", "080010001800")]
    [InlineData("info-mixed", "088202101118A801")]
    [InlineData("info-max", "08FFFFFFFF0F10FFFFFFFF0F18FFFFFFFF0F")]
    [InlineData("null", "")]
    public void AllActualExchangeContractsMatchPinnedPayloads(string name, string expected)
    {
        Assert.Equal(expected, Convert.ToHexString(Payload(Query(name))));
    }

    [Fact]
    public void ContractsKeepEveryNativeRequiredFieldWidthNullArrayAndInfoEnum()
    {
        Check(typeof(Goods), ["NpcID", "ShopID", "UID", "TimeInSeconds", "uk5", "Items"],
            [typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(Goods.Item[])]);
        Check(typeof(Goods.Item), ["Index", "Count", "uk"], [typeof(uint), typeof(uint), typeof(uint)]);
        Check(typeof(Buy), ["Type", "NpcID", "ShopID", "Count", "Index", "Items"],
            [typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(Buy.Item[])]);
        Check(typeof(Buy.Item), ["ID", "Count"], [typeof(uint), typeof(uint)]);
        Check(typeof(Info), ["Type", "NpcID", "ShopID"], [typeof(uint), typeof(uint), typeof(uint)]);
        Assert.Null(new Goods().Items); Assert.Null(new Buy().Items);
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(Info._Type)));
        Assert.Equal(new byte[] { 1, 2 }, Enum.GetValues<Info._Type>().Select(value => (byte)value));
    }

    private static void Check(Type type, string[] names, Type[] types)
    {
        Assert.NotNull(type.GetCustomAttribute<ProtoContractAttribute>());
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(names, fields.Select(field => field.Name));
        Assert.Equal(types, fields.Select(field => field.FieldType));
        Assert.Equal(Enumerable.Range(1, names.Length), fields.Select(field => field.GetCustomAttribute<ProtoMemberAttribute>()!.Tag));
        Assert.All(fields, field =>
        {
            Assert.False(field.IsInitOnly);
            Assert.True(field.GetCustomAttribute<ProtoMemberAttribute>()!.IsRequired);
        });
    }

    private static byte[] Payload(object? value)
    {
        using var output = new MemoryStream();
        Serializer.Serialize((Stream)output, value);
        return output.ToArray();
    }

    private static object? Query(string name) => name switch
    {
        "goods-zero" => new Goods(),
        "goods-empty" => new Goods { Items = [] },
        "goods-item-zero" => new Goods { Items = [new()] },
        "goods-null-element" => new Goods { Items = [null!] },
        "goods-mixed" => new Goods { NpcID = 17, ShopID = 168, UID = 123, TimeInSeconds = 4567, uk5 = 9,
            Items = [new() { Index = 7, Count = 3, uk = 4 }, new() { Index = uint.MaxValue }] },
        "goods-max" => new Goods { NpcID = uint.MaxValue, ShopID = uint.MaxValue, UID = uint.MaxValue,
            TimeInSeconds = uint.MaxValue, uk5 = uint.MaxValue,
            Items = [new() { Index = uint.MaxValue, Count = uint.MaxValue, uk = uint.MaxValue }] },
        "buy-zero" => new Buy(),
        "buy-item-zero" => new Buy { Items = [new()] },
        "buy-mixed" => new Buy { Type = 1, NpcID = 17, ShopID = 168, Count = 2, Index = 9,
            Items = [new() { ID = 7, Count = 3 }, new() { ID = 8, Count = 4 }] },
        "buy-max" => new Buy { Type = uint.MaxValue, NpcID = uint.MaxValue, ShopID = uint.MaxValue,
            Count = uint.MaxValue, Index = uint.MaxValue,
            Items = [new() { ID = uint.MaxValue, Count = uint.MaxValue }] },
        "info-zero" => new Info(),
        "info-mixed" => new Info { Type = 258, NpcID = 17, ShopID = 168 },
        "info-max" => new Info { Type = uint.MaxValue, NpcID = uint.MaxValue, ShopID = uint.MaxValue },
        "null" => null,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };
}
