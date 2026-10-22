using System.Reflection;
using ProtoBuf;
using WolfConquer.Core.World;
using Message = WolfConquer.Core.World.LegacyNativeMsgMessage;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeSystemMessageContractTests
{
    // Actual Framework/x86 observations from a source-shaped independent probe,
    // object-typed Serialize and preserved protobuf-net2.0.0.668 SHA66653D78... .
    // Original game executables were not run. Null repeated strings are omitted.
    [Theory]
    [InlineData("zeros", "0800100018002000280030003800400048005000")]
    [InlineData("default-system", "088080FC0710D50F18002000280030003800400148005000720653595354454D7203414C4C72007225596F7520696E637265617365642074686520776561706F6E2070726F66696369656E637921720072007200")]
    [InlineData("short", "080710081809200A280B300C380D4063480E500F720141720142720143")]
    [InlineData("max", "08FFFFFFFF0F10FFFFFFFF0F18FFFFFFFF0F20FFFFFFFF0F28FFFFFFFF0F30FFFFFFFF0F38FFFFFFFF0F40FFFFFFFF0F48FFFFFFFF0F50FFFFFFFF0F720146720154720072014D72007200720153")]
    [InlineData("empty-array", "0800100018002000280030003800400048005000")]
    [InlineData("null-slots", "0800100018002000280030003800400048005000720072017872007200")]
    [InlineData("seven-empty", "08001000180020002800300038004001480050007200720072007200720072007200")]
    [InlineData("unicode", "08001000180020002800300038004000480050007204576F6C667203E78BBC7200720A636166C3A900F09F90BA72007200720153")]
    public void ConcreteNativeContractMatchesThePreservedSerializer(string name, string expected)
    {
        var proto = Query(name);
        var content = proto.Content;
        using var output = new MemoryStream();
        Serializer.Serialize((Stream)output, (object)proto);
        Assert.Equal(expected, Convert.ToHexString(output.ToArray()));
        Assert.Same(content, proto.Content);
    }

    [Fact]
    public void ContractRetainsEveryRequiredTagMutableFieldWidthAndNullArrayDefault()
    {
        var type = typeof(Message.MsgTalkProto);
        Assert.NotNull(type.GetCustomAttribute<ProtoContractAttribute>());
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "Color", "ChatType", "MessageUID1", "MessageUID2", "ServerID", "Mesh", "UnionType",
            "Count", "New1", "New2", "Content" }, fields.Select(field => field.Name));
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 14 },
            fields.Select(field => field.GetCustomAttribute<ProtoMemberAttribute>()!.Tag));
        foreach (var field in fields)
        {
            Assert.True(field.GetCustomAttribute<ProtoMemberAttribute>()!.IsRequired);
            Assert.Equal(field.Name == "Content" ? typeof(string[]) : typeof(uint), field.FieldType);
            Assert.False(field.IsInitOnly);
        }
        Assert.Null(new Message.MsgTalkProto().Content);
    }

    [Fact]
    public Task RealPacketFramesDefaultSystemPayloadExactlyWithoutRegistrationOrManualProjection() =>
        NativePacketTestThread.Run(() =>
        {
            using var fixture = new NativeSpellTestFixture();
            byte[] payload = Convert.FromHexString("088080FC0710D50F18002000280030003800400148005000720653595354454D7203414C4C72007225596F7520696E637265617365642074686520776561706F6E2070726F66696369656E637921720072007200");
            var message = new Message("You increased the weapon proficiency!", Message.MsgColor.red, Message.ChatMode.TopLeftSystem);
            Assert.Same(fixture.Packet, message.GetArray(fixture.Packet));
            byte[] expected = BitConverter.GetBytes((ushort)(payload.Length + 4))
                .Concat(BitConverter.GetBytes((ushort)2179)).Concat(payload).ToArray();
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(fixture.Packet));
            Assert.Equal(4, fixture.Packet.Position); // Native Finalize rewrites the header last.
            Assert.Equal(1u, message.proto.Count);
            Assert.Equal(7, message.proto.Content.Length);
        });

    private static Message.MsgTalkProto Query(string name) => name switch
    {
        "zeros" => new(),
        "default-system" => new() { Color = 16711680, ChatType = 2005, Count = 1,
            Content = ["SYSTEM", "ALL", "", "You increased the weapon proficiency!", "", "", ""] },
        "short" => new() { Color = 7, ChatType = 8, MessageUID1 = 9, MessageUID2 = 10,
            ServerID = 11, Mesh = 12, UnionType = 13, Count = 99, New1 = 14, New2 = 15, Content = ["A", "B", "C"] },
        "max" => new() { Color = uint.MaxValue, ChatType = uint.MaxValue, MessageUID1 = uint.MaxValue,
            MessageUID2 = uint.MaxValue, ServerID = uint.MaxValue, Mesh = uint.MaxValue, UnionType = uint.MaxValue,
            Count = uint.MaxValue, New1 = uint.MaxValue, New2 = uint.MaxValue, Content = ["F", "T", "", "M", "", "", "S"] },
        "empty-array" => new() { Content = [] },
        "null-slots" => new() { Content = [null!, "", null!, "x", null!, "", ""] },
        "seven-empty" => new() { Count = 1, Content = ["", "", "", "", "", "", ""] },
        "unicode" => new() { Content = ["Wolf", "狼", "", "café\0🐺", "", "", "S"] },
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };
}
