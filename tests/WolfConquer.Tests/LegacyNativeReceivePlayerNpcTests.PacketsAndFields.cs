using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeReceivePlayerNpcTests
{
    [Fact]
    public Task TeamArenaScoreCodecWritesExactCompleteScalarFrame() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var returned = scope.Packet.TeamArenaMatchScoreCreate(17, 2, "WolfOne", 4000, 18, 9, "WolfTwo", uint.MaxValue);
        Assert.Same(scope.Packet, returned);
        Assert.Equal(96, scope.Packet.Size);
        Assert.Equal(4, scope.Packet.Position);
        var expected = new byte[96];
        BitConverter.GetBytes((ushort)96).CopyTo(expected, 0);
        BitConverter.GetBytes((ushort)2190).CopyTo(expected, 2);
        BitConverter.GetBytes(17u).CopyTo(expected, 4);
        BitConverter.GetBytes(2u).CopyTo(expected, 8);
        Encoding.ASCII.GetBytes("WolfOne").CopyTo(expected, 12);
        BitConverter.GetBytes(4000u).CopyTo(expected, 44);
        BitConverter.GetBytes(18u).CopyTo(expected, 48);
        BitConverter.GetBytes(9u).CopyTo(expected, 52);
        Encoding.ASCII.GetBytes("WolfTwo").CopyTo(expected, 56);
        BitConverter.GetBytes(uint.MaxValue).CopyTo(expected, 88);
        BitConverter.GetBytes(1u).CopyTo(expected, 92);
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
    });

    [Theory]
    [InlineData("", new byte[] { })]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789", new byte[] { 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 49, 50, 51, 52, 53, 54 })]
    [InlineData("é你", new byte[] { 195, 169 })]
    public Task TeamArenaScoreRetainsNativeFixedStringLengthAndUtf8PrefixQuirk(string name, byte[] prefix) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Packet.TeamArenaMatchScoreCreate(0, 0, name, 0, 0, 0, name, 0);
        var bytes = NativeSpellTestFixture.Bytes(scope.Packet);
        Assert.Equal(96, bytes.Length);
        Assert.Equal(prefix, bytes.Skip(12).Take(prefix.Length));
        Assert.All(bytes.Skip(12 + prefix.Length).Take(32 - prefix.Length), b => Assert.Equal((byte)0, b));
        Assert.Equal(bytes.Skip(12).Take(32), bytes.Skip(56).Take(32));
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, 92));
    });

    [Theory]
    [InlineData(true, 12)]
    [InlineData(false, 56)]
    public Task TeamArenaScoreNullNameFaultRetainsNativeWrittenPrefixAndUnfinalizedSize(bool first, int position) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Packet.Size = 71;
        Assert.Throws<NullReferenceException>(() => scope.Packet.TeamArenaMatchScoreCreate(17, 2, first ? null! : "First", 3,
            18, 4, first ? "Second" : null!, 5));
        Assert.Equal(position, scope.Packet.Position);
        Assert.Equal(71, scope.Packet.Size);
        unsafe
        {
            Assert.Equal(17u, *(uint*)(scope.Packet.Memory + 4));
            Assert.Equal(2u, *(uint*)(scope.Packet.Memory + 8));
            Assert.Equal(0u, *(uint*)scope.Packet.Memory); // Finalize did not run.
        }
    });

    [Fact]
    public Task TeamArenaScoreUsesActualNativeSealLifecycleWithoutChangingReportedPayloadLength() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        LegacyNativePacket.SealString = "CQWOLF!!";
        scope.Packet.TeamArenaMatchScoreCreate(1, 2, "A", 3, 4, 5, "B", 6);
        var bytes = NativeSpellTestFixture.Bytes(scope.Packet);
        Assert.Equal(104, bytes.Length);
        Assert.Equal((ushort)96, BitConverter.ToUInt16(bytes, 0));
        Assert.Equal((ushort)2190, Opcode(bytes));
        Assert.Equal(Encoding.ASCII.GetBytes("CQWOLF!!"), bytes.Skip(96));
    });

    [Fact]
    public Task ReceivePhysicalSlotsRetainNativeZeroAndIndependentWritableState() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var p = scope.Client.Player;
        Assert.Equal(0, p.CapriceStamp.Value);
        Assert.Equal(0, p.BloodTideStamp.Value);
        Assert.Equal(0, p.DuelEndStamp.Value);
        Assert.Equal(0, p.TidalWaveStamp.Value);
        Assert.Equal(0, p.QuenchStamp.Value);
        Assert.Equal(0u, p.LightningShieldLeft);
        Assert.Equal(0u, p.SkillTournamentLifes);
        Assert.Equal(0u, p.DragonWarHits);
        Assert.Equal(0u, p.DragonWarScore);
        Assert.False(p.EagleEyeCountDown);
        p.CapriceStamp = new LegacyNativeTime32(-7);
        p.BloodTideStamp = new LegacyNativeTime32(11);
        p.DuelEndStamp = new LegacyNativeTime32(19);
        p.TidalWaveStamp = new LegacyNativeTime32(23);
        p.QuenchStamp = new LegacyNativeTime32(29);
        p.LightningShieldLeft = uint.MaxValue;
        p.SkillTournamentLifes = 3;
        p.DragonWarHits = 5;
        p.DragonWarScore = 71;
        p.EagleEyeCountDown = true;
        Assert.Equal(new[] { -7, 11, 19, 23, 29 },
            new[] { p.CapriceStamp.Value, p.BloodTideStamp.Value, p.DuelEndStamp.Value, p.TidalWaveStamp.Value, p.QuenchStamp.Value });
        Assert.Equal(uint.MaxValue, p.LightningShieldLeft);
        Assert.Equal(3u, p.SkillTournamentLifes);
        Assert.Equal(5u, p.DragonWarHits);
        Assert.Equal(71u, p.DragonWarScore);
        Assert.True(p.EagleEyeCountDown);
    });
}
