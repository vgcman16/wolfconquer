using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyAttributeSetPacketTests : IDisposable
{
    private readonly string _usersRoot = Path.Combine(Path.GetTempPath(), "wolf-attribute-set-handler-" + Guid.NewGuid());

    [Fact]
    public void LegacyAttributeSetOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2203, WorldPacketOpcodes.LegacyAttributeSet);
        Assert.Equal(WorldPacketOpcodes.LegacyAttributeSet, LegacyAttributeSetPacket.Opcode);
    }

    [Fact]
    public void LegacyAttributeSetPacketRoundTripsSourceShape()
    {
        var packet = new LegacyAttributeSetPacket(
            Strength: 3,
            Agility: 2,
            Vitality: 4,
            Spirit: 1);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyAttributeSet, frame.Header.Opcode);
        Assert.Equal(LegacyAttributeSetPacket.PayloadLength, frame.Payload.Length);
        Assert.True(LegacyAttributeSetPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
        Assert.Equal(10U, parsed.Total);
    }

    [Fact]
    public async Task HandlerEchoesAcceptedAttributeSetAndUpdatesRemainingPoints()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, attributePoints: 12, maxHitpoints: 61_512);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot));
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await LoginAsync(handler, connection, characterUid: 1293600);
        var countBeforeFirstRequest = sent.Count;

        var accepted = new LegacyAttributeSetPacket(Strength: 3, Agility: 2, Vitality: 4, Spirit: 1);
        await handler.HandleFrameAsync(connection, ReadFrame(accepted.ToFrameBytes()));

        Assert.Equal(countBeforeFirstRequest + 4, sent.Count);
        var echo = sent[countBeforeFirstRequest];
        Assert.True(LegacyAttributeSetPacket.TryParse(ReadFrame(echo), out var parsedEcho));
        Assert.Equal(accepted, parsedEcho);
        AssertHitpointRefresh(sent[countBeforeFirstRequest + 1], sent[countBeforeFirstRequest + 2]);
        Assert.True(
            LegacyUserTotalRefineLevPacket.TryParse(ReadFrame(sent[countBeforeFirstRequest + 3]), out var refine));
        Assert.Equal(0U, refine.Type);
        Assert.Equal(1_293_600U, refine.Uid);

        var countBeforeSecondRequest = sent.Count;
        var overspendAfterMutation = new LegacyAttributeSetPacket(Strength: 3, Agility: 0, Vitality: 0, Spirit: 0);
        await handler.HandleFrameAsync(connection, ReadFrame(overspendAfterMutation.ToFrameBytes()));

        Assert.Equal(countBeforeSecondRequest, sent.Count);
    }

    [Fact]
    public async Task HandlerSkipsAttributeSetWhenRequestExceedsAvailablePoints()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, attributePoints: 2);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot));
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await LoginAsync(handler, connection, characterUid: 1293600);
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyAttributeSetPacket(Strength: 3, Agility: 0, Vitality: 0, Spirit: 0).ToFrameBytes()));

        Assert.Equal(countBeforeRequest, sent.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_usersRoot))
        {
            Directory.Delete(_usersRoot, recursive: true);
        }
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static ValueTask LoginAsync(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        uint characterUid)
    {
        return handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(characterUid).ToFrameBytes()));
    }

    private static void AssertHitpointRefresh(byte[] maxFrameBytes, byte[] pairFrameBytes)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(maxFrameBytes), out var maxFrame));
        Assert.Equal(1_293_600U, maxFrame.Uid);
        var maxUpdate = Assert.Single(maxFrame.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maxUpdate.Type);
        Assert.Equal(61_512UL, maxUpdate.Value);

        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(pairFrameBytes), out var pairFrame));
        Assert.Equal(1_293_600U, pairFrame.Uid);
        Assert.Equal(2, pairFrame.Updates.Count);
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, pairFrame.Updates[0].Type);
        Assert.Equal(61_512UL, pairFrame.Updates[0].Value);
        Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, pairFrame.Updates[1].Type);
        Assert.Equal(61_258UL, pairFrame.Updates[1].Value);
    }

    private void WriteSnapshot(long accountId, long uid, long attributePoints, uint maxHitpoints = 0)
    {
        var maxHitpointsLine = maxHitpoints == 0 ? "" : $"MaxHitpoints={maxHitpoints}\r\n";

        File.WriteAllText(
            Path.Combine(_usersRoot, accountId + ".ini"),
            $$"""
            [Character]
            UID={{uid}}
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Conquer=0
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
            Atributes={{attributePoints}}
            MinHitPoints=61258
            {{maxHitpointsLine}}MinMana=5000
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            RacePoints=0
            ChampionPoints=0
            GuildDontion=900
            PkPoints=0
            Online=7
            Day={{DateTime.Now.DayOfYear}}
            Achivement=0/171966688/262144/523784/939949824/524288/1342381056/3/576/0/0/524288/25167856/6817280/0/
            Activeness=0/80/2/1/10/1/22/0/0/
            Dragon=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Pheonix=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Tiger=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Turtle=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            ChiPoints=153850

            [PaidNobility]
            Rank=King
            Identifier=1293600
            """);
    }
}
