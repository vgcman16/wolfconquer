using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyHeroRewardPacketTests
{
    [Fact]
    public void LegacyHeroRewardOpcodesMatchLegacyWorldOpcodes()
    {
        Assert.Equal((ushort)2077, WorldPacketOpcodes.LegacyHeroReward);
        Assert.Equal((ushort)2063, WorldPacketOpcodes.LegacyHeroRewardsInfo);
        Assert.Equal(WorldPacketOpcodes.LegacyHeroReward, LegacyHeroRewardPacket.Opcode);
        Assert.Equal(WorldPacketOpcodes.LegacyHeroRewardsInfo, LegacyHeroRewardListPacket.Opcode);
    }

    [Fact]
    public void LegacyHeroRewardRequestRoundTripsSourceHeader()
    {
        var packet = new LegacyHeroRewardPacket(LegacyHeroRewardAction.Show, Prize: 5);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyHeroReward, frame.Header.Opcode);
        Assert.Equal(LegacyHeroRewardPacket.PayloadLength, frame.Payload.Length);
        Assert.True(LegacyHeroRewardPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyHeroRewardAction.Show, parsed.Action);
        Assert.Equal(5U, parsed.Prize);
    }

    [Fact]
    public void LegacyHeroRewardRequestAcceptsTrailingSourceBytes()
    {
        var writer = new PacketWriter(LegacyHeroRewardPacket.Opcode, payloadLength: 9);
        writer.Payload[0] = (byte)LegacyHeroRewardAction.ClaimSubPrize;
        writer.WriteUInt32(1, 7);
        writer.WriteUInt32(5, 0xAABBCCDD);
        var frame = ReadFrame(writer.ToArray());

        Assert.True(LegacyHeroRewardPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyHeroRewardAction.ClaimSubPrize, parsed.Action);
        Assert.Equal(7U, parsed.Prize);
    }

    [Fact]
    public void LegacyHeroRewardRequestRejectsShortPayload()
    {
        var writer = new PacketWriter(LegacyHeroRewardPacket.Opcode, payloadLength: 4);

        Assert.False(LegacyHeroRewardPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    [Fact]
    public void LegacyHeroRewardListRoundTripsItems()
    {
        var packet = new LegacyHeroRewardListPacket(
            Stage: 5,
            AllDone: 1,
            Items:
            [
                new LegacyHeroRewardListItem(Uid: 501, Unknown: 0, Claim: 1),
                new LegacyHeroRewardListItem(Uid: 506, Unknown: 0, Claim: 0),
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyHeroRewardsInfo, frame.Header.Opcode);
        Assert.True(LegacyHeroRewardListPacket.TryParse(frame, out var parsed));
        Assert.Equal(5U, parsed.Stage);
        Assert.Equal(1, parsed.AllDone);
        Assert.Equal(
            [new LegacyHeroRewardListItem(Uid: 501, Unknown: 0, Claim: 1), new LegacyHeroRewardListItem(Uid: 506, Unknown: 0, Claim: 0)],
            parsed.Items);
    }

    [Fact]
    public async Task HandlerSendsHeroRewardListFromRuntimeState()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(new Dictionary<string, string>
        {
            ["HeroRewards"] = "4/501/1/506/1/5/1/602/0/",
        });
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session),
            ReadFrame(new LegacyHeroRewardPacket(LegacyHeroRewardAction.Show, Prize: 5).ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyHeroRewardListPacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(5U, response.Stage);
        Assert.Equal(1, response.AllDone);
        Assert.Equal(
            [
                new LegacyHeroRewardListItem(Uid: 501, Unknown: 0, Claim: 1),
                new LegacyHeroRewardListItem(Uid: 506, Unknown: 0, Claim: 1),
                new LegacyHeroRewardListItem(Uid: 5, Unknown: 0, Claim: 1),
            ],
            response.Items);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyHeroReward session=accountId=1293600 username=HeroOne characterUid=1293600 characterName=HeroOne", output);
        Assert.Contains("action=Show prize=5 stage=5 goals=3 allDone=1.", output);
    }

    [Theory(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    [InlineData(101U, 723700U, 0)]
    [InlineData(102U, 1088001U, 0)]
    [InlineData(201U, 730001U, 1)]
    public async Task HandlerClaimPrizeGrantsConfiguredBoundRewardAndMarksGoalClaimed(
        uint prize,
        uint expectedItemId,
        byte expectedPlus)
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(new Dictionary<string, string>
        {
            ["HeroRewards"] = $"1/{prize}/0/",
        });
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new LegacyHeroRewardPacket(LegacyHeroRewardAction.ClaimPrize, prize).ToFrameBytes()));

        Assert.Equal(2, sent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[0]), out var item));
        Assert.Equal(expectedItemId, item.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, item.Mode);
        Assert.Equal(1, item.Bound);
        Assert.Equal(expectedPlus, item.Plus);
        Assert.Equal(1, item.StackSize);
        Assert.True(LegacyHeroRewardPacket.TryParse(ReadFrame(sent[1]), out var claimEcho));
        Assert.Equal(LegacyHeroRewardAction.ClaimPrize, claimEcho.Action);
        Assert.Equal(prize, claimEcho.Prize);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal($"1/{prize}/1/", updatedSession!.Character.Attributes["HeroRewards"]);
        var reward = Assert.Single(updatedSession.PlayerItems.InventoryItems);
        Assert.Equal(expectedItemId, reward.ItemId);
        Assert.Equal(1, reward.Bound);
        Assert.Equal(expectedPlus, reward.Plus);
        Assert.Contains($"WorldServer handled LegacyHeroReward claim", log.ToString());
        Assert.Contains($"action=ClaimPrize prize={prize}", log.ToString());
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerClaimPrizeSendsMessageBoxAndDoesNotClaimWhenInventoryIsFull()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            new Dictionary<string, string>
            {
                ["HeroRewards"] = "1/101/0/",
            },
            new PlayerItemSnapshot(
                Enumerable.Range(1, 40)
                    .Select(index => new PlayerItemSnapshotItem
                    {
                        Uid = (uint)index,
                        ItemId = 1000000,
                        Durability = 1,
                        MaximumDurability = 1,
                        StackSize = 1,
                    })
                    .ToArray(),
                [],
                [],
                [],
                []));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new LegacyHeroRewardPacket(LegacyHeroRewardAction.ClaimPrize, Prize: 101).ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(responseBytes), out var messageBox));
        Assert.Equal("Please make 1 spaces in your inventory.", messageBox.Text);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("1/101/0/", updatedSession!.Character.Attributes["HeroRewards"]);
        Assert.DoesNotContain(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == 723700);
        Assert.Empty(updatedSession.PlayerItems.WarehouseItems);
        Assert.Contains("inventory is full", log.ToString());
    }

    private static WorldSession CreateSession(
        IReadOnlyDictionary<string, string> attributes,
        PlayerItemSnapshot? playerItems = null)
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "HeroOne",
            CreateSnapshot(attributes),
            playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }

    private static CharacterSnapshot CreateSnapshot(IReadOnlyDictionary<string, string> attributes)
    {
        return new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "HeroOne",
            Body: 1008,
            Face: 154,
            Hair: 27,
            Class: 7051,
            FirstClass: 0,
            SecondClass: 7050,
            Reborn: 2,
            Level: 140,
            Experience: 0,
            MapId: 1015,
            X: 830,
            Y: 584,
            PreviousMapId: 1000,
            PreviousMapX: 500,
            PreviousMapY: 650,
            Strength: 0,
            Agility: 36,
            Vitality: 447,
            Spirit: 0,
            AttributePoints: 12,
            MinHitPoints: 61_258,
            MinMana: 5_000,
            Money: 98_602_439_956,
            ConquerPoints: 639_778_904,
            BoundConquerPoints: 456,
            BankConquerPoints: 123,
            PkPoints: 0,
            Online: 7,
            SourceFile: "1293600.ini",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
    }

    private static ConquerTcpConnection CreateConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
        });
    }

    private static string FindRepositoryRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "WolfConquer.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName ?? "";
        }

        throw new DirectoryNotFoundException("Could not find WolfConquer.sln.");
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
