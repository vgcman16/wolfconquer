using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Packet = WolfConquer.Core.Networking.LegacyNativePacket;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class CorrectedOnlyNativeTeamSocialCodecTests
{
    [Theory]
    [InlineData("name", 0)] [InlineData("name", 7)]
    [InlineData("name", 8)] [InlineData("name", 39)]
    [InlineData("known", 0)] [InlineData("known", 3)]
    [InlineData("known", 4)] [InlineData("known", 5)]
    [InlineData("partner", 0)] [InlineData("partner", 3)]
    [InlineData("partner", 4)] [InlineData("partner", 5)] [InlineData("partner", 9)]
    public Task MissingRecordTailPreservesAllCallerValuesAndTheInitialCursor(string codec, int count) =>
        NativePacketTestThread.Run(() =>
        {
            byte[] input = new byte[] { 0xA5 }.Concat(Record(codec).Take(count)).ToArray();
            using var packet = new Packet(input); packet.Seek(1);
            var output = new CallerOutputs();
            Assert.Throws<EndOfStreamException>(() => output.Read(packet, codec));
            output.Unchanged();
            Assert.Equal(1, packet.Position); Assert.Equal(input.Length, packet.Size);
            Assert.Equal(input, NativeSpellTestFixture.Bytes(packet));
        });

    [Theory]
    [InlineData("name")] [InlineData("known")] [InlineData("partner")]
    public Task MissingInputNeverPublishesDefaultOutputs(string codec) => NativePacketTestThread.Run(() =>
    {
        var output = new CallerOutputs();
        Assert.Throws<ArgumentNullException>(() => output.Read(null!, codec));
        output.Unchanged();
    });

    [Fact]
    public Task IndependentFixedRecordsRetainUnknownBitsAndNulBehaviorAtExactInputEnds() => NativePacketTestThread.Run(() =>
    {
        using var name = new Packet(Record("name"));
        name.GetEliteSetTeamName(out uint first, out uint second, out string text);
        Assert.Equal(0x12345678u, first); Assert.Equal(0x9ABCDEF0u, second); Assert.Equal("A", text);
        Assert.Equal(40, name.Position); Assert.Equal(Record("name"), NativeSpellTestFixture.Bytes(name));

        using var known = new Packet(Record("known"));
        known.GetKnowPersons(out uint uid, out var action, out bool online);
        Assert.Equal(0x12345678u, uid); Assert.Equal((byte)255, (byte)action); Assert.False(online);
        Assert.Equal(6, known.Position); Assert.Equal(Record("known"), NativeSpellTestFixture.Bytes(known));

        using var partner = new Packet(Record("partner"));
        partner.GetTradePartner(out uid, out var partnerAction, out online, out int hours);
        Assert.Equal(0x12345678u, uid); Assert.Equal((byte)255, (byte)partnerAction); Assert.True(online);
        Assert.Equal(0x12345678, hours); Assert.Equal(10, partner.Position);
        Assert.Equal(Record("partner"), NativeSpellTestFixture.Bytes(partner));
    });

    [Theory]
    [InlineData("name", 44)] [InlineData("match", 68)] [InlineData("team-score", 96)]
    [InlineData("known", 128)] [InlineData("partner", 128)]
    public unsafe Task WholeBuilderRefusalCannotLeakAnyPreparedPrefix(string codec, int completeLength) =>
        NativePacketTestThread.Run(() =>
        {
            using var seal = new NativeStringPacketTestScope();
            using var packet = new Packet(completeLength - 1);
            byte[] before = Enumerable.Repeat((byte)0xA5, packet.Capacity).ToArray();
            Marshal.Copy(before, 0, (nint)packet.Memory, before.Length);
            packet.Size = 3; packet.Seek(2);
            Assert.Throws<InvalidOperationException>(() => Build(packet, codec));
            Assert.Equal(3, packet.Size); Assert.Equal(2, packet.Position);
            Assert.Equal(before, NativeStringPacketTestScope.Bytes(packet, packet.Capacity));
        });

    private static byte[] Record(string codec) => Convert.FromHexString(codec switch
    {
        "name" => "78563412F0DEBC9A410042" + new string('0', 58),
        "known" => "78563412FF02",
        "partner" => "78563412FF0178563412",
        _ => throw new ArgumentOutOfRangeException(nameof(codec))
    });

    private static void Build(Packet packet, string codec)
    {
        switch (codec)
        {
            case "name": packet.TeamEliteSetTeamName(2039, LegacyNativeMsgTeamEliteSetTeamName.State.Remove, 91, "n"); break;
            case "match": packet.TeamElitePKMatchUICreate(2283, LegacyNativeMsgTeamElitePKMatchUI.State.Reward,
                LegacyNativeMsgTeamElitePKMatchUI.EffectTyp.Effect_Win, 91, "n", 30); break;
            case "team-score": packet.TeamArenaMatchScoreCreate(1, 2, "n", 3, 4, 5, "n", 6); break;
            case "known": packet.KnowPersonsCreate(LegacyNativeKnowPersonsPacket.Action.AddFriend, 1, true, 2, 3, 4, 5, "s", "n"); break;
            case "partner": packet.TradePartnerCreate(1, LegacyNativeTradePartnerPacket.Action.AddPartner, true, 2, 3, 4, 5, "s", "n"); break;
            default: throw new ArgumentOutOfRangeException(nameof(codec));
        }
    }

    private sealed class CallerOutputs
    {
        private uint first = 91, second = 92;
        private string name = "before";
        private LegacyNativeKnowPersonsPacket.Action known = (LegacyNativeKnowPersonsPacket.Action)99;
        private LegacyNativeTradePartnerPacket.Action partner = (LegacyNativeTradePartnerPacket.Action)98;
        private bool online = true;
        private int hours = -17;

        internal void Read(Packet packet, string codec)
        {
            switch (codec)
            {
                case "name": packet.GetEliteSetTeamName(out first, out second, out name); break;
                case "known": packet.GetKnowPersons(out first, out known, out online); break;
                case "partner": packet.GetTradePartner(out first, out partner, out online, out hours); break;
                default: throw new ArgumentOutOfRangeException(nameof(codec));
            }
        }

        internal void Unchanged()
        {
            Assert.Equal(91u, first); Assert.Equal(92u, second); Assert.Equal("before", name);
            Assert.Equal((LegacyNativeKnowPersonsPacket.Action)99, known);
            Assert.Equal((LegacyNativeTradePartnerPacket.Action)98, partner);
            Assert.True(online); Assert.Equal(-17, hours);
        }
    }
}
