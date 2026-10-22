using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Replay = WolfConquer.Core.World.LegacyNativeNpcServerReplay;
using Query = WolfConquer.Core.World.LegacyNativeNpcServerQuery;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNpcServerReplayTests
{
    [Fact]
    public void QueryIsNativeSixteenByteSequentialValueWithAllSevenMutablePhysicalFieldsIncludingUnserializedStamp()
    {
        (string Name, Type Type, int Offset)[] expected =
        [
            ("Stamp", typeof(int), 0), ("ID", typeof(LegacyNativeNpcID), 4), ("Mesh", typeof(ushort), 8),
            ("OptionID", typeof(byte), 10), ("InteractType", typeof(byte), 11),
            ("Action", typeof(Replay.Mode), 12), ("NpcType", typeof(LegacyNativeNpcType), 14)
        ];
        Assert.True(typeof(Query).IsValueType);
        Assert.Equal(LayoutKind.Sequential, typeof(Query).StructLayoutAttribute!.Value);
        Assert.Equal(16, Marshal.SizeOf<Query>());
        FieldInfo[] fields = typeof(Query).GetFields(BindingFlags.Public | BindingFlags.Instance);
        Assert.Equal(expected.Length, fields.Length); Assert.Empty(typeof(Query).GetProperties());
        foreach (var row in expected)
        {
            FieldInfo field = Assert.Single(fields, field => field.Name == row.Name);
            Assert.Equal(row.Type, field.FieldType); Assert.False(field.IsInitOnly);
            Assert.Equal(row.Offset, Marshal.OffsetOf<Query>(row.Name).ToInt32());
        }
        var empty = default(Query);
        Assert.Equal(0, empty.Stamp); Assert.Equal((LegacyNativeNpcID)0u, empty.ID);
        Assert.Equal((ushort)0, empty.Mesh); Assert.Equal((byte)0, empty.OptionID); Assert.Equal((byte)0, empty.InteractType);
        Assert.Equal((Replay.Mode)0, empty.Action); Assert.Equal((LegacyNativeNpcType)0, empty.NpcType);
    }

    [Fact]
    public void ModeAndNpcTypeKeepCompleteNonFlagsDeclarationsAndExactUnsignedWidths()
    {
        Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(typeof(Replay.Mode)));
        Assert.Null(typeof(Replay.Mode).GetCustomAttribute<FlagsAttribute>());
        Assert.Equal(new[] { "Statue", "Cursor", "PlaceFurniture" }, Enum.GetNames<Replay.Mode>());
        Assert.Equal(new ushort[] { 3, 5, 6 }, Enum.GetValues<Replay.Mode>().Select(value => (ushort)value));
        Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(typeof(LegacyNativeNpcType)));
        Assert.Null(typeof(LegacyNativeNpcType).GetCustomAttribute<FlagsAttribute>());
        var expected = new Dictionary<string, ushort>
        {
            ["Stun"] = 0, ["Shop"] = 1, ["Talker"] = 2, ["Beautician"] = 5, ["Upgrader"] = 6,
            ["Socketer"] = 7, ["Pole"] = 10, ["Booth"] = 14, ["Gambling"] = 19, ["Stake"] = 21,
            ["Scarecrow"] = 22, ["Furniture"] = 25, ["Gate"] = 26, ["ClanInfo"] = 31, ["DialogAndGui"] = 32, ["Flag"] = 47
        };
        Assert.Equal(expected.Keys, Enum.GetNames<LegacyNativeNpcType>());
        foreach (var row in expected) Assert.Equal(row.Value, (ushort)Enum.Parse<LegacyNativeNpcType>(row.Key));
    }

    [Fact]
    public void WholeNpcIdCatalogRetainsAll619OriginalNamesValuesSixAliasesAndHighUnsignedIds()
    {
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(LegacyNativeNpcID)));
        Assert.Null(typeof(LegacyNativeNpcID).GetCustomAttribute<FlagsAttribute>());
        var fields = typeof(LegacyNativeNpcID).GetFields(BindingFlags.Public | BindingFlags.Static);
        Assert.Equal(619, fields.Length);
        Assert.Equal(613, fields.Select(field => (uint)field.GetRawConstantValue()!).Distinct().Count());
        // Pinned from the whole native NpcID.cs: ordinal name=value lines, LF, no terminal LF.
        // One bounded metadata control avoids copying 619 assertion lines or reading private source at test time.
        string canonical = string.Join('\n', fields.Select(field => field.Name + "=" +
            ((uint)field.GetRawConstantValue()!).ToString(CultureInfo.InvariantCulture)).OrderBy(value => value, StringComparer.Ordinal));
        Assert.Equal("16A783F0936AB81BC32C4AD13F3FEAD3A2E7596C35CFDCE35AFD50A6DCBE1117",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))));
        Assert.Equal(LegacyNativeNpcID.JiangMaker, LegacyNativeNpcID.Frouty);
        Assert.Equal(LegacyNativeNpcID.PokerMillionaireLee, LegacyNativeNpcID.PokerMillinaireLee);
        Assert.Equal(LegacyNativeNpcID.Crystal, LegacyNativeNpcID.Crystal1);
        Assert.Equal(LegacyNativeNpcID.PokerWarehouseman, LegacyNativeNpcID.WHPoker);
        Assert.Equal(LegacyNativeNpcID.KillerOfElite, LegacyNativeNpcID.Top2);
        Assert.Equal(LegacyNativeNpcID.ServerRewards, LegacyNativeNpcID.Top3);
        Assert.Equal(4_294_967_294u, (uint)LegacyNativeNpcID.SelectP7WeaponSoulPack);
        Assert.Equal(4_294_966_295u, (uint)LegacyNativeNpcID.L60UniqueGearPack);
        Assert.False(Enum.IsDefined((LegacyNativeNpcID)0u)); Assert.False(Enum.IsDefined((LegacyNativeNpcID)uint.MaxValue));
    }

    [Fact]
    public void BothCompleteCodecsExposeNativeQueryAndOutWidthsWithoutReceiverAttributes()
    {
        MethodInfo[] methods = typeof(Replay).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.Equal(new[] { "NpcDialog", "NpcServerCreate" }, methods.Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal));
        var reader = Assert.Single(methods, method => method.Name == "NpcDialog");
        Assert.Equal(typeof(LegacyNativePacket), reader.ReturnType);
        Assert.Equal(new[] { typeof(LegacyNativePacket), typeof(uint).MakeByRefType(), typeof(ushort).MakeByRefType(),
            typeof(byte).MakeByRefType(), typeof(byte).MakeByRefType(), typeof(Replay.Mode).MakeByRefType(), typeof(string).MakeByRefType() },
            reader.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.All(reader.GetParameters().Skip(1), parameter => Assert.True(parameter.IsOut));
        var writer = Assert.Single(methods, method => method.Name == "NpcServerCreate");
        Assert.Equal(typeof(LegacyNativePacket), writer.ReturnType);
        Assert.Equal(new[] { typeof(LegacyNativePacket), typeof(Query) }, writer.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.All(methods, method => Assert.DoesNotContain(method.GetCustomAttributes(), attribute => attribute.GetType().Name.Contains("PacketAttribute", StringComparison.Ordinal)));
    }
}
