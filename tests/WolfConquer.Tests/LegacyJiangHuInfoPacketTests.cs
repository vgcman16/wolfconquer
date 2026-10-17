using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyJiangHuInfoPacketTests
{
    [Fact]
    public void LegacyJiangHuStartupMatchesCapturedIconBarFallback()
    {
        var snapshot = Snapshot(reborn: 2);

        Assert.True(LegacyJiangHuInfoPacket.TryCreateStartup(snapshot, out var packet));

        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal(9, bytes.Length);
        Assert.Equal(WorldPacketOpcodes.LegacyJiangHuInfo, frame.Header.Opcode);
        Assert.Equal("090022080001010030", Convert.ToHexString(bytes).ToLowerInvariant());
        Assert.True(LegacyJiangHuInfoPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyJiangHuInfoPacket.IconBarMode, parsed.Mode);
        Assert.Equal(["0"], parsed.Values);
    }

    [Fact]
    public void LegacyJiangHuStartupSkipsNonRebornTwoCharacters()
    {
        Assert.False(LegacyJiangHuInfoPacket.TryCreateStartup(Snapshot(reborn: 1), out _));
    }

    private static CharacterSnapshot Snapshot(long reborn)
    {
        return CharacterSnapshotParser.ParseText($$"""
            [Character]
            UID=1293600
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=705
            Y=577
            FirstClass=0
            SecoundeClass=7050
            Reborn={{reborn}}
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
            MinHitPoints=61229
            MinMana=5000
            ConquerPoints=639784138
            BankCps=0
            BoundConquerPoints=0
            Money=98602439956
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
