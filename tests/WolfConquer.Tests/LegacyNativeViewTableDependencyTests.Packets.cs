using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Table = WolfConquer.Core.Data.LegacyNativeRoulettes.RouletteTable;
using Color = WolfConquer.Core.World.LegacyNativeMsgRouletteOpenGui.Color;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeViewTableDependencyTests
{
    [Fact]
    public Task AllFixedRouletteWritersUseLiteralWireOrderAndWidths() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        s.Packet.RouletteTableCreate(0x12345678, 9, LegacyNativeMsgRouletteTable.TableType.ConquerPoints, 0x2233, 0x4455, 0x6677, 255);
        Assert.Equal(Frame(2278, w => { w.Write(0x12345678u); w.Write(9u); w.Write(2u); w.Write((ushort)0x2233); w.Write((ushort)0x4455); w.Write((ushort)0x6677); w.Write((byte)255); }), Bytes(s.Packet));
        s.Packet.RoulettedAddNewPlayerCreate(17, 0x1234, Color.Mauve, "Wolf");
        Assert.Equal(Frame(2019, w => { w.Write(17u); w.Write(0x1234u); w.Write((byte)3); Name(w, "Wolf"); }), Bytes(s.Packet));
        s.Packet.RouletteScreenCreate(17);
        Assert.Equal(Frame(2112, w => { w.Write((byte)0); w.Write(17u); }), Bytes(s.Packet));
        s.Packet.RouletteSignUpCreate(LegacyNativeMsgRouletteSignUp.ActionJoin.Quit, 17);
        Assert.Equal(Frame(2048, w => { w.Write((byte)7); w.Write(17u); }), Bytes(s.Packet));
        s.Packet.RouletteNoWinnerCreate(37);
        Assert.Equal(Frame(2330, w => w.Write((byte)37)), Bytes(s.Packet));
    });

    [Fact]
    public Task RecordAndOpenGuiEncodeActualMemberStateAndNullableLists() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var member = s.Member(color: Color.Green); member.Betting = 50; member.Winning = uint.MaxValue;
        s.Packet.RouletteRecordCreate([member], 13);
        Assert.Equal(Frame(2013, w => { w.Write((byte)13); w.Write((byte)1); w.Write(50u); w.Write(uint.MaxValue); Name(w, "Wolf"); }), Bytes(s.Packet));
        s.Packet.RouletteRecordCreate(null, 0);
        Assert.Equal(Frame(2013, w => { w.Write((byte)0); w.Write((byte)0); }), Bytes(s.Packet));
        s.Packet.RouletteOpenGuiCreate(LegacyNativeMsgRouletteTable.TableType.Money, Color.Watch, 255, [member]);
        Assert.Equal(Frame(2030, w => { w.Write(1u); w.Write((byte)99); w.Write((byte)255); w.Write((byte)1); w.Write(17u); w.Write(s.Client.Player.Mesh); w.Write((byte)2); Name(w, "Wolf"); }), Bytes(s.Packet));
        s.Packet.RouletteOpenGuiCreate(LegacyNativeMsgRouletteTable.TableType.Money, Color.None, 0);
        Assert.Equal(Frame(2030, w => { w.Write(1u); w.Write((byte)5); w.Write((byte)0); w.Write((byte)0); }), Bytes(s.Packet));
    });

    [Fact]
    public Task BettingPacketWritesStraightSnapshotBeforeExtraSnapshotWithoutFiltering() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var member = s.Member();
        member.MyLuckNumber[7] = new() { Number = 37, BetPrice = uint.MaxValue };
        member.MyLuckExtra[151] = new() { Number = 151, BetPrice = 2 };
        s.Packet.RouletteShareBettingCreate(member, Color.Gray);
        Assert.Equal(Frame(2090, w => { w.Write((byte)4); w.Write((byte)2); w.Write((byte)37); w.Write(uint.MaxValue); w.Write((byte)151); w.Write(2u); }), Bytes(s.Packet));
        member.MyLuckNumber.Clear(); member.MyLuckExtra.Clear();
        s.Packet.RouletteShareBettingCreate(member, Color.None);
        Assert.Equal(Frame(2090, w => { w.Write((byte)5); w.Write((byte)0); }), Bytes(s.Packet));
    });

    [Fact]
    public Task RecordByteCountWrapsButWholeWriterStillWritesAll256Entries() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var member = s.Member(); member.Betting = 3; member.Winning = 7;
        var members = Enumerable.Repeat(member, 256).ToArray();
        s.Packet.RouletteRecordCreate(members, 36);
        var actual = Bytes(s.Packet);
        Assert.Equal(10246, actual.Length); Assert.Equal((byte)0, actual[5]);
        Assert.Equal(Frame(2013, w =>
        {
            w.Write((byte)36); w.Write((byte)0);
            for (int index = 0; index < 256; index++) { w.Write(3u); w.Write(7u); Name(w, "Wolf"); }
        }), actual);
    });

    [Fact]
    public unsafe Task RouletteReadersKeepUnknownEnumValuesAndAdvanceExactNativeCursor() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        byte[] frame = Frame(2048, w => { w.Write((byte)255); w.Write(0x12345678u); });
        Marshal.Copy(frame, 0, (IntPtr)s.Packet.Memory, frame.Length);
        s.Packet.Size = frame.Length; // Actual copied input, not the spare owned allocation.
        s.Packet.Seek(4); s.Packet.GetRouletteSignUp(out var action, out uint uid);
        Assert.Equal((byte)255, (byte)action); Assert.Equal(0x12345678u, uid); Assert.Equal(9, s.Packet.Position);
        s.Packet.Seek(4); s.Packet.GetRouletteAction(out var mode);
        Assert.Equal((byte)255, (byte)mode); Assert.Equal(5, s.Packet.Position);
        Assert.Equal(frame.Length, s.Packet.Size);
    });

    [Fact]
    public Task FullShowHandWriterUsesActualPokerOwnersFlagsPotAndPlayerRecords() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var table = new LegacyNativePokerTableRuntime(0x11223344)
        {
            X = 10, Y = 11, Mesh = 0x55667788, Number = 15, UnLimited = true, IsCPs = true,
            MinBet = uint.MaxValue, State = (LegacyNativePokerFlags.TableState)4, TotalPot = ulong.MaxValue
        };
        table.Players[99] = new LegacyNativePokerPlayer("Player", 17) { Seat = 7 };
        Assert.Same(s.Packet, s.Packet.CreateShowHandTable(table));
        Assert.Equal(Frame(2015, w =>
        {
            w.Write(0x11223344u); w.Write(new byte[8]); w.Write((ushort)10); w.Write((ushort)11); w.Write(0x55667788u);
            w.Write((ushort)0); w.Write(15u); w.Write(1); w.Write(1); w.Write((ulong)uint.MaxValue); w.Write(0);
            w.Write((byte)4); w.Write(ulong.MaxValue); w.Write((byte)1); w.Write(17u); w.Write((byte)7); w.Write((byte)1);
        }), Bytes(s.Packet));
    });

    [Fact]
    public Task DisplayWritersKeepNativeMutationBeforeNullFaultsRatherThanPrevalidating() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        s.Packet.Seek(99);
        Assert.Throws<NullReferenceException>(() => s.Packet.CreateShowHandTable(null!)); Assert.Equal(4, s.Packet.Position);
        Assert.Throws<NullReferenceException>(() => s.Packet.RouletteShareBettingCreate(null!, Color.Mauve)); Assert.Equal(5, s.Packet.Position);
        Assert.Throws<NullReferenceException>(() => s.Packet.RouletteRecordCreate([null!], 5)); Assert.Equal(6, s.Packet.Position);
        Assert.Throws<NullReferenceException>(() => s.Packet.RouletteOpenGuiCreate(LegacyNativeMsgRouletteTable.TableType.Money, Color.Gray, 10, [null!])); Assert.Equal(11, s.Packet.Position);
        var member = new Table.Member { Betting = 2, Winning = 3 };
        Assert.Throws<NullReferenceException>(() => s.Packet.RouletteRecordCreate([member], 5)); Assert.Equal(14, s.Packet.Position);
    });

    [Fact]
    public void ExactRouletteMetadataKeepsOneByteStructsAndNativeEnums()
    {
        Assert.Equal(1, Marshal.SizeOf<LegacyNativeMsgRouletteOpenGui>());
        Assert.Equal(1, Marshal.SizeOf<LegacyNativeMsgRouletteCheck>());
        Assert.Equal(1, Marshal.SizeOf<LegacyNativeMsgRouletteSignUp>());
        Assert.Equal(1, Marshal.SizeOf<LegacyNativeMsgRouletteAction>());
        Assert.Equal((byte)0, (byte)LegacyNativeMsgRouletteSignUp.ActionJoin.Join);
        Assert.Equal((byte)4, (byte)LegacyNativeMsgRouletteSignUp.ActionJoin.Watch);
        Assert.Equal((byte)5, (byte)LegacyNativeMsgRouletteSignUp.ActionJoin.InfoTable);
        Assert.Equal((byte)7, (byte)LegacyNativeMsgRouletteSignUp.ActionJoin.Quit);
        Assert.Equal((byte)3, (byte)LegacyNativeMsgRouletteAction.Action.Join);
        Assert.Equal((byte)2, (byte)LegacyNativeMsgRouletteAction.Action.RemovePlayer);
        Assert.Equal((byte)1, (byte)LegacyNativeMsgRouletteAction.Action.ShowSpectators);
        Assert.Equal((byte)0, (byte)LegacyNativeMsgRouletteAction.Action.Record);
    }
}
