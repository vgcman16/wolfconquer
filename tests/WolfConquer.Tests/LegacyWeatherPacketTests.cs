using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyWeatherPacketTests
{
    [Fact]
    public void LegacyWeatherOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2306, WorldPacketOpcodes.LegacyWeather);
        Assert.Equal(WorldPacketOpcodes.LegacyWeather, LegacyWeatherPacket.Opcode);
    }

    [Fact]
    public void LegacyWeatherStartupWritesLiveRainWindFrame()
    {
        Assert.True(LegacyWeatherPacket.TryCreateStartup(Snapshot(weatherType: 4), out var packet));

        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal("1800020904000000fa000000fa000000fa000000fa000000", Convert.ToHexString(bytes).ToLowerInvariant());
        Assert.True(LegacyWeatherPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyWeatherType.RainWind, parsed.Type);
        Assert.Equal(250U, parsed.Intensity);
        Assert.Equal(250U, parsed.Direction);
        Assert.Equal(250U, parsed.Color);
        Assert.Equal(250U, parsed.SpeedSeconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(11)]
    public void LegacyWeatherStartupSkipsUnsupportedLegacySwitchValues(uint weatherType)
    {
        Assert.False(LegacyWeatherPacket.TryCreateStartup(Snapshot(weatherType), out _));
    }

    private static CharacterSnapshot Snapshot(uint weatherType)
    {
        return CharacterSnapshotParser.ParseText($$"""
            [Character]
            UID=1293600
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            VipLevel=0
            weatherType={{weatherType}}
            PkPoints=0
            Online=0
            """);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
