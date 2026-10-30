using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClanTests
{
    [Fact]
    public Task MemberWriterSerializesAllFieldsAndTheNativeFixedStringPrefix() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        var row = new LegacyNativeClanMember { Name = "狼A", UID = 123, Level = 130,
            Rank = LegacyNativeClanRanks.Leader, Online = true, Class = 145, Donation = 0x12345678 };
        p.Stream.ClanMembersCreate(new[] { row });
        byte[] bytes = NativeSpellTestFixture.Bytes(p.Stream);
        Assert.Equal(72, bytes.Length);
        Assert.Equal((ushort)72, BitConverter.ToUInt16(bytes));
        Assert.Equal((ushort)2049, BitConverter.ToUInt16(bytes, 2));
        Assert.Equal(4u, U32(bytes, 4));
        Assert.Equal(0u, U32(bytes, 8));
        Assert.Equal(0u, U32(bytes, 12));
        Assert.Equal(1u, U32(bytes, 16));
        // Native fixed writer copies min(string.Length, fixedLength) UTF8 bytes: two bytes, not the whole character.
        Assert.Equal(new byte[] { 0xE7, 0x8B }, bytes[20..22]);
        Assert.All(bytes[22..52], value => Assert.Equal((byte)0, value));
        Assert.Equal(130u, U32(bytes, 52));
        Assert.Equal((ushort)100, BitConverter.ToUInt16(bytes, 56));
        Assert.Equal((ushort)1, BitConverter.ToUInt16(bytes, 58));
        Assert.Equal(145u, U32(bytes, 60));
        Assert.Equal(1u, U32(bytes, 64));
        Assert.Equal(0x12345678u, U32(bytes, 68));
        Assert.Equal(4, p.Stream.Position);
    });

    [Fact]
    public Task EmptyMemberArrayKeepsGoldenAndLaterNullRowLeavesTheCompleteTargetUnchanged() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        p.Stream.ClanMembersCreate(Array.Empty<LegacyNativeClanMember>());
        Assert.Equal("1400010804000000000000000000000000000000", Convert.ToHexString(NativeSpellTestFixture.Bytes(p.Stream)));
        AssertClanPacketRefusal<ArgumentException>(p.Stream, () =>
            p.Stream.ClanMembersCreate(new[] { new LegacyNativeClanMember { Name = "A" }, null! }));
    });

    [Theory]
    [InlineData(false, "10000108170000004433221188776655")]
    [InlineData(true, "1100010817000000443322118877665500")]
    public Task CallbackNullOmitsCountWhileEmptyListWritesZeroCount(bool empty, string hex) => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        p.Stream.ClanCallBackCreate(LegacyNativeMsgClan.Info.Quit, 0x11223344, 0x55667788, empty ? Array.Empty<string>() : null);
        Assert.Equal(hex, Convert.ToHexString(NativeSpellTestFixture.Bytes(p.Stream)));
    });

    [Fact]
    public Task RelationAndSingleClientWritersUseNativeStringListContractAndRoundTripParser() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        p.Stream.ClanRelationCreate(0x11223344, "A", "狼", LegacyNativeMsgClan.Info.AddAlly);
        Assert.Equal("19000108130000004433221100000000020100410300E78BBC",
            Convert.ToHexString(NativeSpellTestFixture.Bytes(p.Stream)));
        p.Stream.Seek(4);
        p.Stream.GetClan(out var mode, out var uid, out var parameter, out var list);
        Assert.Equal(LegacyNativeMsgClan.Info.AddAlly, mode);
        Assert.Equal(0x11223344u, uid);
        Assert.Equal(0u, parameter);
        Assert.Equal(new[] { "A", "狼" }, list);
        p.Stream.ClanAppendSingleClientCreate(9, "Wolf", LegacyNativeMsgClan.Info.RequestAlly);
        Assert.Equal("17000108110000000900000000000000010400576F6C66",
            Convert.ToHexString(NativeSpellTestFixture.Bytes(p.Stream)));
    });

    [Theory]
    [InlineData("None", false)]
    [InlineData("none", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public Task BulletinGuardOnlySuppressesExactNoneAndUsesPlayerClanUid(string? text, bool sends) => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        var clan = Clan(99, "Wolf");
        clan.ClanBuletin = text;
        p.Client.Player.ClanUID = 7;
        clan.SendBuletin(p.Stream, p.Client);
        Assert.Equal(sends ? 1 : 0, p.Queue.Count);
        if (!sends) return;
        var bytes = p.Queue.Dequeue();
        Assert.Equal(25u, U32(bytes, 4));
        Assert.Equal(7u, U32(bytes, 8));
        Assert.Equal(0u, U32(bytes, 12));
        Assert.Equal((byte)1, bytes[16]);
        Assert.Equal((ushort)(text?.Length ?? 0), BitConverter.ToUInt16(bytes, 17));
    });

    [Fact]
    public Task ParserStartsAtCurrentCursorAndRetainsUnknownModeBits() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        p.Stream.InitWriter();
        p.Stream.Write(0xDEADBEEFu);
        p.Stream.Write(7u);
        p.Stream.Write(9u);
        p.Stream.Write(11u);
        p.Stream.Write(new[] { "Tail" });
        p.Stream.Size = p.Stream.Position; // Actual completed initialized fixture extent.
        p.Stream.Seek(8);
        p.Stream.GetClan(out var mode, out var uid, out var parameter, out var list);
        Assert.Equal((LegacyNativeMsgClan.Info)7, mode);
        Assert.Equal(9u, uid);
        Assert.Equal(11u, parameter);
        Assert.Equal(new[] { "Tail" }, list);
    });

    [Fact]
    public Task MissingMemberArrayPreservesTheCompleteTargetBeforePreparation() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        AssertClanPacketRefusal<ArgumentNullException>(p.Stream, () => p.Stream.ClanMembersCreate(null!));
    });

    [Fact]
    public Task BulletinMissingOwnersPreserveTheCompleteTarget() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        AssertClanPacketRefusal<ArgumentNullException>(p.Stream, () => p.Stream.ClanBulletinCreate(null!, Clan(8, "Wolf")));
        p.Client.Player.ClanUID = 7;
        AssertClanPacketRefusal<ArgumentNullException>(p.Stream, () => p.Stream.ClanBulletinCreate(p.Client, null!));
    });

    private static unsafe void AssertClanPacketRefusal<T>(LegacyNativePacket packet, Action action) where T : Exception
    {
        byte[] before = new byte[packet.Capacity];
        Marshal.Copy((nint)packet.Memory, before, 0, before.Length);
        nuint cursor = (nuint)packet.stream;
        int size = packet.Size;
        Assert.Throws<T>(action);
        Assert.Equal(cursor, (nuint)packet.stream);
        Assert.Equal(size, packet.Size);
        byte[] after = new byte[before.Length];
        Marshal.Copy((nint)packet.Memory, after, 0, after.Length);
        Assert.Equal(before, after);
    }
}
