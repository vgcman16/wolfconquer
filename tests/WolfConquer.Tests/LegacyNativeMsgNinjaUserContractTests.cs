using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using ProtoBuf;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Query = WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaUser;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeMsgNinjaUserContractTests
{
    // Actual owned Framework/x86 source-shaped controls, object Serialize API,
    // preserved protobuf-net2.0.0.668 SHA66653D78.... No native decoder exists.
    [Theory]
    [InlineData("default", "080010001800200028003000380040004800500058006000680070007800800100880100900100980100A00100A80100")]
    [InlineData("flag-zero", "080110001800200028003000380040004800500058006000680070007800800100880100900100980100A00100A80100")]
    [InlineData("flag-high", "0801100018FFFFFFFFFFFFFFFFFF01200028003000380040004800500058006000680070007800800100880100900100980100A00100A80100")]
    [InlineData("points-zero", "080210001800200028003000380040004800500058006000680070007800800100880100900100980100A00100A80100")]
    [InlineData("points-max", "08021000180020FFFFFFFF0F28003000380040004800500058006000680070007800800100880100900100980100A00100A80100")]
    [InlineData("loading-mixed", "0800107B180120022803300438054006480750085809600A680B700C787080010E88010F900110980111A00112A80113")]
    [InlineData("all-max", "08FFFFFFFF0F10FFFFFFFF0F18FFFFFFFFFFFFFFFFFF0120FFFFFFFF0F28FFFFFFFF0F30FFFFFFFF0F38FFFFFFFF0F40FFFFFFFF0F48FFFFFFFF0F50FFFFFFFF0F58FFFFFFFF0F60FFFFFFFF0F68FFFFFFFF0F70FFFFFFFF0F78FFFFFFFF0F8001FFFFFFFF0F8801FFFFFFFF0F9001FFFFFFFF0F9801FFFFFFFF0FA001FFFFFFFF0FA801FFFFFFFF0F")]
    [InlineData("null", "")]
    public Task ActualEncoderPreservesEveryPinnedRequiredZeroAndFullWidthField(string name, string hex) =>
        NativePacketTestThread.Run(() =>
        {
            using var fixture = new NativeSpellTestFixture();
            Fill(fixture.Packet);
            Assert.Same(fixture.Packet, fixture.Packet.CreateNinjaUser(Value(name)!));
            byte[] payload = Convert.FromHexString(hex);
            var expected = new byte[payload.Length + 4];
            BinaryPrimitives.WriteUInt16LittleEndian(expected, (ushort)expected.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2), 2395);
            payload.CopyTo(expected, 4);
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(fixture.Packet));
            Assert.Equal(4, fixture.Packet.Position);
            Assert.Equal(expected.Length, fixture.Packet.Size);
            Tail(fixture.Packet);
            Assert.Empty(fixture.Queue);
        });

    [Fact]
    public void CompleteContractKeepsRequiredMutableWidthsDefaultsEnumAndNativeConstant()
    {
        Assert.NotNull(typeof(Query).GetCustomAttribute<ProtoContractAttribute>());
        FieldInfo[] fields = Fields();
        Assert.Equal(new[] { "Type", "UID", "Flag", "Points", "Fire", "Water", "Earth", "Wind", "Lightning",
            "FireNew", "WaterNew", "EarthNew", "WindNew", "LightningNew", "Score", "FireMastery", "WaterMastery",
            "EarthMastery", "WindMastery", "LightningMastery", "UnlockMastery" }, fields.Select(field => field.Name));
        Assert.Equal(Enumerable.Range(1, 21), fields.Select(field => field.GetCustomAttribute<ProtoMemberAttribute>()!.Tag));
        var defaults = new Query();
        Assert.All(fields, field =>
        {
            Assert.False(field.IsInitOnly);
            Assert.Equal(field.Name == "Flag" ? typeof(ulong) : typeof(uint), field.FieldType);
            Assert.Equal(0UL, Convert.ToUInt64(field.GetValue(defaults)));
            Assert.True(field.GetCustomAttribute<ProtoMemberAttribute>()!.IsRequired);
        });
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(Query.TypeID)));
        Assert.Null(typeof(Query.TypeID).GetCustomAttribute<FlagsAttribute>());
        Assert.Equal(new[] { "Loading", "Flag", "Points" }, Enum.GetNames<Query.TypeID>());
        Assert.Equal(new[] { 0, 1, 2 }, Enum.GetValues<Query.TypeID>().Select(value => (int)value));
        Assert.Equal((ushort)2395, LegacyNativeMsgNinja.MsgNinjaUser_ID);
        Assert.DoesNotContain(typeof(LegacyNativeMsgNinja).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
            method => method.Name == "GetNinjaUser");
    }

    private static FieldInfo[] Fields() => typeof(Query).GetFields(BindingFlags.Instance | BindingFlags.Public)
        .OrderBy(field => field.MetadataToken).ToArray();

    private static Query? Value(string name)
    {
        if (name == "all-max")
        {
            var maximum = new Query();
            foreach (var field in Fields())
                field.SetValue(maximum, field.FieldType == typeof(ulong) ? (object)ulong.MaxValue : uint.MaxValue);
            return maximum;
        }
        return name switch
        {
            "default" => new(),
            "flag-zero" => new() { Type = 1 },
            "flag-high" => new() { Type = 1, Flag = ulong.MaxValue },
            "points-zero" => new() { Type = 2 },
            "points-max" => new() { Type = 2, Points = uint.MaxValue },
            "loading-mixed" => new() { UID = 123, Flag = 1, Points = 2, Fire = 3, Water = 4, Earth = 5,
                Wind = 6, Lightning = 7, FireNew = 8, WaterNew = 9, EarthNew = 10, WindNew = 11,
                LightningNew = 12, Score = 112, FireMastery = 14, WaterMastery = 15, EarthMastery = 16,
                WindMastery = 17, LightningMastery = 18, UnlockMastery = 19 },
            "null" => null,
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
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
