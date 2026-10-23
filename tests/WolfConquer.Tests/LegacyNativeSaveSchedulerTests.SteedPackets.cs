using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSaveSchedulerTests
{
    [Fact]
    public Task GateOpeningMovesRealNpcCoordinatesButRetainsGateSetCoordinatesAndBroadcastsNativeRemovalAction() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var race = CreateRace(s, true);
        Set(race, "isOn", true); s.Queue.Clear();
        DateTime before = DateTime.Now; Invoke(race, "OpenGate"); DateTime after = DateTime.Now;
        var gate = Field<LegacyNativeSobNpcRuntime>(race, "Gate");
        Assert.Equal((ushort)0, gate.X); Assert.Equal((ushort)0, gate.Y);
        Assert.Equal((ushort)5, race.GateX); Assert.Equal((ushort)5, race.GateY);
        Assert.True(race.IsOn); Assert.False(race.CanJoin);
        Assert.InRange(Field<DateTime>(race, "GateOpened"), before, after);
        Assert.Equal("1300FE0708AD98014800608701680070007800", Convert.ToHexString(Assert.Single(s.Queue)));
    });

    [Theory]
    [InlineData(0u, "1000FE07080048006000680070007800")]
    [InlineData(1u, "1300FE070801180A4800609103680070007800")]
    public Task SendDataRetainsSourceBlankQueryBugOnlyForZeroUid(uint uid, string expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var map = s.Map(1950);
        var race = new LegacyNativeMsgSteedRace(); Set(race, "Map", map);
        Invoke(race, "SendData", (ushort)401, 10u, uid);
        Assert.Equal(expected, Convert.ToHexString(Assert.Single(s.Queue)));
    });

    [Theory]
    [InlineData(0, 1, 1000000)]
    [InlineData(400000, 1, 200000)]
    [InlineData(1000000, 1, 100000)]
    [InlineData(2147483647, 2, 500002)]
    [InlineData(-2147483648, -1, 100000)]
    public void WholeAwardFormulaRetainsNativeIntOverflowAndMinimum(int time, int rank, int expected)
    {
        var race = new LegacyNativeMsgSteedRace();
        Assert.Equal(expected, (int)Invoke(race, "AwardPlayer", null!, time, rank)!);
    }

    [Fact]
    public void AwardZeroRankDoesNotBecomeAnAcceptedDefault() =>
        Assert.Throws<DivideByZeroException>(() => Invoke(new LegacyNativeMsgSteedRace(), "AwardPlayer", null!, 50, 0));

    [Fact]
    public Task RaceRecordWritersPreserveSignedFieldsPaddingAndFixedUtf8Prefix() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        s.Packet.RaceRecordCreate(LegacyNativeMsgRaceRecord.RaceRecordTypes.AddRecord, -1, "狼A", int.MinValue, int.MaxValue);
        byte[] bytes = Bytes(s.Packet);
        Assert.Equal(52, bytes.Length); Assert.Equal((ushort)1071, Opcode(bytes));
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 4)); Assert.Equal(-1, BitConverter.ToInt32(bytes, 8));
        Assert.Equal(new byte[] { 0xE7, 0x8B }, bytes[12..14]); Assert.All(bytes[14..44], b => Assert.Equal((byte)0, b));
        Assert.Equal(int.MinValue, BitConverter.ToInt32(bytes, 44)); Assert.Equal(int.MaxValue, BitConverter.ToInt32(bytes, 48));
        s.Packet.RaceRecordCreate(LegacyNativeMsgRaceRecord.RaceRecordTypes.EndTime, 1, 2, 3, 4, 5);
        Assert.Equal("24002F040100000001000000020000000300000000000000000000000400000005000000", Convert.ToHexString(Bytes(s.Packet)));
    });
}
