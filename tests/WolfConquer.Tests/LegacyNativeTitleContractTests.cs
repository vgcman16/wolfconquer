using System.Reflection;
using ProtoBuf;
using Msg = WolfConquer.Core.World.LegacyNativeMsgTitleStorage;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeTitleContractTests
{
    // Actual independent Framework/x86 source-shaped probe against preserved
    // protobuf-net2.0.0.668 SHA66653D78..., using object-typed native Serialize.
    [Theory]
    [InlineData("outer-zero", "0800100018002000")]
    [InlineData("nested-zero", "08001000180020002A080800100018002000")]
    [InlineData("action-0", "0800100718A11F20032A0908A11F100318642008")]
    [InlineData("action-1", "0801100718A11F20032A0908A11F100318642008")]
    [InlineData("action-3", "0803100718A11F20032A0908A11F100318642008")]
    [InlineData("action-4", "0804100718A11F20032A0908A11F100318642008")]
    [InlineData("action-5", "0805100718A11F20032A0908A11F100318642008")]
    [InlineData("action-6", "0806100718A11F20032A0908A11F100318642008")]
    [InlineData("max", "080510FFFFFFFF0F18FFFFFFFF0F20FFFFFFFF0F2A1808FFFFFFFF0F10FFFFFFFF0F18FFFFFFFF0F20FFFFFFFF0F")]
    [InlineData("unknown-two", "0802100018002000")]
    [InlineData("unknown-max", "08FFFFFFFFFFFFFFFFFF01100018002000")]
    [InlineData("null-outer", "")]
    public void ActualTitleContractsMatchPinnedPayloadsIncludingUnsignedEnumSignExtension(string name, string expected)
    {
        var query = Query(name);
        var title = query?.Title;
        using var output = new MemoryStream();
        Serializer.Serialize((Stream)output, (object?)query);
        Assert.Equal(expected, Convert.ToHexString(output.ToArray()));
        if (query is not null) Assert.Same(title, query.Title);
    }

    [Fact]
    public void BothContractsRetainAllRequiredTagsAndNativeMutableStorage()
    {
        Check(typeof(Msg.TitleStorage), new[] { "ActionID", "dwparam1", "dwparam2", "dwparam3", "Title" },
            new[] { typeof(Msg.Action), typeof(uint), typeof(uint), typeof(uint), typeof(Msg.Title) });
        Check(typeof(Msg.Title), new[] { "ID", "SubId", "dwparam1", "dwparam2" },
            new[] { typeof(uint), typeof(uint), typeof(uint), typeof(uint) });
        Assert.Null(new Msg.TitleStorage().Title);
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(Msg.Action)));
        Assert.NotNull(typeof(Msg.Action).GetCustomAttribute<FlagsAttribute>());
    }

    private static void Check(Type type, string[] names, Type[] types)
    {
        Assert.NotNull(type.GetCustomAttribute<ProtoContractAttribute>());
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(names, fields.Select(field => field.Name));
        Assert.Equal(types, fields.Select(field => field.FieldType));
        Assert.Equal(Enumerable.Range(1, names.Length), fields.Select(field => field.GetCustomAttribute<ProtoMemberAttribute>()!.Tag));
        foreach (var field in fields)
        {
            Assert.False(field.IsInitOnly);
            Assert.True(field.GetCustomAttribute<ProtoMemberAttribute>()!.IsRequired);
        }
    }

    private static Msg.TitleStorage? Query(string name)
    {
        if (name.StartsWith("action-", StringComparison.Ordinal))
            return new() { ActionID = (Msg.Action)uint.Parse(name[7..]), dwparam1 = 7, dwparam2 = 4001, dwparam3 = 3,
                Title = new() { ID = 4001, SubId = 3, dwparam1 = 100, dwparam2 = 8 } };
        return name switch
        {
            "outer-zero" => new(),
            "nested-zero" => new() { Title = new() },
            "max" => new() { ActionID = Msg.Action.UnEquip, dwparam1 = uint.MaxValue, dwparam2 = uint.MaxValue,
                dwparam3 = uint.MaxValue, Title = new() { ID = uint.MaxValue, SubId = uint.MaxValue,
                    dwparam1 = uint.MaxValue, dwparam2 = uint.MaxValue } },
            "unknown-two" => new() { ActionID = (Msg.Action)2 },
            "unknown-max" => new() { ActionID = (Msg.Action)uint.MaxValue },
            "null-outer" => null,
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }
}
