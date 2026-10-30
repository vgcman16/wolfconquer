using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Corrected-only controls. Never execute these tiny-capacity cases against the
// old builders or use an accepting stand-in for an actual native client owner.
public sealed partial class LegacyNativeClanTests
{
    [Theory]
    [InlineData("name")]
    [InlineData("leader")]
    [InlineData("single")]
    public Task RequiredClanNamesRefuseBeforeChangingAnExistingFrame(string missing) => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        p.Stream.ClanCallBackCreate(LegacyNativeMsgClan.Info.Quit, 17, 23, ["before"]);
        AssertClanPacketRefusal<ArgumentNullException>(p.Stream, () =>
        {
            if (missing == "single") p.Stream.ClanAppendSingleClientCreate(9, null!, LegacyNativeMsgClan.Info.RequestAlly);
            else p.Stream.ClanRelationCreate(9, missing == "name" ? null! : "A",
                missing == "leader" ? null! : "B", LegacyNativeMsgClan.Info.AddAlly);
        });
        Assert.Empty(p.Queue);
    });

    [Fact]
    public Task MissingLateMemberNameDoesNotPublishEarlierRowsOrMutateTheSourceArray() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        var first = new LegacyNativeClanMember { Name = "A", Donation = 91, Level = 17 };
        var last = new LegacyNativeClanMember { Name = null!, Donation = 92, Class = 145 };
        var rows = new[] { first, last };
        p.Stream.ClanCallBackCreate(LegacyNativeMsgClan.Info.Quit, 17, 23, ["before"]);
        AssertClanPacketRefusal<ArgumentException>(p.Stream, () => p.Stream.ClanMembersCreate(rows));
        Assert.Same(first, rows[0]); Assert.Same(last, rows[1]);
        Assert.Equal("A", first.Name); Assert.Equal(91u, first.Donation); Assert.Equal((byte)17, first.Level);
        Assert.Null(last.Name); Assert.Equal(92u, last.Donation); Assert.Equal(145u, last.Class);
        Assert.Empty(p.Queue);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task BulletinRejectsMissingOrForeignPhysicalPlayerBeforeChangingTheTarget(bool foreign) => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        var original = p.Client.Player;
        var other = foreign ? new LegacyNativeGameClientRuntime() : null;
        p.Client.Player = foreign ? other!.Player : null!;
        try
        {
            if (foreign) AssertClanPacketRefusal<InvalidOperationException>(p.Stream, () => p.Stream.ClanBulletinCreate(p.Client, Clan(9, "Wolf")));
            else AssertClanPacketRefusal<ArgumentNullException>(p.Stream, () => p.Stream.ClanBulletinCreate(p.Client, Clan(9, "Wolf")));
            Assert.Same(other?.Player, p.Client.Player);
            Assert.Empty(p.Queue);
        }
        finally { p.Client.Player = original; }
    });

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public Task MemberCapacityFailureAfterPreparingValidRowsLeavesTargetAndSourceUnchanged(int count) => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        var rows = Enumerable.Range(0, count).Select(index => new LegacyNativeClanMember
            { Name = "A", Donation = (uint)(91 + index), Level = 17 }).ToArray();
        var references = (LegacyNativeClanMember[])rows.Clone();
        using var packet = InitializedClanTarget(Enumerable.Repeat((byte)0xA5, 20 + 52 * count - 1).ToArray());
        packet.Size = 3; packet.Seek(2);
        AssertClanPacketRefusal<InvalidOperationException>(packet, () => packet.ClanMembersCreate(rows));
        for (int index = 0; index < rows.Length; index++)
        {
            Assert.Same(references[index], rows[index]); Assert.Equal("A", rows[index].Name);
            Assert.Equal((uint)(91 + index), rows[index].Donation); Assert.Equal((byte)17, rows[index].Level);
        }
        Assert.Empty(p.Queue);
    });

    [Theory]
    [InlineData("members")]
    [InlineData("bulletin")]
    [InlineData("relation")]
    [InlineData("single")]
    [InlineData("callback")]
    public Task EveryClanBuilderPreservesTheWholeTargetWhenTheCompleteFrameCannotFit(string builder) => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        p.Client.Player.ClanUID = 7;
        byte[] golden = ClanBuilderGolden(builder);
        using var packet = InitializedClanTarget(Enumerable.Repeat((byte)0xA5, golden.Length - 1).ToArray());
        packet.Size = 3; packet.Seek(2);
        AssertClanPacketRefusal<InvalidOperationException>(packet, () => BuildClanPacket(builder, packet, p.Client));
        Assert.Equal(7u, p.Client.Player.ClanUID);
        Assert.Empty(p.Queue);
    });

    [Theory]
    [InlineData("members", false)] [InlineData("members", true)]
    [InlineData("bulletin", false)] [InlineData("bulletin", true)]
    [InlineData("relation", false)] [InlineData("relation", true)]
    [InlineData("single", false)] [InlineData("single", true)]
    [InlineData("callback", false)] [InlineData("callback", true)]
    public Task ExactCapacityClanFramesKeepIndependentBytesAndTheExistingSeal(string builder, bool sealedFrame) => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        using var seal = new ClanPacketSealScope(sealedFrame);
        p.Client.Player.ClanUID = 7;
        byte[] golden = ClanBuilderGolden(builder);
        byte[] expected = sealedFrame ? golden.Concat("TQServer"u8.ToArray()).ToArray() : golden;
        using var packet = InitializedClanTarget(Enumerable.Repeat((byte)0xA5, expected.Length).ToArray());
        Assert.Same(packet, BuildClanPacket(builder, packet, p.Client));
        Assert.Equal(expected.Length, packet.Size); Assert.Equal(4, packet.Position);
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(packet));
        Assert.Empty(p.Queue);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CallbackUnrepresentableListsRefuseWithoutChangingTheFrameOrSource(bool longEntry) => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        p.Stream.ClanCallBackCreate(LegacyNativeMsgClan.Info.Quit, 17, 23, ["before"]);
        string[] values = longEntry ? [new string('A', 65536)] : Enumerable.Repeat("A", 256).ToArray();
        string[] snapshot = (string[])values.Clone();
        AssertClanPacketRefusal<ArgumentOutOfRangeException>(p.Stream, () =>
            p.Stream.ClanCallBackCreate(LegacyNativeMsgClan.Info.Quit, 9, 11, values));
        Assert.Equal(snapshot, values); Assert.Empty(p.Queue);
    });

    [Fact]
    public Task CallbackNullEntriesRemainEmptyAndRoundTripThroughTheWholePreparedReader() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        string[] source = [null!, "狼"];
        p.Stream.ClanCallBackCreate(LegacyNativeMsgClan.Info.Quit, 9, 11, source);
        Assert.Equal(Convert.FromHexString("1800010817000000090000000B0000000200000300E78BBC"), NativeSpellTestFixture.Bytes(p.Stream));
        p.Stream.GetClan(out var mode, out var uid, out var parameter, out var list);
        Assert.Equal(LegacyNativeMsgClan.Info.Quit, mode); Assert.Equal(9u, uid); Assert.Equal(11u, parameter);
        Assert.Equal(new[] { "", "狼" }, list); Assert.Equal(24, p.Stream.Position);
        Assert.Null(source[0]); Assert.Equal("狼", source[1]); Assert.Empty(p.Queue);
    });

    private static LegacyNativePacket BuildClanPacket(string builder, LegacyNativePacket packet, LegacyNativeGameClientRuntime client) => builder switch
    {
        "members" => packet.ClanMembersCreate([]),
        "bulletin" => packet.ClanBulletinCreate(client, new LegacyNativeClanRuntime { ClanBuletin = "A" }),
        "relation" => packet.ClanRelationCreate(9, "A", "B", LegacyNativeMsgClan.Info.AddAlly),
        "single" => packet.ClanAppendSingleClientCreate(9, "A", LegacyNativeMsgClan.Info.RequestAlly),
        "callback" => packet.ClanCallBackCreate(LegacyNativeMsgClan.Info.Quit, 9, 11, ["A"]),
        _ => throw new ArgumentOutOfRangeException(nameof(builder)),
    };

    private static byte[] ClanBuilderGolden(string builder) => Convert.FromHexString(builder switch
    {
        "members" => "1400010804000000000000000000000000000000",
        "bulletin" => "1400010819000000070000000000000001010041",
        "relation" => "1700010813000000090000000000000002010041010042",
        "single" => "1400010811000000090000000000000001010041",
        "callback" => "1400010817000000090000000B00000001010041",
        _ => throw new ArgumentOutOfRangeException(nameof(builder)),
    });

    private static unsafe LegacyNativePacket InitializedClanTarget(byte[] input)
    {
        var packet = new LegacyNativePacket(input.Length);
        Marshal.Copy(input, 0, (nint)packet.Memory, input.Length);
        packet.Size = input.Length;
        return packet;
    }

    private sealed class ClanPacketSealScope : IDisposable
    {
        private static readonly FieldInfo Seal = typeof(LegacyNativePacket).GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previous = Seal.GetValue(null);
        internal ClanPacketSealScope(bool enabled) => Seal.SetValue(null, enabled ? "TQServer" : null);
        public void Dispose() => Seal.SetValue(null, previous);
    }
}
