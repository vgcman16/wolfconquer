using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFullAccountColorActionTests
{
    private const uint NpcId = 465_123;
    private const long CharacterUid = 1_293_870;

    [Theory]
    [InlineData(20, 5_855_577U)]
    [InlineData(21, 0U)]
    public async Task ColorOptionsSendOnlyCurrent7899LegacyColorAction(
        byte optionId,
        uint expectedColor)
    {
        var sent = new List<byte[]>();
        var connection = new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9_958),
            (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        var session = CreateSession();
        using var log = new StringWriter();

        var handled = await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            NpcId,
            optionId,
            input: string.Empty,
            CancellationToken.None);

        Assert.True(handled);
        var frame = ReadFrame(Assert.Single(sent));
        Assert.Equal((ushort)2_046, frame.Header.Opcode);
        Assert.Equal(MsgActionPacket.Opcode, frame.Header.Opcode);
        Assert.True(MsgActionPacket.TryParse(frame, out var action));
        Assert.Equal(checked((uint)CharacterUid), action.ObjId);
        Assert.Equal(MsgActionType.LegacyColor, action.Type);
        Assert.Equal((ushort)104, (ushort)action.Type);
        Assert.Equal(expectedColor, action.DwParam);
        Assert.Equal(430U, action.PositionX);
        Assert.Equal(378U, action.PositionY);
        Assert.Equal(0U, action.Timestamp);
        Assert.DoesNotContain(sent, bytes => LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out _));
        Assert.Contains($"optionId={optionId}", log.ToString(), StringComparison.Ordinal);
        Assert.Contains($"color={expectedColor}", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("packetOrder=MsgAction", log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
    }

    private static WorldSession CreateSession()
    {
        return new WorldSession(
            CharacterUid,
            Username: "full-account-color-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "FullAccountColorHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 15,
                FirstClass: 15,
                SecondClass: 0,
                Reborn: 2,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
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
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
    }
}
