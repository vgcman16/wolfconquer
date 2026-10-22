using System.Reflection;
using ProtoBuf;
using Msg = WolfConquer.Core.World.LegacyNativeMsgCollectionStorage;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeCollectionContractTests
{
    // Independent Framework/x86 source-shaped controls from preserved
    // protobuf-net2.0.0.668 SHA66653D78..., object-typed native Serialize API.
    [Theory]
    [InlineData("zero", "08001000")]
    [InlineData("scrap-zero", "080010001A0408001000")]
    [InlineData("item-zero", "08001000220A08001000180020002800")]
    [InlineData("user-seven", "080010002807")]
    [InlineData("action-0", "0800104D")]
    [InlineData("action-1", "0801104D")]
    [InlineData("action-2", "0802104D")]
    [InlineData("action-3", "0803104D")]
    [InlineData("action-4", "0804104D")]
    [InlineData("action-5", "0805104D")]
    [InlineData("action-6", "0806104D")]
    [InlineData("action-7", "0807104D")]
    [InlineData("action-8", "0808104D")]
    [InlineData("mixed", "080610E0B986021A0708DE80CB0110071A0708A1D4CB011000220E08E0B986021001181720901C2804220D08F18787021000180020002800287B")]
    [InlineData("max", "08FFFFFFFFFFFFFFFFFF0110FFFFFFFF0F1A0C08FFFFFFFF0F10FFFFFFFF0F221E08FFFFFFFF0F10FFFFFFFF0F18FFFFFFFF0F20FFFFFFFF0F28FFFFFFFF0F28FFFFFFFF0F")]
    [InlineData("null-lists", "08001000")]
    [InlineData("null-element", "080010002200")]
    [InlineData("null-outer", "")]
    public void ActualContractsMatchIndependentPinnedSerializerControls(string name, string expected)
    {
        var query = Query(name);
        var scraps = query?.pScrap;
        var items = query?.items;
        using var output = new MemoryStream();
        Serializer.Serialize((Stream)output, (object?)query);
        Assert.Equal(expected, Convert.ToHexString(output.ToArray()));
        if (query is not null)
        {
            Assert.Same(scraps, query.pScrap);
            Assert.Same(items, query.items);
        }
    }

    [Fact]
    public void ContractsRetainAllPhysicalFieldsRequiredTagsAndNativeEnumValues()
    {
        Check(typeof(Msg), ["Type", "Identity", "pScrap", "items", "UserID"],
            [typeof(Msg.Types), typeof(uint), typeof(List<Msg.Scrap>), typeof(List<Msg.item>), typeof(uint)], 4);
        Check(typeof(Msg.Scrap), ["Type", "Amount"], [typeof(uint), typeof(uint)], 2);
        Check(typeof(Msg.item), ["Type", "Activate", "Amount", "RemainingTime", "F5"],
            [typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(uint)], 5);
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(Msg.Types)));
        Assert.NotNull(typeof(Msg.Types).GetCustomAttribute<FlagsAttribute>());
        Assert.Equal(new uint[] { 0, 1, 2, 3, 4, 5, 6, 8 }, Enum.GetValues<Msg.Types>().Select(value => (uint)value));
        var first = new Msg(); var second = new Msg();
        Assert.Empty(first.pScrap); Assert.Empty(first.items);
        Assert.NotSame(first.pScrap, second.pScrap); Assert.NotSame(first.items, second.items);
        Assert.Equal(Msg.Types.STORAGE_LOAD, first.Type);
        Assert.Equal(0u, first.Identity); Assert.Equal(0u, first.UserID);
    }

    [Fact]
    public void ExplicitNativeConstructorSlotAssignsAFreshPhysicalOwnerWithoutChangingOldStorage()
    {
        var client = new WolfConquer.Core.World.LegacyNativeGameClientRuntime();
        var old = new WolfConquer.Core.World.LegacyNativeCollectionStorageRuntime(client);
        old.Scraps[7] = new() { Type = 7, Amount = 9 };
        client.CollectionStorage = old;
        client.Player.CollectionID = 77;
        WolfConquer.Core.World.LegacyNativeCollectionStorageMemberPhases
            .AssignCollectionStorageAtNativeConstructorSlot(client);
        var assigned = Assert.IsType<WolfConquer.Core.World.LegacyNativeCollectionStorageRuntime>(client.CollectionStorage);
        Assert.NotSame(old, assigned);
        Assert.Same(client, assigned.user);
        Assert.Empty(assigned.Scraps); Assert.Empty(assigned.Items);
        Assert.Equal(9u, Assert.Single(old.Scraps).Value.Amount);
        Assert.Equal(77u, client.Player.CollectionID);
    }

    private static void Check(Type type, string[] names, Type[] types, int required)
    {
        Assert.NotNull(type.GetCustomAttribute<ProtoContractAttribute>());
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(names, fields.Select(field => field.Name));
        Assert.Equal(types, fields.Select(field => field.FieldType));
        Assert.Equal(Enumerable.Range(1, names.Length), fields.Select(field => field.GetCustomAttribute<ProtoMemberAttribute>()!.Tag));
        for (int index = 0; index < fields.Length; index++)
        {
            Assert.False(fields[index].IsInitOnly);
            Assert.Equal(index < required, fields[index].GetCustomAttribute<ProtoMemberAttribute>()!.IsRequired);
        }
    }

    private static Msg? Query(string name)
    {
        if (name.StartsWith("action-", StringComparison.Ordinal))
            return new() { Type = (Msg.Types)uint.Parse(name[7..]), Identity = 77 };
        var result = new Msg();
        switch (name)
        {
            case "zero": return result;
            case "scrap-zero": result.pScrap.Add(new()); return result;
            case "item-zero": result.items.Add(new()); return result;
            case "user-seven": result.UserID = 7; return result;
            case "null-lists": result.pScrap = null!; result.items = null!; return result;
            case "null-element": result.items.Add(null!); return result;
            case "null-outer": return null;
            case "mixed":
                result.Type = Msg.Types.STORAGE_ITEM; result.Identity = 4300000; result.UserID = 123;
                result.pScrap.Add(new() { Type = 3326046, Amount = 7 });
                result.pScrap.Add(new() { Type = 3336737, Amount = 0 });
                result.items.Add(new() { Type = 4300000, Activate = 1, Amount = 23, RemainingTime = 3600, F5 = 4 });
                result.items.Add(new() { Type = 4310001 });
                return result;
            case "max":
                result.Type = (Msg.Types)uint.MaxValue; result.Identity = uint.MaxValue; result.UserID = uint.MaxValue;
                result.pScrap.Add(new() { Type = uint.MaxValue, Amount = uint.MaxValue });
                result.items.Add(new() { Type = uint.MaxValue, Activate = uint.MaxValue, Amount = uint.MaxValue,
                    RemainingTime = uint.MaxValue, F5 = uint.MaxValue });
                return result;
            default: throw new ArgumentOutOfRangeException(nameof(name));
        }
    }
}
