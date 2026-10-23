using System.Text;
using WolfConquer.Core.Data;
using Attr = WolfConquer.Core.World.LegacyNativeChiRuntime.ChiAttribute;
using Power = WolfConquer.Core.World.LegacyNativeChiRuntime.ChiPower;
using Kind = WolfConquer.Core.World.LegacyNativeMsgChiInfo.ChiPowerType;
using Stat = WolfConquer.Core.World.LegacyNativeChiAttribute;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeChiTests
{
    [Fact]
    public Task NativeChiAttributeBinaryLayoutIsByteTypeLittleEndianUshortAndBoolean() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var attr = new Attr((Stat)511, 0xA1B2, true);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true)) attr.Serialize(writer);
        Assert.Equal(Convert.FromHexString("FFB2A101"), buffer.ToArray());
        Assert.Equal(255 * 10000 + 0xA1B2, (int)attr);
        buffer.Position = 0;
        using var reader = new BinaryReader(buffer, Encoding.UTF8, leaveOpen: true);
        var read = new Attr(Stat.HPAdd, 7, false);
        read.Deserialize(reader);
        Assert.Equal((Stat)255, read.Type); Assert.Equal((ushort)0xA1B2, read.Value); Assert.True(read.Locked);
        Assert.Equal(4L, buffer.Position);
        Assert.Equal((Stat)511, attr.Type); // Serialization does not normalize source enum storage.
    });

    [Fact]
    public Task DeserializeFaultsKeepOnlyFieldsAlreadyReadAndDoNotRollbackStruct() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var attr = new Attr(Stat.HPAdd, 77, true);
        using var buffer = new MemoryStream(new byte[] { 3, 0x34, 0x12 });
        using var reader = new BinaryReader(buffer);
        Assert.Throws<EndOfStreamException>(() => attr.Deserialize(reader));
        Assert.Equal(Stat.Immunity, attr.Type); Assert.Equal((ushort)0x1234, attr.Value); Assert.True(attr.Locked);
        attr = new Attr(Stat.HPAdd, 77, true);
        using var shortBuffer = new MemoryStream(new byte[] { 4, 0x34 });
        using var shortReader = new BinaryReader(shortBuffer);
        Assert.Throws<EndOfStreamException>(() => attr.Deserialize(shortReader));
        Assert.Equal(Stat.Breakthrough, attr.Type); Assert.Equal((ushort)77, attr.Value); Assert.True(attr.Locked);
    });

    [Fact]
    public Task AttributeShallowCopyIsDistinctArrayOfValueCopiesAndKeepsNullFault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var source = new[] { new Attr(Stat.HPAdd, 3000, true), new Attr((Stat)999, ushort.MaxValue, false) };
        var copy = Attr.ShallowCopy(source);
        Assert.NotSame(source, copy); Assert.Equal(source, copy);
        source[0].Value = 1; copy[1].Locked = true;
        Assert.Equal((ushort)3000, copy[0].Value); Assert.False(source[1].Locked);
        Assert.Empty(Attr.ShallowCopy(Array.Empty<Attr>()));
        Assert.Throws<NullReferenceException>(() => Attr.ShallowCopy(null!));
    });

    [Fact]
    public Task PowerTextSerializesNativeTrailingSlashTruncatedTypeAndAllCurrentSlots() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var power = new Power((Kind)255)
        {
            UID = 7, Name = "not serialized", Rank = 999, UnLocked = true, Exp = uint.MaxValue,
            Attributes = new[] { new Attr((Stat)511, ushort.MaxValue, true), new Attr(Stat.HPAdd, 1001, false) },
        };
        Assert.Equal("1/4294967295/255/65535/1/6/1001/0/", power.ToString());
        power.UnLocked = false;
        Assert.Equal("0/4294967295/255/65535/1/6/1001/0/", power.ToString());
        power.Attributes = Array.Empty<Attr>(); Assert.Equal("0/4294967295/", power.ToString());
        power.Attributes = null!; Assert.Throws<NullReferenceException>(() => power.ToString());
    });

    [Fact]
    public Task LoadLockedPowerUpdatesIdentityAndUnlockOnlyRetainingOldExperienceAndAttributes() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var power = MinimumPower(Kind.Turtle, 7, 9); power.Exp = 123;
        var attributes = power.Attributes;
        power.Load("0/999/7/500/1/", 88, null!);
        Assert.Equal(88u, power.UID); Assert.Null(power.Name); Assert.False(power.UnLocked);
        Assert.Equal(123u, power.Exp); Assert.Same(attributes, power.Attributes);
        Assert.All(attributes, attr => { Assert.Equal(Stat.CriticalStrike, attr.Type); Assert.Equal((ushort)10, attr.Value); Assert.False(attr.Locked); });
        Assert.Equal(9, power.Rank); Assert.Equal(Kind.Turtle, power.Type);
        power.Load("1", 99, "single-token"); // ReadLine requires a slash, so remains locked.
        Assert.False(power.UnLocked); Assert.Equal(123u, power.Exp); Assert.Equal("single-token", power.Name);
        power.Load(null!, 100, "null-line"); Assert.False(power.UnLocked); Assert.Equal(100u, power.UID);
    });

    [Fact]
    public Task LoadConsumesExactByteAndUshortFieldsWithDefaultOnBadTokensAndMissingTail() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var power = MinimumPower(Kind.Dragon); power.Exp = 88;
        power.Load("1/4294967295/255/65535/2/256/65536/1/7/-1/255/", 23, "loaded");
        Assert.True(power.UnLocked); Assert.Equal(uint.MaxValue, power.Exp);
        Assert.Equal((Stat)255, power.Attributes[0].Type); Assert.Equal(ushort.MaxValue, power.Attributes[0].Value); Assert.True(power.Attributes[0].Locked);
        Assert.Equal(Stat.None, power.Attributes[1].Type); Assert.Equal((ushort)0, power.Attributes[1].Value); Assert.True(power.Attributes[1].Locked);
        Assert.Equal(Stat.AddAttack, power.Attributes[2].Type); Assert.Equal((ushort)0, power.Attributes[2].Value); Assert.True(power.Attributes[2].Locked);
        Assert.Equal(default(Attr), power.Attributes[3]);
        Assert.Equal(23u, power.UID); Assert.Equal("loaded", power.Name);
        power.Load("2/9/", 24, "not-one"); Assert.False(power.UnLocked); Assert.Equal(uint.MaxValue, power.Exp);
    });

    [Fact]
    public Task LoadNullAttributeArrayKeepsIdentityUnlockAndExperiencePrefixBeforeNativeFault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var power = new Power(Kind.Phoenix) { Attributes = null!, Exp = 1, Rank = 5 };
        Assert.Throws<NullReferenceException>(() => power.Load("1/77/", 99, "prefix"));
        Assert.Equal(99u, power.UID); Assert.Equal("prefix", power.Name); Assert.True(power.UnLocked);
        Assert.Equal(77u, power.Exp); Assert.Equal(5, power.Rank); Assert.Null(power.Attributes);
    });

    [Fact]
    public Task RetreatedPowersTextHasFixedFourSlotsFullIntTypesAndIgnoresLockedOrExtraSlots() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var powers = new[] { new Attr((Stat)511, 65535, true), new Attr(Stat.HPAdd, 1001, false), new Attr(Stat.None, 0, true), new Attr(Stat.LukyStrike, 4, false), new Attr(Stat.AddAttack, 999, true) };
        Assert.Equal("511-65535|6-1001|0-0|14-4", LegacyNativeChiTable.PowersToString(powers));
        Assert.Equal("", LegacyNativeChiTable.PowersToString(null!));
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeChiTable.PowersToString(Array.Empty<Attr>()));
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeChiTable.PowersToString(powers[..3]));
        var read = LegacyNativeChiTable.ReadPowers("511-65535|6-1001|0-0|14-4")!;
        Assert.Equal(4, read.Length); Assert.Equal((Stat)511, read[0].Type); Assert.Equal(ushort.MaxValue, read[0].Value);
        Assert.All(read, attr => Assert.False(attr.Locked));
        Assert.True(powers[0].Locked);
    });

    [Fact]
    public Task RetreatedReadKeepsNativeShortInputDefaultsAndMalformedOrOverlongFaults() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Null(LegacyNativeChiTable.ReadPowers(null!)); Assert.Null(LegacyNativeChiTable.ReadPowers(""));
        var shortRead = LegacyNativeChiTable.ReadPowers("7-500")!;
        Assert.Equal(4, shortRead.Length); Assert.Equal(Stat.AddAttack, shortRead[0].Type); Assert.Equal((ushort)500, shortRead[0].Value);
        Assert.Equal(default(Attr), shortRead[3]);
        Assert.Throws<FormatException>(() => LegacyNativeChiTable.ReadPowers("-1-5"));
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeChiTable.ReadPowers("7"));
        Assert.Throws<OverflowException>(() => LegacyNativeChiTable.ReadPowers("7-65536"));
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeChiTable.ReadPowers("1-1|2-2|3-3|4-4|5-5"));
        Assert.Throws<FormatException>(() => LegacyNativeChiTable.ReadPowers("7-5|"));
    });
}
