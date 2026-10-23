using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeArenaMatchPacketTests
{
    [Fact]
    public void SignupRetainsTheExactNativeLayoutAndEveryDialogAndButtonAlias()
    {
        Assert.Equal(60, Marshal.SizeOf<LegacyNativeMsgArenaSignup>());
        Assert.Equal(LayoutKind.Explicit, typeof(LegacyNativeMsgArenaSignup).StructLayoutAttribute!.Value);
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(LegacyNativeMsgArenaSignup.DialogType)));
        var dialogs = new Dictionary<string, uint> { ["ArenaIconOn"]=0,["ArenaIconOff"]=1,["StartCountDown"]=2,
            ["ArenaGui"]=3,["OpponentGaveUp"]=4,["Match"]=5,["YouAreKicked"]=6,["StartTheFight"]=7,
            ["Dialog"]=8,["Dialog2"]=9,["Continue"]=10 };
        var buttons = new Dictionary<string, uint> { ["Lose"]=3,["Win"]=1,["DoGiveUp"]=2,["Accept"]=1,
            ["MatchOff"]=3,["MatchOn"]=5,["SignUp"]=0 };
        Assert.Equal(dialogs.Count, Enum.GetNames<LegacyNativeMsgArenaSignup.DialogType>().Length);
        Assert.Equal(buttons.Count, Enum.GetNames<LegacyNativeMsgArenaSignup.DialogButton>().Length);
        foreach (var pair in dialogs) Assert.Equal(pair.Value, (uint)Enum.Parse<LegacyNativeMsgArenaSignup.DialogType>(pair.Key));
        foreach (var pair in buttons) Assert.Equal(pair.Value, (uint)Enum.Parse<LegacyNativeMsgArenaSignup.DialogButton>(pair.Key));
    }

    [Fact]
    public Task SignupCodecUsesActualPlayerIdentityAndArenaInfoNotTheRecordProjection() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.LevelAndClass(130, 16004);
        scope.Client.ArenaStatistic!.UID = 999; scope.Client.ArenaStatistic.Name = "DifferentRecord";
        scope.Client.ArenaStatistic.Class = 1; scope.Client.ArenaStatistic.Level = 2;
        scope.Client.ArenaStatistic.Info.TodayRank = 33; scope.Client.ArenaStatistic.Info.ArenaPoints = 4400;
        var packet = scope.Packet;
        Assert.Same(packet, packet.ArenaSignupCreate(LegacyNativeMsgArenaSignup.DialogType.Match,
            LegacyNativeMsgArenaSignup.DialogButton.MatchOn, scope.Client));
        byte[] expected = new byte[72]; U16(expected, 0, 72); U16(expected, 2, 2140);
        U32(expected, 4, 5); U32(expected, 8, 5); U32(expected, 12, 17); Ascii32(expected, 20, "PlayerOne");
        U32(expected, 52, 33); U32(expected, 56, 16004); U32(expected, 64, 4400); U32(expected, 68, 130);
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(packet));
        Assert.Equal(4, packet.Position); Assert.Equal(72, packet.Size); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task SignupCodecWritesUnknownEnumsWithoutNormalizingAndKeepsTheRealPacketSeal() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); LegacyNativePacket.SealString = "TQServer";
        scope.Packet.ArenaSignupCreate((LegacyNativeMsgArenaSignup.DialogType)uint.MaxValue,
            (LegacyNativeMsgArenaSignup.DialogButton)12345, scope.Client);
        byte[] bytes = NativeSpellTestFixture.Bytes(scope.Packet);
        Assert.Equal(80, bytes.Length); Assert.Equal((ushort)72, BitConverter.ToUInt16(bytes, 0));
        Assert.Equal(uint.MaxValue, BitConverter.ToUInt32(bytes, 4)); Assert.Equal(12345u, BitConverter.ToUInt32(bytes, 8));
        Assert.Equal(new byte[] { 84,81,83,101,114,118,101,114 }, bytes[72..]);
    });

    [Fact]
    public Task NullInfoSignupFaultRetainsIdentityPrefixAndNeverFinalizes() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Client.ArenaStatistic!.Info = null!;
        Assert.Throws<NullReferenceException>(() => scope.Packet.ArenaSignupCreate(
            LegacyNativeMsgArenaSignup.DialogType.Dialog, LegacyNativeMsgArenaSignup.DialogButton.Win, scope.Client));
        Assert.Equal(52, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        byte[] prefix = NativeSpellTestFixture.Bytes(scope.Packet);
        Assert.Equal(8u, BitConverter.ToUInt32(prefix, 4)); Assert.Equal(1u, BitConverter.ToUInt32(prefix, 8));
        Assert.Equal(17u, BitConverter.ToUInt32(prefix, 12)); Assert.Equal(0u, BitConverter.ToUInt32(prefix, 16));
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task NullUserSignupFaultOccursAfterBothDialogWordsButBeforeAnyPlayerIdentityWrite() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Throws<NullReferenceException>(() => scope.Packet.ArenaSignupCreate(
            LegacyNativeMsgArenaSignup.DialogType.Continue, LegacyNativeMsgArenaSignup.DialogButton.MatchOff, null!));
        Assert.Equal(12, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        byte[] bytes = NativeSpellTestFixture.Bytes(scope.Packet);
        Assert.Equal(10u, BitConverter.ToUInt32(bytes, 4)); Assert.Equal(3u, BitConverter.ToUInt32(bytes, 8));
        Assert.Equal(0xA5A5A5A5u, BitConverter.ToUInt32(bytes, 12));
    });

    [Fact]
    public Task WholeSignupReaderConsumesExactlyTwoUIntWordsAndDoesNotValidateUnknownValues() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Packet.InitWriter();
        scope.Packet.Write(0xFEDCBA98u); scope.Packet.Write(0x87654321u); scope.Packet.Write(0x11223344u); scope.Packet.Seek(4);
        scope.Packet.GetArenaSignup(out var dialog, out var option);
        Assert.Equal(0xFEDCBA98u, (uint)dialog); Assert.Equal(0x87654321u, (uint)option);
        Assert.Equal(12, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Equal(0x11223344u, scope.Packet.ReadUInt32()); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task SignupReaderRetainsTheNativeReadBoundaryZeroWithoutAdvancing() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Packet.Seek(LegacyNativePacket.MAX_SIZE - 4);
        scope.Packet.GetArenaSignup(out var dialog, out var option);
        Assert.Equal(0u, (uint)dialog); Assert.Equal(0u, (uint)option);
        Assert.Equal(LegacyNativePacket.MAX_SIZE - 4, scope.Packet.Position);
    });

    [Fact]
    public Task WholeSendSignupTargetsTheSuppliedRealOwnerEvenWhenNotAMatchParticipant() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var secondQueue = new Queue<byte[]>(); var outsiderQueue = new Queue<byte[]>();
        var second = scope.Peer(18, secondQueue); var outsider = scope.Peer(19, outsiderQueue);
        var match = new LegacyNativeArenaMatchRuntime(scope.Client, second, 100);
        match.SendSignUp(scope.Packet, outsider);
        byte[] bytes = Assert.Single(outsiderQueue);
        Assert.Equal(72, bytes.Length); Assert.Equal((ushort)2140, BitConverter.ToUInt16(bytes, 2));
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 4)); Assert.Equal(0u, BitConverter.ToUInt32(bytes, 8));
        Assert.Equal(19u, BitConverter.ToUInt32(bytes, 12)); Assert.Null(outsider.ArenaMatch);
        Assert.Empty(scope.Queue); Assert.Empty(secondQueue);
        Assert.Same(match, scope.Client.ArenaMatch); Assert.Same(match, second.ArenaMatch);
    });

    [Fact]
    public Task WholeSendSignupStillBuildsPacketForAFakeOwnerBeforeRealSendFiltersIt() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var second = scope.Peer(18, new Queue<byte[]>());
        var match = new LegacyNativeArenaMatchRuntime(scope.Client, second, 100); scope.Client.Fake = true;
        match.SendSignUp(scope.Packet, scope.Client);
        Assert.Equal(72, scope.Packet.Size); Assert.Equal(4, scope.Packet.Position); Assert.Empty(scope.Queue);
    });
}
