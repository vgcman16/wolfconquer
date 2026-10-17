using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class VisibleDx8D13TrainingVitalityInfoPacketTests
{
    private const string PowerLine = "1/5963000/8/2500/1/3/200/0/6/3500/0/2/200/0/";

    [Fact]
    public void VisibleDx8D13UsesClientOwned2299WithProvenChiLayout()
    {
        var snapshot = CreateSnapshot();
        var bytes = ChiInfoPacketProfileSelector.CreateFrame(
            ConquerProtocolProfile.VisibleDx8D13,
            snapshot,
            LegacyChiInfoAction.Send);
        var frame = ReadFrame(bytes);

        Assert.Equal((ushort)2299, frame.Header.Opcode);
        Assert.True(VisibleDx8D13TrainingVitalityInfoPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyChiInfoAction.Send, parsed.Action);
        Assert.Equal(1_293_600U, parsed.Uid);
        Assert.Equal(153_850, parsed.ChiPoints);
        Assert.Equal(1U, parsed.LockedFlags);
        var power = Assert.Single(parsed.Powers);
        Assert.Equal(LegacyChiPowerType.Dragon, power.Type);
        Assert.Equal(5_963_000U, power.Experience);
        Assert.True(power.Attributes[0].Locked);

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal((ushort)LegacyChiInfoAction.Send, reader.ReadUInt16(0));
        Assert.Equal(1_293_600U, reader.ReadUInt32(2));
        Assert.Equal(153_850, reader.ReadInt32(6));
        Assert.Equal(1U, reader.ReadUInt32(10));
        Assert.Equal(1U, reader.ReadUInt32(14));
        Assert.Equal((byte)LegacyChiPowerType.Dragon, reader.Payload[18]);
    }

    [Fact]
    public void RootD11KeepsLegacy2204Opcode()
    {
        var frame = ReadFrame(
            ChiInfoPacketProfileSelector.CreateFrame(
                ConquerProtocolProfile.RootD11_7899,
                CreateSnapshot(),
                LegacyChiInfoAction.Upgrade));

        Assert.Equal(LegacyChiInfoPacket.Opcode, frame.Header.Opcode);
        Assert.Equal((ushort)2204, frame.Header.Opcode);
        Assert.True(LegacyChiInfoPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyChiInfoAction.Upgrade, parsed.Action);
        Assert.False(VisibleDx8D13TrainingVitalityInfoPacket.TryParse(frame, out _));
    }

    [Fact]
    public void D13ParserRejectsLegacyVipUserHandleOpcode()
    {
        var legacyFrame = ReadFrame(LegacyChiInfoPacket.FromSnapshot(CreateSnapshot()).ToFrameBytes());

        Assert.Equal((ushort)2204, legacyFrame.Header.Opcode);
        Assert.False(VisibleDx8D13TrainingVitalityInfoPacket.TryParse(legacyFrame, out _));
    }

    private static CharacterSnapshot CreateSnapshot()
    {
        return new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "Chi",
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: 10,
            FirstClass: 10,
            SecondClass: 0,
            Reborn: 1,
            Level: 140,
            Experience: 0,
            MapId: 1002,
            X: 430,
            Y: 378,
            PreviousMapId: 1002,
            PreviousMapX: 430,
            PreviousMapY: 378,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: 1,
            MinMana: 0,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture",
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ChiPoints"] = "153850",
                ["Dragon"] = PowerLine,
            },
            PaidNobility: new Dictionary<string, string>());
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
