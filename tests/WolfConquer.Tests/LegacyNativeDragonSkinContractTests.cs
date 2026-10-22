using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using ProtoBuf;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Query = WolfConquer.Core.World.LegacyNativeDragonSkin.MsgDragonSkin;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeDragonSkinContractTests
{
    // Actual owned Framework/x86 source-shaped controls with the preserved
    // protobuf-net2.0.0.668 serializer SHA66653D78..., object Serialize API.
    [Theory]
    [InlineData("default", "")]
    [InlineData("restore", "080510112001")]
    [InlineData("my-show", "10111809")]
    [InlineData("all-max", "080610FFFFFFFF0F18FFFFFFFF0F2001")]
    [InlineData("unknown-seven", "0807")]
    [InlineData("unknown-negative", "08FFFFFFFFFFFFFFFFFF01")]
    [InlineData("unknown-max", "08FFFFFFFF07")]
    [InlineData("null", "")]
    public Task ActualCreateAndGetKeepPinnedOptionalFieldsUnknownFlagsAndNativeCursors(string name, string hex) =>
        NativePacketTestThread.Run(() =>
        {
            using var fixture = new NativeSpellTestFixture();
            Fill(fixture.Packet);
            Query? source = Value(name);
            Assert.Same(fixture.Packet, fixture.Packet.CreateAnimaSkin(source!));
            byte[] expected = Frame(hex);
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(fixture.Packet));
            Assert.Equal(expected.Length, fixture.Packet.Size);
            Assert.Equal(4, fixture.Packet.Position);
            Tail(fixture.Packet);
            Assert.Empty(fixture.Queue);

            // Native Get resets to the header, ignores its opcode, consumes the
            // declared body only and does not include trailing supplied bytes.
            byte[] input = expected.Concat(new byte[] { 0xA5, 0x5A }).ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(input.AsSpan(2), 0x7AAA);
            using var packet = new LegacyNativePacket(input);
            packet.Seek(3);
            packet.GetDragonSkin(out Query result);
            Query wanted = source ?? new();
            Assert.NotNull(result);
            Assert.NotSame(source, result);
            Assert.Equal(wanted.Type, result.Type);
            Assert.Equal(wanted.UserID, result.UserID);
            Assert.Equal(wanted.SkinType, result.SkinType);
            Assert.Equal(wanted.OK, result.OK);
            Assert.Equal(expected.Length, packet.Position);
            Assert.Equal(input.Length, packet.Size);
        });

    [Fact]
    public void ContractRetainsAllFourOptionalMutableFieldsAndSignedFlagsEnum()
    {
        Assert.NotNull(typeof(Query).GetCustomAttribute<ProtoContractAttribute>());
        FieldInfo[] fields = typeof(Query).GetFields(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "Type", "UserID", "SkinType", "OK" }, fields.Select(field => field.Name));
        Assert.Equal(new[] { typeof(Query.TypeID), typeof(uint), typeof(uint), typeof(bool) }, fields.Select(field => field.FieldType));
        Assert.Equal(new[] { 1, 2, 3, 4 }, fields.Select(field => field.GetCustomAttribute<ProtoMemberAttribute>()!.Tag));
        Assert.All(fields, field =>
        {
            Assert.False(field.IsInitOnly);
            Assert.False(field.GetCustomAttribute<ProtoMemberAttribute>()!.IsRequired);
        });
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(Query.TypeID)));
        Assert.NotNull(typeof(Query.TypeID).GetCustomAttribute<FlagsAttribute>());
        Assert.Equal(Enumerable.Range(0, 7), Enum.GetValues<Query.TypeID>().Select(value => (int)value));
        var defaults = new Query();
        Assert.Equal(Query.TypeID.MyShow, defaults.Type);
        Assert.Equal(0u, defaults.UserID); Assert.Equal(0u, defaults.SkinType); Assert.False(defaults.OK);
        Assert.DoesNotContain(typeof(LegacyNativeDragonSkin).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
            method => method.Name == "Process");
    }

    [Fact]
    public void NullStreamGetPublishesFreshDefaultOutputBeforeDereferenceFault()
    {
        var old = new Query { UserID = 17, SkinType = 9, OK = true };
        Query output = old;
        Assert.Throws<NullReferenceException>(() => LegacyNativeDragonSkin.GetDragonSkin(null!, out output));
        Assert.NotSame(old, output);
        Assert.Equal(Query.TypeID.MyShow, output.Type);
        Assert.Equal(0u, output.UserID); Assert.Equal(0u, output.SkinType); Assert.False(output.OK);
        Assert.Equal(17u, old.UserID); Assert.Equal(9u, old.SkinType); Assert.True(old.OK);
    }

    [Fact]
    public void GenuineIdictionaryRemoveDispatchRetainsAliasesAndMissingKeyAndNullFaults()
    {
        var row = new LegacyNativeAnimaSkinRuntime.Item { ID = 777, isEquip = true };
        IDictionary<uint, LegacyNativeAnimaSkinRuntime.Item> rows =
            new System.Collections.Concurrent.ConcurrentDictionary<uint, LegacyNativeAnimaSkinRuntime.Item>();
        rows[17] = row;
        LegacyNativeSystemExtensions.Remove(rows, 17u);
        Assert.Empty(rows);
        Assert.Equal(777u, row.ID); Assert.True(row.isEquip);
        LegacyNativeSystemExtensions.Remove(rows, 17u);
        Assert.Empty(rows);
        Assert.Throws<NullReferenceException>(() =>
            LegacyNativeSystemExtensions.Remove<uint, LegacyNativeAnimaSkinRuntime.Item>(null!, 17));
    }

    private static Query? Value(string name) => name switch
    {
        "default" => new(),
        "restore" => new() { Type = Query.TypeID.Restore, UserID = 17, OK = true },
        "my-show" => new() { UserID = 17, SkinType = 9 },
        "all-max" => new() { Type = Query.TypeID.Effect, UserID = uint.MaxValue, SkinType = uint.MaxValue, OK = true },
        "unknown-seven" => new() { Type = (Query.TypeID)7 },
        "unknown-negative" => new() { Type = (Query.TypeID)(-1) },
        "unknown-max" => new() { Type = (Query.TypeID)int.MaxValue },
        "null" => null,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static byte[] Frame(string hex)
    {
        byte[] payload = Convert.FromHexString(hex);
        byte[] result = new byte[payload.Length + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(result, (ushort)result.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(2), 2415);
        payload.CopyTo(result, 4);
        return result;
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
