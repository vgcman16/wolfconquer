using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyApprenticeInformationPacketTests
{
    [Fact]
    public void OfflineApprenticeStartupWritesSyntheticExpectedLayout()
    {
        Assert.True(LegacyApprenticeInformationStartupPackets.TryCreateOfflineApprentice(
            CreateRecord(), out var packet, emitClientCaughtStartupFrame: true));
        Assert.Equal(CreateSyntheticFrame(), packet.ToFrameBytes());
        Assert.Equal(103_004U, packet.Mesh);
        Assert.Equal(42U, packet.Level);
        Assert.Equal(11U, packet.Class);
        Assert.Equal((ushort)2200, LegacyApprenticeInformationPacket.Opcode);
    }

    [Fact]
    public void OfflineApprenticeStartupIsFencedByDefaultForStagedClient()
    {
        var record = CreateRecord();
        Assert.False(LegacyApprenticeInformationStartupPackets.TryCreateOfflineApprentice(record, out _));
        Assert.Empty(LegacyApprenticeInformationStartupPackets.CreateOfflineApprenticeStartup(
            [record], ownerId: 12_001));
    }

    [Fact]
    public void ApprenticeInformationParserReadsSyntheticActionAndStringLayout()
    {
        Assert.True(LegacyApprenticeInformationPacket.TryParse(ReadFrame(CreateSyntheticFrame()), out var packet));
        Assert.Equal(LegacyApprenticeInformationAction.Apprentice, packet.Mode);
        Assert.Equal("NULL", packet.MentorName);
        Assert.Equal("FixtureScout", packet.ApprenticeName);
        Assert.Equal("NULL", packet.ApprenticeSpouse);
        Assert.Equal("fixture-signature", packet.PersonalSignature);
        Assert.Equal(12_001U, packet.MentorId);
        Assert.Equal(12_002U, packet.ApprenticeId);
    }

    [Fact]
    public void StartupFiltersUnsupportedRelationTypesAndOwners()
    {
        var records = new[] { CreateRecord(), CreateRecord(relationType: 3), CreateRecord(ownerId: 12_003) };
        Assert.False(LegacyApprenticeInformationStartupPackets.TryCreateOfflineApprentice(
            records[1], out _, emitClientCaughtStartupFrame: true));
        var packets = LegacyApprenticeInformationStartupPackets.CreateOfflineApprenticeStartup(
            records, ownerId: 12_001, emitClientCaughtStartupFrame: true);
        Assert.Equal(12_002U, Assert.Single(packets).ApprenticeId);
    }

    [Fact]
    public void StartupSuppressesMalformedOfflineApprenticeRows()
    {
        var valid = CreateRecord();
        var malformed = new[]
        {
            valid with { Values = [0, 0] },
            valid with { TargetId = -1 },
            valid with { Values = [0, 0, 0, 37, 0, 0, 0, 0, 0, 0, 0, -1, 42, 2] },
            valid with { TargetName = "" },
        };
        foreach (var record in malformed)
        {
            Assert.False(LegacyApprenticeInformationStartupPackets.TryCreateOfflineApprentice(
                record, out _, emitClientCaughtStartupFrame: true));
        }
        var packets = LegacyApprenticeInformationStartupPackets.CreateOfflineApprenticeStartup(
            [.. malformed, valid], ownerId: 12_001, emitClientCaughtStartupFrame: true);
        Assert.Equal("FixtureScout", Assert.Single(packets).ApprenticeName);
    }

    [Fact]
    public void OfflineApprenticeStartupMapsNumericFields()
    {
        Assert.True(LegacyApprenticeInformationStartupPackets.TryCreateOfflineApprentice(
            CreateRecord(), out var packet, emitClientCaughtStartupFrame: true));
        Assert.Equal(12_001U, packet.MentorId);
        Assert.Equal(12_002U, packet.ApprenticeId);
        Assert.Equal(103_004U, packet.Mesh);
        Assert.Equal(0U, packet.SharedBattlePower);
        Assert.Equal(37U, packet.EnroleDate);
        Assert.Equal(42U, packet.Level);
        Assert.Equal(11U, packet.Class);
        Assert.Equal((ushort)0, packet.PkPoints);
        Assert.Equal(0U, packet.Online);
        Assert.Equal(0U, packet.Unknown2);
        Assert.Equal(0UL, packet.ApprenticeExperience);
        Assert.Equal((ushort)0, packet.ApprenticeBlessing);
        Assert.Equal((ushort)0, packet.ApprenticeComposing);
    }

    [Fact]
    public void ApprenticeInformationParserRejectsWrongOpcodeAndMalformedStrings()
    {
        var wrongOpcode = new PacketWriter(2201, LegacyApprenticeInformationPacket.MinimumPayloadLength);
        Assert.False(LegacyApprenticeInformationPacket.TryParse(ReadFrame(wrongOpcode.ToArray()), out _));
        var malformed = CreateSyntheticFrame();
        malformed[PacketHeader.Size + 80] = 3;
        Assert.False(LegacyApprenticeInformationPacket.TryParse(ReadFrame(malformed), out _));
    }

    private static SocialAssociationRecord CreateRecord(int ownerId = 12_001, int relationType = 5)
    {
        return SocialAssociationParser.ParseRow("synthetic-associations.txt", 2,
            $"{ownerId}/{relationType}/0/0/0/12002/37/0/0/0/FixtureScout/0/0/0/0/103004/42/2/fixture-signature/11/");
    }

    private static byte[] CreateSyntheticFrame()
    {
        // Independently assemble a fixture; do not obtain expected bytes from the production serializer.
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)0);
        writer.Write((ushort)2200);
        foreach (uint value in new uint[] { 2, 12_001, 12_002, 103_004, 0, 999_999, 37, 42, 11 })
        {
            writer.Write(value);
        }
        writer.Write((ushort)0);
        writer.Write(new byte[22]);
        writer.Write(0U);
        writer.Write(0U);
        writer.Write(0UL);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((byte)4);
        foreach (var value in new[] { "NULL", "FixtureScout", "NULL", "fixture-signature" })
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }
        stream.Position = 0;
        writer.Write(checked((ushort)stream.Length));
        return stream.ToArray();
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
