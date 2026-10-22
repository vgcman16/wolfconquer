using System.Reflection;
using System.Text;
using ProtoBuf;
using WolfConquer.Core.World;
using Query = WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaInfo;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaInfoBehaviorTests
{
    [Fact]
    public void InfoContractKeepsAllRequiredMutableWidthsDefaultsAndNativeEnumGaps()
    {
        Assert.NotNull(typeof(Query).GetCustomAttribute<ProtoContractAttribute>());
        var fields = typeof(Query).GetFields(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "Type", "ID", "Points", "Position" }, fields.Select(field => field.Name));
        Assert.Equal(new[] { typeof(Query.TypeID), typeof(uint), typeof(uint), typeof(uint) }, fields.Select(field => field.FieldType));
        Assert.Equal(new[] { 1, 2, 3, 4 }, fields.Select(field => field.GetCustomAttribute<ProtoMemberAttribute>()!.Tag));
        var defaults = new Query();
        Assert.All(fields, field =>
        {
            Assert.False(field.IsInitOnly); Assert.True(field.GetCustomAttribute<ProtoMemberAttribute>()!.IsRequired);
            Assert.Equal(0UL, Convert.ToUInt64(field.GetValue(defaults)));
        });
        Assert.NotNull(typeof(Query.TypeID).GetCustomAttribute<FlagsAttribute>());
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(Query.TypeID)));
        Assert.Equal(new[] { "Equip", "Unequip", "UpLevel", "BoostAptitude", "SaveDiscard", "Hat", "AddPoints",
            "AddMastery", "InstantUpgrade", "UnlockMastery" }, Enum.GetNames<Query.TypeID>());
        Assert.Equal(new[] { 0, 1, 2, 4, 5, 6, 7, 8, 9, 11 }, Enum.GetValues<Query.TypeID>().Select(value => (int)value));
        Assert.False(Enum.IsDefined((Query.TypeID)3)); Assert.False(Enum.IsDefined((Query.TypeID)10));
        Assert.Equal(Query.TypeID.Equip, defaults.Type);
        Assert.Equal((ushort)2394, LegacyNativeMsgNinja.MsgNinjaInfo_ID);
        Assert.DoesNotContain(typeof(Query).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
            method => method.Name == "Process");
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    [InlineData("ar-EG")]
    public void PrintPacketUsesTwoDigitUpperHexTrailingSpacesAndLiteralLfPair(string culture)
    {
        using var scope = new ConsoleScope(culture);
        byte[] input = [0, 1, 9, 10, 15, 16, 127, 128, 254, 255];
        byte[] before = input.ToArray();
        Query.PrintPacket(input);
        Assert.Equal("00 01 09 0A 0F 10 7F 80 FE FF \n\n", scope.Writer.ToString());
        Assert.Equal(before, input);
        Assert.Equal("<NOT-LF>", scope.Writer.NewLine);
    }

    [Fact]
    public void PrintPacketEmptyArrayStillWritesExactlyTwoLiteralLfs()
    {
        using var scope = new ConsoleScope();
        Query.PrintPacket([]);
        Assert.Equal("\n\n", scope.Writer.ToString());
    }

    [Fact]
    public void PrintPacketNullFaultsBeforeAnyHexOrNewlineIsWritten()
    {
        using var scope = new ConsoleScope();
        Assert.Throws<NullReferenceException>(() => Query.PrintPacket(null!));
        Assert.Empty(scope.Writer.ToString());
    }

    [Fact]
    public void PrintPacketDoesNotGroupOrTruncateAnyByteAcrossTheWholeByteDomain()
    {
        using var scope = new ConsoleScope();
        byte[] input = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        const string digits = "0123456789ABCDEF";
        var expected = new StringBuilder();
        foreach (byte value in input) expected.Append(digits[value >> 4]).Append(digits[value & 15]).Append(' ');
        expected.Append("\n\n");
        Query.PrintPacket(input);
        Assert.Equal(expected.ToString(), scope.Writer.ToString());
        Assert.Equal(770, scope.Writer.ToString().Length);
    }
}
