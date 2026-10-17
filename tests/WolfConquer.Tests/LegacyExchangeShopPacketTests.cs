using System.Buffers;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyExchangeShopPacketTests
{
    [Fact]
    public void LegacyExchangeShopOpcodesMatchLegacyWorldOpcodes()
    {
        Assert.Equal((ushort)2109, WorldPacketOpcodes.LegacyExchangeShopBuy);
        Assert.Equal((ushort)2212, WorldPacketOpcodes.LegacyExchangeShopGoodsInfo);
        Assert.Equal((ushort)2317, WorldPacketOpcodes.LegacyExchangeShopGoods);
        Assert.Equal(WorldPacketOpcodes.LegacyExchangeShopBuy, LegacyExchangeShopBuyPacket.Opcode);
        Assert.Equal(WorldPacketOpcodes.LegacyExchangeShopGoodsInfo, LegacyExchangeShopGoodsInfoPacket.Opcode);
        Assert.Equal(WorldPacketOpcodes.LegacyExchangeShopGoods, LegacyExchangeShopGoodsPacket.Opcode);
    }

    [Fact]
    public void LegacyExchangeShopBuyRoundTripsSourceShapedGoodsRequest()
    {
        var packet = new LegacyExchangeShopBuyPacket(
            LegacyExchangeShopBuyType.Goods,
            NpcID: 24_702,
            ShopID: 168,
            Count: 2,
            Index: 444,
            Items: [new LegacyExchangeShopBuyItem(ID: 3_303_526, Count: 4)]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyExchangeShopBuyPacket.Opcode, frame.Header.Opcode);
        Assert.Equal("080010FEC00118A801200228BC03320708E6D0C9011004", Convert.ToHexString(frame.Payload.ToArray()));
        Assert.True(LegacyExchangeShopBuyPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyExchangeShopBuyType.Goods, parsed.Type);
        Assert.Equal(24_702U, parsed.NpcID);
        Assert.Equal(168U, parsed.ShopID);
        Assert.Equal(2U, parsed.Count);
        Assert.Equal(444U, parsed.Index);
        var item = Assert.Single(parsed.Items);
        Assert.Equal(3_303_526U, item.ID);
        Assert.Equal(4U, item.Count);
    }

    [Fact]
    public void LegacyExchangeShopBuyRoundTripsGoodsExEchoResponse()
    {
        var packet = new LegacyExchangeShopBuyPacket(
            LegacyExchangeShopBuyType.GoodsEx,
            NpcID: 24_702,
            ShopID: 168,
            Count: 1,
            Index: 9,
            Items: []);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyExchangeShopBuyPacket.Opcode, frame.Header.Opcode);
        Assert.True(LegacyExchangeShopBuyPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyExchangeShopBuyType.GoodsEx, parsed.Type);
        Assert.Equal(24_702U, parsed.NpcID);
        Assert.Equal(168U, parsed.ShopID);
        Assert.Equal(1U, parsed.Count);
        Assert.Equal(9U, parsed.Index);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyExchangeShopBuyRejectsMissingRequiredFields()
    {
        var writer = new PacketWriter(LegacyExchangeShopBuyPacket.Opcode, payloadLength: 2);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = (byte)LegacyExchangeShopBuyType.Goods;
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyExchangeShopBuyPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyExchangeShopGoodsInfoRoundTripsShowRequest()
    {
        var packet = new LegacyExchangeShopGoodsInfoPacket(
            LegacyExchangeShopGoodsInfoType.Show,
            NpcID: 123,
            ShopID: 168);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyExchangeShopGoodsInfo, frame.Header.Opcode);
        Assert.True(LegacyExchangeShopGoodsInfoPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyExchangeShopGoodsInfoType.Show, parsed.Type);
        Assert.Equal(123U, parsed.NpcID);
        Assert.Equal(168U, parsed.ShopID);
    }

    [Fact]
    public void LegacyExchangeShopGoodsInfoRejectsMissingRequiredFields()
    {
        var writer = new PacketWriter(LegacyExchangeShopGoodsInfoPacket.Opcode, payloadLength: 2);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = (byte)LegacyExchangeShopGoodsInfoType.Show;
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyExchangeShopGoodsInfoPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyExchangeShopGoodsInfoParsesSyntheticOpenRequest()
    {
        var writer = new PacketWriter(LegacyExchangeShopGoodsInfoPacket.Opcode, payloadLength: 6);
        byte[] syntheticPayload = [0x08, 3, 0x10, 77, 0x18, 0];
        syntheticPayload.CopyTo(writer.Payload);
        var frame = ReadFrame(writer.ToArray());

        Assert.True(LegacyExchangeShopGoodsInfoPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyExchangeShopGoodsInfoType.Open, parsed.Type);
        Assert.Equal(77U, parsed.NpcID);
        Assert.Equal(0U, parsed.ShopID);
    }

    [Fact]
    public async Task HandlerRepliesToSyntheticOpenTypeWithEmptyRandomShop()
    {
        var usersRoot = Path.Combine(Path.GetTempPath(), "wolf-exchange-open-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(usersRoot);
            File.WriteAllText(Path.Combine(usersRoot, "41001.ini"), SnapshotText());
            var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot));
            var sent = new List<byte[]>();
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });

            await handler.HandleFrameAsync(connection, ReadFrame(new LegacyMsgLoginGamePacket(41_001).ToFrameBytes()));
            var countBeforeQuery = sent.Count;
            var writer = new PacketWriter(LegacyExchangeShopGoodsInfoPacket.Opcode, payloadLength: 6);
            byte[] syntheticPayload = [0x08, 3, 0x10, 77, 0x18, 0];
            syntheticPayload.CopyTo(writer.Payload);

            await handler.HandleFrameAsync(connection, ReadFrame(writer.ToArray()));

            Assert.Equal(countBeforeQuery + 1, sent.Count);
            Assert.True(LegacyExchangeShopGoodsPacket.TryParse(ReadFrame(sent[countBeforeQuery]), out var response));
            Assert.Equal(77U, response.NpcID);
            Assert.Equal(168U, response.ShopID);
            Assert.Equal(41_001U, response.Uid);
            Assert.Empty(response.Items);
        }
        finally
        {
            if (Directory.Exists(usersRoot))
            {
                Directory.Delete(usersRoot, recursive: true);
            }
        }
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerExchangeShopGoodsInfoRefreshSpendsDonationAndOpensGoodsExRandomShop()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var usersRoot = Path.Combine(Path.GetTempPath(), "wolf-exchange-refresh-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(usersRoot);
            File.WriteAllText(Path.Combine(usersRoot, "41001.ini"), SnapshotText(guildDonation: 100));
            using var log = new StringWriter();
            var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
            var handler = new WorldServerFrameHandler(
                new CharacterSnapshotLoader(usersRoot),
                handoffService: null,
                worldData,
                log);
            var sent = new List<byte[]>();
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });

            await handler.HandleFrameAsync(connection, ReadFrame(new LegacyMsgLoginGamePacket(41_001).ToFrameBytes()));
            var countBeforeQuery = sent.Count;
            var request = new LegacyExchangeShopGoodsInfoPacket(
                LegacyExchangeShopGoodsInfoType.Refresh,
                NpcID: 24_702,
                ShopID: 168);

            await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));

            Assert.Equal(countBeforeQuery + 3, sent.Count);
            Assert.True(LegacyExchangeShopGoodsPacket.TryParse(ReadFrame(sent[countBeforeQuery]), out var response));
            Assert.Equal(24_702U, response.NpcID);
            Assert.Equal(168U, response.ShopID);
            Assert.Equal(41_001U, response.Uid);
            Assert.Equal(8, response.Items.Count);

            var goodsExShopIds = worldData.ExchangeShopGoodsEx
                .Where(row => row.Fields.Count >= 3 && row.Fields[2] == "168")
                .Select(row => uint.Parse(row.Fields[0], CultureInfo.InvariantCulture))
                .ToHashSet();
            Assert.All(response.Items, item =>
            {
                Assert.Contains(item.Index, goodsExShopIds);
                Assert.Equal(0U, item.Count);
                Assert.Equal(0U, item.Unknown);
            });
            Assert.Equal(response.Items.Count, response.Items.Select(item => item.Index).Distinct().Count());

            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[countBeforeQuery + 1]), out var donationUpdate));
            Assert.Equal(LegacyMsgUpdateDataType.MyDontion, Assert.Single(donationUpdate.Updates).Type);
            Assert.Equal(50UL, donationUpdate.Updates[0].Value);
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[countBeforeQuery + 2]), out var mirrorUpdate));
            Assert.Equal(LegacyMsgUpdateDataType.DonationPoints, Assert.Single(mirrorUpdate.Updates).Type);
            Assert.Equal(50UL, mirrorUpdate.Updates[0].Value);

            var persistedCharacter = new CharacterSnapshotLoader(usersRoot).LoadByAccountId(41_001);
            Assert.NotNull(persistedCharacter);
            Assert.Equal("50", persistedCharacter.Attributes["GuildDontion"]);

            var output = log.ToString();
            Assert.Contains("handled LegacyExchangeShopGoodsInfo", output);
            Assert.Contains("source=MrRayzo.ElTabakh.CMsgExchangeShopGoodsInfo.Process2", output);
            Assert.Contains("guildDonation=50", output);
        }
        finally
        {
            if (Directory.Exists(usersRoot))
            {
                Directory.Delete(usersRoot, recursive: true);
            }
        }
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerExchangeShopBuyGoodsConsumesMaterialsAddsRewardAndEchoes()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "wolf-exchange-buy-" + Guid.NewGuid());
        var usersRoot = Path.Combine(tempRoot, "Users");
        var itemsRoot = Path.Combine(tempRoot, "PlayersItems");
        try
        {
            Directory.CreateDirectory(usersRoot);
            Directory.CreateDirectory(itemsRoot);
            File.WriteAllText(Path.Combine(usersRoot, "41001.ini"), SnapshotText());
            File.WriteAllBytes(
                Path.Combine(itemsRoot, "41001.bin"),
                PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                [
                    PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 3_303_483, position: 0, stackSize: 300),
                    PlayerItemSnapshotLoaderTests.DbItem(uid: 2002, itemId: 3_303_485, position: 0, stackSize: 1),
                ],
                []));
            using var log = new StringWriter();
            var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
            var handler = new WorldServerFrameHandler(
                new CharacterSnapshotLoader(usersRoot, itemsRoot, playerSpellsRoot: null, playerProficienciesRoot: null),
                handoffService: null,
                worldData,
                log);
            var sent = new List<byte[]>();
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });

            await handler.HandleFrameAsync(connection, ReadFrame(new LegacyMsgLoginGamePacket(41_001).ToFrameBytes()));
            var countBeforeBuy = sent.Count;
            var request = new LegacyExchangeShopBuyPacket(
                LegacyExchangeShopBuyType.Goods,
                NpcID: 24_702,
                ShopID: 23,
                Count: 1,
                Index: 444,
                Items: []);

            await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));

            Assert.Equal(countBeforeBuy + 4, sent.Count);
            Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(sent[countBeforeBuy]), out var removeWonderHerb));
            Assert.Equal(2001U, removeWonderHerb.Id);
            Assert.Equal(MsgItemUsageAction.RemoveInventory, removeWonderHerb.Action);
            Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(sent[countBeforeBuy + 1]), out var removeCertificate));
            Assert.Equal(2002U, removeCertificate.Id);
            Assert.Equal(MsgItemUsageAction.RemoveInventory, removeCertificate.Action);

            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeBuy + 2]), out var reward));
            Assert.Equal(1U, reward.Uid);
            Assert.Equal(3_303_526U, reward.ItemId);
            Assert.Equal(LegacyGameItemPacket.AddItemMode, reward.Mode);
            Assert.Equal((ushort)1, reward.StackSize);

            Assert.True(LegacyExchangeShopBuyPacket.TryParse(ReadFrame(sent[countBeforeBuy + 3]), out var echo));
            Assert.Equal(request.Type, echo.Type);
            Assert.Equal(request.NpcID, echo.NpcID);
            Assert.Equal(request.ShopID, echo.ShopID);
            Assert.Equal(request.Count, echo.Count);
            Assert.Equal(request.Index, echo.Index);
            Assert.Empty(echo.Items);

            var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(41_001);
            Assert.DoesNotContain(persistedItems.InventoryItems, item => item.ItemId == 3_303_483);
            Assert.DoesNotContain(persistedItems.InventoryItems, item => item.ItemId == 3_303_485);
            var persistedReward = Assert.Single(
                persistedItems.InventoryItems,
                item => item.ItemId == 3_303_526);
            Assert.Equal(1U, persistedReward.Uid);
            Assert.Equal((ushort)1, persistedReward.StackSize);

            var output = log.ToString();
            Assert.Contains("persisted LegacyExchangeShopBuy items", output);
            Assert.Contains("source=MrRayzo.ElTabakh.CMsgExchangeShopBuy.Process1.Type0", output);
            Assert.Contains("handled LegacyExchangeShopBuy", output);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerExchangeShopBuyGoodsTracksLegacySessionAmountLimit()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "wolf-exchange-buy-limit-" + Guid.NewGuid());
        var usersRoot = Path.Combine(tempRoot, "Users");
        var itemsRoot = Path.Combine(tempRoot, "PlayersItems");
        try
        {
            Directory.CreateDirectory(usersRoot);
            Directory.CreateDirectory(itemsRoot);
            File.WriteAllText(Path.Combine(usersRoot, "41001.ini"), SnapshotText());
            File.WriteAllBytes(
                Path.Combine(itemsRoot, "41001.bin"),
                PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                [
                    PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 3_303_483, position: 0, stackSize: 600),
                    PlayerItemSnapshotLoaderTests.DbItem(uid: 2002, itemId: 3_303_485, position: 0, stackSize: 2),
                ],
                []));
            using var log = new StringWriter();
            var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
            var handler = new WorldServerFrameHandler(
                new CharacterSnapshotLoader(usersRoot, itemsRoot, playerSpellsRoot: null, playerProficienciesRoot: null),
                handoffService: null,
                worldData,
                log);
            var sent = new List<byte[]>();
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });

            await handler.HandleFrameAsync(connection, ReadFrame(new LegacyMsgLoginGamePacket(41_001).ToFrameBytes()));
            var countBeforeBuy = sent.Count;
            var request = new LegacyExchangeShopBuyPacket(
                LegacyExchangeShopBuyType.Goods,
                NpcID: 24_702,
                ShopID: 23,
                Count: 1,
                Index: 444,
                Items: []);

            await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));
            var countAfterFirstBuy = sent.Count;
            await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));

            Assert.Equal(countBeforeBuy + 4, countAfterFirstBuy);
            Assert.Equal(countAfterFirstBuy, sent.Count);

            var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(41_001);
            Assert.Single(persistedItems.InventoryItems, item => item.ItemId == 3_303_526);
            Assert.Contains(persistedItems.InventoryItems, item => item.ItemId == 3_303_483 && item.StackSize == 300);
            Assert.Contains(persistedItems.InventoryItems, item => item.ItemId == 3_303_485 && item.StackSize == 1);

            var output = log.ToString();
            Assert.Contains("handled LegacyExchangeShopBuy", output);
            Assert.Contains("amountLimit=1", output);
            Assert.Contains("purchasedCount=1", output);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerExchangeShopBuyGoodsExDefersRandomDonationExchange()
    {
        var usersRoot = Path.Combine(Path.GetTempPath(), "wolf-exchange-buy-ex-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(usersRoot);
            File.WriteAllText(Path.Combine(usersRoot, "41001.ini"), SnapshotText());
            using var log = new StringWriter();
            var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), log);
            var sent = new List<byte[]>();
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });

            await handler.HandleFrameAsync(connection, ReadFrame(new LegacyMsgLoginGamePacket(41_001).ToFrameBytes()));
            var countBeforeBuy = sent.Count;
            var request = new LegacyExchangeShopBuyPacket(
                LegacyExchangeShopBuyType.GoodsEx,
                NpcID: 24_702,
                ShopID: 168,
                Count: 1,
                Index: 1,
                Items: []);

            await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));

            Assert.Equal(countBeforeBuy, sent.Count);
            Assert.Contains("world data was not loaded", log.ToString());
        }
        finally
        {
            if (Directory.Exists(usersRoot))
            {
                Directory.Delete(usersRoot, recursive: true);
            }
        }
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerExchangeShopBuyGoodsExSpendsDonationAndPersistsReward()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "wolf-exchange-buy-goodsex-" + Guid.NewGuid());
        var usersRoot = Path.Combine(tempRoot, "Users");
        var itemsRoot = Path.Combine(tempRoot, "PlayersItems");
        try
        {
            Directory.CreateDirectory(usersRoot);
            Directory.CreateDirectory(itemsRoot);
            File.WriteAllText(Path.Combine(usersRoot, "41001.ini"), SnapshotText(guildDonation: 100));
            using var log = new StringWriter();
            var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
            var handler = new WorldServerFrameHandler(
                new CharacterSnapshotLoader(usersRoot, itemsRoot, playerSpellsRoot: null, playerProficienciesRoot: null),
                handoffService: null,
                worldData,
                log);
            var sent = new List<byte[]>();
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });

            await handler.HandleFrameAsync(connection, ReadFrame(new LegacyMsgLoginGamePacket(41_001).ToFrameBytes()));
            var countBeforeBuy = sent.Count;
            var request = new LegacyExchangeShopBuyPacket(
                LegacyExchangeShopBuyType.GoodsEx,
                NpcID: 24_702,
                ShopID: 168,
                Count: 1,
                Index: 155,
                Items: []);

            await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));

            Assert.Equal(countBeforeBuy + 4, sent.Count);
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeBuy]), out var reward));
            Assert.Equal(1U, reward.Uid);
            Assert.Equal(3_007_386U, reward.ItemId);
            Assert.Equal(LegacyGameItemPacket.AddItemMode, reward.Mode);
            Assert.Equal((ushort)1, reward.StackSize);

            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[countBeforeBuy + 1]), out var donationUpdate));
            Assert.Equal(LegacyMsgUpdateDataType.MyDontion, Assert.Single(donationUpdate.Updates).Type);
            Assert.Equal(70UL, donationUpdate.Updates[0].Value);
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[countBeforeBuy + 2]), out var mirrorUpdate));
            Assert.Equal(LegacyMsgUpdateDataType.DonationPoints, Assert.Single(mirrorUpdate.Updates).Type);
            Assert.Equal(70UL, mirrorUpdate.Updates[0].Value);

            Assert.True(LegacyExchangeShopBuyPacket.TryParse(ReadFrame(sent[countBeforeBuy + 3]), out var echo));
            Assert.Equal(request.Type, echo.Type);
            Assert.Equal(request.NpcID, echo.NpcID);
            Assert.Equal(request.ShopID, echo.ShopID);
            Assert.Equal(request.Count, echo.Count);
            Assert.Equal(request.Index, echo.Index);

            var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(41_001);
            var persistedReward = Assert.Single(
                persistedItems.InventoryItems,
                item => item.ItemId == 3_007_386);
            Assert.Equal(1U, persistedReward.Uid);
            Assert.Equal((ushort)1, persistedReward.StackSize);
            var persistedCharacter = new CharacterSnapshotLoader(usersRoot).LoadByAccountId(41_001);
            Assert.NotNull(persistedCharacter);
            Assert.Equal("70", persistedCharacter.Attributes["GuildDontion"]);

            var output = log.ToString();
            Assert.Contains("handled LegacyExchangeShopBuy", output);
            Assert.Contains("source=MrRayzo.ElTabakh.CMsgExchangeShopBuy.Process1.Type1", output);
            Assert.Contains("guildDonation=70", output);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void LegacyExchangeShopGoodsEmptyResponseRoundTripsRequiredFields()
    {
        var packet = LegacyExchangeShopGoodsPacket.Empty(
            npcId: 123,
            shopId: 168,
            uid: 41_001,
            timeInSeconds: 45_600);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyExchangeShopGoods, frame.Header.Opcode);
        Assert.True(LegacyExchangeShopGoodsPacket.TryParse(frame, out var parsed));
        Assert.Equal(123U, parsed.NpcID);
        Assert.Equal(168U, parsed.ShopID);
        Assert.Equal(41_001U, parsed.Uid);
        Assert.Equal(45_600U, parsed.TimeInSeconds);
        Assert.Equal(0U, parsed.Unknown5);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyExchangeShopGoodsRoundTripsItems()
    {
        var packet = new LegacyExchangeShopGoodsPacket(
            NpcID: 123,
            ShopID: 168,
            Uid: 41_001,
            TimeInSeconds: 45_600,
            Unknown5: 0,
            Items: [new LegacyExchangeShopGoodsItem(Index: 7, Count: 2, Unknown: 99)]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyExchangeShopGoodsPacket.TryParse(frame, out var parsed));
        var item = Assert.Single(parsed.Items);
        Assert.Equal(7U, item.Index);
        Assert.Equal(2U, item.Count);
        Assert.Equal(99U, item.Unknown);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static string SnapshotText(long guildDonation = 0)
    {
        return $$"""
            [Character]
            UID=41001
            Body=1008
            Face=154
            Name=FixtureShopper
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
            Strength=0
            Agility=36
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            Money=0
            ConquerPoints=0
            BoundConquerPoints=0
            BankCps=0
            PkPoints=0
            Online=0
            GuildDontion={{guildDonation}}
            """;
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFilePath)!);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find WolfConquer.sln.");
    }
}
