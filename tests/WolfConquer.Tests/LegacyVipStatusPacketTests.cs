using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyVipStatusPacketTests
{
    [Fact]
    public void LegacyVipStatusOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2284, WorldPacketOpcodes.LegacyMsgVipStatus);
        Assert.Equal(WorldPacketOpcodes.LegacyMsgVipStatus, LegacyVipStatusPacket.Opcode);
    }

    [Fact]
    public void LegacyVipStatusStartupMatchesCapturedNoVipFrame()
    {
        var packet = LegacyVipStatusPacket.FromSnapshot(Snapshot(vipLevel: 0));

        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal("0800ec0800000100", Convert.ToHexString(bytes).ToLowerInvariant());
        Assert.True(LegacyVipStatusPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyVipStatusFlags.None, parsed.Flags);
    }

    [Fact]
    public void LegacyVipStatusFlagsMatchLegacyVipThresholds()
    {
        Assert.Equal(LegacyVipStatusFlags.None, LegacyVipStatusPacket.FlagsForVipLevel(0));
        Assert.Equal(LegacyVipStatusFlags.VipLevelOne, LegacyVipStatusPacket.FlagsForVipLevel(1));
        Assert.Equal(LegacyVipStatusFlags.VipLevelOne, LegacyVipStatusPacket.FlagsForVipLevel(5));
        Assert.Equal(LegacyVipStatusFlags.FullVip, LegacyVipStatusPacket.FlagsForVipLevel(6));
    }

    private static CharacterSnapshot Snapshot(long vipLevel)
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
            VipLevel={{vipLevel}}
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
