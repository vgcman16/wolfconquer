using System.Buffers.Binary;
using WolfConquer.Core.World;
using Info = WolfConquer.Core.World.LegacyNativeMsgApprenticeInformation;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMentorShareTests
{
    [Theory]
    [InlineData(0u)] [InlineData(1u)] [InlineData(2u)] [InlineData(4294967295u)]
    public Task InformationGetArrayPreservesEveryFieldAndVariableUtf8StringOrder(uint mode) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var info = Info.Create();
        info.Mode = (Info.Action)mode; info.Mentor_ID = uint.MaxValue; info.Apprentice_ID = 7; info.Mesh = 0xABCDEF12;
        info.Shared_Battle_Power = 50; info.Enrole_date = 0xDEADBEEF; info.Level = 300; info.Class = 1005;
        info.PkPoints = ushort.MaxValue; info.Online = uint.MaxValue; info.UnKnow2 = 0x87654321;
        info.Apprentice_Experience = ulong.MaxValue; info.Apprentice_Blessing = 0x1234; info.Apprentice_Composing = 0x5678;
        info.WriteString("Mentor名", "Spouseé", "Apprentice狼", "Signature✓");
        Assert.Same(scope.Packet, info.GetArray(scope.Packet));
        var bytes = NativeSpellTestFixture.Bytes(scope.Packet); Assert.Equal(InfoWire(info), bytes);
        var read = ReadInfo(bytes);
        Assert.Equal(mode, read.Mode); Assert.Equal(uint.MaxValue, read.Mentor); Assert.Equal(7u, read.Apprentice);
        Assert.Equal(info.Mesh, read.Mesh); Assert.Equal(50u, read.Share); Assert.Equal(999999u, read.Constant);
        Assert.Equal(info.Enrole_date, read.Date); Assert.Equal(300u, read.Level); Assert.Equal(1005u, read.Class);
        Assert.Equal(ushort.MaxValue, read.Pk); Assert.Equal(uint.MaxValue, read.Online); Assert.Equal(info.UnKnow2, read.Unknown);
        Assert.Equal(ulong.MaxValue, read.Experience); Assert.Equal(info.Apprentice_Blessing, read.Blessing); Assert.Equal(info.Apprentice_Composing, read.Composing);
        Assert.Equal(new[] { "Mentor名", "Apprentice狼", "Spouseé", "Signature✓" }, read.Names);
        Assert.Equal((ushort)150, info.Length); Assert.NotEqual(150, bytes.Length); Assert.Equal(4, scope.Packet.Position);
    });

    [Fact]
    public Task CreateAndZeroBornRowsKeepLiteralFactoryDifferencesAndNullStringListEntries() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var zero = new Info();
        Assert.Equal((ushort)0, zero.Length); Assert.Equal((ushort)0, zero.PacketID); Assert.Equal(0u, zero._999999);
        Assert.Null(zero.MentorName); Assert.Null(zero.ApprenticeSpouse); Assert.Null(zero.ApprenticeName); Assert.Null(zero.PersonalSignature);
        zero.GetArray(scope.Packet); Assert.Equal(InfoWire(zero), NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Equal(93, scope.Packet.Size);
        var created = Info.Create(); Assert.Equal((ushort)150, created.Length); Assert.Equal((ushort)2200, created.PacketID); Assert.Equal(999999u, created._999999);
        created.GetArray(scope.Packet); var read = ReadInfo(NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(new[] { "", "", "", "" }, read.Names); Assert.Equal(93, scope.Packet.Size);
        created.Length = ushort.MaxValue; created.PacketID = 123;
        created.GetArray(scope.Packet); Assert.Equal((ushort)123, BinaryPrimitives.ReadUInt16LittleEndian(NativeSpellTestFixture.Bytes(scope.Packet).AsSpan(2)));
        Assert.Equal(93, scope.Packet.Size); Assert.Equal(ushort.MaxValue, created.Length);
    });

    [Fact]
    public Task FillUsesCurrentClientPlayerLevelByteAndDoesNotOverwriteUnrelatedRecordFields() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var info = Info.Create();
        Set(scope.Player, "_level", (ushort)300); Set(scope.Player, "_class", uint.MaxValue); Set(scope.Player, "_pkpoints", ushort.MaxValue);
        info.Mentor_ID = 123; info.Apprentice_ID = 456; info.Apprentice_Experience = ulong.MaxValue; info.WriteString("M", "S", "A", "B");
        info.Fill(scope.Client);
        Assert.Equal(44u, info.Level); Assert.Equal(uint.MaxValue, info.Class); Assert.Equal(ushort.MaxValue, info.PkPoints);
        Assert.Equal(scope.Player.Mesh, info.Mesh); Assert.Equal(1u, info.Online); Assert.Equal(123u, info.Mentor_ID); Assert.Equal(456u, info.Apprentice_ID);
        Assert.Equal(ulong.MaxValue, info.Apprentice_Experience); Assert.Equal("M", info.MentorName);
        info.Level = 999;
        Assert.Throws<NullReferenceException>(() => info.Fill(null!)); Assert.Equal(999u, info.Level);
        Assert.Throws<NullReferenceException>(() => info.GetArray(null!)); Assert.Equal(999u, info.Level);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task PkReadGetterAndSignatureUseActualPlayerBackingWithoutInstallingAColdSetter() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = new LegacyNativeGameClientRuntime();
        Assert.Equal((ushort)0, client.Player.PKPoints); Assert.Equal("", client.Player.Signature);
        Assert.Null(typeof(LegacyNativePlayerRuntime).GetProperty(nameof(client.Player.PKPoints))!.SetMethod);
        Set(client.Player, "_pkpoints", ushort.MaxValue); Assert.Equal(ushort.MaxValue, client.Player.PKPoints);
        client.Player.Signature = "Actual physical signature";
        var info = Info.Create(); info.Fill(client); Assert.Equal(ushort.MaxValue, info.PkPoints); Assert.Null(info.PersonalSignature);
        Assert.Equal("Actual physical signature", client.Player.Signature);
    });
}
