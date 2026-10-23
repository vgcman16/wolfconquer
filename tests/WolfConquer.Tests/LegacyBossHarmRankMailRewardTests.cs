using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests : IDisposable
{
    private const uint KillerUid = 9_999;
    private const int BossFamilyId = 9_901;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    private readonly string rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "WolfConquerBossHarmRankMailTests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task HandlerBossDeathCreatesHarmRankMailForOnlineTopNineAndConsumesRanking()
    {
        var bossFamily = CreateBossFamily();
        var spawn = CreateBossSpawn(bossFamily);
        var worldData = CreateWorldDataCatalog(bossFamily, spawn);
        var boss = LegacyMonsterInstance.FromGenerator(
            spawn,
            bossFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();

        var ranked = CreateRankedHunters();
        var sentByUid = new Dictionary<uint, List<byte[]>>();
        var sessionsByUid = new Dictionary<uint, WorldSession>();
        var connectionsByUid = new Dictionary<uint, ConquerTcpConnection>();
        foreach (var hunter in ranked)
        {
            var sent = new List<byte[]>();
            var connection = CreateConnection(sent);
            var session = CreateSession(hunter.Uid, hunter.Name);
            sessions.Set(connection, session);
            sentByUid[hunter.Uid] = sent;
            sessionsByUid[hunter.Uid] = session;
            connectionsByUid[hunter.Uid] = connection;
        }

        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(KillerUid, "LastHit");
        sessions.Set(killerConnection, killerSession);

        var now = DateTimeOffset.UtcNow;
        foreach (var hunter in ranked)
        {
            var result = monsterStates.ApplyDamage(
                boss,
                hunter.Damage,
                now.AddSeconds(hunter.Rank),
                hunter.Uid,
                hunter.Name);

            Assert.False(result.Killed);
        }

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            boss,
            log);

        Assert.Empty(monsterStates.GetBossHarmRanking(boss, DateTimeOffset.UtcNow));

        foreach (var hunter in ranked.Where(hunter => hunter.Rank <= 9))
        {
            var entry = Assert.Single(mailboxes.List(hunter.Uid));
            AssertBossHarmRankMail(entry, hunter.Rank);
        }

        Assert.Empty(mailboxes.List(ranked.Single(hunter => hunter.Rank == 10).Uid));

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            boss,
            log);

        foreach (var hunter in ranked.Where(hunter => hunter.Rank <= 9))
        {
            Assert.Single(mailboxes.List(hunter.Uid));
        }

        var output = log.ToString();
        Assert.Contains("WorldServer logged legacy boss kill reward", output);
        Assert.Contains(
            "sourceLog=\"[Bosses] Name: MailboxBoss | Killer: LastHit | Hunters: " +
            "Hunter1     |      Hunter2     |      Hunter3     |      Hunter4     |      Hunter5     |      \"",
            output);
        Assert.Contains("source=MonsterRole.GetScorePosition LoginQueue boss reward log.", output);

        var firstRank = ranked.Single(hunter => hunter.Rank == 1);
        var immediateMailList = Assert.Single(
            ParseMailLists(sentByUid[firstRank.Uid]),
            packet => packet.Type == LegacyMailListType.NewMail);
        var immediateMail = Assert.Single(immediateMailList.Mails);
        Assert.Equal(100_000UL, immediateMail.Money);
        Assert.Equal(1_000UL, immediateMail.EMoney);
        Assert.Equal(1UL, immediateMail.Flag);

        sentByUid[firstRank.Uid].Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connectionsByUid[firstRank.Uid],
                log,
                sessionsByUid[firstRank.Uid],
                Sessions: sessions,
                Mailboxes: mailboxes),
            ReadFrame(CreateEmptyFrame(WorldPacketOpcodes.LegacyMailList)));

        var mailList = Assert.Single(ParseMailLists(sentByUid[firstRank.Uid]));
        Assert.Equal(LegacyMailListType.FullPage, mailList.Type);
        var listedMail = Assert.Single(mailList.Mails);
        Assert.Equal(100_000UL, listedMail.Money);
        Assert.Equal(1_000UL, listedMail.EMoney);
        Assert.Equal(1UL, listedMail.Flag);
        Assert.Equal("STR_SYSTEM_NAME@@", listedMail.Title);
        Assert.Equal("STR_SYSTEM_NAME@@", listedMail.SenderName);
    }

    [Fact]
    public async Task HandlerBossDeathPreserves8311DeityLandDoubleLastHitterMessages()
    {
        var bossFamily = CreateBossFamily(id: 8_311, name: "DeityOverlapBoss");
        var spawn = CreateBossSpawn(
            bossFamily,
            mapId: checked((int)LegacyBossDeathRewardPolicy.DeityLandBossRewardMapId));
        var worldData = CreateWorldDataCatalog(bossFamily, spawn);
        var boss = LegacyMonsterInstance.FromGenerator(
            spawn,
            bossFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var usersRoot = Path.Combine(rootDirectory, "Users-DeityLand-LastHitterCp");
        Directory.CreateDirectory(usersRoot);
        var sessions = new WorldSessionStore((connection, previous, current) =>
        {
            _ = connection;
            if (previous is not null &&
                !string.IsNullOrWhiteSpace(current.UsersRoot) &&
                CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(previous.Character, current.Character))
            {
                Assert.True(
                    CharacterSnapshotScalarPersistence.TryPersist(current.UsersRoot, current.Character, out _, out var reason),
                    reason);
            }
        });
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: checked((int)LegacyBossDeathRewardPolicy.DeityLandBossRewardMapId),
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyDeityLandRandomDropRate", "0"),
                ("LegacyMonsterDropRoll", "40")));
        killerSession = killerSession with { UsersRoot = usersRoot };
        WriteCharacterSnapshotForSession(usersRoot, killerSession);
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            boss,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(1_000, updatedSession!.Character.ConquerPoints);
        var persisted = CharacterSnapshotParser.ParseText(
            File.ReadAllText(Path.Combine(usersRoot, KillerUid.ToString(CultureInfo.InvariantCulture) + ".ini")));
        Assert.Equal(1_000, persisted.ConquerPoints);

        var lastHitterMessages = ParseMessages(killerSent)
            .Where(packet =>
                packet.Content.Count > 3 &&
                packet.Content[3] == "You have received 500 Conquer Points for being the last hitter of DeityOverlapBoss!")
            .ToArray();

        Assert.Equal(2, lastHitterMessages.Length);
        Assert.All(lastHitterMessages, packet => Assert.Equal(LegacyMsgMessagePacket.TalkChatType, packet.ChatType));
        var output = log.ToString();
        Assert.Contains("WorldServer skipped legacy monster-death random Conquer Point reward", output);
        Assert.Contains("WorldServer applied legacy boss last-hitter Conquer Point reward", output);
        Assert.Contains("points=1000", output);
        Assert.Contains("source=MonsterRole.GetBossReward direct ConquerPoints award.", output);
    }

    [Fact]
    public async Task HandlerDeityLandBossDeathSendsDictionaryAndFixedDropBundle()
    {
        var bossFamily = CreateBossFamily(id: 9_998, name: "DeityLandDropBoss");
        var spawn = CreateBossSpawn(
            bossFamily,
            mapId: checked((int)LegacyBossSpecialDeathDropPolicy.DeityLandBossDropMapId));
        var worldData = CreateWorldDataCatalog(bossFamily, spawn);
        var boss = LegacyMonsterInstance.FromGenerator(
            spawn,
            bossFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: checked((int)LegacyBossSpecialDeathDropPolicy.DeityLandBossDropMapId),
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyBoss10250PrimaryDropIndex", "1"),
                ("LegacyBoss10250SecondaryDropIndex", "2"),
                ("LegacyDeityLandRandomDropRate", "0"),
                ("LegacyMonsterDropRoll", "40")));
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            boss,
            log);

        var floorItems = ParseFloorItems(killerSent);

        Assert.Equal(45, floorItems.Count);
        AssertDropCount(floorItems, 1_088_000, 4);
        AssertDropCount(floorItems, 3_303_483, 30);
        AssertDropCount(floorItems, 3_005_127, 5);
        AssertDropCount(floorItems, 3_004_181, 1);
        AssertDropCount(floorItems, 4_200_001, 1);
        AssertDropCount(floorItems, 4_200_002, 2);
        AssertDropCount(floorItems, 4_200_003, 1);
        AssertDropCount(floorItems, 3_316_793, 1);

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy boss special death drops", output);
        Assert.Contains("killer.Player.Map == 10250 && Boss > 0", output);
        Assert.Contains("AddRandomRelic", output);
        Assert.DoesNotContain("boss-family monster death drops are source-backed but not modeled", output);
    }

    [Fact]
    public async Task HandlerDeityLandBossDeathStoresGeneratedRandomRelicFloorPickup()
    {
        var bossFamily = CreateBossFamily(id: 9_997, name: "DeityLandRelicBoss");
        var spawn = CreateBossSpawn(
            bossFamily,
            mapId: checked((int)LegacyBossSpecialDeathDropPolicy.DeityLandBossDropMapId));
        var worldData = CreateWorldDataCatalog(
            bossFamily,
            spawn,
            xuanbaoRandomAttributes: CreateRelicRandomAttributes());
        var boss = LegacyMonsterInstance.FromGenerator(
            spawn,
            bossFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var floorItemsRuntime = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: checked((int)LegacyBossSpecialDeathDropPolicy.DeityLandBossDropMapId),
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyBoss10250PrimaryDropIndex", "1"),
                ("LegacyBoss10250SecondaryDropIndex", "2"),
                ("LegacyBoss10250RelicItemIndex", "0"),
                ("LegacyBoss10250RelicDurabilityRoll", "10"),
                ("LegacyBoss10250RelicAttributeCount", "3"),
                ("LegacyBoss10250RelicAttributeIndexes", "0,0,0"),
                ("LegacyBoss10250RelicAttributeValues", "200,100,900"),
                ("LegacyDeityLandRandomDropRate", "0"),
                ("LegacyMonsterDropRoll", "40")));
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            boss,
            log,
            floorItemsRuntime);

        var floorItems = ParseFloorItems(killerSent);

        Assert.Equal(46, floorItems.Count);
        AssertDropCount(floorItems, 4_100_001, 1);

        var relicEntry = Assert.Single(
            floorItemsRuntime.SnapshotVisible(DateTimeOffset.UtcNow),
            entry => entry.Packet.ItemId == 4_100_001U);
        Assert.True(relicEntry.OwnerOnly);
        Assert.NotNull(relicEntry.Item);
        Assert.Equal(4_100_001U, relicEntry.Item!.ItemId);
        Assert.Equal(1_099, relicEntry.Item.Durability);
        Assert.Equal(1_099, relicEntry.Item.MaximumDurability);
        Assert.Equal(200_103U, relicEntry.Item.Attribute1);
        Assert.Equal(100_104U, relicEntry.Item.Attribute2);
        Assert.Equal(900_108U, relicEntry.Item.Attribute3);
        Assert.Equal(0U, relicEntry.Item.Attribute4);
        Assert.Equal(0U, relicEntry.Item.Attribute5);

        var output = log.ToString();
        Assert.Contains("generatedItems=1", output);
        Assert.DoesNotContain("AddRandomRelic() needs", output);
        Assert.DoesNotContain("boss special generated death drop was sent but floor item runtime state is not loaded", output);
    }

    [Fact]
    public async Task HandlerThrillingSpookBossDeathSendsSpecialDropBundleWithGeneratedSoulPools()
    {
        var bossFamily = CreateBossFamily(id: 4_212, name: "ThrillingSpook");
        var spawn = CreateBossSpawn(bossFamily);
        var worldData = CreateWorldDataCatalog(bossFamily, spawn, CreateGenerateSoulsItemTypes());
        var boss = LegacyMonsterInstance.FromGenerator(
            spawn,
            bossFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            attributes: new Dictionary<string, string>
            {
                ["LegacyBoss4212RareDrop"] = "0",
                ["LegacyBossSoulsPurificationRollModulo"] = "2",
                ["LegacyBossSoulsPurificationIndexes"] = "1,0",
                ["LegacyBossSoulsAccessoryRollModulo"] = "2",
                ["LegacyBossSoulsAccessoryIndexes"] = "1,0",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            boss,
            log);

        var floorItems = ParseFloorItems(killerSent);

        Assert.Equal(40, floorItems.Count);
        AssertDropCount(floorItems, 3_335_128, 5);
        AssertDropCount(floorItems, 3_005_127, 1);
        AssertDropCount(floorItems, 3_311_633, 1);
        AssertDropCount(floorItems, 800_002, 1);
        AssertDropCount(floorItems, 800_001, 1);
        AssertDropCount(floorItems, 370_001, 1);
        AssertDropCount(floorItems, 350_001, 1);
        AssertDropCount(floorItems, 3_335_125, 1);
        AssertDropCount(floorItems, 3_328_253, 3);
        AssertDropCount(floorItems, 3_328_254, 3);
        AssertDropCount(floorItems, 3_330_770, 10);
        AssertDropCount(floorItems, 3_316_793, 10);
        AssertDropCount(floorItems, 729_463, 1);
        AssertDropCount(floorItems, 3_321_805, 1);
        Assert.DoesNotContain(floorItems, item => item.ItemId == 4_200_010U);
        Assert.DoesNotContain(floorItems, item => item.ItemId == 729_305U);

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy boss special death drops", output);
        Assert.DoesNotContain("PurificationItems[7]", output);
        Assert.DoesNotContain("boss-family monster death drops are source-backed but not modeled", output);
    }

    [Fact]
    public async Task HandlerStudyPointBossDeathSendsSpecialDropBundleWithGeneratedSoulPools()
    {
        var bossFamily = CreateBossFamily(
            id: checked((int)LegacyBossSpecialDeathDropPolicy.StudyPointBossFamilyId),
            name: "StudyPointBoss");
        var spawn = CreateBossSpawn(bossFamily);
        var worldData = CreateWorldDataCatalog(bossFamily, spawn, CreateGenerateSoulsItemTypes());
        var boss = LegacyMonsterInstance.FromGenerator(
            spawn,
            bossFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            attributes: new Dictionary<string, string>
            {
                ["LegacyBoss4171RareDrop"] = "0",
                ["LegacyBossSoulsPurificationRollModulo"] = "2",
                ["LegacyBossSoulsPurificationIndexes"] = "1,0",
                ["LegacyBossSoulsAccessoryRollModulo"] = "2",
                ["LegacyBossSoulsAccessoryIndexes"] = "1,0",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            boss,
            log);

        var floorItems = ParseFloorItems(killerSent);

        Assert.Equal(39, floorItems.Count);
        AssertDropCount(floorItems, 4_200_007, 1);
        AssertDropCount(floorItems, 700_132, 1);
        AssertDropCount(floorItems, 3_005_127, 3);
        AssertDropCount(floorItems, 3_335_128, 3);
        AssertDropCount(floorItems, 3_328_253, 3);
        AssertDropCount(floorItems, 3_328_254, 3);
        AssertDropCount(floorItems, 800_002, 1);
        AssertDropCount(floorItems, 800_001, 1);
        AssertDropCount(floorItems, 370_001, 1);
        AssertDropCount(floorItems, 350_001, 1);
        AssertDropCount(floorItems, 729_463, 1);
        AssertDropCount(floorItems, 3_330_770, 10);
        AssertDropCount(floorItems, 3_316_793, 10);
        Assert.DoesNotContain(floorItems, item => item.ItemId == 4_200_010U);
        Assert.DoesNotContain(floorItems, item => item.ItemId == 729_305U);

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy boss special death drops", output);
        Assert.DoesNotContain("PurificationItems[7]", output);
        Assert.DoesNotContain("boss-family monster death drops are source-backed but not modeled", output);
    }

    [Fact]
    public async Task HandlerFamily20055DeathSendsGenerateSoulsLevel6BundleWithOptionalItems()
    {
        var bossFamily = CreateBossFamily(
            id: checked((int)LegacyBossSpecialDeathDropPolicy.StudyPointTeamRewardFamilyId),
            name: "StudyPointTeamRewardBoss");
        var spawn = CreateBossSpawn(bossFamily);
        var worldData = CreateWorldDataCatalog(bossFamily, spawn, CreateGenerateSoulsItemTypes());
        var boss = LegacyMonsterInstance.FromGenerator(
            spawn,
            bossFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            attributes: new Dictionary<string, string>
            {
                ["LegacyBoss20055Add711679"] = "1",
                ["LegacyBoss20055Add711188"] = "1",
                ["LegacyBossSoulsPurificationRollModulo"] = "2",
                ["LegacyBossSoulsPurificationIndexes"] = "1,0",
                ["LegacyBossSoulsAccessoryRollModulo"] = "2",
                ["LegacyBossSoulsAccessoryIndexes"] = "1,0",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            boss,
            log);

        var floorItems = ParseFloorItems(killerSent);

        Assert.Equal(7, floorItems.Count);
        AssertDropCount(floorItems, 700_002, 1);
        AssertDropCount(floorItems, 700_001, 1);
        AssertDropCount(floorItems, 370_001, 1);
        AssertDropCount(floorItems, 350_001, 1);
        AssertDropCount(floorItems, 729_463, 1);
        AssertDropCount(floorItems, 711_679, 1);
        AssertDropCount(floorItems, 711_188, 1);

        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs Boss > 0 && Family.ID == 20055\"", output);
        Assert.DoesNotContain("PurificationItems[6]", output);
        Assert.DoesNotContain("boss-family monster death drops are source-backed but not modeled", output);
    }

    [Fact]
    public async Task HandlerFamily6643DeathSendsGenerateSoulsLevel6Bundle()
    {
        var bossFamily = CreateBossFamily(
            id: checked((int)LegacyBossSpecialDeathDropPolicy.SoulBoxFamilyId),
            name: "SoulBoxBoss");
        var spawn = CreateBossSpawn(bossFamily);
        var worldData = CreateWorldDataCatalog(bossFamily, spawn, CreateGenerateSoulsItemTypes());
        var boss = LegacyMonsterInstance.FromGenerator(
            spawn,
            bossFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossSoulsPurificationRollModulo"] = "2",
                ["LegacyBossSoulsPurificationIndexes"] = "1,0",
                ["LegacyBossSoulsAccessoryRollModulo"] = "2",
                ["LegacyBossSoulsAccessoryIndexes"] = "1,0",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            boss,
            log);

        var floorItems = ParseFloorItems(killerSent);

        Assert.Equal(5, floorItems.Count);
        AssertDropCount(floorItems, 700_002, 1);
        AssertDropCount(floorItems, 700_001, 1);
        AssertDropCount(floorItems, 370_001, 1);
        AssertDropCount(floorItems, 350_001, 1);
        AssertDropCount(floorItems, 729_463, 1);
        Assert.DoesNotContain(floorItems, item => item.ItemId is 711_679U or 711_188U);

        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs Boss > 0 && Family.ID == 6643\"", output);
        Assert.DoesNotContain("PurificationItems[6]", output);
        Assert.DoesNotContain("boss-family monster death drops are source-backed but not modeled", output);
    }

    [Fact]
    public async Task HandlerMap1011Family3130DeathSendsAdditiveSourceSpecialDrop()
    {
        var monsterFamily = CreateBossFamily(id: 3_130, name: "Map1011SpecialMonster", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_011);
        var worldData = CreateWorldDataCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_011,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMapSpecialDropRate"] = "1",
                ["LegacyMapSpecialDropItemIndex"] = "2",
                ["LegacyMonsterDropRoll"] = "40",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        var floorItem = Assert.Single(ParseFloorItems(killerSent));

        Assert.Equal(1_088_000U, floorItem.ItemId);
        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs Map != 3846 && (Map == 1011 || Map == 1020) && (Family.ID == 3130 || Family.ID == 3134)\"", output);
        Assert.DoesNotContain("boss-family monster death drops are source-backed but not modeled", output);
    }

    [Fact]
    public async Task HandlerMap11127DeathSendsSourceSpecialDropBundle()
    {
        var monsterFamily = CreateBossFamily(id: 9_911, name: "Map11127SpecialMonster", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 11_127);
        var worldData = CreateWorldDataCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 11_127,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "124",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        var floorItems = ParseFloorItems(killerSent);

        Assert.Equal(4, floorItems.Count);
        AssertDropCount(floorItems, 722_723, 4);
        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs Map == 11127\"", output);
        Assert.DoesNotContain("boss-family monster death drops are source-backed but not modeled", output);
    }

    [Fact]
    public async Task HandlerMap11127GenericBossDeathDoesNotBypassGenerateBossFamilyReturn()
    {
        var bossFamily = CreateBossFamily(id: 9_912, name: "Map11127GenericBoss");
        var spawn = CreateBossSpawn(bossFamily, mapId: 11_127);
        var worldData = CreateWorldDataCatalog(bossFamily, spawn);
        var boss = LegacyMonsterInstance.FromGenerator(
            spawn,
            bossFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 11_127,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "124",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            boss,
            log);

        Assert.Empty(ParseFloorItems(killerSent));
        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs GenerateBossFamily()\"", output);
        Assert.DoesNotContain("branch=\"MonsterRole.cs Map == 11127\"", output);
        Assert.DoesNotContain("boss-family monster death drops are source-backed but not modeled", output);
    }

    [Fact]
    public async Task HandlerDeityLandMeteorDoveDeathDropsFiveSourceMeteors()
    {
        var monsterFamily = CreateBossFamily(id: 10_252, name: "MeteorDove", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 10_250);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(1_088_001)],
            questMissions: [CreateQuestMission(3_944, intentAmount: 1)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_250,
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyDeityLandRandomDropRate", "0"),
                ("LegacyMonsterDropRoll", "40")));
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        var floorItems = ParseFloorItems(killerSent);

        Assert.Equal(5, floorItems.Count);
        AssertDropCount(floorItems, 1_088_001, 5);
        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 10250 && Name == \"MeteorDove\"\"", output);
        Assert.DoesNotContain("boss-family monster death drops are source-backed but not modeled", output);
    }

    [Fact]
    public async Task HandlerDeityLandRandomDropRoll16UsesSourceOverwriteAndAngelwing()
    {
        var monsterFamily = CreateBossFamily(id: 10_253, name: "DeityRandomDevil", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 10_250);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(3_330_770)],
            questMissions: [CreateQuestMission(3_944, intentAmount: 1)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_250,
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyDeityLandRandomDropRate", "1"),
                ("LegacyDeityLandRandomDropRoll", "16"),
                ("LegacyMonsterDropRoll", "40")));
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        var floorItem = Assert.Single(ParseFloorItems(killerSent));

        Assert.Equal(3_330_770U, floorItem.ItemId);
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Any(
                content => content.Contains(
                    "DeityRandomDevil that you killed dropped 1 Item3330770(s)!",
                    StringComparison.Ordinal)));
        Assert.Contains(
            ParseStringPackets(killerSent),
            packet => packet.Uid == KillerUid &&
                packet.Id == LegacyStringId.Effect &&
                packet.Strings.SequenceEqual(["angelwing"]));
        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 10250 Core.Rate(3) random item ladder\"", output);
        Assert.DoesNotContain("boss-family monster death drops are source-backed but not modeled", output);
    }

    [Fact]
    public async Task HandlerDeityLandInventoryRuneRewardsStackAndSendSourceEffects()
    {
        var monsterFamily = CreateBossFamily(id: 10_254, name: "DeityRewardDevil", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 10_250);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(4_050_001), CreateItemType(4_060_001)],
            questMissions: [CreateQuestMission(3_944, intentAmount: 1)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var existingRune = new PlayerItemSnapshotItem
        {
            Uid = 900,
            ItemId = 4_050_001,
            StackSize = 2,
        };
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_250,
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyDeityLandRandomDropRate", "0"),
                ("LegacyDeityLandReward4050001Rate", "1"),
                ("LegacyDeityLandReward4060001Rate", "1"),
                ("LegacyMonsterDropRoll", "40")));
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot([existingRune], [], [], [], []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var updated405 = Assert.Single(updatedSession!.PlayerItems.InventoryItems, item => item.ItemId == 4_050_001U);
        var added406 = Assert.Single(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == 4_060_001U);
        Assert.Equal(900U, updated405.Uid);
        Assert.Equal((ushort)3, updated405.StackSize);
        Assert.Equal((ushort)1, added406.StackSize);

        var itemPackets = ParseGameItems(killerSent)
            .Where(packet => packet.ItemId is 4_050_001U or 4_060_001U)
            .ToArray();
        Assert.Contains(itemPackets, packet => packet.ItemId == 4_050_001U && packet.Mode == LegacyGameItemPacket.UpdateItemMode);
        Assert.Contains(itemPackets, packet => packet.ItemId == 4_060_001U && packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Equal(
            2,
            ParseMessages(killerSent)
                .SelectMany(packet => packet.Content)
                .Count(content => content == "You have got 1 Rune!"));
        Assert.Equal(
            2,
            ParseStringPackets(killerSent)
                .Count(packet => packet.Uid == KillerUid &&
                    packet.Id == LegacyStringId.Effect &&
                    packet.Strings.SequenceEqual(["angelwing"])));

        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 10250 Core.Rate(4) AddItemWitchStack(4050001)\"", output);
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 10250 Core.Rate(5) AddItemWitchStack(4060001)\"", output);
    }

    [Fact]
    public async Task HandlerDeityLandInventoryRewardFullBagSendsSourceFullMessage()
    {
        var monsterFamily = CreateBossFamily(id: 10_255, name: "DeityFullBagDevil", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 10_250);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(4_050_001), CreateItemType(4_060_001)],
            questMissions: [CreateQuestMission(3_944, intentAmount: 1)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var fillerItems = Enumerable.Range(1, 40)
            .Select(index => new PlayerItemSnapshotItem
            {
                Uid = checked((uint)index),
                ItemId = checked((uint)(100_000 + index)),
                StackSize = 1,
            })
            .ToArray();
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_250,
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyDeityLandRandomDropRate", "0"),
                ("LegacyDeityLandReward4050001Rate", "1"),
                ("LegacyDeityLandReward4060001Rate", "1"),
                ("LegacyMonsterDropRoll", "40")));
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(fillerItems, [], [], [], []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(40, updatedSession!.PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(updatedSession.PlayerItems.InventoryItems, item => item.ItemId is 4_050_001U or 4_060_001U);
        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId is 4_050_001U or 4_060_001U);
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Any(
                content => content.Contains(
                    "Your inventory is too full to contain more chest keys.",
                    StringComparison.Ordinal)));

        var output = log.ToString();
        Assert.Contains("skipped legacy DeityLand inventory rewards", output);
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 10250 Inventory.HaveSpace(1)\"", output);
    }

    [Fact]
    public async Task HandlerDeityLandIndependentInventoryRewardsGrantItemsAndNoticeOnlyFragments()
    {
        var monsterFamily = CreateBossFamily(id: 10_256, name: "DeityIndependentRewardDevil", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 10_250);
        var rewardItemIds = new[]
        {
            3_306_368,
            3_330_770,
            3_316_793,
            3_009_102,
            3_009_103,
            3_009_104,
            4_040_001,
        };
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: rewardItemIds.Select(itemId => CreateItemType(itemId)).ToArray(),
            questMissions: [CreateQuestMission(3_944, intentAmount: 1)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_250,
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyDeityLandRandomDropRate", "0"),
                ("LegacyDeityLandRedRuneFragmentRate", "1"),
                ("LegacyDeityLandYellowRuneFragmentRate", "1"),
                ("LegacyDeityLandReward3306368Rate", "1"),
                ("LegacyDeityLandReward3330770Rate", "1"),
                ("LegacyDeityLandReward3316793Rate", "1"),
                ("LegacyDeityLandReward3009102Rate", "1"),
                ("LegacyDeityLandReward3009103Rate", "1"),
                ("LegacyDeityLandReward3009104Rate", "1"),
                ("LegacyDeityLandReward4040001Rate", "1"),
                ("LegacyMonsterDropRoll", "40")));
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        foreach (var itemId in rewardItemIds)
        {
            var item = Assert.Single(updatedSession!.PlayerItems.InventoryItems, inventory => inventory.ItemId == (uint)itemId);
            Assert.Equal((ushort)1, item.StackSize);
        }

        var itemPackets = ParseGameItems(killerSent);
        foreach (var itemId in rewardItemIds)
        {
            Assert.Contains(
                itemPackets,
                packet => packet.ItemId == (uint)itemId &&
                    packet.Mode == LegacyGameItemPacket.AddItemMode);
        }

        var messageContents = ParseMessages(killerSent).SelectMany(packet => packet.Content).ToArray();
        Assert.Contains("You have got 1 RedRuneFragment!", messageContents);
        Assert.Contains("You have got 1 YellowRuneFragment!", messageContents);
        Assert.Contains("You have got 1 BlueRuneFragment!", messageContents);
        Assert.Equal(2, messageContents.Count(content => content == "You have got 1 Ex!"));
        Assert.Contains("You have got 1 MoonFruit!", messageContents);
        Assert.Contains("You have got 1 SoulFruit!", messageContents);
        Assert.Contains("You have got 1 RedBodhiBean!", messageContents);
        Assert.Contains("You have got 1 Rune!", messageContents);
        Assert.Equal(
            9,
            ParseStringPackets(killerSent)
                .Count(packet => packet.Uid == KillerUid &&
                    packet.Id == LegacyStringId.Effect &&
                    packet.Strings.SequenceEqual(["angelwing"])));

        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 10250 Core.Rate(0.007) AddItemWitchStack(3306368)\"", output);
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 10250 Core.Rate(22) AddItemWitchStack(4040001)\"", output);
    }

    [Theory]
    [InlineData("1", "1", "1", 3_303_483U, "You have got 1 WonderHerb!", "Core.Rate(44) AddItemWitchStack(3303483)")]
    [InlineData("0", "1", "1", 3_307_083U, "You have got 1 JadeofFaith!", "else Core.Rate(1) Inventory.Add(3307083)")]
    [InlineData("0", "0", "1", 3_304_961U, "You have got 1 SafeZoneScroll!", "else Core.Rate(1) AddItemWitchStack(3304961)")]
    public async Task HandlerDeityLandChainedInventoryRewardsRespectSourceElseIfOrder(
        string wonderHit,
        string jadeHit,
        string safeZoneHit,
        uint expectedItemId,
        string expectedMessage,
        string expectedBranch)
    {
        var monsterFamily = CreateBossFamily(id: 10_257, name: "DeityChainedRewardDevil", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 10_250);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items:
            [
                CreateItemType(3_303_483),
                CreateItemType(3_307_083),
                CreateItemType(3_304_961),
            ],
            questMissions: [CreateQuestMission(3_944, intentAmount: 1)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_250,
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyDeityLandRandomDropRate", "0"),
                ("LegacyDeityLandReward3303483Rate", wonderHit),
                ("LegacyDeityLandReward3307083Rate", jadeHit),
                ("LegacyDeityLandReward3304961Rate", safeZoneHit),
                ("LegacyMonsterDropRoll", "40")));
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var rewardItem = Assert.Single(updatedSession!.PlayerItems.InventoryItems);
        Assert.Equal(expectedItemId, rewardItem.ItemId);
        Assert.Contains(
            ParseGameItems(killerSent),
            packet => packet.ItemId == expectedItemId &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Any(content => content == expectedMessage));
        Assert.Single(
            ParseStringPackets(killerSent),
            packet => packet.Uid == KillerUid &&
                packet.Id == LegacyStringId.Effect &&
                packet.Strings.SequenceEqual(["angelwing"]));

        var output = log.ToString();
        Assert.Contains(expectedBranch, output);
    }

    [Fact]
    public async Task HandlerDeityLandInventoryRandomRelicUsesSourceGeneratedRelicMathAndPersistsAttributes()
    {
        var monsterFamily = CreateBossFamily(id: 10_258, name: "DeityRelicRewardDevil", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 10_250);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(4_100_001)],
            questMissions: [CreateQuestMission(3_944, intentAmount: 1)],
            xuanbaoRandomAttributes: CreateRelicRandomAttributes());
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        WorldSessionStore sessions;
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_250,
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyDeityLandRandomDropRate", "0"),
                ("LegacyDeityLandInventoryRelicFirstRate", "1"),
                ("LegacyDeityLandInventoryRelicSecondRate", "1"),
                ("LegacyBoss10250RelicItemIndex", "0"),
                ("LegacyBoss10250RelicDurabilityRoll", "10"),
                ("LegacyBoss10250RelicAttributeCount", "3"),
                ("LegacyBoss10250RelicAttributeIndexes", "0,0,0"),
                ("LegacyBoss10250RelicAttributeValues", "200,100,900"),
                ("LegacyMonsterDropRoll", "40")));
        var playerItemsRoot = Path.Combine(rootDirectory, "PlayersItems-DeityLandRandomRelic");
        Directory.CreateDirectory(playerItemsRoot);
        new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(KillerUid, killerSession.PlayerItems);
        sessions = new WorldSessionStore((_, previous, current) =>
        {
            if (previous is not null)
            {
                new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(current.Character.Uid, current.PlayerItems);
            }
        });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var relic = Assert.Single(updatedSession!.PlayerItems.InventoryItems, item => item.ItemId == 4_100_001U);
        Assert.Equal(1_099, relic.Durability);
        Assert.Equal(1_099, relic.MaximumDurability);
        Assert.Equal(200_103U, relic.Attribute1);
        Assert.Equal(100_104U, relic.Attribute2);
        Assert.Equal(900_108U, relic.Attribute3);
        Assert.Equal(0U, relic.Attribute4);
        Assert.Equal(0U, relic.Attribute5);

        var persistedItems = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(KillerUid);
        var persistedRelic = Assert.Single(persistedItems.InventoryItems, item => item.ItemId == 4_100_001U);
        Assert.Equal(1_099, persistedRelic.Durability);
        Assert.Equal(1_099, persistedRelic.MaximumDurability);
        Assert.Equal(200_103U, persistedRelic.Attribute1);
        Assert.Equal(100_104U, persistedRelic.Attribute2);
        Assert.Equal(900_108U, persistedRelic.Attribute3);
        Assert.Equal(0U, persistedRelic.Attribute4);
        Assert.Equal(0U, persistedRelic.Attribute5);

        Assert.Contains(
            ParseGameItems(killerSent),
            packet => packet.ItemId == 4_100_001U &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Any(content => content == "You have got 1 Random Relic!"));
        Assert.Single(
            ParseStringPackets(killerSent),
            packet => packet.Uid == KillerUid &&
                packet.Id == LegacyStringId.Effect &&
                packet.Strings.SequenceEqual(["angelwing"]));

        var output = log.ToString();
        Assert.Contains("applied legacy DeityLand random relic inventory reward", output);
        Assert.Contains("Core.Rate(1) && Core.Rate(1) Inventory.AddRandomRelic", output);
        Assert.DoesNotContain("AddRandomRelic() needs", output);
    }

    [Theory]
    [InlineData("1", "1", "1", "1", 3_303_973U, "You have got 1 RareChestKey!", "Core.Rate(1) && Core.Rate(5) AddItemWitchStack(3303973)")]
    [InlineData("1", "0", "1", "1", 3_303_974U, "You have got 1 EpicChestKey!", "else Core.Rate(1) && Core.Rate(1) AddItemWitchStack(3303974)")]
    public async Task HandlerDeityLandChestKeyInventoryRewardsRespectSourceElseIfOrder(
        string rareFirstHit,
        string rareSecondHit,
        string epicFirstHit,
        string epicSecondHit,
        uint expectedItemId,
        string expectedMessage,
        string expectedBranch)
    {
        var monsterFamily = CreateBossFamily(id: 10_259, name: "DeityChestKeyRewardDevil", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 10_250);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items:
            [
                CreateItemType(3_303_973),
                CreateItemType(3_303_974),
            ],
            questMissions: [CreateQuestMission(3_944, intentAmount: 1)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_250,
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyDeityLandRandomDropRate", "0"),
                ("LegacyDeityLandRareChestKeyFirstRate", rareFirstHit),
                ("LegacyDeityLandRareChestKeySecondRate", rareSecondHit),
                ("LegacyDeityLandEpicChestKeyFirstRate", epicFirstHit),
                ("LegacyDeityLandEpicChestKeySecondRate", epicSecondHit),
                ("LegacyMonsterDropRoll", "40")));
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var rewardItem = Assert.Single(updatedSession!.PlayerItems.InventoryItems);
        Assert.Equal(expectedItemId, rewardItem.ItemId);
        Assert.Contains(
            ParseGameItems(killerSent),
            packet => packet.ItemId == expectedItemId &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Any(content => content == expectedMessage));
        Assert.Single(
            ParseStringPackets(killerSent),
            packet => packet.Uid == KillerUid &&
                packet.Id == LegacyStringId.Effect &&
                packet.Strings.SequenceEqual(["angelwing"]));

        var output = log.ToString();
        Assert.Contains(expectedBranch, output);
    }



    [Fact]
    public async Task HandlerDeityLandStoneSpriteDeathDropsSourceStoneAndAngelwing()
    {
        var monsterFamily = CreateBossFamily(id: 10_262, name: "StoneSprite", boss: false);
        monsterFamily = monsterFamily with
        {
            Attributes = new Dictionary<string, string>(monsterFamily.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["life"] = "1",
            },
        };
        var spawn = CreateBossSpawn(monsterFamily, mapId: 10_250);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(730_004)],
            questMissions: [CreateQuestMission(3_944, intentAmount: 1)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        var floorItemsRuntime = new FloorItemRuntimeStateStore();
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_250,
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyDeityLandRandomDropRate", "0"),
                ("LegacyDeityLandStoneSpriteRewardItemId", "730004"),
                ("LegacyMonsterDropRoll", "40")));
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItemsRuntime);

        var floorItem = Assert.Single(ParseFloorItems(killerSent));

        Assert.Equal(730_004U, floorItem.ItemId);
        Assert.True(floorItemsRuntime.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.Equal(FloorItemRuntimePickupKind.Item, runtimeEntry.Kind);
        Assert.Contains(
            ParseStringPackets(killerSent),
            packet => packet.Uid == KillerUid &&
                packet.Id == LegacyStringId.Effect &&
                packet.Strings.SequenceEqual(["angelwing"]));
        var output = log.ToString();
        Assert.Contains("Name.Contains(\"Sprite\")", output);
        Assert.Contains("dropKind=item", output);
        Assert.Contains("itemId=730004", output);
    }

    [Fact]
    public async Task HandlerDeityLandSilverSpriteDeathDropsSourceMoneyAndAngelwing()
    {
        var monsterFamily = CreateBossFamily(id: 10_263, name: "SilverSprite", boss: false);
        monsterFamily = monsterFamily with
        {
            Attributes = new Dictionary<string, string>(monsterFamily.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["life"] = "1",
            },
        };
        var spawn = CreateBossSpawn(monsterFamily, mapId: 10_250);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(1_091_020)],
            questMissions: [CreateQuestMission(3_944, intentAmount: 1)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        var floorItemsRuntime = new FloorItemRuntimeStateStore();
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_250,
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyDeityLandRandomDropRate", "0"),
                ("LegacyMonsterDropRoll", "40")));
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItemsRuntime);

        var floorItem = Assert.Single(ParseFloorItems(killerSent));

        Assert.Equal(1_091_020U, floorItem.ItemId);
        Assert.True(floorItemsRuntime.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.Equal(FloorItemRuntimePickupKind.Money, runtimeEntry.Kind);
        Assert.Equal(50_000, runtimeEntry.Amount);
        Assert.Contains(
            ParseStringPackets(killerSent),
            packet => packet.Uid == KillerUid &&
                packet.Id == LegacyStringId.Effect &&
                packet.Strings.SequenceEqual(["angelwing"]));
        var output = log.ToString();
        Assert.Contains("Name.Contains(\"Sprite\")", output);
        Assert.Contains("dropKind=money", output);
        Assert.Contains("itemId=1091020", output);
        Assert.Contains("moneyAmount=50000", output);
    }

    [Fact]
    public async Task HandlerMap1001BullMasterDeathGrantsHeartForAcceptedQuest()
    {
        var monsterFamily = CreateBossFamily(id: 2_232, name: "BullMaster", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(710_260)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = WithAcceptedQuest(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_001,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            1_838);
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var heart = Assert.Single(updatedSession!.PlayerItems.InventoryItems, item => item.ItemId == 710_260U);
        Assert.Equal(1, heart.StackSize);
        Assert.Contains(
            ParseGameItems(killerSent),
            packet => packet.ItemId == 710_260U &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        var dialog = Assert.Single(ParseNpcReplies(killerSent), packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox);
        Assert.Contains("You~killed~the~Bull~Master~and~received~its~heart.", dialog.Text);
        var output = log.ToString();
        Assert.Contains("Map == 1001", output);
        Assert.Contains("Family.ID == 2232", output);
        Assert.Contains("itemId=710260", output);
    }

    [Fact]
    public async Task HandlerMap1001BullMasterDeathShowsSourceAlreadyOwnedDialog()
    {
        var monsterFamily = CreateBossFamily(id: 2_232, name: "BullMaster", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(710_260)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = WithAcceptedQuest(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_001,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            1_838);
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(
                [
                    new PlayerItemSnapshotItem
                    {
                        Uid = 71_026,
                        ItemId = 710_260,
                        StackSize = 1,
                    },
                ],
                [],
                [],
                [],
                []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 710_260U);
        var dialog = Assert.Single(ParseNpcReplies(killerSent), packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox);
        Assert.Contains("You~received~the~Bull~heart!", dialog.Text);
        Assert.Contains("reason=already-owned", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1001BullMasterDeathShowsSourceFullInventoryDialog()
    {
        var monsterFamily = CreateBossFamily(id: 2_232, name: "BullMaster", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(710_260)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var fullInventory = Enumerable.Range(0, 40)
            .Select(index => new PlayerItemSnapshotItem
            {
                Uid = checked((uint)(80_000 + index)),
                ItemId = checked((uint)(800_000 + index)),
                StackSize = 1,
            })
            .ToArray();
        var baseSession = WithAcceptedQuest(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_001,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            1_838);
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(fullInventory, [], [], [], []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 710_260U);
        var dialog = Assert.Single(ParseNpcReplies(killerSent), packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox);
        Assert.Contains("Your~inventory~is~full.~You`ll~need~to~make~some~room,~first.", dialog.Text);
        Assert.Contains("reason=inventory-full", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1001BullGhostDeathCapturesSourceGhostAndSendsLocationEffect()
    {
        var monsterFamily = CreateBossFamily(id: 2_231, name: "MadBull", boss: false);
        monsterFamily = monsterFamily with
        {
            Attributes = new Dictionary<string, string>(monsterFamily.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["life"] = "1",
            },
        };
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001) with
        {
            BoundX = 102,
            BoundY = 292,
        };
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(710_261)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = WithPosition(
            WithAcceptedQuest(
                CreateSession(
                    KillerUid,
                    "LastHit",
                    mapId: 1_001,
                    attributes: new Dictionary<string, string>
                    {
                        ["LegacyMap1001BullGhostCaptureRate"] = "1",
                        ["LegacyMonsterDropRoll"] = "40",
                    }),
                1_837),
            x: 102,
            y: 292);
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(
                [
                    new PlayerItemSnapshotItem { Uid = 71_101, ItemId = 710_261, StackSize = 1 },
                    new PlayerItemSnapshotItem { Uid = 71_102, ItemId = 710_261, StackSize = 1 },
                ],
                [],
                [],
                [],
                []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(3, updatedSession!.PlayerItems.InventoryItems.Count(item => item.ItemId == 710_261U));
        Assert.Contains(
            ParseGameItems(killerSent),
            packet => packet.ItemId == 710_261U &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(
            ParseStringPackets(killerSent),
            packet => packet.Id == LegacyStringId.LocationEffect &&
                packet.Uid == ((uint)102 | ((uint)292 << 16)) &&
                packet.Strings.SequenceEqual(["zf2-e027"]));
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Any(content => content == "You obtained a Bull Ghost."));
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Any(content => content == "You`ve collected 3 Bull Ghost."));
        var output = log.ToString();
        Assert.Contains("Family.ID == 2231", output);
        Assert.Contains("Core.Rate(1, 3)=true", output);
        Assert.Contains("count=3", output);
    }

    [Fact]
    public async Task HandlerMap1001BullGhostDeathSkipsWhenTooFarFromSourcePoint()
    {
        var monsterFamily = CreateBossFamily(id: 2_231, name: "MadBull", boss: false);
        monsterFamily = monsterFamily with
        {
            Attributes = new Dictionary<string, string>(monsterFamily.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["life"] = "1",
            },
        };
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001) with
        {
            BoundX = 106,
            BoundY = 292,
        };
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(710_261)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = WithPosition(
            WithAcceptedQuest(
                CreateSession(
                    KillerUid,
                    "LastHit",
                    mapId: 1_001,
                    attributes: new Dictionary<string, string>
                    {
                        ["LegacyMap1001BullGhostCaptureRate"] = "1",
                        ["LegacyMonsterDropRoll"] = "40",
                    }),
                1_837),
            x: 106,
            y: 292);
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.DoesNotContain(ParseStringPackets(killerSent), packet => packet.Id == LegacyStringId.LocationEffect);
        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 710_261U);
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Any(
                content => content == "Only when you herd the Mad Bull to (102,292) before killing them can you capture its ghost."));
        Assert.Contains("reason=too-far", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1001BullGhostDeathKeepsSourceFailedCapturePath()
    {
        var monsterFamily = CreateBossFamily(id: 2_231, name: "MadBull", boss: false);
        monsterFamily = monsterFamily with
        {
            Attributes = new Dictionary<string, string>(monsterFamily.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["life"] = "1",
            },
        };
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001) with
        {
            BoundX = 102,
            BoundY = 292,
        };
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(710_261)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = WithPosition(
            WithAcceptedQuest(
                CreateSession(
                    KillerUid,
                    "LastHit",
                    mapId: 1_001,
                    attributes: new Dictionary<string, string>
                    {
                        ["LegacyMap1001BullGhostCaptureRate"] = "0",
                        ["LegacyMonsterDropRoll"] = "40",
                    }),
                1_837),
            x: 102,
            y: 292);
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.Contains(ParseStringPackets(killerSent), packet => packet.Id == LegacyStringId.LocationEffect);
        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 710_261U);
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Any(
                content => content == "Well, pay more attention next time. You failed in retrieving the Bull Ghost."));
        Assert.Contains("Core.Rate(1, 3)=false", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1001BullGhostDeathKeepsSourceFullInventoryPath()
    {
        var monsterFamily = CreateBossFamily(id: 2_231, name: "MadBull", boss: false);
        monsterFamily = monsterFamily with
        {
            Attributes = new Dictionary<string, string>(monsterFamily.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["life"] = "1",
            },
        };
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001) with
        {
            BoundX = 102,
            BoundY = 292,
        };
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(710_261)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var fullInventory = Enumerable.Range(0, 40)
            .Select(index => new PlayerItemSnapshotItem
            {
                Uid = checked((uint)(90_000 + index)),
                ItemId = checked((uint)(900_000 + index)),
                StackSize = 1,
            })
            .ToArray();
        var baseSession = WithPosition(
            WithAcceptedQuest(
                CreateSession(
                    KillerUid,
                    "LastHit",
                    mapId: 1_001,
                    attributes: new Dictionary<string, string>
                    {
                        ["LegacyMap1001BullGhostCaptureRate"] = "1",
                        ["LegacyMonsterDropRoll"] = "40",
                    }),
                1_837),
            x: 102,
            y: 292);
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(fullInventory, [], [], [], []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.Contains(ParseStringPackets(killerSent), packet => packet.Id == LegacyStringId.LocationEffect);
        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 710_261U);
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Any(
                content => content == "You inventory is full. Make some room and come back for the Bull Ghost."));
        Assert.Contains("reason=inventory-full", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1001BurntBullHornDeathStacksSourceHornWhenFerventPillActive()
    {
        var monsterFamily = CreateBossFamily(id: 57, name: "BullMonster", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(710_259)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = WithAcceptedQuest(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_001,
                attributes: new Dictionary<string, string>
                {
                    ["OnFerentPill"] = "1",
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            1_836);
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(
                [
                    new PlayerItemSnapshotItem { Uid = 71_259, ItemId = 710_259, StackSize = 9 },
                ],
                [],
                [],
                [],
                []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var horn = Assert.Single(updatedSession!.PlayerItems.InventoryItems, item => item.ItemId == 710_259U);
        Assert.Equal(10, horn.StackSize);
        Assert.Contains(
            ParseGameItems(killerSent),
            packet => packet.ItemId == 710_259U &&
                packet.Mode == LegacyGameItemPacket.UpdateItemMode &&
                packet.StackSize == 10);
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Any(
                content => content == "You`ve obtained 10 Burnt Bull Horns and put it in your inventory."));
        var output = log.ToString();
        Assert.Contains("Family.ID == 57", output);
        Assert.Contains("OnFerentPill=true", output);
        Assert.Contains("count=10", output);
    }

    [Fact]
    public async Task HandlerMap1001BurntBullHornDeathKeepsSourceInactivePillPath()
    {
        var monsterFamily = CreateBossFamily(id: 57, name: "BullMonster", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(710_259)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = WithAcceptedQuest(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_001,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            1_836);
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 710_259U);
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Any(
                content => content == "Bring the Fervent Pill received from Master Mo Mo to go to the Mystic Castle. Use the pill first, and then kill the Bull Monsters to get 10 Burnt Bull Horns for Master Mo Mo."));
        Assert.Contains("reason=fervent-pill-inactive", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1001BurntBullHornDeathKeepsSourceAlreadyCompleteNotice()
    {
        var monsterFamily = CreateBossFamily(id: 57, name: "BullMonster", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(710_259)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = WithAcceptedQuest(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_001,
                attributes: new Dictionary<string, string>
                {
                    ["OnFerentPill"] = "1",
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            1_836);
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(
                [
                    new PlayerItemSnapshotItem { Uid = 71_259, ItemId = 710_259, StackSize = 10 },
                ],
                [],
                [],
                [],
                []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 710_259U);
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Any(
                content => content == "You`ve obtained 10 Burnt Bull Horns and put it in your inventory."));
        Assert.Contains("count=10", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1001BurntBullHornDeathKeepsSourceFullInventoryPath()
    {
        var monsterFamily = CreateBossFamily(id: 57, name: "BullMonster", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(710_259)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var fullInventory = Enumerable.Range(0, 40)
            .Select(index => new PlayerItemSnapshotItem
            {
                Uid = checked((uint)(91_000 + index)),
                ItemId = checked((uint)(910_000 + index)),
                StackSize = 1,
            })
            .ToArray();
        var baseSession = WithAcceptedQuest(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_001,
                attributes: new Dictionary<string, string>
                {
                    ["OnFerentPill"] = "1",
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            1_836);
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(fullInventory, [], [], [], []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 710_259U);
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Any(
                content => content == "Your inventory is full. Make some room and come back for the Burnt Bull Horn."));
        Assert.Contains("reason=inventory-full", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1001JinxTombBatDeathGrantsSourceHeart()
    {
        var monsterFamily = CreateBossFamily(id: 2_189, name: "JinxTombBat", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(711_518)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_001,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var heart = Assert.Single(updatedSession!.PlayerItems.InventoryItems, item => item.ItemId == 711_518U);
        Assert.Equal(1, heart.StackSize);
        Assert.Contains(
            ParseGameItems(killerSent),
            packet => packet.ItemId == 711_518U &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        var dialog = Assert.Single(ParseNpcReplies(killerSent), packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox);
        Assert.Contains("You~killed~the~Jinx~Tomb~Bat~and~received~a~Jinx~Tomb~Bat~Heart!", dialog.Text);
        Assert.Contains("Family.ID == 2189", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1001JinxTombBatDeathSkipsWhenHeartAlreadyOwned()
    {
        var monsterFamily = CreateBossFamily(id: 2_189, name: "JinxTombBat", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(711_518)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_001,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(
                [
                    new PlayerItemSnapshotItem { Uid = 71_518, ItemId = 711_518, StackSize = 1 },
                ],
                [],
                [],
                [],
                []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 711_518U);
        Assert.DoesNotContain(ParseNpcReplies(killerSent), packet => packet.Text.Contains("Jinx~Tomb~Bat", StringComparison.Ordinal));
        Assert.DoesNotContain("Jinx Tomb Bat heart", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1001JinxTombBatDeathKeepsSourceFullInventoryDialog()
    {
        var monsterFamily = CreateBossFamily(id: 2_189, name: "JinxTombBat", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(711_518)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var fullInventory = Enumerable.Range(0, 40)
            .Select(index => new PlayerItemSnapshotItem
            {
                Uid = checked((uint)(92_000 + index)),
                ItemId = checked((uint)(920_000 + index)),
                StackSize = 1,
            })
            .ToArray();
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_001,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(fullInventory, [], [], [], []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 711_518U);
        var dialog = Assert.Single(ParseNpcReplies(killerSent), packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox);
        Assert.Contains("Your~inventory~is~full.~Hurry~to~make~1~empty~space.", dialog.Text);
        Assert.Contains("reason=inventory-full", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1001BatEssenceDeathStacksSourceEssenceAndIncrementsQuest()
    {
        var monsterFamily = CreateBossFamily(id: 56, name: "TombBat", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(721_877)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = WithAcceptedQuest(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_001,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMap1001BatEssenceRate"] = "1",
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            1_832);
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(
                [
                    new PlayerItemSnapshotItem { Uid = 72_187, ItemId = 721_877, StackSize = 4 },
                ],
                [],
                [],
                [],
                []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var essence = Assert.Single(updatedSession!.PlayerItems.InventoryItems, item => item.ItemId == 721_877U);
        Assert.Equal(5, essence.StackSize);
        Assert.True(updatedSession.Character.PlayerQuests.TryGetQuest(1_832, out var quest));
        Assert.Equal(1U, quest.Intentions[0]);
        Assert.Contains(
            ParseGameItems(killerSent),
            packet => packet.ItemId == 721_877U &&
                packet.Mode == LegacyGameItemPacket.UpdateItemMode &&
                packet.StackSize == 5);
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Any(content => content == "You found the BatEssence from the Tomb Bats."));
        var output = log.ToString();
        Assert.Contains("Family.ID == 56", output);
        Assert.Contains("Core.Rate(20)=true", output);
        Assert.Contains("questId=1832", output);
    }

    [Fact]
    public async Task HandlerMap1001BatEssenceDeathKeepsSourceRateMissSilentPath()
    {
        var monsterFamily = CreateBossFamily(id: 56, name: "TombBat", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(721_877)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = WithAcceptedQuest(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_001,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMap1001BatEssenceRate"] = "0",
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            1_832);
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 721_877U);
        Assert.DoesNotContain(
            ParseMessages(killerSent),
            packet => packet.Content.Any(content => content == "You found the BatEssence from the Tomb Bats."));
        Assert.Contains("reason=rate-miss", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1001BatEssenceDeathSkipsWhenFiveEssencesAlreadyCollected()
    {
        var monsterFamily = CreateBossFamily(id: 56, name: "TombBat", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(721_877)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = WithAcceptedQuest(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_001,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMap1001BatEssenceRate"] = "1",
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            1_832);
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(
                [
                    new PlayerItemSnapshotItem { Uid = 72_187, ItemId = 721_877, StackSize = 5 },
                ],
                [],
                [],
                [],
                []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 721_877U);
        Assert.DoesNotContain(
            ParseMessages(killerSent),
            packet => packet.Content.Any(content => content == "You found the BatEssence from the Tomb Bats."));
        Assert.Contains("reason=already-collected", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1001BatEssenceDeathKeepsSourceFullInventoryDialog()
    {
        var monsterFamily = CreateBossFamily(id: 56, name: "TombBat", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(721_877)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var fullInventory = Enumerable.Range(0, 40)
            .Select(index => new PlayerItemSnapshotItem
            {
                Uid = checked((uint)(93_000 + index)),
                ItemId = checked((uint)(930_000 + index)),
                StackSize = 1,
            })
            .ToArray();
        var baseSession = WithAcceptedQuest(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_001,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMap1001BatEssenceRate"] = "1",
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            1_832);
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(fullInventory, [], [], [], []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 721_877U);
        var dialog = Assert.Single(ParseNpcReplies(killerSent), packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox);
        Assert.Contains("You~inventory~is~full.~You`ll~need~to~make~some~room,~first.", dialog.Text);
        Assert.Contains("reason=inventory-full", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1001TombBatKeyDeathStacksSourceKeyAndIncrementsQuest()
    {
        var monsterFamily = CreateBossFamily(id: 20, name: "TombBat", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(721_876)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = WithAcceptedQuest(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_001,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMap1001TombBatKeyRate"] = "1",
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            1_829);
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(
                [
                    new PlayerItemSnapshotItem { Uid = 72_176, ItemId = 721_876, StackSize = 4 },
                ],
                [],
                [],
                [],
                []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var key = Assert.Single(updatedSession!.PlayerItems.InventoryItems, item => item.ItemId == 721_876U);
        Assert.Equal(5, key.StackSize);
        Assert.True(updatedSession.Character.PlayerQuests.TryGetQuest(1_829, out var quest));
        Assert.Equal(1U, quest.Intentions[0]);
        Assert.Contains(
            ParseGameItems(killerSent),
            packet => packet.ItemId == 721_876U &&
                packet.Mode == LegacyGameItemPacket.UpdateItemMode &&
                packet.StackSize == 5);
        var dialog = Assert.Single(ParseNpcReplies(killerSent), packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox);
        Assert.Contains("You~received~a~Key~from~the~Tomb~Bat.", dialog.Text);
        var output = log.ToString();
        Assert.Contains("Family.ID == 20", output);
        Assert.Contains("Core.Rate(20)=true", output);
        Assert.Contains("questId=1829", output);
    }

    [Fact]
    public async Task HandlerMap1001TombBatKeyDeathKeepsSourceRateMissSilentPath()
    {
        var monsterFamily = CreateBossFamily(id: 20, name: "TombBat", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(721_876)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = WithAcceptedQuest(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_001,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMap1001TombBatKeyRate"] = "0",
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            1_829);
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 721_876U);
        Assert.DoesNotContain(
            ParseNpcReplies(killerSent),
            packet => packet.Text.Contains("Tomb~Bat", StringComparison.Ordinal));
        Assert.Contains("reason=rate-miss", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1001TombBatKeyDeathSkipsWhenBlockingItemOwned()
    {
        var monsterFamily = CreateBossFamily(id: 20, name: "TombBat", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(721_876)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = WithAcceptedQuest(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_001,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMap1001TombBatKeyRate"] = "1",
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            1_829);
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(
                [
                    new PlayerItemSnapshotItem { Uid = 72_170, ItemId = 721_870, StackSize = 1 },
                ],
                [],
                [],
                [],
                []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 721_876U);
        Assert.DoesNotContain(ParseNpcReplies(killerSent), packet => packet.Text.Contains("Key", StringComparison.Ordinal));
        Assert.Contains("reason=blocking-item-owned", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1001TombBatKeyDeathShowsSourceFiveKeysDialog()
    {
        var monsterFamily = CreateBossFamily(id: 20, name: "TombBat", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(721_876)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = WithAcceptedQuest(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_001,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMap1001TombBatKeyRate"] = "1",
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            1_829);
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(
                [
                    new PlayerItemSnapshotItem { Uid = 72_176, ItemId = 721_876, StackSize = 5 },
                ],
                [],
                [],
                [],
                []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 721_876U);
        var dialog = Assert.Single(ParseNpcReplies(killerSent), packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox);
        Assert.Contains("Sorry,~but~you`ve~already~had~5~Keys.", dialog.Text);
        Assert.Contains("reason=already-collected", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1001TombBatKeyDeathKeepsSourceFullInventoryDialog()
    {
        var monsterFamily = CreateBossFamily(id: 20, name: "TombBat", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 1_001);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(721_876)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var fullInventory = Enumerable.Range(0, 40)
            .Select(index => new PlayerItemSnapshotItem
            {
                Uid = checked((uint)(94_000 + index)),
                ItemId = checked((uint)(940_000 + index)),
                StackSize = 1,
            })
            .ToArray();
        var baseSession = WithAcceptedQuest(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_001,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMap1001TombBatKeyRate"] = "1",
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            1_829);
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(fullInventory, [], [], [], []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 721_876U);
        var dialog = Assert.Single(ParseNpcReplies(killerSent), packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox);
        Assert.Contains("You~inventory~is~full.~You`ll~need~to~make~some~room,~first.", dialog.Text);
        Assert.Contains("reason=inventory-full", log.ToString());
    }

    [Fact]
    public async Task HandlerDisCityMap2022DeathIncrementsSourceKillCounter()
    {
        var monsterFamily = CreateBossFamily(id: 9_921, name: "DisCityMonster", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 2_022);
        var worldData = CreateWorldDataCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 2_022,
            attributes: new Dictionary<string, string>
            {
                ["KillersDisCity"] = "2",
                ["LegacyMonsterDropRoll"] = "40",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("3", updatedSession!.Character.Attributes["KillersDisCity"]);
        Assert.Contains("branch=\"MonsterRole.cs Map == 2022\"", log.ToString());
    }

    [Fact]
    public async Task HandlerUltimatePlutoMap2024DeathSendsSourceItemDrop()
    {
        var monsterFamily = CreateBossFamily(id: 66_432, name: "UltimatePluto", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 2_024);
        var worldData = CreateWorldDataCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 2_024,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        var floorItem = Assert.Single(ParseFloorItems(killerSent));

        Assert.Equal(790_001U, floorItem.ItemId);
        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs Map == 2024 && Family.ID == 66432\"", output);
        Assert.Contains("KillTheUltimatePluto", output);
    }

    [Theory]
    [InlineData(1_081, 10)]
    [InlineData(1_080, 20)]
    [InlineData(2_060, 30)]
    public async Task HandlerBattlefieldMonsterDeathSendsSourceMessageAndAppliesActivePoints(
        int mapId,
        int expectedPoints)
    {
        var monsterFamily = CreateBossFamily(id: 9_922, name: "BattlefieldMonster", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId);
        var worldData = CreateWorldDataCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: mapId,
            attributes: new Dictionary<string, string>
            {
                ["BattleFieldPoints"] = "4",
                ["LegacyBattleFieldTournamentAlive"] = "1",
                ["LegacyMonsterDropRoll"] = "40",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(
            (4 + expectedPoints).ToString(CultureInfo.InvariantCulture),
            updatedSession!.Character.Attributes["BattleFieldPoints"]);
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Contains(
                FormattableString.Invariant($"You received {expectedPoints} BattlePoints.")));
        Assert.Contains($"battlePoints={expectedPoints} pointsApplied=True", log.ToString());
    }

    [Fact]
    public async Task HandlerDragonFurnaceMap10428Family2984DeathTeleportsAndLogsFusionGap()
    {
        var monsterFamily = CreateBossFamily(id: 2_984, name: "DragonFurnaceMonster", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 10_428);
        var worldData = CreateWorldDataCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_428,
            attributes: new Dictionary<string, string>
            {
                ["DragonFurnace"] = "4200001",
                ["LegacyDragonFurnaceUpgradeSuccess"] = "0",
                ["LegacyMonsterDropRoll"] = "40",
            }) with
            {
                PlayerItems = new PlayerItemSnapshot(
                    [
                        new PlayerItemSnapshotItem
                        {
                            Uid = 90_001,
                            ItemId = 4_200_001,
                            StackSize = 2,
                        },
                    ],
                    [],
                    [],
                    [],
                    []),
            };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(1_002, updatedSession!.Character.MapId);
        Assert.Equal(351, updatedSession.Character.X);
        Assert.Equal(422, updatedSession.Character.Y);
        Assert.Equal(10_428, updatedSession.Character.PreviousMapId);

        var teleport = Assert.Single(ParseActions(killerSent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_002U, teleport.DwParam);
        Assert.Equal(351U, teleport.PositionX);
        Assert.Equal(422U, teleport.PositionY);
        Assert.Equal(1_002, teleport.DwParam3);

        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs Map == 10428 && Family.ID == 2984\"", output);
        Assert.Contains("dragon-furnace-monster-death", output);
    }

    [Fact]
    public async Task HandlerDragonFurnaceMap10428Family2984DeathAppliesSourceFusionSuccess()
    {
        var monsterFamily = CreateBossFamily(id: 2_984, name: "DragonFurnaceMonster", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 10_428);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            CreateDragonFurnaceItemTypes(),
            CreateDragonFurnaceSpiritRates());
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_428,
            attributes: new Dictionary<string, string>
            {
                ["DragonFurnace"] = "4200001",
                ["LegacyDragonFurnaceUpgradeSuccess"] = "1",
                ["LegacyMonsterDropRoll"] = "40",
            }) with
            {
                PlayerItems = new PlayerItemSnapshot(
                    [
                        new PlayerItemSnapshotItem
                        {
                            Uid = 90_001,
                            ItemId = 4_200_001,
                            StackSize = 2,
                        },
                    ],
                    [],
                    [],
                    [],
                    []),
            };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.DoesNotContain(updatedSession!.PlayerItems.InventoryItems, item => item.ItemId == 4_200_001U);
        var reward = Assert.Single(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == 4_200_002U);
        Assert.Equal(1, reward.StackSize);
        Assert.Equal(1_002, updatedSession.Character.MapId);
        Assert.Equal(351, updatedSession.Character.X);
        Assert.Equal(422, updatedSession.Character.Y);

        var itemPacket = Assert.Single(ParseGameItems(killerSent), packet => packet.ItemId == 4_200_002U);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, itemPacket.Mode);
        var messageBox = Assert.Single(ParseNpcReplies(killerSent), packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox);
        Assert.Contains("Fusing Succeeded", messageBox.Text);
        Assert.Contains("Item4200002", messageBox.Text);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy DragonFurnace monster death fusion success", output);
        Assert.DoesNotContain("dragon-furnace-monster-death", output);
    }

    [Fact]
    public async Task HandlerDragonFurnaceMap10428Family2984DeathAppliesSourceFusionFailureExperience()
    {
        var monsterFamily = CreateBossFamily(id: 2_984, name: "GuardDragonFurnaceMonster", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 10_428);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            CreateDragonFurnaceItemTypes(),
            CreateDragonFurnaceSpiritRates());
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_428,
            attributes: new Dictionary<string, string>
            {
                ["DragonFurnace"] = "4200001",
                ["LegacyDragonFurnaceUpgradeSuccess"] = "0",
                ["LegacyMonsterDropRoll"] = "40",
            });
        var killerSession = baseSession with
        {
            Character = baseSession.Character with
            {
                Level = 1,
                Experience = 0,
            },
            PlayerItems = new PlayerItemSnapshot(
                [
                    new PlayerItemSnapshotItem
                    {
                        Uid = 90_001,
                        ItemId = 4_200_001,
                        StackSize = 2,
                    },
                ],
                [],
                [],
                [],
                []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.DoesNotContain(updatedSession!.PlayerItems.InventoryItems, item => item.ItemId == 4_200_001U);
        Assert.Equal(5, updatedSession.Character.Experience);
        Assert.Equal(1, updatedSession.Character.Level);
        Assert.Equal(1_002, updatedSession.Character.MapId);

        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 4_200_002U);
        var experienceUpdate = Assert.Single(
            ParseMsgUpdates(killerSent),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));
        Assert.Contains(
            new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Experience, 5),
            experienceUpdate.Updates);
        var messageBox = Assert.Single(ParseNpcReplies(killerSent), packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox);
        Assert.Contains("Fusing Failed", messageBox.Text);
        Assert.Contains("lost 2 Item4200001", messageBox.Text);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy DragonFurnace monster death fusion failure", output);
        Assert.DoesNotContain("dragon-furnace-monster-death", output);
    }

    [Fact]
    public async Task HandlerImperialMausoleumDukeDeathSpawnsTargetableTrueFormAndCollapseTeleport()
    {
        var disguisedFamily = CreateBossFamily(id: 452_373, name: "DukeOfHellDisguise", boss: false);
        var trueFormFamily = CreateBossFamily(id: 452_374, name: "DukeOfHellTrueForm", boss: false);
        var spawn = CreateBossSpawn(disguisedFamily, mapId: 44_461) with
        {
            BoundX = 336,
            BoundY = 193,
            SourceRelativePath = "44461/DukeOfHellDisguise/1.ini",
        };
        var worldData = CreateWorldDataCatalog(disguisedFamily, spawn, extraMonsters: [trueFormFamily]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            disguisedFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 44_461,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        var killerSession = baseSession with
        {
            Character = baseSession.Character with
            {
                X = 336,
                Y = 193,
                PreviousMapId = 44_461,
                PreviousMapX = 336,
                PreviousMapY = 193,
            },
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var trueFormSession));
        Assert.NotNull(trueFormSession);
        Assert.Equal(44_462, trueFormSession!.Character.MapId);
        Assert.Equal(336, trueFormSession.Character.X);
        Assert.Equal(193, trueFormSession.Character.Y);
        Assert.Equal(44_461, trueFormSession.Character.PreviousMapId);
        Assert.True(trueFormSession.Character.Attributes.TryGetValue("DynamicID", out var firstDynamicId));
        Assert.NotNull(firstDynamicId);
        Assert.NotEqual("0", firstDynamicId);

        var firstMessageBox = Assert.Single(
            ParseNpcReplies(killerSent),
            packet => packet.Text.Contains("Duke of Hell has been enraged", StringComparison.Ordinal));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, firstMessageBox.InteractType);
        var trueFormSpawn = Assert.Single(ParseMonsterSpawns(killerSent), packet => packet.MonsterId == 452_374U);
        Assert.InRange(trueFormSpawn.Uid, 900_000U, 999_999U);
        Assert.Equal(336, trueFormSpawn.X);
        Assert.Equal(193, trueFormSpawn.Y);

        await KillBossAsync(
            killerConnection,
            trueFormSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            new LegacyMonsterInstance(
                trueFormSpawn.Uid,
                44_462,
                trueFormSpawn.X,
                trueFormSpawn.Y,
                trueFormSpawn.Mesh,
                trueFormSpawn.MonsterLevel,
                trueFormSpawn.Hitpoints,
                trueFormSpawn.MaxLife,
                trueFormSpawn.MonsterId,
                "DukeOfHellTrueForm",
                Boss: false,
                GeneratorId: checked((int)trueFormSpawn.Uid),
                SpawnIndex: 0,
                DynamicId: uint.Parse(firstDynamicId, CultureInfo.InvariantCulture)),
            log);

        Assert.True(sessions.TryGet(killerConnection, out var collapsedSession));
        Assert.NotNull(collapsedSession);
        Assert.Equal(44_463, collapsedSession!.Character.MapId);
        Assert.Equal(44_462, collapsedSession.Character.PreviousMapId);
        Assert.True(collapsedSession.Character.Attributes.TryGetValue("DynamicID", out var secondDynamicId));
        Assert.NotNull(secondDynamicId);
        Assert.NotEqual(firstDynamicId, secondDynamicId);

        Assert.Contains(
            ParseNpcReplies(killerSent),
            packet => packet.Text.Contains("imperial mausoleum will soon collapse", StringComparison.Ordinal));
        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 44461 && Family.ID == 452373\"", output);
        Assert.Contains("branch=\"MonsterRole.cs Map.AddMapMonster monsterId=452374 moveback\"", output);
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 44462\"", output);
        Assert.DoesNotContain("monster instance was not found", output);
    }

    [Fact]
    public async Task HandlerPursuingDevilsMap44456DeathSpawnsFollowupAndIncrementsQuestObjective()
    {
        var pursuingFamily = CreateBossFamily(id: 657_218, name: "PursuingDevil", boss: false);
        var followupFamily = CreateBossFamily(id: 657_219, name: "PursuingDevilFollowup", boss: false);
        var spawn = CreateBossSpawn(pursuingFamily, mapId: 44_456) with
        {
            SourceRelativePath = "44456/PursuingDevil/1.ini",
            Attributes = new Dictionary<string, string>
            {
                ["DynamicID"] = "12345",
            },
        };
        var worldData = CreateWorldDataCatalog(pursuingFamily, spawn, extraMonsters: [followupFamily]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            pursuingFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 44_456,
            attributes: new Dictionary<string, string>
            {
                ["DynamicID"] = "12345",
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacyVisibleMonsterCount"] = "1",
            });
        var killerSession = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            3_799,
                            PlayerQuestStatus.Accepted,
                            0,
                            [0U]),
                    ]),
            },
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        var followupSpawns = ParseMonsterSpawns(killerSent)
            .Where(packet => packet.MonsterId == 657_219U)
            .ToArray();
        Assert.True(followupSpawns.Length == 1, log.ToString());
        var followupSpawn = followupSpawns.Single();
        Assert.InRange(followupSpawn.Uid, 900_000U, 999_999U);
        Assert.True(monsterStates.TryGetDynamicMonsterByUid(
            44_456,
            12_345,
            followupSpawn.Uid,
            out var followupInstance,
            out var followupMonsterFamily));
        Assert.Equal(657_219U, followupInstance.MonsterId);
        Assert.Equal(657_219, followupMonsterFamily.Id);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var quest = Assert.Single(updatedSession!.Character.PlayerQuests.Quests, entry => entry.Uid == 3_799U);
        Assert.Equal([1U], quest.Intentions);

        var questFrame = Assert.Single(ParseQuestObjectiveFrames(killerSent), packet => packet.Uid == 3_799U);
        Assert.Equal([1U], questFrame.Intentions);

        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs Map.AddMapMonster monsterId=657219 movego\"", output);
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 44456\"", output);
        Assert.Contains("pursuing-devils-autopatcher", output);
    }

    [Fact]
    public async Task HandlerLyraMap1791Family2172DeathSpawnsFiveGhostsAndWarning()
    {
        var lyraFamily = CreateBossFamily(id: 2_172, name: "Lyra", boss: false);
        var ghostFamily = CreateBossFamily(id: 2_173, name: "LyraGhost", boss: false);
        var spawn = CreateBossSpawn(lyraFamily, mapId: 1_791) with
        {
            SourceRelativePath = "1791/Lyra/1.ini",
            Attributes = new Dictionary<string, string>
            {
                ["DynamicID"] = "9876",
            },
        };
        var worldData = CreateWorldDataCatalog(lyraFamily, spawn, extraMonsters: [ghostFamily]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            lyraFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_791,
            attributes: new Dictionary<string, string>
            {
                ["DynamicID"] = "9876",
                ["LegacyMonsterDropRoll"] = "40",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        var ghostSpawns = ParseMonsterSpawns(killerSent)
            .Where(packet => packet.MonsterId == 2_173U)
            .ToArray();
        Assert.Equal(5, ghostSpawns.Length);
        foreach (var ghostSpawn in ghostSpawns)
        {
            Assert.InRange(ghostSpawn.Uid, 900_000U, 999_999U);
            Assert.True(monsterStates.TryGetDynamicMonsterByUid(
                1_791,
                9_876,
                ghostSpawn.Uid,
                out var ghostInstance,
                out var ghostMonsterFamily));
            Assert.Equal(2_173U, ghostInstance.MonsterId);
            Assert.Equal(2_173, ghostMonsterFamily.Id);
        }

        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Contains("Some Lyra Ghosts are coming! Watch out!"));
        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 1791 && Family.ID == 2172\"", output);
        Assert.Contains("branch=\"MonsterRole.cs Map.AddMapMonster monsterId=2173 movego range=7\"", output);
        Assert.Contains("branch=\"MonsterRole.cs Map.AddMapMonster monsterId=2173 movego range=3\"", output);
    }

    [Fact]
    public async Task HandlerEpicTrojanMap3833Family7485DeathSetsStageAndSpawnsFlameDevastator()
    {
        var triggerFamily = CreateBossFamily(id: 7_485, name: "FlameDevastatorSeal", boss: false);
        var followupFamily = CreateBossFamily(id: 7_486, name: "FlameDevastator", boss: false);
        var spawn = CreateBossSpawn(triggerFamily, mapId: 3_833) with
        {
            Attributes = new Dictionary<string, string>
            {
                ["DynamicID"] = "2468",
            },
        };
        var worldData = CreateWorldDataCatalog(triggerFamily, spawn, extraMonsters: [followupFamily]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            triggerFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_833,
            attributes: new Dictionary<string, string>
            {
                ["DynamicID"] = "2468",
                ["LegacyMonsterDropRoll"] = "40",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("51", updatedSession!.Character.Attributes["StageEpicTrojanQuest"]);

        var followupSpawn = Assert.Single(ParseMonsterSpawns(killerSent), packet => packet.MonsterId == 7_486U);
        Assert.InRange(followupSpawn.Uid, 900_000U, 999_999U);
        Assert.Equal(830, followupSpawn.X);
        Assert.Equal(584, followupSpawn.Y);
        Assert.True(monsterStates.TryGetDynamicMonsterByUid(
            3_833,
            2_468,
            followupSpawn.Uid,
            out var followupInstance,
            out var followupMonsterFamily));
        Assert.Equal(7_486U, followupInstance.MonsterId);
        Assert.Equal(7_486, followupMonsterFamily.Id);

        var messageContents = ParseMessages(killerSent).SelectMany(packet => packet.Content).ToArray();
        Assert.Contains("The~earth~is~shaking,~and~the~Flame~Devastator~revived!", messageContents);

        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 3833 && Family.ID == 7485\"", output);
        Assert.Contains("branch=\"MonsterRole.cs Map.AddMapMonster monsterId=7486 ride_screen\"", output);
        Assert.Contains("spawnedFollowup=True", output);
    }

    [Fact]
    public async Task HandlerEpicTrojanMap3833Family7485DeathSkipsDuplicateFlameDevastatorSpawn()
    {
        var triggerFamily = CreateBossFamily(id: 7_485, name: "FlameDevastatorSeal", boss: false);
        var followupFamily = CreateBossFamily(id: 7_486, name: "FlameDevastator", boss: false);
        var spawn = CreateBossSpawn(triggerFamily, mapId: 3_833) with
        {
            Attributes = new Dictionary<string, string>
            {
                ["DynamicID"] = "2468",
            },
        };
        var worldData = CreateWorldDataCatalog(triggerFamily, spawn, extraMonsters: [followupFamily]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            triggerFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        monsterStates.RegisterDynamicMonster(
            new LegacyMonsterInstance(
                Uid: 900_000,
                MapId: 3_833,
                X: 831,
                Y: 585,
                Mesh: 900,
                Level: 138,
                Hitpoints: 200_000,
                MaxLife: 200_000,
                MonsterId: 7_486,
                Name: "FlameDevastator",
                Boss: false,
                GeneratorId: 900_000,
                SpawnIndex: 0,
                DynamicId: 2_468),
            followupFamily);
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_833,
            attributes: new Dictionary<string, string>
            {
                ["DynamicID"] = "2468",
                ["LegacyMonsterDropRoll"] = "40",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("51", updatedSession!.Character.Attributes["StageEpicTrojanQuest"]);
        Assert.DoesNotContain(ParseMonsterSpawns(killerSent), packet => packet.MonsterId == 7_486U);

        var messageContents = ParseMessages(killerSent).SelectMany(packet => packet.Content).ToArray();
        Assert.Contains("The~earth~is~shaking,~and~the~Flame~Devastator~revived!", messageContents);

        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 3833 && Family.ID == 7485\"", output);
        Assert.Contains("spawnedFollowup=False", output);
        Assert.DoesNotContain("branch=\"MonsterRole.cs Map.AddMapMonster monsterId=7486 ride_screen\"", output);
    }

    [Fact]
    public async Task HandlerEpicTrojanMap3833Family7486DeathSetsStageSendsEffectAndSpawnsFlameDevastator()
    {
        var triggerFamily = CreateBossFamily(id: 7_486, name: "FlameDevastator", boss: false);
        var followupFamily = CreateBossFamily(id: 7_487, name: "FlameDevastatorFinal", boss: false);
        var spawn = CreateBossSpawn(triggerFamily, mapId: 3_833) with
        {
            Attributes = new Dictionary<string, string>
            {
                ["DynamicID"] = "2468",
            },
        };
        var worldData = CreateWorldDataCatalog(triggerFamily, spawn, extraMonsters: [followupFamily]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            triggerFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_833,
            attributes: new Dictionary<string, string>
            {
                ["DynamicID"] = "2468",
                ["LegacyMonsterDropRoll"] = "40",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("52", updatedSession!.Character.Attributes["StageEpicTrojanQuest"]);
        Assert.Contains(
            ParseStringPackets(killerSent),
            packet => packet.Id == LegacyStringId.LocationEffect &&
                packet.Uid == ((uint)45 | ((uint)45 << 16)) &&
                packet.Strings.SequenceEqual(["xidag_bafi"]));

        var followupSpawn = Assert.Single(ParseMonsterSpawns(killerSent), packet => packet.MonsterId == 7_487U);
        Assert.InRange(followupSpawn.Uid, 900_000U, 999_999U);
        Assert.Equal(830, followupSpawn.X);
        Assert.Equal(584, followupSpawn.Y);
        Assert.True(monsterStates.TryGetDynamicMonsterByUid(
            3_833,
            2_468,
            followupSpawn.Uid,
            out var followupInstance,
            out var followupMonsterFamily));
        Assert.Equal(7_487U, followupInstance.MonsterId);
        Assert.Equal(7_487, followupMonsterFamily.Id);

        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 3833 && Family.ID == 7486\"", output);
        Assert.Contains("branch=\"MonsterRole.cs Map.AddMapMonster monsterId=7487 xidag_bafi\"", output);
        Assert.Contains("spawnedFollowup=True", output);
    }

    [Fact]
    public async Task HandlerEpicTrojanMap3833Family7486DeathSkipsDuplicateFlameDevastatorSpawn()
    {
        var triggerFamily = CreateBossFamily(id: 7_486, name: "FlameDevastator", boss: false);
        var followupFamily = CreateBossFamily(id: 7_487, name: "FlameDevastatorFinal", boss: false);
        var spawn = CreateBossSpawn(triggerFamily, mapId: 3_833) with
        {
            Attributes = new Dictionary<string, string>
            {
                ["DynamicID"] = "2468",
            },
        };
        var worldData = CreateWorldDataCatalog(triggerFamily, spawn, extraMonsters: [followupFamily]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            triggerFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        monsterStates.RegisterDynamicMonster(
            new LegacyMonsterInstance(
                Uid: 900_000,
                MapId: 3_833,
                X: 831,
                Y: 585,
                Mesh: 900,
                Level: 138,
                Hitpoints: 200_000,
                MaxLife: 200_000,
                MonsterId: 7_487,
                Name: "FlameDevastatorFinal",
                Boss: false,
                GeneratorId: 900_000,
                SpawnIndex: 0,
                DynamicId: 2_468),
            followupFamily);
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_833,
            attributes: new Dictionary<string, string>
            {
                ["DynamicID"] = "2468",
                ["LegacyMonsterDropRoll"] = "40",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("52", updatedSession!.Character.Attributes["StageEpicTrojanQuest"]);
        Assert.Contains(
            ParseStringPackets(killerSent),
            packet => packet.Id == LegacyStringId.LocationEffect &&
                packet.Uid == ((uint)45 | ((uint)45 << 16)) &&
                packet.Strings.SequenceEqual(["xidag_bafi"]));
        Assert.DoesNotContain(ParseMonsterSpawns(killerSent), packet => packet.MonsterId == 7_487U);

        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 3833 && Family.ID == 7486\"", output);
        Assert.Contains("spawnedFollowup=False", output);
        Assert.DoesNotContain("branch=\"MonsterRole.cs Map.AddMapMonster monsterId=7487 xidag_bafi\"", output);
    }

    [Fact]
    public async Task HandlerEpicTrojanMap3833Family7487DeathKeepsSourceNoWeaponCompletionPath()
    {
        var finalFamily = CreateBossFamily(id: 7_487, name: "FlameDevastatorFinal", boss: false);
        var spawn = CreateBossSpawn(finalFamily, mapId: 3_833) with
        {
            Attributes = new Dictionary<string, string>
            {
                ["DynamicID"] = "2468",
            },
        };
        var worldData = CreateWorldDataCatalog(finalFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            finalFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_833,
            attributes: new Dictionary<string, string>
            {
                ["DynamicID"] = "2468",
                ["LegacyMonsterDropRoll"] = "40",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(3_832, updatedSession!.Character.MapId);
        Assert.Equal(154, updatedSession.Character.X);
        Assert.Equal(132, updatedSession.Character.Y);
        Assert.Equal(3_833, updatedSession.Character.PreviousMapId);
        Assert.Equal(830, updatedSession.Character.PreviousMapX);
        Assert.Equal(584, updatedSession.Character.PreviousMapY);
        Assert.Contains(
            ParseNpcReplies(killerSent),
            packet => packet.Text.Contains("beheaded~the~Flame~Devastator", StringComparison.Ordinal));
        var teleport = Assert.Single(ParseActions(killerSent), packet => packet.Type == MsgActionType.Teleport);
        Assert.Equal(1_780U, teleport.DwParam);
        Assert.Equal(154U, teleport.PositionX);
        Assert.Equal(132U, teleport.PositionY);
        Assert.Contains(
            ParseStringPackets(killerSent),
            packet => packet.Uid == KillerUid &&
                packet.Id == LegacyStringId.Effect &&
                packet.Strings.SequenceEqual(["role-select1"]));

        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 3833 && Family.ID == 7487\"", output);
        Assert.Contains("weaponConverted=False", output);
    }

    [Fact]
    public async Task HandlerEpicTrojanMap3833Family7487DeathConvertsFirstEligibleInventoryWeapon()
    {
        var finalFamily = CreateBossFamily(id: 7_487, name: "FlameDevastatorFinal", boss: false);
        var spawn = CreateBossSpawn(finalFamily, mapId: 3_833) with
        {
            Attributes = new Dictionary<string, string>
            {
                ["DynamicID"] = "2468",
            },
        };
        var worldData = CreateWorldDataCatalog(finalFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            finalFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var peerSent = new List<byte[]>();
        var peerConnection = CreateConnection(peerSent);
        sessions.Set(peerConnection, CreateSession(8_888, "Watcher", mapId: 3_832));
        var weapon = new PlayerItemSnapshotItem
        {
            Uid = 41_033,
            ItemId = 410_339,
            Durability = 50,
            MaximumDurability = 50,
            StackSize = 1,
        };
        var bladeSpiritItem = new PlayerItemSnapshotItem
        {
            Uid = 300_334,
            ItemId = 3_003_340,
            StackSize = 1,
        };
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_833,
            attributes: CreateEpicTrojanCompletionAttributes()) with
            {
                PlayerItems = new PlayerItemSnapshot(
                    [
                        new PlayerItemSnapshotItem { Uid = 105_000, ItemId = 1_050_000, StackSize = 1 },
                        new PlayerItemSnapshotItem { Uid = 500_339, ItemId = 500_339, StackSize = 1 },
                        weapon,
                        bladeSpiritItem,
                    ],
                    [],
                    [],
                    [],
                    [],
                    []),
            };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var converted = Assert.Single(updatedSession!.PlayerItems.InventoryItems, item => item.Uid == weapon.Uid);
        Assert.Equal(614_339U, converted.ItemId);
        Assert.DoesNotContain(updatedSession.PlayerItems.InventoryItems, item => item.Uid == bladeSpiritItem.Uid);
        Assert.Equal("0", updatedSession.Character.Attributes["StageEpicTrojanQuest"]);
        Assert.Equal("0", updatedSession.Character.Attributes["EpicTrojanMrMirrorPrograss"]);
        Assert.Equal("0", updatedSession.Character.Attributes["EpicTrojanEvilArrayPoints"]);
        Assert.Equal("0", updatedSession.Character.Attributes["EpicTrojanAbysalStage"]);
        Assert.Equal("0", updatedSession.Character.Attributes["EpicTrijanKillGhostReaver"]);
        Assert.Equal("0", updatedSession.Character.Attributes["ChangeEpicTrojan"]);
        Assert.Equal("0", updatedSession.Character.Attributes["ChangeArrayEpicTrojan"]);
        Assert.Equal("0", updatedSession.Character.Attributes["ChangeMr_MirrorEpicTrojan"]);
        Assert.Equal("0", updatedSession.Character.Attributes["ChangeGeneralPakEpicTrojan"]);
        Assert.Equal("1", updatedSession.Character.Attributes["CanChangeEpicMaterial"]);
        Assert.Equal("1", updatedSession.Character.Attributes["CanChangeArrayEpicMaterial"]);
        Assert.Equal("1", updatedSession.Character.Attributes["CanChangeMr_MirrorEpicMaterial"]);
        Assert.Equal("1", updatedSession.Character.Attributes["CanChangGeneralPakMaterial"]);

        var weaponUpdate = Assert.Single(
            ParseGameItems(killerSent),
            packet => packet.Uid == weapon.Uid && packet.Mode == LegacyGameItemPacket.UpdateItemMode);
        Assert.Equal(614_339U, weaponUpdate.ItemId);
        Assert.Contains(
            ParseItemUsage(killerSent),
            packet => packet.Id == bladeSpiritItem.Uid && packet.Action == MsgItemUsageAction.RemoveInventory);

        var successMessage =
            "LastHit~successfully~prevented~Twin~City~from~an~olden~massacre,~and~obtained~an~Epic~Weapon!";
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
                packet.Color == LegacyMsgMessagePacket.RedColor &&
                packet.Content.Contains(successMessage));
        Assert.Contains(
            ParseMessages(peerSent),
            packet => packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
                packet.Color == LegacyMsgMessagePacket.RedColor &&
                packet.Content.Contains(successMessage));

        var output = log.ToString();
        Assert.Contains("weaponConverted=True", output);
        Assert.Contains("convertedFrom=inventory", output);
        Assert.Contains("convertedWeaponItemId=614339", output);
        Assert.Contains("resetEpicTrojan=True", output);
        Assert.Contains("bladeSpiritItemConsumed=True", output);
    }

    [Fact]
    public async Task HandlerEpicTrojanMap3833Family7487DeathFallsBackToEquipmentWeapon()
    {
        var finalFamily = CreateBossFamily(id: 7_487, name: "FlameDevastatorFinal", boss: false);
        var spawn = CreateBossSpawn(finalFamily, mapId: 3_833) with
        {
            Attributes = new Dictionary<string, string>
            {
                ["DynamicID"] = "2468",
            },
        };
        var worldData = CreateWorldDataCatalog(finalFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            finalFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var weapon = new PlayerItemSnapshotItem
        {
            Uid = 42_033,
            ItemId = 420_333,
            Durability = 70,
            MaximumDurability = 70,
            Position = 4,
            StackSize = 1,
        };
        var bladeSpiritItem = new PlayerItemSnapshotItem
        {
            Uid = 300_335,
            ItemId = 3_003_340,
            StackSize = 2,
        };
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_833,
            attributes: CreateEpicTrojanCompletionAttributes()) with
            {
                PlayerItems = new PlayerItemSnapshot(
                    [
                        new PlayerItemSnapshotItem { Uid = 1_050_001, ItemId = 1_050_000, StackSize = 1 },
                        bladeSpiritItem,
                    ],
                    [],
                    [],
                    [weapon],
                    [],
                    []),
            };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var converted = Assert.Single(updatedSession!.PlayerItems.EquipmentItems, item => item.Uid == weapon.Uid);
        Assert.Equal(614_333U, converted.ItemId);
        var remainingBladeSpirit = Assert.Single(updatedSession.PlayerItems.InventoryItems, item => item.Uid == bladeSpiritItem.Uid);
        Assert.Equal(1, remainingBladeSpirit.StackSize);
        Assert.Equal("0", updatedSession.Character.Attributes["StageEpicTrojanQuest"]);

        var weaponUpdate = Assert.Single(
            ParseGameItems(killerSent),
            packet => packet.Uid == weapon.Uid && packet.Mode == LegacyGameItemPacket.UpdateItemMode);
        Assert.Equal(614_333U, weaponUpdate.ItemId);
        Assert.Contains(
            ParseGameItems(killerSent),
            packet => packet.Uid == bladeSpiritItem.Uid &&
                packet.ItemId == 3_003_340U &&
                packet.StackSize == 1 &&
                packet.Mode == LegacyGameItemPacket.UpdateItemMode);
        Assert.DoesNotContain(
            ParseItemUsage(killerSent),
            packet => packet.Id == bladeSpiritItem.Uid && packet.Action == MsgItemUsageAction.RemoveInventory);

        var output = log.ToString();
        Assert.Contains("weaponConverted=True", output);
        Assert.Contains("convertedFrom=equipment", output);
        Assert.Contains("convertedWeaponItemId=614333", output);
        Assert.Contains("bladeSpiritItemConsumed=True", output);
    }

    [Fact]
    public async Task HandlerEpicTrojanMap3831Family7484DeathFinishesQuest3272AndAcceptsQuest3273()
    {
        var miseryFamily = CreateBossFamily(id: 7_484, name: "EvilMonkMisery", boss: false);
        var spawn = CreateBossSpawn(miseryFamily, mapId: 3_831) with
        {
            Attributes = new Dictionary<string, string>
            {
                ["DynamicID"] = "1357",
            },
        };
        var worldData = CreateWorldDataCatalog(
            miseryFamily,
            spawn,
            questMissions:
            [
                CreateQuestMission(3_272, intentAmount: 0),
                CreateQuestMission(3_273, intentAmount: 0),
            ]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            miseryFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        var playerQuestsRoot = Path.Combine(rootDirectory, "PlayersQuests-EpicTrojan-3831");
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_831,
            attributes: new Dictionary<string, string>
            {
                ["DynamicID"] = "1357",
                ["LegacyMonsterDropRoll"] = "40",
                ["StageEpicTrojanQuest"] = "30",
            });
        var killerSession = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            3_272,
                            PlayerQuestStatus.Accepted,
                            0,
                            []),
                    ]),
            },
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            playerQuestsRoot: playerQuestsRoot);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("40", updatedSession!.Character.Attributes["StageEpicTrojanQuest"]);
        var finishedQuest = Assert.Single(updatedSession.Character.PlayerQuests.Quests, quest => quest.Uid == 3_272U);
        Assert.Equal(PlayerQuestStatus.Finished, finishedQuest.Status);
        var acceptedQuest = Assert.Single(updatedSession.Character.PlayerQuests.Quests, quest => quest.Uid == 3_273U);
        Assert.Equal(PlayerQuestStatus.Accepted, acceptedQuest.Status);
        var persistedQuests = new PlayerQuestSnapshotLoader(playerQuestsRoot)
            .LoadByCharacterUid(KillerUid)
            .Quests;
        Assert.Equal(
            PlayerQuestStatus.Finished,
            Assert.Single(persistedQuests, quest => quest.Uid == 3_272U).Status);
        Assert.Equal(
            PlayerQuestStatus.Accepted,
            Assert.Single(persistedQuests, quest => quest.Uid == 3_273U).Status);

        var questFrames = ParseQuestListFrames(killerSent);
        Assert.Contains(
            questFrames,
            packet => packet.Mode == LegacyQuestListMode.FinishQuest &&
                packet.Items.Single().QuestId == 3_272U &&
                packet.Items.Single().Status == LegacyQuestStatus.Finished);
        Assert.Contains(
            questFrames,
            packet => packet.Mode == LegacyQuestListMode.AcceptQuest &&
                packet.Items.Single().QuestId == 3_273U &&
                packet.Items.Single().Status == LegacyQuestStatus.Accepted);

        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 3831 && Family.ID == 7484\"", output);
        Assert.Contains("finishNpcId=10583", output);
        Assert.Contains("acceptNpcId=10600", output);
        Assert.Contains("stage=40", output);
        Assert.Contains("general-pak-autopatcher", output);
    }

    [Fact]
    public async Task HandlerEpicTrojanMap3834Family7483DeathIncrementsGhostReaverProgressAndSendsEffect()
    {
        var ghostFamily = CreateBossFamily(id: 7_483, name: "GhostReaver", boss: false);
        var spawn = CreateBossSpawn(ghostFamily, mapId: 3_834) with
        {
            Attributes = new Dictionary<string, string>
            {
                ["DynamicID"] = "2468",
            },
        };
        var worldData = CreateWorldDataCatalog(ghostFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            ghostFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var floorItemsRuntime = new FloorItemRuntimeStateStore();
        var usersRoot = Path.Combine(rootDirectory, "Users-EpicTrojan-3834");
        Directory.CreateDirectory(usersRoot);
        var sessions = new WorldSessionStore((connection, previous, current) =>
        {
            if (previous is not null &&
                CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(previous.Character, current.Character))
            {
                CharacterSnapshotScalarPersistence.TryPersist(current.UsersRoot, current.Character, out var path, out var reason);
            }
        });
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_834,
            attributes: new Dictionary<string, string>
            {
                ["DynamicID"] = "2468",
                ["LegacyMonsterDropRoll"] = "40",
                ["StageEpicTrojanQuest"] = "0",
                ["EpicTrojanMrMirrorPrograss"] = "5",
            }) with
        {
            UsersRoot = usersRoot,
        };
        WriteCharacterSnapshotForSession(usersRoot, killerSession);
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItemsRuntime);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("30", updatedSession!.Character.Attributes["StageEpicTrojanQuest"]);
        Assert.Equal("1", updatedSession.Character.Attributes["EpicTrijanKillGhostReaver"]);
        Assert.Equal("6", updatedSession.Character.Attributes["EpicTrojanMrMirrorPrograss"]);
        var persisted = CharacterSnapshotParser.ParseText(
            File.ReadAllText(Path.Combine(usersRoot, KillerUid.ToString(CultureInfo.InvariantCulture) + ".ini")));
        Assert.Equal("30", persisted.Attributes["StageEpicTrojanQuest"]);
        Assert.False(persisted.Attributes.ContainsKey("EpicTrijanKillGhostReaver"));
        Assert.Equal("6", persisted.Attributes["EpicTrojanMrMirrorPrograss"]);

        var floorItem = Assert.Single(ParseFloorItems(killerSent));
        Assert.Equal(1_182U, floorItem.ItemId);
        Assert.Equal(163, floorItem.X);
        Assert.Equal(228, floorItem.Y);
        Assert.Equal(LegacyFloorItemDropType.Effect, floorItem.DropType);
        Assert.Equal(2, floorItem.PlusOrColor);
        Assert.Equal(0UL, floorItem.Timer);
        Assert.True(floorItemsRuntime.TryGetVisibleEntry(floorItem.Uid, out var retainedEffect));
        Assert.Equal(monster.MapId, retainedEffect.MapId);
        Assert.Equal(1_182U, retainedEffect.Packet.ItemId);
        Assert.Equal(LegacyFloorItemDropType.Effect, retainedEffect.Packet.DropType);
        var beforeSourceLifetime = DateTimeOffset.UtcNow.AddSeconds(3);
        Assert.True(floorItemsRuntime.TryGetVisibleEntry(floorItem.Uid, beforeSourceLifetime, out _));
        var afterSourceLifetime = DateTimeOffset.UtcNow.AddSeconds(5);
        Assert.False(floorItemsRuntime.TryGetVisibleEntry(floorItem.Uid, afterSourceLifetime, out _));
        Assert.Empty(ParseQuestListFrames(killerSent));
        Assert.DoesNotContain(ParseNpcReplies(killerSent), packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox);
        Assert.DoesNotContain(ParseActions(killerSent), packet => packet.Type == MsgActionType.Teleport);

        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 3834 && Family.ID == 7483\"", output);
        Assert.Contains("progressBefore=5", output);
        Assert.Contains("progressAfter=6", output);
        Assert.Contains("completed=False", output);
        Assert.Contains("storedFloorEffect=True", output);
        Assert.Contains("sourceEffectLifetimeSeconds=4", output);
        Assert.DoesNotContain("epic-trojan-ghost-reaver-floor-effect-lifetime", output);
        Assert.DoesNotContain("resource=MsgItem.SpecialSecounds", output);
    }

    [Fact]
    public async Task HandlerEpicTrojanMap3834Family7483DeathAtEighteenFinishesQuest3271AndReturnsToGeneralPak()
    {
        var ghostFamily = CreateBossFamily(id: 7_483, name: "GhostReaver", boss: false);
        var spawn = CreateBossSpawn(ghostFamily, mapId: 3_834) with
        {
            Attributes = new Dictionary<string, string>
            {
                ["DynamicID"] = "2468",
            },
        };
        var worldData = CreateWorldDataCatalog(
            ghostFamily,
            spawn,
            questMissions:
            [
                CreateQuestMission(3_271, intentAmount: 0),
                CreateQuestMission(3_277, intentAmount: 0),
            ]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            ghostFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        var playerQuestsRoot = Path.Combine(rootDirectory, "PlayersQuests-EpicTrojan-3834");
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_834,
            attributes: new Dictionary<string, string>
            {
                ["DynamicID"] = "2468",
                ["LegacyMonsterDropRoll"] = "40",
                ["StageEpicTrojanQuest"] = "0",
                ["EpicTrojanMrMirrorPrograss"] = "17",
            });
        var killerSession = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            3_271,
                            PlayerQuestStatus.Accepted,
                            0,
                            []),
                    ]),
            },
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            playerQuestsRoot: playerQuestsRoot);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(3_830, updatedSession!.Character.MapId);
        Assert.Equal(162, updatedSession.Character.X);
        Assert.Equal(218, updatedSession.Character.Y);
        Assert.Equal(3_834, updatedSession.Character.PreviousMapId);
        Assert.Equal("30", updatedSession.Character.Attributes["StageEpicTrojanQuest"]);
        Assert.Equal("1", updatedSession.Character.Attributes["EpicTrijanKillGhostReaver"]);
        Assert.Equal("18", updatedSession.Character.Attributes["EpicTrojanMrMirrorPrograss"]);
        Assert.Equal(PlayerQuestStatus.Finished, Assert.Single(updatedSession.Character.PlayerQuests.Quests, quest => quest.Uid == 3_271U).Status);
        Assert.Equal(PlayerQuestStatus.Accepted, Assert.Single(updatedSession.Character.PlayerQuests.Quests, quest => quest.Uid == 3_277U).Status);
        var persistedQuests = new PlayerQuestSnapshotLoader(playerQuestsRoot)
            .LoadByCharacterUid(KillerUid)
            .Quests;
        Assert.Equal(
            PlayerQuestStatus.Finished,
            Assert.Single(persistedQuests, quest => quest.Uid == 3_271U).Status);
        Assert.Equal(
            PlayerQuestStatus.Accepted,
            Assert.Single(persistedQuests, quest => quest.Uid == 3_277U).Status);

        var floorItem = Assert.Single(ParseFloorItems(killerSent));
        Assert.Equal(1_182U, floorItem.ItemId);
        Assert.Equal(LegacyFloorItemDropType.Effect, floorItem.DropType);
        Assert.Contains(
            ParseQuestListFrames(killerSent),
            packet => packet.Mode == LegacyQuestListMode.FinishQuest &&
                packet.Items.Single().QuestId == 3_271U &&
                packet.Items.Single().Status == LegacyQuestStatus.Finished);
        Assert.Contains(
            ParseQuestListFrames(killerSent),
            packet => packet.Mode == LegacyQuestListMode.AcceptQuest &&
                packet.Items.Single().QuestId == 3_277U &&
                packet.Items.Single().Status == LegacyQuestStatus.Accepted);
        Assert.Contains(
            ParseNpcReplies(killerSent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text.Contains("All~the~18~Ghost~Reavers", StringComparison.Ordinal));
        var teleport = Assert.Single(ParseActions(killerSent), packet => packet.Type == MsgActionType.Teleport);
        Assert.Equal(1_780U, teleport.DwParam);
        Assert.Equal(162U, teleport.PositionX);
        Assert.Equal(218U, teleport.PositionY);

        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 3834 && Family.ID == 7483\"", output);
        Assert.Contains("progressBefore=17", output);
        Assert.Contains("progressAfter=18", output);
        Assert.Contains("completed=True", output);
        Assert.Contains("routeQuestId=10581", output);
        Assert.Contains("general-pak-return-autopatcher", output);
    }

    [Fact]
    public async Task HandlerSaturnMap2353DeathWhileAliveGrantsArrestToken()
    {
        var saturnFamily = CreateBossFamily(id: 23_530, name: "SaturnMonster", boss: false);
        var spawn = CreateBossSpawn(saturnFamily, mapId: 2_353);
        var worldData = CreateWorldDataCatalog(
            saturnFamily,
            spawn,
            items: [CreateItemType(711_624)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            saturnFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 2_353,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacySaturnAlive"] = "1",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var token = Assert.Single(updatedSession!.PlayerItems.InventoryItems, item => item.ItemId == 711_624U);
        Assert.Equal(1, token.StackSize);
        Assert.Contains(
            ParseGameItems(killerSent),
            packet => packet.ItemId == 711_624U && packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(
            ParseNpcReplies(killerSent),
            packet => packet.Text.Contains("You have got 1 Arrest Token!", StringComparison.Ordinal));
        Assert.DoesNotContain(
            ParseMessages(killerSent),
            packet => packet.Content.Any(content => content.Contains("Conquer Points", StringComparison.Ordinal)));

        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 2353 && MsgSchedules.Saturn.Mode == Alive\"", output);
    }

    [Fact]
    public async Task HandlerSaturnMap2353DeathWhileAliveWithFullInventorySendsSourceMessage()
    {
        var saturnFamily = CreateBossFamily(id: 23_530, name: "SaturnMonster", boss: false);
        var spawn = CreateBossSpawn(saturnFamily, mapId: 2_353);
        var worldData = CreateWorldDataCatalog(
            saturnFamily,
            spawn,
            items: [CreateItemType(711_624)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            saturnFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var fillerItems = Enumerable.Range(1, 40)
            .Select(index => new PlayerItemSnapshotItem
            {
                Uid = checked((uint)index),
                ItemId = checked((uint)(100_000 + index)),
                StackSize = 1,
            })
            .ToArray();
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 2_353,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacySaturnAlive"] = "1",
            });
        var killerSession = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(fillerItems, [], [], [], []),
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(40, updatedSession!.PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_624U);
        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 711_624U);
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Any(content => content.Contains("Your inventory is full.", StringComparison.Ordinal)));

        var output = log.ToString();
        Assert.Contains("skipped legacy Saturn monster death Arrest Token", output);
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 2353 && MsgSchedules.Saturn.Mode == Alive\"", output);
    }

    [Fact]
    public async Task HandlerSaturnMap2353DeathWhenInactiveTeleportsToTwinCity()
    {
        var saturnFamily = CreateBossFamily(id: 23_530, name: "SaturnMonster", boss: false);
        var spawn = CreateBossSpawn(saturnFamily, mapId: 2_353);
        var worldData = CreateWorldDataCatalog(
            saturnFamily,
            spawn,
            items: [CreateItemType(711_624)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            saturnFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 2_353,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacySaturnAlive"] = "0",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 2353 && MsgSchedules.Saturn.Mode != Alive\"", output);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(1_002, updatedSession!.Character.MapId);
        Assert.Equal(410, updatedSession.Character.X);
        Assert.Equal(354, updatedSession.Character.Y);
        Assert.DoesNotContain(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_624U);
        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 711_624U);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerDeityLandMap10250DeathAutoAcceptsQuest3944AndIncrementsObjective(bool startsAvailable)
    {
        var deityFamily = CreateBossFamily(id: 10_251, name: "DeityLandDevil", boss: false);
        var spawn = CreateBossSpawn(deityFamily, mapId: 10_250);
        var worldData = CreateWorldDataCatalog(
            deityFamily,
            spawn,
            questMissions: [CreateQuestMission(3_944, intentAmount: 1)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            deityFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_250,
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyMonsterDeathConquerPointRoll", "1"),
                ("LegacyMonsterDropRoll", "40"),
                ("LegacyDeityLandRandomDropRate", "0")));
        var killerSession = startsAvailable
            ? baseSession with
            {
                Character = baseSession.Character with
                {
                    PlayerQuests = new PlayerQuestSnapshot(
                        [
                            new PlayerQuestSnapshotEntry(
                                3_944,
                                PlayerQuestStatus.Available,
                                77,
                                [55U]),
                        ]),
                },
            }
            : baseSession;
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var quest = Assert.Single(updatedSession!.Character.PlayerQuests.Quests, entry => entry.Uid == 3_944U);
        Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
        Assert.Equal(0U, quest.Time);
        Assert.Equal([1U], quest.Intentions);

        var acceptFrame = Assert.Single(
            ParseQuestListFrames(killerSent),
            packet => packet.Mode == LegacyQuestListMode.AcceptQuest &&
                packet.Items.Any(item => item.QuestId == 3_944U));
        Assert.Equal(new LegacyQuestListItem(3_944, LegacyQuestStatus.Accepted, 0), Assert.Single(acceptFrame.Items));

        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(killerSent), packet => packet.Uid == 3_944U);
        Assert.Equal([1U], objectiveFrame.Intentions);

        var output = log.ToString();
        Assert.Contains("accepted legacy DeityLand monster death quest", output);
        Assert.Contains("branch=\"MonsterRole.cs killer.Player.Map == 10250 quest 3944\"", output);
    }

    [Fact]
    public async Task HandlerDeityLandMap10250DeathAcceptedQuestAt99SendsCompletionPrompt()
    {
        var deityFamily = CreateBossFamily(id: 10_251, name: "DeityLandDevil", boss: false);
        var spawn = CreateBossSpawn(deityFamily, mapId: 10_250);
        var worldData = CreateWorldDataCatalog(deityFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            deityFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_250,
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyMonsterDeathConquerPointRoll", "1"),
                ("LegacyMonsterDropRoll", "40"),
                ("LegacyDeityLandRandomDropRate", "0")));
        var killerSession = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            3_944,
                            PlayerQuestStatus.Accepted,
                            12,
                            [99U]),
                    ]),
            },
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var quest = Assert.Single(updatedSession!.Character.PlayerQuests.Quests, entry => entry.Uid == 3_944U);
        Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
        Assert.Equal(12U, quest.Time);
        Assert.Equal([100U], quest.Intentions);

        Assert.DoesNotContain(
            ParseQuestListFrames(killerSent),
            packet => packet.Mode == LegacyQuestListMode.AcceptQuest &&
                packet.Items.Any(item => item.QuestId == 3_944U));
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(killerSent), packet => packet.Uid == 3_944U);
        Assert.Equal([100U], objectiveFrame.Intentions);
        Assert.Contains(
            ParseMessages(killerSent),
            packet => packet.Content.Any(
                content => content.Contains(
                    "You have killed 100 devils in the DeityLand! Go claim your rewards from the Victoy Chest!",
                    StringComparison.Ordinal)));

        var output = log.ToString();
        Assert.Contains("sent legacy DeityLand monster death quest completion prompt", output);
    }

    [Fact]
    public async Task HandlerDeityLandMap10250DeathFinishedQuestDoesNotIncrement()
    {
        var deityFamily = CreateBossFamily(id: 10_251, name: "DeityLandDevil", boss: false);
        var spawn = CreateBossSpawn(deityFamily, mapId: 10_250);
        var worldData = CreateWorldDataCatalog(deityFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            deityFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_250,
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyMonsterDeathConquerPointRoll", "1"),
                ("LegacyMonsterDropRoll", "40"),
                ("LegacyDeityLandRandomDropRate", "0")));
        var killerSession = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            3_944,
                            PlayerQuestStatus.Finished,
                            12,
                            [99U]),
                    ]),
            },
        };
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var quest = Assert.Single(updatedSession!.Character.PlayerQuests.Quests, entry => entry.Uid == 3_944U);
        Assert.Equal(PlayerQuestStatus.Finished, quest.Status);
        Assert.Equal([99U], quest.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(killerSent), packet => packet.Uid == 3_944U);
        Assert.DoesNotContain(
            ParseMessages(killerSent),
            packet => packet.Content.Any(
                content => content.Contains("You have killed 100 devils in the DeityLand!", StringComparison.Ordinal)));

        var output = log.ToString();
        Assert.Contains("skipped legacy DeityLand monster death quest increment", output);
    }

    [Fact]
    public async Task HandlerDragonIslandFamily4220DeathSendsSourceFixedDropBundle()
    {
        var bossFamily = CreateBossFamily(
            id: checked((int)LegacyBossSpecialDeathDropPolicy.DragonIslandFamilyId),
            name: "DragonIslandBoss");
        var spawn = CreateBossSpawn(
            bossFamily,
            mapId: checked((int)LegacyBossSpecialDeathDropPolicy.DragonIslandMapId));
        var worldData = CreateWorldDataCatalog(bossFamily, spawn);
        var boss = LegacyMonsterInstance.FromGenerator(
            spawn,
            bossFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: checked((int)LegacyBossSpecialDeathDropPolicy.DragonIslandMapId),
            attributes: new Dictionary<string, string>
            {
                ["LegacyBoss4220RareDrop"] = "0",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            boss,
            log);

        var floorItems = ParseFloorItems(killerSent);

        Assert.Equal(48, floorItems.Count);
        AssertDropCount(floorItems, 3_330_609, 1);
        AssertDropCount(floorItems, 3_333_402, 1);
        AssertDropCount(floorItems, 3_330_770, 10);
        AssertDropCount(floorItems, 3_316_793, 10);
        AssertDropCount(floorItems, 3_328_253, 3);
        AssertDropCount(floorItems, 3_328_254, 3);
        AssertDropCount(floorItems, 3_306_368, 8);
        AssertDropCount(floorItems, 3_009_104, 2);
        AssertDropCount(floorItems, 3_009_002, 2);
        AssertDropCount(floorItems, 3_005_360, 2);
        AssertDropCount(floorItems, 3_004_181, 2);
        AssertDropCount(floorItems, 3_335_128, 2);
        AssertDropCount(floorItems, 3_310_995, 2);
        Assert.DoesNotContain(floorItems, item => item.ItemId == 420_009U);

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy boss special death drops", output);
        Assert.DoesNotContain("boss-family monster death drops are source-backed but not modeled", output);
    }

    [Fact]
    public async Task HandlerQueenOfEvilDeathSendsSourceFixedDropBundleWithRandomTalismanPool()
    {
        var bossFamily = CreateBossFamily(
            id: checked((int)LegacyBossSpecialDeathDropPolicy.QueenOfEvilFamilyId),
            name: "QueenofEvil");
        var spawn = CreateBossSpawn(bossFamily);
        var worldData = CreateWorldDataCatalog(bossFamily, spawn, CreateQueenOfEvilItemTypes());
        var boss = LegacyMonsterInstance.FromGenerator(
            spawn,
            bossFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            attributes: new Dictionary<string, string>
            {
                ["LegacyBoss3970DragonBallCount"] = "3",
                ["LegacyBoss3970TalismanIndexes"] = "4,3,2,1,0",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            boss,
            log);

        var floorItems = ParseFloorItems(killerSent);

        Assert.Equal(59, floorItems.Count);
        AssertDropCount(floorItems, 3_328_253, 3);
        AssertDropCount(floorItems, 3_328_254, 3);
        AssertDropCount(floorItems, 3_330_770, 20);
        AssertDropCount(floorItems, 3_316_793, 20);
        AssertDropCount(floorItems, 1_088_000, 3);
        AssertDropCount(floorItems, 3_335_128, 5);
        AssertDropCount(floorItems, 4_030_401, 1);
        AssertDropCount(floorItems, 4_030_301, 1);
        AssertDropCount(floorItems, 4_030_201, 1);
        AssertDropCount(floorItems, 4_030_101, 1);
        AssertDropCount(floorItems, 4_030_001, 1);
        Assert.DoesNotContain(floorItems, item => item.ItemId == 4_030_002U);

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy boss special death drops", output);
        Assert.Contains("GenerateBossFamily()", output);
        Assert.DoesNotContain("boss-family monster death drops are source-backed but not modeled", output);
    }

    [Fact]
    public async Task HandlerQueenOfEvilWarriorDeathGrantsSourceClassStack()
    {
        var bossFamily = CreateBossFamily(
            id: checked((int)LegacyBossSpecialDeathDropPolicy.QueenOfEvilFamilyId),
            name: "QueenofEvil");
        var spawn = CreateBossSpawn(bossFamily);
        var worldData = CreateWorldDataCatalog(bossFamily, spawn, CreateQueenOfEvilItemTypes());
        var boss = LegacyMonsterInstance.FromGenerator(
            spawn,
            bossFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            classCode: 2_001,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBoss3970DragonBallCount"] = "1",
                ["LegacyBoss3970TalismanIndexes"] = "0,1,2,3,4",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            boss,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var reward = Assert.Single(updatedSession!.PlayerItems.InventoryItems, item => item.ItemId == 3_333_403U);
        Assert.Equal(5, reward.StackSize);

        var itemPacket = Assert.Single(ParseGameItems(killerSent), packet => packet.ItemId == 3_333_403U);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, itemPacket.Mode);
        Assert.Equal(5, itemPacket.StackSize);
        Assert.Contains("WorldServer applied legacy QueenofEvil class reward", log.ToString());
    }

    [Fact]
    public async Task HandlerQueenOfEvilNinjaDeathGrantsSourceNinjaPoints()
    {
        var bossFamily = CreateBossFamily(
            id: checked((int)LegacyBossSpecialDeathDropPolicy.QueenOfEvilFamilyId),
            name: "QueenofEvil");
        var spawn = CreateBossSpawn(bossFamily);
        var worldData = CreateWorldDataCatalog(bossFamily, spawn, CreateQueenOfEvilItemTypes());
        var boss = LegacyMonsterInstance.FromGenerator(
            spawn,
            bossFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            classCode: 5_001,
            attributes: new Dictionary<string, string>
            {
                ["NinjaPoints"] = "7",
                ["LegacyBoss3970DragonBallCount"] = "1",
                ["LegacyBoss3970TalismanIndexes"] = "0,1,2,3,4",
            });
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            boss,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("57", updatedSession!.Character.Attributes["NinjaPoints"]);
        Assert.DoesNotContain(
            ParseGameItems(killerSent),
            packet => packet.ItemId is 3_333_403U or 3_335_128U or 3_321_098U or 3_321_107U);
        Assert.Contains("ninjaPoints=50", log.ToString());
    }

    [Fact]
    public async Task HandlerFamily3319MonsterDeathGuildBranchAppliesSourceDonationSetter()
    {
        var result = await KillFamily3319Async(
            "guild",
            new Dictionary<string, string>
            {
                ["GuildDontion"] = "4",
                ["MyDontion"] = "4",
            });

        Assert.Equal("5", result.UpdatedSession.Character.Attributes["GuildDontion"]);
        Assert.Equal("5", result.UpdatedSession.Character.Attributes["GuildDonation"]);
        Assert.Equal("5", result.UpdatedSession.Character.Attributes["MyDontion"]);
        var updates = ParseMsgUpdates(result.Sent);
        Assert.Contains(
            updates,
            packet => packet.Updates.Any(update => update.Type == LegacyMsgUpdateDataType.MyDontion && update.Value == 5));
        Assert.Contains(
            updates,
            packet => packet.Updates.Any(update => update.Type == LegacyMsgUpdateDataType.DonationPoints && update.Value == 5));
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content[3] == "LastHit He Get 1 Points in His Guild !");
        Assert.Contains("reward=guild-donation", result.Log);
    }

    [Fact]
    public async Task HandlerFamily3319MonsterDeathChiBranchAppliesSourceChiPoints()
    {
        var result = await KillFamily3319Async(
            "chi",
            new Dictionary<string, string>
            {
                ["ChiPoints"] = "10",
            });

        Assert.Equal("1010", result.UpdatedSession.Character.Attributes["ChiPoints"]);
        Assert.Contains(ParseChiInfoPackets(result.Sent), packet => packet.ChiPoints == 1_010);
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content[3] == "LastHit He Get 100 Chi Points !");
        Assert.Contains("reward=chi", result.Log);
    }

    [Fact]
    public async Task HandlerFamily3319MonsterDeathNinjaBranchAppliesSourceArchivePoints()
    {
        var result = await KillFamily3319Async(
            "ninja",
            new Dictionary<string, string>
            {
                ["NinjaPoints"] = "2",
            },
            classCode: 5_001);

        Assert.Equal("12", result.UpdatedSession.Character.Attributes["NinjaPoints"]);
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content[3] == "LastHit He Get 10 Points in His Ninja Archive !");
        Assert.Contains("MsgNinjaUser", result.Log);
        Assert.Contains("reward=ninja", result.Log);
    }

    [Fact]
    public async Task HandlerFamily3319MonsterDeathStudyBranchAppliesSourceSubclassAward()
    {
        var result = await KillFamily3319Async(
            "study",
            new Dictionary<string, string>
            {
                ["SubProfInfo"] = "7#0#0#",
            });

        Assert.True(LegacySubClassHitpointsCalculator.TryParseLegacyState(
            result.UpdatedSession.Character.Attributes["SubProfInfo"],
            out var state));
        Assert.Equal(1_007, state.StudyPoints);
        var messages = ParseMessages(result.Sent);
        Assert.Contains(messages, packet => packet.Content[3] == "You received 1000 StudyPoints.");
        Assert.Contains(messages, packet => packet.Content[3] == "LastHit He Get 1000 Points in His SupClass !");
        Assert.Contains(ParseStringPackets(result.Sent), packet => packet.Strings.Contains("zf2-e300"));
        Assert.Contains("reward=study-points", result.Log);
    }

    [Theory]
    [InlineData("inner1000", 1_200UL, "LastHit He Get 1000 Points in His innerPower !", "reward=inner-power-1000")]
    [InlineData("inner500", 700UL, "LastHit He Get 500 Points in His innerPower !", "reward=inner-power-500")]
    public async Task HandlerFamily3319MonsterDeathInnerPowerBranchesApplySourcePotency(
        string branch,
        ulong expectedPotency,
        string expectedMessage,
        string expectedLogToken)
    {
        var innerPowerRow = CreateInnerPowerRow((int)KillerUid, "LastHit", 200);
        var result = await KillFamily3319Async(
            branch,
            innerPowerRanks: [innerPowerRow]);

        Assert.Contains(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(
                update => update.Type == LegacyMsgUpdateDataType.InnerPowerPotency &&
                    update.Value == expectedPotency));
        Assert.Equal(
            expectedPotency.ToString(CultureInfo.InvariantCulture),
            result.WorldData.InnerPowerRanksByCharacterId[(int)KillerUid][0].Fields[2]);
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content[3] == expectedMessage);
        Assert.Contains(expectedLogToken, result.Log);
    }

    [Fact]
    public async Task HandlerFamily8418Map10342MastreyFruitBranchDropsSourceItemAndMessage()
    {
        var result = await KillFamily8418Async("mastrey-fruit");

        Assert.Contains(ParseFloorItems(result.Sent), packet => packet.ItemId == 3_001_044U);
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content[3] == "LastHit He Get 1 Mastrey Froute !");
        Assert.Contains("DropItemID(3001044)", result.Log);
    }

    [Fact]
    public async Task HandlerFamily8418Map10342ClassExperienceBranchSendsSourceSetterUpdate()
    {
        var result = await KillFamily8418Async(
            "class-exp",
            attributes: new Dictionary<string, string>
            {
                ["ClassExperience"] = "7",
            });

        Assert.Equal("57", result.UpdatedSession.Character.Attributes["ClassExperience"]);
        Assert.Contains(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(
                update => update.Type == LegacyMsgUpdateDataType.ClassExperience &&
                    update.Value == 57));
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content[3] == "LastHit He Get 50 ClassExperience   in Map Huant");
        Assert.Contains("ClassExperience += 50", result.Log);
    }

    [Fact]
    public async Task HandlerFamily8418Map10342InventoryBranchStacksSourceRewardItem()
    {
        var result = await KillFamily8418Async(
            "inventory3335127",
            itemTypes: [CreateItemType(3_335_127)]);

        var rewardItem = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(3_335_127U, rewardItem.ItemId);
        Assert.Equal(1, rewardItem.StackSize);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 3_335_127U);
        Assert.Contains("AddItemWitchStack(3335127)", result.Log);
    }

    [Fact]
    public async Task HandlerFamily8418Map10342FallbackBranchDropsSourceRefineryItem()
    {
        var result = await KillFamily8418Async("fallback3004280");

        Assert.Contains(ParseFloorItems(result.Sent), packet => packet.ItemId == 3_004_280U);
        Assert.Contains("DropItemID(3004280)", result.Log);
    }

    [Theory]
    [InlineData(2_435, 720_679U, "You killed a Heaven Demon and found a Frost CP Pack (69000CPs)!", "LastHit found a Frost CP Pack (69000CPs)!")]
    [InlineData(2_436, 720_685U, "You killed a Chaos Demon and found a Nimbus CP Pack (138000CPs)!", "LastHit found a Nimbus CP Pack (138000CPs)!")]
    [InlineData(2_437, 720_691U, "You killed a Sacred Demon and found a Kylin CP Pack (276000CPs)", "LastHit found a Kylin CP Pack (276000CPs)!")]
    [InlineData(2_438, 720_697U, "You killed an Aurora Demon and found a Pilgrim CP Pack (690000CPs)!", "LastHit got a Pilgrim CP Pack (690000CPs)!")]
    [InlineData(2_420, 720_654U, "You killed a Demon and found a Joy CP Pack (1380CPs)!", "LastHit found a Joy CP Pack (1380CPs)!")]
    [InlineData(2_421, 720_662U, "You killed a Ancient Demon and found a Mystic CP Pack (6900CPs)!", "LastHit found a Mystic CP Pack (6900CPs)!")]
    [InlineData(2_422, 720_667U, "You killed a Flood Demon and found a Fantasy CP Pack (13800CPs)!", "LastHit found a Fantasy CP Pack (13800CPs)!")]
    public async Task HandlerHouseDemonOwnerInstanceDropsFirstSourceRewardAndMessages(
        int familyId,
        uint itemId,
        string messageBox,
        string globalMessage)
    {
        var result = await KillHouseDemonAsync(familyId, "first");

        Assert.Contains(ParseFloorItems(result.Sent), packet => packet.ItemId == itemId);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == messageBox);
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content[3] == globalMessage);
        Assert.Contains(FormattableString.Invariant($"Family.ID == {familyId}"), result.Log);
        Assert.Contains(FormattableString.Invariant($"DropItemID({itemId})"), result.Log);
    }

    [Theory]
    [InlineData(2_435, 720_680U, "You killed a Heaven Demon and found a Heaven Pill equal to the EXP of 2 and a half EXP Balls!")]
    [InlineData(2_436, 720_686U, "You killed a Chaos Demon and found a Mystery Pill equal to the EXP of 2 and 1/3 EXP Balls!")]
    [InlineData(2_437, 720_692U, "You killed a Sacred Demon and found a Wind Pill equal to the EXP of 5 EXP Balls!")]
    [InlineData(2_438, 720_698U, "You killed an Aurora Demon and got a Wind Pill equal to the EXP of 8 and 1/3 EXP Balls!")]
    [InlineData(2_420, 720_668U, "You killed a Demon and found a Magic Ball equal to the EXP of 1/6 of an EXP Ball!")]
    [InlineData(2_421, 720_669U, "You killed the Ancient Demon and found a Super Ball equal to the EXP of 5/6 of an EXP Ball!")]
    [InlineData(2_422, 720_670U, "You killed the Flood Demon and found an Ultra Ball equal to EXP worth 1 and 2/3 EXP Balls!")]
    public async Task HandlerHouseDemonOwnerInstanceDropsFallbackSourceReward(
        int familyId,
        uint itemId,
        string messageBox)
    {
        var result = await KillHouseDemonAsync(familyId, "fallback");

        Assert.Contains(ParseFloorItems(result.Sent), packet => packet.ItemId == itemId);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == messageBox);
        Assert.DoesNotContain(ParseMessages(result.Sent), packet => packet.Content[3].Contains("CP Pack", StringComparison.Ordinal));
        Assert.Contains(FormattableString.Invariant($"DropItemID({itemId})"), result.Log);
    }

    [Fact]
    public async Task HandlerHouseDemonDifferentDynamicInstanceSkipsSourceReward()
    {
        var result = await KillHouseDemonAsync(2_435, "first", dynamicId: 2_468);

        Assert.DoesNotContain(ParseFloorItems(result.Sent), packet => packet.ItemId == 720_679U);
        Assert.DoesNotContain(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text.Contains("Heaven Demon", StringComparison.Ordinal));
        Assert.DoesNotContain("legacy house guest monster death reward", result.Log);
    }

    [Theory]
    [InlineData(69, false, 100U, 1UL)]
    [InlineData(70, false, 100U, 2UL)]
    [InlineData(100, false, 100U, 3UL)]
    [InlineData(120, false, 100U, 4UL)]
    [InlineData(140, false, 999_999U, 0UL)]
    [InlineData(140, true, 1_000_000U, 1_000UL)]
    public async Task HandlerQuest2375MonsterDeathCreditsSourceSpiritBeadTier(
        byte level,
        bool boss,
        uint maxLife,
        ulong expectedAmount)
    {
        var result = await KillSpiritBeadQuest2375MonsterAsync(level, boss, maxLife);

        var expectedCount = checked(7UL + expectedAmount);
        Assert.Equal(
            expectedCount.ToString(CultureInfo.InvariantCulture),
            result.UpdatedSession.Character.Attributes["DailySpiritBeadCount"]);
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content[3] == $"You received {expectedAmount.ToString(CultureInfo.InvariantCulture)} spirites.");
        Assert.Contains(FormattableString.Invariant($"amount={expectedAmount}"), result.Log);
        Assert.Contains("QuestGUI.CheckQuest(2375, Accepted)", result.Log);
    }

    [Fact]
    public async Task HandlerQuest2375MonsterDeathSkipsWhenQuestNotAccepted()
    {
        var result = await KillSpiritBeadQuest2375MonsterAsync(
            level: 80,
            boss: false,
            maxLife: 100,
            accepted: false);

        Assert.Equal("7", result.UpdatedSession.Character.Attributes["DailySpiritBeadCount"]);
        Assert.DoesNotContain(
            ParseMessages(result.Sent),
            packet => packet.Content[3].Contains("spirites", StringComparison.Ordinal));
        Assert.DoesNotContain("quest 2375 spirit bead", result.Log);
    }

    [Fact]
    public async Task HandlerFamily4212Quest6126CreditsAcceptedKiller()
    {
        var result = await KillFamily4212Quest6126MonsterAsync(killerAccepted: true);

        var quest = Assert.Single(
            result.UpdatedKillerSession.Character.PlayerQuests.Quests,
            entry => entry.Uid == 6_126U);
        Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
        Assert.Equal([1U], quest.Intentions);

        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.KillerSent), packet => packet.Uid == 6_126U);
        Assert.Equal([1U], objectiveFrame.Intentions);
        Assert.Contains("Family.ID == 4212 quest 6126", result.Log);
        Assert.Contains("killerCredited=True", result.Log);
    }

    [Fact]
    public async Task HandlerFamily4212Quest6126CreditsSameDynamicTeamMember()
    {
        var result = await KillFamily4212Quest6126MonsterAsync(
            killerAccepted: false,
            includeTeamMember: true,
            teammateAccepted: true);

        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.KillerSent), packet => packet.Uid == 6_126U);
        Assert.NotNull(result.UpdatedTeammateSession);
        var teammateQuest = Assert.Single(
            result.UpdatedTeammateSession!.Character.PlayerQuests.Quests,
            entry => entry.Uid == 6_126U);
        Assert.Equal(PlayerQuestStatus.Accepted, teammateQuest.Status);
        Assert.Equal([1U], teammateQuest.Intentions);

        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.TeammateSent!), packet => packet.Uid == 6_126U);
        Assert.Equal([1U], objectiveFrame.Intentions);
        Assert.Contains("teammateCredits=1", result.Log);
    }

    [Fact]
    public async Task HandlerFamily4212Quest6126SkipsDifferentDynamicTeamMember()
    {
        var result = await KillFamily4212Quest6126MonsterAsync(
            killerAccepted: false,
            includeTeamMember: true,
            teammateAccepted: true,
            teammateDynamicId: 2_468);

        Assert.NotNull(result.UpdatedTeammateSession);
        var teammateQuest = Assert.Single(
            result.UpdatedTeammateSession!.Character.PlayerQuests.Quests,
            entry => entry.Uid == 6_126U);
        Assert.Equal(PlayerQuestStatus.Accepted, teammateQuest.Status);
        Assert.Equal([0U], teammateQuest.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.KillerSent), packet => packet.Uid == 6_126U);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.TeammateSent!), packet => packet.Uid == 6_126U);
        Assert.DoesNotContain("legacy family 4212 quest 6126 monster death credit", result.Log);
    }

    [Fact]
    public async Task HandlerMap3071Family2700DropsFixedBundleAndForcedRareReward()
    {
        var result = await KillMap3071Family2700MonsterAsync("710212");

        var floorItems = ParseFloorItems(result.Sent);
        AssertDropCount(floorItems, 1_088_001U, 3);
        AssertDropCount(floorItems, 723_341U, 3);
        AssertDropCount(floorItems, 710_212U, 1);
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Contains("killer.Player.Map == 3071 && Family.ID == 2700", result.Log);
        Assert.Contains("forced DropItemID(710212)", result.Log);
        Assert.Contains("scatterRange=6", result.Log);
    }

    [Fact]
    public async Task HandlerMap3071Family2700FallbackGrantsGoldCoin()
    {
        var result = await KillMap3071Family2700MonsterAsync("goldCoin");

        var floorItems = ParseFloorItems(result.Sent);
        AssertDropCount(floorItems, 1_088_001U, 3);
        AssertDropCount(floorItems, 723_341U, 3);
        var goldCoin = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_609U);
        Assert.Equal(1, goldCoin.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 711_609U && packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(
            ParseStringPackets(result.Sent),
            packet => packet.Id == LegacyStringId.Effect &&
                packet.Strings.Contains("dispel4", StringComparer.Ordinal));
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "You~received~a~Gold~Coin!");
        Assert.Contains("forced Inventory.Add(711609)", result.Log);
    }

    [Fact]
    public async Task HandlerMap3071Family2700FallbackWithFullInventorySendsSourceDialog()
    {
        var result = await KillMap3071Family2700MonsterAsync("goldCoin", fullInventory: true);

        var floorItems = ParseFloorItems(result.Sent);
        AssertDropCount(floorItems, 1_088_001U, 3);
        AssertDropCount(floorItems, 723_341U, 3);
        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_609U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_609U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Your~inventory~is~full!");
        Assert.Contains("skipped legacy map 3071 coin reward", result.Log);
    }

    [Fact]
    public async Task HandlerMap3071Family7022ForcedGoldGrantsGoldCoin()
    {
        var result = await KillMap3071Family7022MonsterAsync("gold");

        Assert.Empty(ParseFloorItems(result.Sent));
        var goldCoin = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_609U);
        Assert.Equal(1, goldCoin.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 711_609U && packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(
            ParseStringPackets(result.Sent),
            packet => packet.Id == LegacyStringId.Effect &&
                packet.Strings.Contains("dispel4", StringComparer.Ordinal));
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "You~received~a~Gold~Coin!");
        Assert.Contains("Family.ID == 7022", result.Log);
        Assert.Contains("forced Core.Rate(1, 200) Inventory.Add(711609)", result.Log);
    }

    [Fact]
    public async Task HandlerMap3071Family7022ForcedSilverGrantsSilverCoinTalkMessage()
    {
        var result = await KillMap3071Family7022MonsterAsync("silver");

        var silverCoin = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_610U);
        Assert.Equal(1, silverCoin.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 711_610U && packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(
            ParseStringPackets(result.Sent),
            packet => packet.Id == LegacyStringId.Effect &&
                packet.Strings.Contains("dispel7", StringComparer.Ordinal));
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content[3] == "You~received~a~Silver~Coin!");
        Assert.DoesNotContain(
            ParseNpcReplies(result.Sent),
            packet => packet.Text.Contains("Silver", StringComparison.Ordinal));
        Assert.Contains("forced Core.Rate(2, 199) Inventory.Add(711610)", result.Log);
    }

    [Fact]
    public async Task HandlerMap3071Family7022ForcedCopperGrantsCopperCoinTalkMessage()
    {
        var result = await KillMap3071Family7022MonsterAsync("copper");

        var copperCoin = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_611U);
        Assert.Equal(1, copperCoin.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 711_611U && packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(
            ParseStringPackets(result.Sent),
            packet => packet.Id == LegacyStringId.Effect &&
                packet.Strings.Contains("dispel5", StringComparer.Ordinal));
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content[3] == "You~received~a~Copper~Coin!");
        Assert.Contains("forced fallback Inventory.Add(711611)", result.Log);
    }

    [Fact]
    public async Task HandlerMap3071Family7022WithFullInventorySendsSourceDialog()
    {
        var result = await KillMap3071Family7022MonsterAsync("copper", fullInventory: true);

        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_611U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_611U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Your~inventory~is~full!");
        Assert.Contains("skipped legacy map 3071 coin reward", result.Log);
    }

    [Fact]
    public async Task HandlerMap3071Family7022OuterRateMissSkipsCoinReward()
    {
        var result = await KillMap3071Family7022MonsterAsync("none");

        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Empty(ParseFloorItems(result.Sent));
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId is 711_609U or 711_610U or 711_611U);
        Assert.Contains("forced Core.Rate(1, 30)=false", result.Log);
        Assert.Contains("skipped legacy map 3071 family 7022 coin reward", result.Log);
    }

    [Fact]
    public async Task HandlerMap3935Family40125GrantsStarStonePackBounty()
    {
        var result = await KillMap3935StarStonePackMonsterAsync(40_125);

        var reward = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_600_031U);
        Assert.Equal(1, reward.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 3_600_031U && packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "You've received a bounty of (Star Stone Pack) for conquering the divine Maniac!");
        Assert.Contains("map 3935 Star Stone Pack reward", result.Log);
        Assert.Contains("familyId=40125", result.Log);
    }

    [Theory]
    [InlineData(40_121)]
    [InlineData(40_122)]
    [InlineData(40_123)]
    [InlineData(40_124)]
    public async Task HandlerMap3935CrystalFamiliesGrantStarStonePackBounty(int familyId)
    {
        var result = await KillMap3935StarStonePackMonsterAsync(familyId);

        var reward = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_600_031U);
        Assert.Equal(1, reward.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 3_600_031U && packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "You've received a bounty of (Star Stone Pack) for conquering the divine crystal!");
        Assert.Contains(FormattableString.Invariant($"familyId={familyId}"), result.Log);
    }

    [Fact]
    public async Task HandlerMap3935Family40120FinishesQuest35034AndGrantsSeniorDragonSoul()
    {
        var result = await KillMap3935SeniorDragonSoulMonsterAsync(questAccepted: true);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 35_034U);
        Assert.Equal(PlayerQuestStatus.Finished, quest.Status);
        var reward = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_600_027U);
        Assert.Equal(1, reward.StackSize);
        Assert.Contains(
            ParseQuestListFrames(result.Sent),
            packet => packet.Mode == LegacyQuestListMode.FinishQuest &&
                packet.Items.Single().QuestId == 35_034U &&
                packet.Items.Single().Status == LegacyQuestStatus.Finished);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 3_600_027U && packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "You've completed the [Scramble for Justice] mission in the realm, you've received a bounty of (Senior Dragon Soul)");
        Assert.Contains("Scramble for Justice", result.Log);
        Assert.Contains("Senior Dragon Soul reward", result.Log);
    }

    [Fact]
    public async Task HandlerMap3935Family40120WithoutQuestStillGrantsSeniorDragonSoul()
    {
        var result = await KillMap3935SeniorDragonSoulMonsterAsync(questAccepted: false);

        Assert.Empty(result.UpdatedSession.Character.PlayerQuests.Quests);
        var reward = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_600_027U);
        Assert.Equal(1, reward.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 3_600_027U && packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.DoesNotContain(ParseQuestListFrames(result.Sent), packet => packet.Mode == LegacyQuestListMode.FinishQuest);
        Assert.DoesNotContain(
            ParseNpcReplies(result.Sent),
            packet => packet.Text.Contains("Scramble for Justice", StringComparison.Ordinal));
        Assert.Contains("Senior Dragon Soul reward", result.Log);
    }

    [Theory]
    [InlineData(40_104)]
    [InlineData(40_109)]
    [InlineData(40_110)]
    [InlineData(40_108)]
    public async Task HandlerMap3935BeastFamiliesIncrementQuest35025Objective(int familyId)
    {
        var result = await KillMap3935BeastQuest35025MonsterAsync(familyId, accepted: true);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 35_025U);
        Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
        Assert.Equal([1U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 35_025U);
        Assert.Equal([1U], objectiveFrame.Intentions);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "You've defeated the divine beast! Hurry and claim your reward from the Kingdom Mission Envoy!");
        Assert.Contains(FormattableString.Invariant($"familyId={familyId}"), result.Log);
        Assert.Contains("beast quest objective", result.Log);
    }

    [Fact]
    public async Task HandlerMap3935BeastQuest35025SkipsWhenAlreadyReached()
    {
        var result = await KillMap3935BeastQuest35025MonsterAsync(40_104, accepted: true, objective0: 1);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 35_025U);
        Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
        Assert.Equal([1U], quest.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 35_025U);
        Assert.DoesNotContain(
            ParseNpcReplies(result.Sent),
            packet => packet.Text.Contains("divine beast", StringComparison.Ordinal));
        Assert.Contains("skipped legacy map 3935 beast quest objective", result.Log);
    }

    [Theory]
    [InlineData(367, 459, 40_104, "WhiteTiger")]
    [InlineData(471, 371, 40_109, "AzureDragon")]
    [InlineData(371, 289, 40_108, "VermilionBird")]
    [InlineData(295, 337, 40_110, "BlackTurtle")]
    public async Task HandlerMap3935Quest35025SpawnLoopCreatesAnchoredBeast(
        int playerX,
        int playerY,
        int spawnedMonsterId,
        string spawnedName)
    {
        var result = await KillMap3935BeastSpawnLoopMonsterAsync(
            playerX,
            playerY,
            spawnedMonsterId,
            spawnedName);

        var spawnPacket = Assert.Single(
            ParseMonsterSpawns(result.Sent),
            packet => packet.MonsterId == (uint)spawnedMonsterId);
        Assert.Equal((ushort)playerX, spawnPacket.X);
        Assert.Equal((ushort)playerY, spawnPacket.Y);
        Assert.Equal(spawnedName, spawnPacket.Names[0]);
        Assert.True(result.MonsterStates.TryGetDynamicMonsterByUid(
            3_935,
            0,
            spawnPacket.Uid,
            out var spawnedInstance,
            out var spawnedFamily));
        Assert.Equal((uint)spawnedMonsterId, spawnedInstance.MonsterId);
        Assert.True(spawnedInstance.RemoveOnDead);
        Assert.Equal(spawnedMonsterId, spawnedFamily.Id);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 35_025U);
        Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
        Assert.Equal([0U], quest.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 35_025U);
        Assert.Contains(FormattableString.Invariant($"monsterId={spawnedMonsterId}"), result.Log);
        Assert.Contains("quest 35025 divine beast AddMapMonster", result.Log);
    }

    [Fact]
    public async Task HandlerMap3935Quest35025SpawnLoopSkipsWhenRateMisses()
    {
        var result = await KillMap3935BeastSpawnLoopMonsterAsync(
            367,
            459,
            40_104,
            "WhiteTiger",
            forcedRate: false);

        Assert.DoesNotContain(ParseMonsterSpawns(result.Sent), packet => packet.MonsterId == 40_104U);
        Assert.Contains("Core.Rate(40)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap3935Quest35025SpawnLoopSkipsDuplicateVisibleBeast()
    {
        var result = await KillMap3935BeastSpawnLoopMonsterAsync(
            367,
            459,
            40_104,
            "WhiteTiger",
            preRegisterVisible: true);

        Assert.DoesNotContain(ParseMonsterSpawns(result.Sent), packet => packet.MonsterId == 40_104U);
        Assert.Contains("visible beast already in screen", result.Log);
    }

    [Fact]
    public async Task HandlerMap3935Quest35028IncrementsEnemyCount()
    {
        var result = await KillMap3935Quest35028MonsterAsync(objective0: 10);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 35_028U);
        Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
        Assert.Equal([11U], quest.Intentions);
        var persistedQuest = Assert.Single(
            new PlayerQuestSnapshotLoader(result.PlayerQuestsRoot).LoadByCharacterUid(KillerUid).Quests,
            entry => entry.Uid == 35_028U);
        Assert.Equal(PlayerQuestStatus.Accepted, persistedQuest.Status);
        Assert.Equal([11U], persistedQuest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 35_028U);
        Assert.Equal([11U], objectiveFrame.Intentions);
        Assert.DoesNotContain(
            ParseNpcReplies(result.Sent),
            packet => packet.Text.Contains("50 enemies", StringComparison.Ordinal));
        Assert.Contains("quest 35028 enemy count", result.Log);
        Assert.Contains("completed=False", result.Log);
    }

    [Fact]
    public async Task HandlerMap3935Quest35028ShowsRealmCrystalBoxAtFifty()
    {
        var result = await KillMap3935Quest35028MonsterAsync(objective0: 49);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 35_028U);
        Assert.Equal([50U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 35_028U);
        Assert.Equal([50U], objectiveFrame.Intentions);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "You've eliminated 50 enemies. Hurry and claim a bounty from Realm Crystal.");
        Assert.Contains("completed=True", result.Log);
    }

    [Fact]
    public async Task HandlerMap3935Quest35028SkipsWhenAlreadyReached()
    {
        var result = await KillMap3935Quest35028MonsterAsync(objective0: 50);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 35_028U);
        Assert.Equal([50U], quest.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 35_028U);
        Assert.DoesNotContain(
            ParseNpcReplies(result.Sent),
            packet => packet.Text.Contains("50 enemies", StringComparison.Ordinal));
        Assert.DoesNotContain("quest 35028 enemy count", result.Log);
    }

    [Fact]
    public async Task HandlerMap3935Quest35007AddsStrikePoints()
    {
        var result = await KillMap3935Quest35007MonsterAsync(objective0: 100, strikePoints: 4);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 35_007U);
        Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
        Assert.Equal([104U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 35_007U);
        Assert.Equal([104U], objectiveFrame.Intentions);
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content.Any(content => content ==
                "You've received 104 Strike Points by killing enemies fiercely. When you earn 300 Strike Points, you can claim a reward from the Kingdom Mission Envoy."));
        Assert.DoesNotContain(
            ParseNpcReplies(result.Sent),
            packet => packet.Text.Contains("Thunder Strike", StringComparison.Ordinal));
        Assert.Contains("amount=4", result.Log);
        Assert.Contains("completed=False", result.Log);
    }

    [Fact]
    public async Task HandlerMap3935Quest35007ClampsMessageAndShowsThunderStrikeBox()
    {
        var result = await KillMap3935Quest35007MonsterAsync(objective0: 298, strikePoints: 5);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 35_007U);
        Assert.Equal([303U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 35_007U);
        Assert.Equal([303U], objectiveFrame.Intentions);
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content.Any(content => content ==
                "You've received 300 Strike Points by killing enemies fiercely. When you earn 300 Strike Points, you can claim a reward from the Kingdom Mission Envoy."));
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "You've completed the [Thunder Strike] mission in the realm. Report back to the Kingdom Mission Envoy to claim your reward.");
        Assert.Contains("objective0=303", result.Log);
        Assert.Contains("displayed=300", result.Log);
        Assert.Contains("completed=True", result.Log);
    }

    [Fact]
    public async Task HandlerMap3935Quest35007SkipsWhenAlreadyReached()
    {
        var result = await KillMap3935Quest35007MonsterAsync(objective0: 300, strikePoints: 5);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 35_007U);
        Assert.Equal([300U], quest.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 35_007U);
        Assert.DoesNotContain(
            ParseMessages(result.Sent),
            packet => packet.Content.Any(content => content.Contains("Strike Points", StringComparison.Ordinal)));
        Assert.DoesNotContain(
            ParseNpcReplies(result.Sent),
            packet => packet.Text.Contains("Thunder Strike", StringComparison.Ordinal));
        Assert.DoesNotContain("quest 35007 Thunder Strike points", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40903Quest3634IncrementsAngerRatCount()
    {
        var result = await KillMap3998Family40903Quest3634MonsterAsync(objective0: 4);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 3_634U);
        Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
        Assert.Equal([5U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 3_634U);
        Assert.Equal([5U], objectiveFrame.Intentions);
        Assert.DoesNotContain(
            ParseNpcReplies(result.Sent),
            packet => packet.Text.Contains("Anger Rats", StringComparison.Ordinal));
        Assert.Contains("map 3998 Anger Rat quest objective", result.Log);
        Assert.Contains("completed=False", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40903Quest3634ShowsChongYanBoxAtTen()
    {
        var result = await KillMap3998Family40903Quest3634MonsterAsync(objective0: 9);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 3_634U);
        Assert.Equal([10U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 3_634U);
        Assert.Equal([10U], objectiveFrame.Intentions);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "You've eliminated enough number of Anger Rats. Hurry and report back to Chong Yan Elder!");
        Assert.Contains("completed=True", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40903Quest3634StillIncrementsAfterThreshold()
    {
        var result = await KillMap3998Family40903Quest3634MonsterAsync(objective0: 10);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 3_634U);
        Assert.Equal([11U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 3_634U);
        Assert.Equal([11U], objectiveFrame.Intentions);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.Text == "You've eliminated enough number of Anger Rats. Hurry and report back to Chong Yan Elder!");
    }

    [Fact]
    public async Task HandlerMap3998Family40903Quest3634SkipsWhenNotAccepted()
    {
        var result = await KillMap3998Family40903Quest3634MonsterAsync(
            objective0: 9,
            status: PlayerQuestStatus.Finished);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 3_634U);
        Assert.Equal(PlayerQuestStatus.Finished, quest.Status);
        Assert.Equal([9U], quest.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 3_634U);
        Assert.DoesNotContain(
            ParseNpcReplies(result.Sent),
            packet => packet.Text.Contains("Anger Rats", StringComparison.Ordinal));
        Assert.DoesNotContain("map 3998 Anger Rat quest objective", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40900Quest3636AddsRuneFragment()
    {
        var result = await KillMap3998Family40900Quest3636MonsterAsync();

        var fragment = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_752U);
        Assert.Equal((ushort)1, fragment.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 3_008_752U &&
                packet.StackSize == 1 &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains("Rune Fragment reward", result.Log);
        Assert.DoesNotContain("map3998-rune-fragment-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40900Quest3636StacksEighthRuneFragmentAndLogsAutoPatcherGap()
    {
        var result = await KillMap3998Family40900Quest3636MonsterAsync(existingFragments: 7);

        var fragment = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_752U);
        Assert.Equal((ushort)8, fragment.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 3_008_752U &&
                packet.StackSize == 8 &&
                packet.Mode == LegacyGameItemPacket.UpdateItemMode);
        Assert.Contains("map3998-rune-fragment-autopatcher", result.Log);
        Assert.Contains("questId=3636", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40900Quest3636SkipsWhenRateMisses()
    {
        var result = await KillMap3998Family40900Quest3636MonsterAsync(forcedRate: false);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_752U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 3_008_752U);
        Assert.Contains("Core.Rate(45)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40900Quest3636ShowsInventoryBoxWhenFull()
    {
        var result = await KillMap3998Family40900Quest3636MonsterAsync(fullInventory: true);

        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_752U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 3_008_752U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("inventory-full", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40902Quest3638AddsVioletBatKingHammer()
    {
        var result = await KillMap3998Family40902Quest3638MonsterAsync();

        var hammer = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_750U);
        Assert.Equal((ushort)1, hammer.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 3_008_750U &&
                packet.StackSize == 1 &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains("Violet Bat King hammer reward", result.Log);
        Assert.Contains("addedItem=True", result.Log);
        Assert.Contains("map3998-violet-bat-king-hammer-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40902Quest3638KeepsExistingHammerAndLogsAutoPatcherGap()
    {
        var result = await KillMap3998Family40902Quest3638MonsterAsync(existingHammer: true);

        var hammer = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_750U);
        Assert.Equal((ushort)1, hammer.StackSize);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 3_008_750U);
        Assert.Contains("addedItem=False", result.Log);
        Assert.Contains("map3998-violet-bat-king-hammer-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40902Quest3638ShowsInventoryBoxWhenFull()
    {
        var result = await KillMap3998Family40902Quest3638MonsterAsync(fullInventory: true);

        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_750U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 3_008_750U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("inventory-full", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40904Quest3641IncrementsLavaScorpionCount()
    {
        var result = await KillMap3998Family40904Quest3641MonsterAsync(objective0: 6);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 3_641U);
        Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
        Assert.Equal([7U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 3_641U);
        Assert.Equal([7U], objectiveFrame.Intentions);
        Assert.Contains("map 3998 Lava Scorpion quest objective", result.Log);
        Assert.Contains("completed=False", result.Log);
        Assert.DoesNotContain("map3998-lava-scorpion-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40904Quest3641LogsAutoPatcherGapAtFifteen()
    {
        var result = await KillMap3998Family40904Quest3641MonsterAsync(objective0: 14);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 3_641U);
        Assert.Equal([15U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 3_641U);
        Assert.Equal([15U], objectiveFrame.Intentions);
        Assert.Contains("completed=True", result.Log);
        Assert.Contains("map3998-lava-scorpion-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40904Quest3641StillIncrementsAfterThreshold()
    {
        var result = await KillMap3998Family40904Quest3641MonsterAsync(objective0: 15);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 3_641U);
        Assert.Equal([16U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 3_641U);
        Assert.Equal([16U], objectiveFrame.Intentions);
        Assert.Contains("map3998-lava-scorpion-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40904Quest3641SkipsWhenNotAccepted()
    {
        var result = await KillMap3998Family40904Quest3641MonsterAsync(
            objective0: 14,
            status: PlayerQuestStatus.Finished);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 3_641U);
        Assert.Equal(PlayerQuestStatus.Finished, quest.Status);
        Assert.Equal([14U], quest.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 3_641U);
        Assert.DoesNotContain("map 3998 Lava Scorpion quest objective", result.Log);
        Assert.DoesNotContain("map3998-lava-scorpion-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40908Quest3644AddsBuildingStone()
    {
        var result = await KillMap3998Family40908Quest3644MonsterAsync();

        var stone = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_742U);
        Assert.Equal((ushort)1, stone.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 3_008_742U &&
                packet.StackSize == 1 &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains("Building Stone reward", result.Log);
        Assert.DoesNotContain("map3998-building-stone-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40908Quest3644StacksToFiftyWithoutAutoPatcher()
    {
        var result = await KillMap3998Family40908Quest3644MonsterAsync(existingStones: 49);

        var stone = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_742U);
        Assert.Equal((ushort)50, stone.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 3_008_742U &&
                packet.StackSize == 50 &&
                packet.Mode == LegacyGameItemPacket.UpdateItemMode);
        Assert.Contains("existingCount=49", result.Log);
        Assert.DoesNotContain("map3998-building-stone-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40908Quest3644LogsAutoPatcherGapWhenAlreadyHasFifty()
    {
        var result = await KillMap3998Family40908Quest3644MonsterAsync(existingStones: 50);

        var stone = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_742U);
        Assert.Equal((ushort)50, stone.StackSize);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 3_008_742U);
        Assert.Contains("existingCount=50", result.Log);
        Assert.Contains("addedItem=False", result.Log);
        Assert.Contains("map3998-building-stone-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40908Quest3644ShowsInventoryBoxWhenFull()
    {
        var result = await KillMap3998Family40908Quest3644MonsterAsync(fullInventory: true);

        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_742U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 3_008_742U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("inventory-full", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40907Quest3648AddsStarOre()
    {
        var result = await KillMap3998Family40907Quest3648MonsterAsync();

        var ore = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_748U);
        Assert.Equal((ushort)1, ore.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 3_008_748U &&
                packet.StackSize == 1 &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains("Star Ore reward", result.Log);
        Assert.DoesNotContain("map3998-star-ore-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40907Quest3648StacksToHundredWithoutAutoPatcher()
    {
        var result = await KillMap3998Family40907Quest3648MonsterAsync(existingOres: 99);

        var ore = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_748U);
        Assert.Equal((ushort)100, ore.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 3_008_748U &&
                packet.StackSize == 100 &&
                packet.Mode == LegacyGameItemPacket.UpdateItemMode);
        Assert.Contains("existingCount=99", result.Log);
        Assert.DoesNotContain("map3998-star-ore-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40907Quest3648LogsAutoPatcherGapWhenAlreadyHasHundred()
    {
        var result = await KillMap3998Family40907Quest3648MonsterAsync(existingOres: 100);

        var ore = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_748U);
        Assert.Equal((ushort)100, ore.StackSize);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 3_008_748U);
        Assert.Contains("existingCount=100", result.Log);
        Assert.Contains("addedItem=False", result.Log);
        Assert.Contains("map3998-star-ore-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40907Quest3648ShowsInventoryBoxWhenFull()
    {
        var result = await KillMap3998Family40907Quest3648MonsterAsync(fullInventory: true);

        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_748U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 3_008_748U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("inventory-full", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40905Quest3645AddsWheelOfNature()
    {
        var result = await KillMap3998Family40905Quest3645MonsterAsync();

        var wheel = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_743U);
        Assert.Equal((ushort)1, wheel.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 3_008_743U &&
                packet.StackSize == 1 &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains("Wheel of Nature reward", result.Log);
        Assert.Contains("addedItem=True", result.Log);
        Assert.DoesNotContain("map3998-wheel-of-nature-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40905Quest3645SkipsWhenRateMisses()
    {
        var result = await KillMap3998Family40905Quest3645MonsterAsync(forcedRate: false);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_743U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 3_008_743U);
        Assert.Contains("Core.Rate(40)=false", result.Log);
        Assert.DoesNotContain("map3998-wheel-of-nature-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40905Quest3645KeepsExistingWheelAndLogsAutoPatcherGap()
    {
        var result = await KillMap3998Family40905Quest3645MonsterAsync(existingWheel: true);

        var wheel = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_743U);
        Assert.Equal((ushort)1, wheel.StackSize);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 3_008_743U);
        Assert.Contains("addedItem=False", result.Log);
        Assert.Contains("map3998-wheel-of-nature-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40905Quest3645ShowsInventoryBoxWhenFull()
    {
        var result = await KillMap3998Family40905Quest3645MonsterAsync(fullInventory: true);

        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_743U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 3_008_743U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("inventory-full", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40905Quest3645SkipsWhenQuestNotAccepted()
    {
        var result = await KillMap3998Family40905Quest3645MonsterAsync(status: PlayerQuestStatus.Finished);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_743U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 3_008_743U);
        Assert.DoesNotContain("Wheel of Nature reward", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40906Quest3646AddsEarthForceFragment()
    {
        var result = await KillMap3998Family40906Quest3646MonsterAsync();

        var fragment = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_754U);
        Assert.Equal((ushort)1, fragment.StackSize);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_745U);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 3_008_754U &&
                packet.StackSize == 1 &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains("Earth Force fragment reward", result.Log);
        Assert.Contains("converted=False", result.Log);
        Assert.DoesNotContain("map3998-earth-force-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40906Quest3646SkipsWhenRateMisses()
    {
        var result = await KillMap3998Family40906Quest3646MonsterAsync(forcedRate: false);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId is 3_008_754U or 3_008_745U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId is 3_008_754U or 3_008_745U);
        Assert.Contains("Core.Rate(40)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40906Quest3646ConvertsFourFragmentsToEarthForce()
    {
        var result = await KillMap3998Family40906Quest3646MonsterAsync(existingFragments: 4);

        var earthForce = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_745U);
        Assert.Equal((ushort)1, earthForce.StackSize);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_754U);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 3_008_754U &&
                packet.StackSize == 5 &&
                packet.Mode == LegacyGameItemPacket.UpdateItemMode);
        Assert.Contains(
            ParseItemUsage(result.Sent),
            packet => packet.Id == 1U &&
                packet.Action == MsgItemUsageAction.RemoveInventory);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 3_008_745U &&
                packet.StackSize == 1 &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains("addedFragment=True", result.Log);
        Assert.Contains("converted=True", result.Log);
        Assert.Contains("map3998-earth-force-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40906Quest3646ConvertsExistingFiveFragments()
    {
        var result = await KillMap3998Family40906Quest3646MonsterAsync(existingFragments: 5);

        var earthForce = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_745U);
        Assert.Equal((ushort)1, earthForce.StackSize);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_754U);
        Assert.DoesNotContain(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 3_008_754U);
        Assert.Contains(
            ParseItemUsage(result.Sent),
            packet => packet.Id == 1U &&
                packet.Action == MsgItemUsageAction.RemoveInventory);
        Assert.Contains("addedFragment=False", result.Log);
        Assert.Contains("converted=True", result.Log);
        Assert.Contains("map3998-earth-force-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap3998Family40906Quest3646KeepsExistingEarthForceAndLogsAutoPatcherGap()
    {
        var result = await KillMap3998Family40906Quest3646MonsterAsync(existingEarthForce: true);

        var earthForce = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_008_745U);
        Assert.Equal((ushort)1, earthForce.StackSize);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 3_008_745U);
        Assert.Contains("alreadyHasEarthForce=True", result.Log);
        Assert.Contains("map3998-earth-force-autopatcher", result.Log);
    }


    [Fact]
    public async Task HandlerMap3998Family40906Quest3646SkipsWhenQuestNotAccepted()
    {
        var result = await KillMap3998Family40906Quest3646MonsterAsync(status: PlayerQuestStatus.Finished);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId is 3_008_754U or 3_008_745U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId is 3_008_754U or 3_008_745U);
        Assert.DoesNotContain("Earth Force reward", result.Log);
    }

    [Fact]
    public async Task HandlerMap1002Family1Quest1307IncrementsPheasantObjectiveAndShowsBox()
    {
        var result = await KillMap1002Family1PheasantMonsterAsync(quest1307Objective: 2);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_307U);
        Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
        Assert.Equal([3U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_307U);
        Assert.Equal([3U], objectiveFrame.Intentions);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "You~killed~Pheasant~and~scared~the~Apes~away!");
        Assert.Contains("map 1002 Pheasant quest 1307 objective", result.Log);
        Assert.DoesNotContain("questId=824", result.Log);
    }

    [Fact]
    public async Task HandlerMap1002Family1Quest824IncrementsWithoutPheasantBox()
    {
        var result = await KillMap1002Family1PheasantMonsterAsync(
            quest1307Status: null,
            quest824Status: PlayerQuestStatus.Accepted,
            quest824Objective: 4);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 824U);
        Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
        Assert.Equal([5U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 824U);
        Assert.Equal([5U], objectiveFrame.Intentions);
        Assert.DoesNotContain(
            ParseNpcReplies(result.Sent),
            packet => packet.Text == "You~killed~Pheasant~and~scared~the~Apes~away!");
        Assert.Contains("map 1002 Pheasant quest 824 objective", result.Log);
        Assert.DoesNotContain("questId=1307", result.Log);
    }

    [Fact]
    public async Task HandlerMap1002Family1PheasantIncrementsBothAcceptedQuests()
    {
        var result = await KillMap1002Family1PheasantMonsterAsync(
            quest1307Objective: 6,
            quest824Status: PlayerQuestStatus.Accepted,
            quest824Objective: 8);

        var quest1307 = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_307U);
        var quest824 = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 824U);
        Assert.Equal([7U], quest1307.Intentions);
        Assert.Equal([9U], quest824.Intentions);

        var objectiveFrames = ParseQuestObjectiveFrames(result.Sent);
        Assert.Contains(objectiveFrames, packet => packet.Uid == 1_307U && packet.Intentions.SequenceEqual([7U]));
        Assert.Contains(objectiveFrames, packet => packet.Uid == 824U && packet.Intentions.SequenceEqual([9U]));
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "You~killed~Pheasant~and~scared~the~Apes~away!");
        Assert.Contains("questId=1307", result.Log);
        Assert.Contains("questId=824", result.Log);
    }

    [Fact]
    public async Task HandlerMap1002Family1PheasantSkipsWhenQuestsNotAccepted()
    {
        var result = await KillMap1002Family1PheasantMonsterAsync(
            quest1307Status: PlayerQuestStatus.Finished,
            quest1307Objective: 2,
            quest824Status: PlayerQuestStatus.Finished,
            quest824Objective: 4);

        var quest1307 = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_307U);
        var quest824 = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 824U);
        Assert.Equal([2U], quest1307.Intentions);
        Assert.Equal([4U], quest824.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid is 1_307U or 824U);
        Assert.DoesNotContain(
            ParseNpcReplies(result.Sent),
            packet => packet.Text == "You~killed~Pheasant~and~scared~the~Apes~away!");
        Assert.DoesNotContain("map 1002 Pheasant quest", result.Log);
    }

    [Fact]
    public async Task HandlerMap1002Family3Quest1735ShowsArtisanLuoBoxAtHundred()
    {
        var result = await KillMap1002Family3RobinMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_735, PlayerQuestStatus.Accepted, 0, [99U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_735U);
        Assert.Equal([100U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_735U);
        Assert.Equal([100U], objectiveFrame.Intentions);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "You`ve~collected~enough~Feathers.~Now~you~can~go~report~to~Artisan~Luo.");
        Assert.Contains("map 1002 Robin quest 1735 objective", result.Log);
        Assert.Contains("completed=True", result.Log);
    }

    [Fact]
    public async Task HandlerMap1002Family3Quest1009IncrementsRobinElimination()
    {
        var result = await KillMap1002Family3RobinMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_009, PlayerQuestStatus.Accepted, 0, [4U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_009U);
        Assert.Equal([5U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_009U);
        Assert.Equal([5U], objectiveFrame.Intentions);
        Assert.Contains("map 1002 Robin quest 1009 objective", result.Log);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet => packet.Text.Contains("Robin", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandlerMap1002Family3Quest2365LogsXuLiangAutoRouteAtHundred()
    {
        var result = await KillMap1002Family3RobinMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(2_365, PlayerQuestStatus.Accepted, 0, [99U, 0U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 2_365U);
        Assert.Equal([100U, 0U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 2_365U);
        Assert.Equal([100U, 0U], objectiveFrame.Intentions);
        Assert.Contains("map1002-robin-quest2365-autopatcher", result.Log);
        Assert.Contains("finishNpcId=7992", result.Log);
        Assert.Contains("finishX=368", result.Log);
        Assert.Contains("finishY=643", result.Log);
        Assert.Contains("completed=True", result.Log);
    }

    [Fact]
    public async Task HandlerMap1002Family3Quest1010IncrementsBothObjectivesAndStacksRobinFeather()
    {
        var result = await KillMap1002Family3RobinMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_010, PlayerQuestStatus.Accepted, 0, [9U, 9U]),
            ],
            existingFeathers: 9);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_010U);
        Assert.Equal([10U, 10U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_010U);
        Assert.Equal([10U, 10U], objectiveFrame.Intentions);
        var feather = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_238U);
        Assert.Equal((ushort)10, feather.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 711_238U &&
                packet.StackSize == 10 &&
                packet.Mode == LegacyGameItemPacket.UpdateItemMode);
        Assert.Contains("map1002-robin-feather-autopatcher", result.Log);
        Assert.Contains("finishNpcId=4467", result.Log);
        Assert.Contains("addedFeather=True", result.Log);
        Assert.Contains("completed=True", result.Log);
    }

    [Fact]
    public async Task HandlerMap1002Family3Quest1010SkipsFeatherAddWhenAlreadyHasTen()
    {
        var result = await KillMap1002Family3RobinMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_010, PlayerQuestStatus.Accepted, 0, [9U, 9U]),
            ],
            existingFeathers: 10);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_010U);
        Assert.Equal([10U, 10U], quest.Intentions);
        var feather = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_238U);
        Assert.Equal((ushort)10, feather.StackSize);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_238U);
        Assert.Contains("map1002-robin-feather-autopatcher", result.Log);
        Assert.Contains("addedFeather=False", result.Log);
    }

    [Fact]
    public async Task HandlerMap1002Family3SkipsWhenRobinQuestsNotAccepted()
    {
        var result = await KillMap1002Family3RobinMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_735, PlayerQuestStatus.Finished, 0, [99U]),
                new PlayerQuestSnapshotEntry(1_009, PlayerQuestStatus.Finished, 0, [4U]),
                new PlayerQuestSnapshotEntry(2_365, PlayerQuestStatus.Finished, 0, [99U, 0U]),
                new PlayerQuestSnapshotEntry(1_010, PlayerQuestStatus.Finished, 0, [9U, 9U]),
            ],
            existingFeathers: 9);

        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid is 1_735U or 1_009U or 2_365U or 1_010U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_238U);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet => packet.Text.Contains("Feathers", StringComparison.Ordinal));
        Assert.DoesNotContain("map 1002 Robin quest", result.Log);
    }

    [Fact]
    public async Task HandlerMap1002Family4Quest1013IncrementsAndAddsHandkerchief()
    {
        var result = await KillMap1002Family4ApparitionMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_013, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_013U);
        Assert.Equal([1U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_013U);
        Assert.Equal([1U], objectiveFrame.Intentions);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_241U);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 711_241U &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains("map1002-apparition-handkerchief-autopatcher", result.Log);
        Assert.Contains("finishNpcId=7993", result.Log);
        Assert.Contains("finishX=495", result.Log);
        Assert.Contains("finishY=622", result.Log);
        Assert.Contains("map 1002 Apparition quest 1013 Handkerchief reward", result.Log);
    }

    [Fact]
    public async Task HandlerMap1002Family4Quest1013SkipsWhenHandkerchiefOwned()
    {
        var result = await KillMap1002Family4ApparitionMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_013, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            existingHandkerchief: true);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_013U);
        Assert.Equal([0U], quest.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_013U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_241U);
        Assert.DoesNotContain("map1002-apparition-handkerchief-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap1002Family4Quest1016SendsSpiritMessageAndIncrements()
    {
        var result = await KillMap1002Family4ApparitionMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_016, PlayerQuestStatus.Accepted, 0, [4U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_016U);
        Assert.Equal([5U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_016U);
        Assert.Equal([5U], objectiveFrame.Intentions);
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
                packet.Content[3] == "You've collected spirit of an Apparition and stored it in Sipirit Container.");
        Assert.Contains("map 1002 Apparition quest 1016 spirit objective", result.Log);
        Assert.Contains("completed=False", result.Log);
        Assert.DoesNotContain("map1002-apparition-spirit-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap1002Family4Quest1016LogsFortunetellerAutoRouteAtTen()
    {
        var result = await KillMap1002Family4ApparitionMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_016, PlayerQuestStatus.Accepted, 0, [9U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_016U);
        Assert.Equal([10U], quest.Intentions);
        Assert.Contains("map1002-apparition-spirit-autopatcher", result.Log);
        Assert.Contains("finishNpcId=600050", result.Log);
        Assert.Contains("finishX=489", result.Log);
        Assert.Contains("finishY=622", result.Log);
        Assert.Contains("completed=True", result.Log);
    }

    [Fact]
    public async Task HandlerMap1002Family4SkipsWhenApparitionQuestsNotAccepted()
    {
        var result = await KillMap1002Family4ApparitionMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_013, PlayerQuestStatus.Finished, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_016, PlayerQuestStatus.Finished, 0, [9U]),
            ]);

        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid is 1_013U or 1_016U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_241U);
        Assert.DoesNotContain(
            ParseMessages(result.Sent),
            packet => packet.Content[3].Contains("Apparition", StringComparison.Ordinal));
        Assert.DoesNotContain("map 1002 Apparition quest", result.Log);
    }

    [Fact]
    public async Task HandlerMap1013DynamicFamily14333Quest1748IncrementsWithoutTeleportBeforeFive()
    {
        var result = await KillMap1013DynamicFamily14333Quest1748MonsterAsync(objective0: 3);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_748U);
        Assert.Equal([4U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_748U);
        Assert.Equal([4U], objectiveFrame.Intentions);
        Assert.Equal(1_013, result.UpdatedSession.Character.MapId);
        Assert.DoesNotContain(ParseActions(result.Sent), packet => packet.Type == MsgActionType.Teleport);
        Assert.DoesNotContain(
            ParseMessages(result.Sent),
            packet => packet.Content[3].Contains("Royal~Doctor~Li", StringComparison.Ordinal));
        Assert.Contains("map 1013 dynamic Frantic Monkeys quest 1748 objective", result.Log);
        Assert.Contains("completed=False", result.Log);
    }

    [Fact]
    public async Task HandlerMap1013DynamicFamily14333Quest1748TeleportsAndMessagesAtFive()
    {
        var result = await KillMap1013DynamicFamily14333Quest1748MonsterAsync(objective0: 4);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_748U);
        Assert.Equal([5U], quest.Intentions);
        Assert.Equal(1_002, result.UpdatedSession.Character.MapId);
        Assert.Equal(299, result.UpdatedSession.Character.X);
        Assert.Equal(244, result.UpdatedSession.Character.Y);
        Assert.Equal(1_013, result.UpdatedSession.Character.PreviousMapId);

        var teleport = Assert.Single(ParseActions(result.Sent), packet => packet.Type == MsgActionType.Teleport);
        Assert.Equal(1_002U, teleport.DwParam);
        Assert.Equal(299U, teleport.PositionX);
        Assert.Equal(244U, teleport.PositionY);
        Assert.Equal(1_002, teleport.DwParam3);
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
                packet.Content[3] == "You`ve~returned~to~Royal~Doctor~Li.");
        Assert.Contains("map 1013 dynamic Frantic Monkeys quest 1748 objective", result.Log);
        Assert.Contains("completed=True", result.Log);
    }

    [Fact]
    public async Task HandlerMap1013Family14333Quest1748SkipsWithoutDynamicId()
    {
        var result = await KillMap1013DynamicFamily14333Quest1748MonsterAsync(
            objective0: 4,
            dynamicId: 0);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_748U);
        Assert.Equal([4U], quest.Intentions);
        Assert.Equal(1_013, result.UpdatedSession.Character.MapId);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_748U);
        Assert.DoesNotContain(ParseActions(result.Sent), packet => packet.Type == MsgActionType.Teleport);
        Assert.DoesNotContain("map 1013 dynamic Frantic Monkeys quest 1748 objective", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family2171DeathAddsJadeSceptreAndPlatformHint()
    {
        var result = await KillMap1015Family2171JadeCourtesanMonsterAsync();

        var jadeSceptre = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 710_217U);
        Assert.Equal((ushort)1, jadeSceptre.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 710_217U &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 408U);
        Assert.Equal([1U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 408U);
        Assert.Equal([1U], objectiveFrame.Intentions);

        var messages = ParseMessages(result.Sent);
        Assert.Contains(
            messages,
            packet => packet.Content[3] == "You`ve killed the Jade Courtesan and obtained her Jade Sceptre.");
        Assert.Contains(
            messages,
            packet => packet.Content[3] == "As long as you collect the Purple Eye, Jade Sceptre, Jadeite, and Hairpin, you can open the Platform (Bird Island 655,693) and go to the Rose Garden.");
        Assert.Contains("map 1015 Jade Courtesan reward", result.Log);
        Assert.Contains("allPlatformItemsCollected=False", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family2171DeathSendsCompletionHintWhenAllPlatformItemsOwned()
    {
        var result = await KillMap1015Family2171JadeCourtesanMonsterAsync(
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 710_216),
                CreateInventoryItem(uid: 2, itemId: 710_218),
                CreateInventoryItem(uid: 3, itemId: 710_219),
            ]);

        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 710_217U);
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content[3] == "You`ve collected the Purple Eye, Jade Scepter, Jadeite, and Hairpin. Go to open the Teleport Platform on Bird Island (655,693).");
        Assert.Contains("allPlatformItemsCollected=True", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family2171DeathSkipsWhenJadeSceptreOwned()
    {
        var result = await KillMap1015Family2171JadeCourtesanMonsterAsync(
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 710_217),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 408U);
        Assert.Equal([9U], quest.Intentions);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 710_217U);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 408U);
        Assert.DoesNotContain(ParseMessages(result.Sent), packet => packet.Content[3].Contains("Jade", StringComparison.Ordinal));
        Assert.Contains("Jade Sceptre already in inventory", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family2171DeathReportsFullInventory()
    {
        var result = await KillMap1015Family2171JadeCourtesanMonsterAsync(
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(800_000 + index)))
                .ToArray());

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 710_217U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 710_217U);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 408U);
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content[3] == "Your inventory is full! Make some room, first.");
        Assert.Contains("reason=\"inventory full\"", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family2157DeathAddsBloodyShawnSpearAndDialog()
    {
        var result = await KillMap1015Family2157BloodyShawnMonsterAsync();

        var spear = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 721_781U);
        Assert.Equal((ushort)1, spear.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 721_781U &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Your~power~and~fortitude~overwhelmed~Bloody~Shawn,~and~you~obtained~his~Spear.~Grab~it~along~and~find~Ruan~Brother.");
        Assert.Contains("map 1015 Bloody Shawn reward", result.Log);
        Assert.Contains("questId=523", result.Log);
        Assert.Contains("itemId=721781", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family2157DeathReportsFullInventory()
    {
        var result = await KillMap1015Family2157BloodyShawnMonsterAsync(
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(810_000 + index)))
                .ToArray());

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 721_781U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 721_781U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Your~inventory~is~full!");
        Assert.Contains("reason=\"inventory full\"", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family2157DeathSkipsWhenSpearOwned()
    {
        var result = await KillMap1015Family2157BloodyShawnMonsterAsync(
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 721_781),
            ]);

        Assert.Equal(1, result.UpdatedSession.PlayerItems.InventoryItems.Count(item => item.ItemId == 721_781U));
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 721_781U);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet => packet.Text.Contains("Bloody~Shawn", StringComparison.Ordinal));
        Assert.Contains("Spear already in inventory", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family2157DeathSkipsWithoutAcceptedQuest523()
    {
        var result = await KillMap1015Family2157BloodyShawnMonsterAsync(
            questStatus: PlayerQuestStatus.Finished);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 721_781U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 721_781U);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet => packet.Text.Contains("Bloody~Shawn", StringComparison.Ordinal));
        Assert.DoesNotContain("map 1015 Bloody Shawn reward", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family2156DeathAddsFreshFishAndDialog()
    {
        var result = await KillMap1015Family2156TroutElfMonsterAsync();

        var fish = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 721_778U);
        Assert.Equal((ushort)1, fish.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 721_778U &&
                packet.Mode == LegacyGameItemPacket.AddItemMode &&
                packet.StackSize == 1);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Trout~Elves~died~and~turned~into~fresh~fish.~Collect~3~fish~and~trade~with~Ye~Sheng~(Bird~Island~324,400)~for~his~Iron~Poise.");
        Assert.Contains("map 1015 Trout Elf reward", result.Log);
        Assert.Contains("fishCountBefore=0", result.Log);
        Assert.Contains("fishCountAfter=1", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family2156DeathStacksFreshFishUntilThree()
    {
        var result = await KillMap1015Family2156TroutElfMonsterAsync(
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 721_778, stackSize: 2),
            ]);

        var fish = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 721_778U);
        Assert.Equal((ushort)3, fish.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 721_778U &&
                packet.Mode == LegacyGameItemPacket.UpdateItemMode &&
                packet.StackSize == 3);
        Assert.Contains("fishCountBefore=2", result.Log);
        Assert.Contains("fishCountAfter=3", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family2156DeathReportsFullInventory()
    {
        var result = await KillMap1015Family2156TroutElfMonsterAsync(
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(820_000 + index)))
                .ToArray());

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 721_778U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 721_778U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Your~inventory~is~full!");
        Assert.Contains("reason=\"inventory full\"", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family2156DeathSkipsWhenThreeFishOwnedOrQuestNotAccepted()
    {
        var enoughFish = await KillMap1015Family2156TroutElfMonsterAsync(
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 721_778, stackSize: 3),
            ]);
        var questFinished = await KillMap1015Family2156TroutElfMonsterAsync(
            questStatus: PlayerQuestStatus.Finished);

        Assert.Equal((ushort)3, Assert.Single(enoughFish.UpdatedSession.PlayerItems.InventoryItems).StackSize);
        Assert.DoesNotContain(ParseGameItems(enoughFish.Sent), packet => packet.ItemId == 721_778U);
        Assert.DoesNotContain(ParseNpcReplies(enoughFish.Sent), packet => packet.Text.Contains("Trout~Elves", StringComparison.Ordinal));
        Assert.DoesNotContain("map 1015 Trout Elf reward", enoughFish.Log);
        Assert.DoesNotContain(questFinished.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 721_778U);
        Assert.DoesNotContain(ParseGameItems(questFinished.Sent), packet => packet.ItemId == 721_778U);
        Assert.DoesNotContain(ParseNpcReplies(questFinished.Sent), packet => packet.Text.Contains("Trout~Elves", StringComparison.Ordinal));
        Assert.DoesNotContain("map 1015 Trout Elf reward", questFinished.Log);
    }

    public static IEnumerable<object[]> Map1015TokenRewardCases()
    {
        yield return
        [
            85,
            1_824U,
            710_566U,
            "LegacyMap1015Family85ScarletTokenRate",
            "Scarlet",
            "You picked up the Scarlet Token. Now hurry up and give it to Felix!",
        ];
        yield return
        [
            2_226,
            1_823U,
            710_565U,
            "LegacyMap1015Family2226PitchTokenRate",
            "Pitch",
            "You picked up the Pitch Token. Now hurry up and give it to Felix!",
        ];
        yield return
        [
            2_225,
            1_822U,
            710_564U,
            "LegacyMap1015Family2225BlackTokenRate",
            "Black",
            "You picked up the Black Token. Now hurry up and give it to Felix!",
        ];
        yield return
        [
            79,
            1_821U,
            710_563U,
            "LegacyMap1015Family79IndigoTokenRate",
            "Indigo",
            "You~picked~up~the~Indigo~Token.~Now~hurry~up~and~give~it~to~Felix!",
        ];
    }

    [Theory]
    [MemberData(nameof(Map1015TokenRewardCases))]
    public async Task HandlerMap1015TokenDeathAddsTokenAndMessage(
        int familyId,
        uint questId,
        uint itemId,
        string rateAttribute,
        string tokenName,
        string sourceMessage)
    {
        var result = await KillMap1015TokenMonsterAsync(familyId, questId, itemId, rateAttribute);

        var token = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == itemId);
        Assert.Equal((ushort)1, token.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == itemId &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content[3] == sourceMessage);
        Assert.Contains("map 1015 token reward", result.Log);
        Assert.Contains($"token={tokenName}", result.Log);
        Assert.Contains($"questId={questId.ToString(CultureInfo.InvariantCulture)}", result.Log);
        Assert.Contains($"itemId={itemId.ToString(CultureInfo.InvariantCulture)}", result.Log);
    }

    [Theory]
    [MemberData(nameof(Map1015TokenRewardCases))]
    public async Task HandlerMap1015TokenDeathSkipsWhenRateMisses(
        int familyId,
        uint questId,
        uint itemId,
        string rateAttribute,
        string tokenName,
        string sourceMessage)
    {
        var result = await KillMap1015TokenMonsterAsync(
            familyId,
            questId,
            itemId,
            rateAttribute,
            forcedRate: false);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == itemId);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == itemId);
        Assert.DoesNotContain(ParseMessages(result.Sent), packet => packet.Content[3] == sourceMessage);
        Assert.Contains($"token={tokenName}", result.Log);
        Assert.Contains("Core.Rate", result.Log);
        Assert.Contains("=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015TokenDeathReportsFullInventory()
    {
        var result = await KillMap1015TokenMonsterAsync(
            familyId: 85,
            questId: 1_824,
            itemId: 710_566,
            rateAttribute: "LegacyMap1015Family85ScarletTokenRate",
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(830_000 + index)))
                .ToArray());

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 710_566U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 710_566U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Sorry, your inventory is full. Make some room first.");
        Assert.Contains("reason=\"inventory full\"", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015TokenDeathSkipsWhenTokenOwnedOrQuestNotAccepted()
    {
        var alreadyOwned = await KillMap1015TokenMonsterAsync(
            familyId: 85,
            questId: 1_824,
            itemId: 710_566,
            rateAttribute: "LegacyMap1015Family85ScarletTokenRate",
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 710_566),
            ]);
        var questFinished = await KillMap1015TokenMonsterAsync(
            familyId: 85,
            questId: 1_824,
            itemId: 710_566,
            rateAttribute: "LegacyMap1015Family85ScarletTokenRate",
            questStatus: PlayerQuestStatus.Finished);

        Assert.Equal(1, alreadyOwned.UpdatedSession.PlayerItems.InventoryItems.Count(item => item.ItemId == 710_566U));
        Assert.DoesNotContain(ParseGameItems(alreadyOwned.Sent), packet => packet.ItemId == 710_566U);
        Assert.DoesNotContain(ParseMessages(alreadyOwned.Sent), packet => packet.Content[3].Contains("Scarlet Token", StringComparison.Ordinal));
        Assert.Contains("token already in inventory", alreadyOwned.Log);
        Assert.DoesNotContain(questFinished.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 710_566U);
        Assert.DoesNotContain(ParseGameItems(questFinished.Sent), packet => packet.ItemId == 710_566U);
        Assert.DoesNotContain(ParseMessages(questFinished.Sent), packet => packet.Content[3].Contains("Scarlet Token", StringComparison.Ordinal));
        Assert.DoesNotContain("map 1015 token reward", questFinished.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family2224Quest1820DeathAddsRedTokenAndDialog()
    {
        var result = await KillMap1015Family2224RedTokenMonsterAsync();

        var token = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 710_562U);
        Assert.Equal((ushort)1, token.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 710_562U &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "You picked up the Red Token. Now hurry up and give it to Felix!");
        Assert.Contains("map 1015 family 2224 Red Token reward", result.Log);
        Assert.Contains("questId=1820", result.Log);
        Assert.Contains("itemId=710562", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family2224Quest1820DeathReportsFullInventory()
    {
        var result = await KillMap1015Family2224RedTokenMonsterAsync(
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(870_000 + index)))
                .ToArray());

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 710_562U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 710_562U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Sorry, your inventory is full. Make some room first.");
        Assert.Contains("reason=\"inventory full\"", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family2224Quest1820DeathSkipsWhenTokenOwned()
    {
        var result = await KillMap1015Family2224RedTokenMonsterAsync(
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 710_562),
            ]);

        Assert.Equal(1, result.UpdatedSession.PlayerItems.InventoryItems.Count(item => item.ItemId == 710_562U));
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 710_562U);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet => packet.Text.Contains("Red Token", StringComparison.Ordinal));
        Assert.Contains("Red Token already in inventory", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family2224Quest1820DeathSkipsWithoutAcceptedQuest()
    {
        var result = await KillMap1015Family2224RedTokenMonsterAsync(questStatus: PlayerQuestStatus.Finished);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 710_562U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 710_562U);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet => packet.Text.Contains("Red Token", StringComparison.Ordinal));
        Assert.DoesNotContain("family 2224 Red Token reward", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family55Quest1820DeathIncrementsBanditL97Objective()
    {
        var result = await KillMap1015Family55BanditL97MonsterAsync(objective0: 48);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_820U);
        Assert.Equal([49U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_820U);
        Assert.Equal([49U], objectiveFrame.Intentions);
        Assert.DoesNotContain(ParseMonsterSpawns(result.Sent), packet => packet.MonsterId == 2_224U);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet => packet.Text.Contains("Bandit Leader L97", StringComparison.Ordinal));
        Assert.Contains("objective below 50", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family55Quest1820DeathSpawnsBanditLeaderAtThreshold()
    {
        var result = await KillMap1015Family55BanditL97MonsterAsync(objective0: 49);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_820U);
        Assert.Equal([50U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_820U);
        Assert.Equal([50U], objectiveFrame.Intentions);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Bandit Leader L97 appeared on Bird Island (262,118). Get rid of him!");

        var spawn = Assert.Single(ParseMonsterSpawns(result.Sent), packet => packet.MonsterId == 2_224U);
        Assert.InRange(spawn.Uid, 900_000U, 999_999U);
        Assert.Equal(830, spawn.X);
        Assert.Equal(584, spawn.Y);
        Assert.True(result.MonsterStates.TryGetDynamicMonsterByUid(
            1_015,
            dynamicId: 0,
            spawn.Uid,
            out var spawned,
            out var spawnedFamily));
        Assert.Equal(2_224U, spawned.MonsterId);
        Assert.Equal(2_224, spawnedFamily.Id);
        Assert.Contains("spawnedFollowup=True", result.Log);
        Assert.Contains("monsterId=2224", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family55Quest1820DeathSkipsSpawnWhenRedTokenOwned()
    {
        var result = await KillMap1015Family55BanditL97MonsterAsync(
            objective0: 49,
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 710_562),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_820U);
        Assert.Equal([50U], quest.Intentions);
        Assert.DoesNotContain(ParseMonsterSpawns(result.Sent), packet => packet.MonsterId == 2_224U);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet => packet.Text.Contains("Bandit Leader L97", StringComparison.Ordinal));
        Assert.Contains("Red Token already in inventory", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family55Quest1820DeathSkipsDuplicateBanditLeaderSpawn()
    {
        var result = await KillMap1015Family55BanditL97MonsterAsync(
            objective0: 49,
            preRegisterBanditLeader: true);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_820U);
        Assert.Equal([50U], quest.Intentions);
        Assert.DoesNotContain(ParseMonsterSpawns(result.Sent), packet => packet.MonsterId == 2_224U);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet => packet.Text.Contains("Bandit Leader L97", StringComparison.Ordinal));
        Assert.Contains("BanditLeaderL97 already visible", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family55Quest1820DeathSkipsWithoutAcceptedQuest()
    {
        var result = await KillMap1015Family55BanditL97MonsterAsync(
            objective0: 49,
            questStatus: PlayerQuestStatus.Finished);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_820U);
        Assert.Equal([49U], quest.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_820U);
        Assert.DoesNotContain(ParseMonsterSpawns(result.Sent), packet => packet.MonsterId == 2_224U);
        Assert.DoesNotContain("family 55 quest 1820", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family55Quest1647And1633DeathIncrementObjectives()
    {
        var result = await KillMap1015Family55BanditL97MonsterAsync(
            objective0: 0,
            quests:
            [
                new PlayerQuestSnapshotEntry(1_647, PlayerQuestStatus.Accepted, 0, [4U]),
                new PlayerQuestSnapshotEntry(1_633, PlayerQuestStatus.Accepted, 0, [2U]),
            ]);

        Assert.Equal([5U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_647U).Intentions);
        Assert.Equal([3U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_633U).Intentions);
        Assert.Equal(2, ParseMessages(result.Sent).Count(packet => packet.Content[3] == "You`ve killed a BanditL97."));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_647U && packet.Intentions.SequenceEqual([5U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_633U && packet.Intentions.SequenceEqual([3U]));
        Assert.Contains("quest 1647 BanditL97 objective", result.Log);
        Assert.Contains("quest 1633 BanditL97 objective", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family55Quest1635DeathAddsFoodSupplyAndMessages()
    {
        var result = await KillMap1015Family55BanditL97MonsterAsync(
            objective0: 0,
            quests:
            [
                new PlayerQuestSnapshotEntry(1_635, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var item = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_468U);
        Assert.Equal((ushort)1, item.StackSize);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a BanditL97.");
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You~received~a~box~of~Food~Supply!");
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 711_468U &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains("questId=1635", result.Log);
        Assert.Contains("Food Supply", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family55Quest1635DeathSendsKillNoticeWhenRateMisses()
    {
        var result = await KillMap1015Family55BanditL97MonsterAsync(
            objective0: 0,
            forceFoodSupplyRate: false,
            quests:
            [
                new PlayerQuestSnapshotEntry(1_635, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_468U);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a BanditL97.");
        Assert.DoesNotContain(ParseMessages(result.Sent), packet => packet.Content[3] == "You~received~a~box~of~Food~Supply!");
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_468U);
        Assert.Contains("Core.Rate(20)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family55Quest1635DeathReportsFullInventoryAfterKillNotice()
    {
        var result = await KillMap1015Family55BanditL97MonsterAsync(
            objective0: 0,
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(880_000 + index)))
                .ToArray(),
            quests:
            [
                new PlayerQuestSnapshotEntry(1_635, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_468U);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a BanditL97.");
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_468U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=\"inventory full\"", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family55Quest1635DeathSkipsOwnedOrFinishedBranches()
    {
        var alreadyOwned = await KillMap1015Family55BanditL97MonsterAsync(
            objective0: 0,
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 711_468),
            ],
            quests:
            [
                new PlayerQuestSnapshotEntry(1_635, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);
        var questFinished = await KillMap1015Family55BanditL97MonsterAsync(
            objective0: 0,
            quests:
            [
                new PlayerQuestSnapshotEntry(1_635, PlayerQuestStatus.Finished, 0, [0U]),
            ]);

        Assert.Equal(1, alreadyOwned.UpdatedSession.PlayerItems.InventoryItems.Count(entry => entry.ItemId == 711_468U));
        Assert.Contains(ParseMessages(alreadyOwned.Sent), packet => packet.Content[3] == "You`ve killed a BanditL97.");
        Assert.DoesNotContain(ParseMessages(alreadyOwned.Sent), packet => packet.Content[3] == "You~received~a~box~of~Food~Supply!");
        Assert.DoesNotContain(ParseGameItems(alreadyOwned.Sent), packet => packet.ItemId == 711_468U);
        Assert.Contains("item count already reached 1", alreadyOwned.Log);
        Assert.DoesNotContain(questFinished.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_468U);
        Assert.DoesNotContain(ParseMessages(questFinished.Sent), packet => packet.Content[3].Contains("BanditL97", StringComparison.Ordinal));
        Assert.DoesNotContain("Food Supply", questFinished.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family55Quest1632DeathAddsBaconAndMessages()
    {
        var result = await KillMap1015Family55BanditL97MonsterAsync(
            objective0: 0,
            quests:
            [
                new PlayerQuestSnapshotEntry(1_632, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var item = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_467U);
        Assert.Equal((ushort)1, item.StackSize);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a BanditL97.");
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You've received a piece of Bacon!");
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 711_467U &&
                packet.Mode == LegacyGameItemPacket.AddItemMode &&
                packet.StackSize == 1);
        Assert.Contains("questId=1632", result.Log);
        Assert.Contains("Bacon", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family55Quest1632DeathStacksBaconUntilFive()
    {
        var result = await KillMap1015Family55BanditL97MonsterAsync(
            objective0: 0,
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 711_467, stackSize: 4),
            ],
            quests:
            [
                new PlayerQuestSnapshotEntry(1_632, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var item = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_467U);
        Assert.Equal((ushort)5, item.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 711_467U &&
                packet.Mode == LegacyGameItemPacket.UpdateItemMode &&
                packet.StackSize == 5);
        Assert.Contains("itemCountBefore=4", result.Log);
        Assert.Contains("itemCountAfter=5", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family55Quest1632DeathSendsKillNoticeWhenRateMisses()
    {
        var result = await KillMap1015Family55BanditL97MonsterAsync(
            objective0: 0,
            forceBaconRate: false,
            quests:
            [
                new PlayerQuestSnapshotEntry(1_632, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_467U);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a BanditL97.");
        Assert.DoesNotContain(ParseMessages(result.Sent), packet => packet.Content[3] == "You've received a piece of Bacon!");
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_467U);
        Assert.Contains("Core.Rate(20)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family55Quest1632DeathReportsFullInventoryAfterKillNotice()
    {
        var result = await KillMap1015Family55BanditL97MonsterAsync(
            objective0: 0,
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(890_000 + index)))
                .ToArray(),
            quests:
            [
                new PlayerQuestSnapshotEntry(1_632, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_467U);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a BanditL97.");
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_467U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=\"inventory full\"", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family55Quest1632DeathSkipsEnoughBaconOrFinishedBranches()
    {
        var enoughBacon = await KillMap1015Family55BanditL97MonsterAsync(
            objective0: 0,
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 711_467, stackSize: 5),
            ],
            quests:
            [
                new PlayerQuestSnapshotEntry(1_632, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);
        var questFinished = await KillMap1015Family55BanditL97MonsterAsync(
            objective0: 0,
            quests:
            [
                new PlayerQuestSnapshotEntry(1_632, PlayerQuestStatus.Finished, 0, [0U]),
            ]);

        Assert.Equal((ushort)5, Assert.Single(enoughBacon.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_467U).StackSize);
        Assert.Contains(ParseMessages(enoughBacon.Sent), packet => packet.Content[3] == "You`ve killed a BanditL97.");
        Assert.DoesNotContain(ParseMessages(enoughBacon.Sent), packet => packet.Content[3] == "You've received a piece of Bacon!");
        Assert.DoesNotContain(ParseGameItems(enoughBacon.Sent), packet => packet.ItemId == 711_467U);
        Assert.Contains("item count already reached 5", enoughBacon.Log);
        Assert.DoesNotContain(questFinished.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_467U);
        Assert.DoesNotContain(ParseMessages(questFinished.Sent), packet => packet.Content[3].Contains("BanditL97", StringComparison.Ordinal));
        Assert.DoesNotContain("Bacon", questFinished.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family19Quest1630DeathIncrementsHawKingL93Objective()
    {
        var result = await KillMap1015Family19HawKingMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_630, PlayerQuestStatus.Accepted, 0, [3U, 4U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_630U);
        Assert.Equal([3U, 5U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_630U);
        Assert.Equal([3U, 5U], objectiveFrame.Intentions);
        Assert.Single(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a HawKingL93.");
        Assert.Contains("quest 1630 HawKingL93 objective", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family19Quest1616And1611DeathIncrementHawKingObjectives()
    {
        var result = await KillMap1015Family19HawKingMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_616, PlayerQuestStatus.Accepted, 0, [7U, 8U]),
                new PlayerQuestSnapshotEntry(1_611, PlayerQuestStatus.Accepted, 0, [2U]),
            ]);

        Assert.Equal([9U, 9U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_616U).Intentions);
        Assert.Equal([3U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_611U).Intentions);
        Assert.Equal(2, ParseMessages(result.Sent).Count(packet => packet.Content[3] == "You`ve killed a HawKing."));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_616U && packet.Intentions.SequenceEqual([9U, 9U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_611U && packet.Intentions.SequenceEqual([3U]));
        Assert.Contains("quest 1616 HawKing objective", result.Log);
        Assert.Contains("quest 1611 HawKing objective", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family19HawKingDeathSkipsFinishedObjectiveBranches()
    {
        var result = await KillMap1015Family19HawKingMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_630, PlayerQuestStatus.Finished, 0, [3U, 4U]),
                new PlayerQuestSnapshotEntry(1_616, PlayerQuestStatus.Finished, 0, [7U, 8U]),
                new PlayerQuestSnapshotEntry(1_611, PlayerQuestStatus.Finished, 0, [2U]),
            ]);

        Assert.Equal([3U, 4U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_630U).Intentions);
        Assert.Equal([7U, 8U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_616U).Intentions);
        Assert.Equal([2U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_611U).Intentions);
        Assert.DoesNotContain(ParseMessages(result.Sent), packet => packet.Content[3].Contains("HawKing", StringComparison.Ordinal));
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid is 1_630U or 1_616U or 1_611U);
        Assert.DoesNotContain("family 19", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family19Quest1814DeathStacksHawkClawAndShowsBox()
    {
        var result = await KillMap1015Family19HawKingMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_814, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 721_910, stackSize: 4),
            ]);

        var item = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_910U);
        Assert.Equal((ushort)5, item.StackSize);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "You've got a Hawk Claw!");
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 721_910U &&
                packet.Mode == LegacyGameItemPacket.UpdateItemMode &&
                packet.StackSize == 5);
        Assert.Contains("questId=1814", result.Log);
        Assert.Contains("Hawk Claw", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family19Quest1814DeathShowsEnoughHawkClawsBox()
    {
        var result = await KillMap1015Family19HawKingMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_814, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 721_910, stackSize: 5),
            ]);

        Assert.Equal((ushort)5, Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_910U).StackSize);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 721_910U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "You've got 5 Hawk Claws! Hurry to deliver them to Doctor Know-it-All!");
        Assert.Contains("item count already reached 5", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family19Quest1813DeathAddsGallbladerrUnlessOwned()
    {
        var result = await KillMap1015Family19HawKingMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_813, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);
        var alreadyOwnsPriorItem = await KillMap1015Family19HawKingMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_813, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 721_908),
            ]);

        var item = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_909U);
        Assert.Equal((ushort)1, item.StackSize);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "You've got a Gallbladerr!");
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 721_909U);
        Assert.Contains("questId=1813", result.Log);
        Assert.Contains("Gallbladerr", result.Log);
        Assert.DoesNotContain(alreadyOwnsPriorItem.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_909U);
        Assert.DoesNotContain(ParseGameItems(alreadyOwnsPriorItem.Sent), packet => packet.ItemId == 721_909U);
        Assert.Contains("already owns Gallbladerr gate item", alreadyOwnsPriorItem.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family19Quest1618DeathAddsFeatherAndReportsFullInventory()
    {
        var result = await KillMap1015Family19HawKingMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_618, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);
        var fullInventory = await KillMap1015Family19HawKingMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_618, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(900_000 + index)))
                .ToArray());

        var item = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_454U);
        Assert.Equal((ushort)1, item.StackSize);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a HawKing.");
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve~got~a~Feather!");
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_454U);
        Assert.Contains("questId=1618", result.Log);
        Assert.Contains("Feather", result.Log);
        Assert.DoesNotContain(fullInventory.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_454U);
        Assert.Contains(ParseMessages(fullInventory.Sent), packet => packet.Content[3] == "You`ve killed a HawKing.");
        Assert.Contains(
            ParseNpcReplies(fullInventory.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=\"inventory full\"", fullInventory.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family19Quest1610DeathGrantsNextOldBook()
    {
        var result = await KillMap1015Family19HawKingMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_610, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 720_987),
                CreateInventoryItem(uid: 2, itemId: 720_988),
            ]);

        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 720_987U);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 720_988U);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 720_989U);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a HawKing.");
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve~got~a~OldBook3!");
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 720_989U);
        Assert.Contains("questId=1610", result.Log);
        Assert.Contains("OldBook3", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family4431179Quest1629DeathIncrementsHawkLeaderObjective()
    {
        var result = await KillMap1015Family4431179HawkLeaderMonsterAsync();

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_629U);
        Assert.Equal([5U], quest.Intentions);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a HawkLeader.");
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_629U && packet.Intentions.SequenceEqual([5U]));
        Assert.Contains("quest 1629 HawkLeader objective", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family78Quest1814And1813DeathAppliesHawkRewards()
    {
        var result = await KillMap1015Family78HawKingL93MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_814, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_813, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 721_910, stackSize: 4),
            ]);

        Assert.Equal((ushort)5, Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_910U).StackSize);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_909U);
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You've got a Hawk Claw!");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You've got a Gallbladerr!");
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 721_910U && packet.Mode == LegacyGameItemPacket.UpdateItemMode);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 721_909U && packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains("questId=1814", result.Log);
        Assert.Contains("questId=1813", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family78ObjectiveBranchesApplyLegacyIncrements()
    {
        var result = await KillMap1015Family78HawKingL93MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_630, PlayerQuestStatus.Accepted, 0, [1U]),
                new PlayerQuestSnapshotEntry(1_628, PlayerQuestStatus.Accepted, 0, [2U]),
                new PlayerQuestSnapshotEntry(1_621, PlayerQuestStatus.Accepted, 0, [3U]),
            ]);

        Assert.Equal([2U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_630U).Intentions);
        Assert.Equal([3U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_628U).Intentions);
        Assert.Equal([4U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_621U).Intentions);
        Assert.Equal(3, ParseMessages(result.Sent).Count(packet => packet.Content[3] == "You`ve killed a HawKingL93."));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_630U && packet.Intentions.SequenceEqual([2U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_628U && packet.Intentions.SequenceEqual([3U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_621U && packet.Intentions.SequenceEqual([4U]));
        Assert.Contains("quest 1630 HawKingL93 objective", result.Log);
        Assert.Contains("quest 1628 HawKingL93 objective", result.Log);
        Assert.Contains("quest 1621 HawKingL93 objective", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family78Quest1624DeathAddsHawkBloodAndReportsFullInventory()
    {
        var result = await KillMap1015Family78HawKingL93MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_624, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);
        var fullInventory = await KillMap1015Family78HawKingL93MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_624, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(910_000 + index)))
                .ToArray());

        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 720_993U);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a HawKingL93.");
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You~received~some~Hawk~Blood!");
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 720_993U);
        Assert.Contains("questId=1624", result.Log);
        Assert.DoesNotContain(fullInventory.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 720_993U);
        Assert.Contains(ParseMessages(fullInventory.Sent), packet => packet.Content[3] == "You`ve killed a HawKingL93.");
        Assert.Contains(ParseNpcReplies(fullInventory.Sent), packet => packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=\"inventory full\"", fullInventory.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family78Quest1622DeathGrantsHawkClawThenFang()
    {
        var claw = await KillMap1015Family78HawKingL93MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_622, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);
        var fang = await KillMap1015Family78HawKingL93MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_622, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 711_456),
            ]);

        Assert.Contains(claw.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_456U);
        Assert.DoesNotContain(claw.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_457U);
        Assert.Contains(ParseMessages(claw.Sent), packet => packet.Content[3] == "You~received~1~Hawk~Claw!");
        Assert.Contains(ParseGameItems(claw.Sent), packet => packet.ItemId == 711_456U);
        Assert.Contains(fang.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_456U);
        Assert.Contains(fang.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_457U);
        Assert.Contains(ParseMessages(fang.Sent), packet => packet.Content[3] == "You~received~1~Hawk~Fang!");
        Assert.Contains(ParseGameItems(fang.Sent), packet => packet.ItemId == 711_457U);
        Assert.Contains("questId=1622", claw.Log);
        Assert.Contains("questId=1622", fang.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family77Quest1812DeathStacksBirdBloodAndShowsEnoughBox()
    {
        var result = await KillMap1015Family77BirdmanL88MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_812, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 721_906, stackSize: 9),
            ]);
        var enough = await KillMap1015Family77BirdmanL88MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_812, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 721_906, stackSize: 10),
            ]);

        Assert.Equal((ushort)10, Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_906U).StackSize);
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You've got some Bird Blood!");
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 721_906U && packet.Mode == LegacyGameItemPacket.UpdateItemMode);
        Assert.Contains("questId=1812", result.Log);
        Assert.Contains("Bird Blood", result.Log);
        Assert.Equal((ushort)10, Assert.Single(enough.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_906U).StackSize);
        Assert.DoesNotContain(ParseGameItems(enough.Sent), packet => packet.ItemId == 721_906U);
        Assert.Contains(ParseNpcReplies(enough.Sent), packet => packet.Text == "You've collected enough Bird Blood!");
        Assert.Contains("item count already reached 10", enough.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family77ItemRewardBranchesAddBirdmanL88Drops()
    {
        var result = await KillMap1015Family77BirdmanL88MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_599, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_601, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_605, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_445U);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_446U);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_449U);
        Assert.Equal(3, ParseMessages(result.Sent).Count(packet => packet.Content[3] == "You`ve killed a BirdmanL88."));
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve~got~a~Bird~Beak!");
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve~got~a~Sharp~Claw!");
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You received a Birdman Feather!");
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_445U && packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_446U && packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_449U && packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains("questId=1599", result.Log);
        Assert.Contains("questId=1601", result.Log);
        Assert.Contains("questId=1605", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family77ItemRewardReportsFullInventoryAfterKillNotice()
    {
        var result = await KillMap1015Family77BirdmanL88MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_599, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(920_000 + index)))
                .ToArray());

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_445U);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a BirdmanL88.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "Please make 1 more space in your inventory.");
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_445U);
        Assert.Contains("reason=\"inventory full\"", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family77ObjectiveBranchesIncrementBirdmanL88Quests()
    {
        var result = await KillMap1015Family77BirdmanL88MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_604, PlayerQuestStatus.Accepted, 0, [2U]),
                new PlayerQuestSnapshotEntry(1_598, PlayerQuestStatus.Accepted, 0, [7U]),
            ]);

        Assert.Equal([3U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_604U).Intentions);
        Assert.Equal([8U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_598U).Intentions);
        Assert.Equal(2, ParseMessages(result.Sent).Count(packet => packet.Content[3] == "You`ve killed a BirdmanL88."));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_604U && packet.Intentions.SequenceEqual([3U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_598U && packet.Intentions.SequenceEqual([8U]));
        Assert.Contains("quest 1604 BirdmanL88 objective", result.Log);
        Assert.Contains("quest 1598 BirdmanL88 objective", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family18DeathDropsRedItem729094FloorItem()
    {
        var result = await KillMap1015Family18BirdmanMonsterAsync([]);

        var floorItem = Assert.Single(ParseFloorItems(result.Sent), packet => packet.ItemId == 729_094U);
        Assert.Equal((byte)14, floorItem.PlusOrColor);
        Assert.Equal((uint)KillerUid, floorItem.ItemOwnerUid);
        Assert.Equal(LegacyFloorItemDropType.Visible, floorItem.DropType);
        Assert.True(result.FloorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.True(runtimeEntry.OwnerOnly);
        Assert.Equal(729_094U, runtimeEntry.Packet.ItemId);
        Assert.Contains("family 18 Birdman floor drop", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family18QuestRewardsAndObjectiveApplyBirdmanBranches()
    {
        var result = await KillMap1015Family18BirdmanMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_812, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_626, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_597, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_592, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_591, PlayerQuestStatus.Accepted, 0, [4U]),
            ],
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 721_906, stackSize: 9),
            ]);

        Assert.Equal((ushort)10, Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_906U).StackSize);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_464U);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 720_986U);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_438U);
        Assert.Equal([5U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_591U).Intentions);
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You've got some Bird Blood!");
        Assert.Equal(4, ParseMessages(result.Sent).Count(packet => packet.Content[3] == "You`ve killed a Birdman."));
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You~received~1~Golden~Beak!");
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve~got~some~Birdman~Feathers!");
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You received a Birdman Arm!");
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_591U && packet.Intentions.SequenceEqual([5U]));
        Assert.Contains("questId=1812", result.Log);
        Assert.Contains("questId=1626", result.Log);
        Assert.Contains("questId=1597", result.Log);
        Assert.Contains("questId=1592", result.Log);
        Assert.Contains("quest 1591 Birdman objective", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family18ItemRewardReportsFullInventoryAfterKillNotice()
    {
        var result = await KillMap1015Family18BirdmanMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_626, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(930_000 + index)))
                .ToArray());

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_464U);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a Birdman.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "Please make 1 more space in your inventory.");
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_464U);
        Assert.Contains("reason=\"inventory full\"", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family18Quest1593DeathAddsToolBagEffectAndDialog()
    {
        var result = await KillMap1015Family18BirdmanMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_593, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_439U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_439U);
        Assert.Contains(
            ParseStringPackets(result.Sent),
            packet => packet.Id == LegacyStringId.Effect &&
                packet.Strings.SequenceEqual(["relive1"]));
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~got~Xu~Fan`s~Tool~Bag!");
        Assert.Contains("questId=1593", result.Log);
        Assert.Contains("Xu Fan", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family2163Quest525DeathConvertsBudHandkerchiefAndShowsHint()
    {
        var result = await KillMap1000Family2163CapriceLeaderMonsterAsync(
            questStatus: PlayerQuestStatus.Accepted,
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 721_777),
            ]);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_777U);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_782U);
        Assert.Contains(
            ParseItemUsage(result.Sent),
            packet => packet.Id == 1U &&
                packet.Action == MsgItemUsageAction.RemoveInventory);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 721_782U &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.Text == "Your~gut~feeling~told~you~that~the~Bud~Handkerchief~has~something~to~do~with~Yang~Yun~in~Bird~Island.~Find~him~(732,516)~now.");
        Assert.Contains("map 1000 family 2163 quest 525", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family2163Quest525DeathKeepsExistingBudVillagerHandkerchief()
    {
        var result = await KillMap1000Family2163CapriceLeaderMonsterAsync(
            questStatus: PlayerQuestStatus.Accepted,
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 721_782),
            ]);

        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_782U);
        Assert.DoesNotContain(ParseItemUsage(result.Sent), packet => packet.Action == MsgItemUsageAction.RemoveInventory);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 721_782U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.Text == "Your~gut~feeling~told~you~that~the~Bud~Handkerchief~has~something~to~do~with~Yang~Yun~in~Bird~Island.~Find~him~(732,516)~now.");
        Assert.Contains("alreadyHasBudVillagerHandkerchief=True", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family2163Quest525DeathReportsFullInventoryBeforeConversion()
    {
        var result = await KillMap1000Family2163CapriceLeaderMonsterAsync(
            questStatus: PlayerQuestStatus.Accepted,
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => index == 1
                    ? CreateInventoryItem(uid: 1, itemId: 721_777)
                    : CreateInventoryItem((uint)index, (uint)(930_000 + index)))
                .ToArray());

        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_777U);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_782U);
        Assert.DoesNotContain(ParseItemUsage(result.Sent), packet => packet.Action == MsgItemUsageAction.RemoveInventory);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 721_782U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=\"inventory full\"", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family2179Quest525DeathSpawnsCapriceLeaderAndShowsDialog()
    {
        var result = await KillMap1000Family2179CapriceMonsterAsync();

        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.Text == "Caprice~Leader~has~appeared~at~(170,230).");
        var spawn = Assert.Single(ParseMonsterSpawns(result.Sent), packet => packet.MonsterId == 2_163U);
        Assert.InRange(spawn.Uid, 900_000U, 999_999U);
        Assert.Equal(170, spawn.X);
        Assert.Equal(230, spawn.Y);
        Assert.True(result.MonsterStates.TryGetDynamicMonsterByUid(
            1_000,
            dynamicId: 2_468,
            spawn.Uid,
            out var spawned,
            out var spawnedFamily));
        Assert.Equal(2_163U, spawned.MonsterId);
        Assert.Equal(2_163, spawnedFamily.Id);
        Assert.Contains("map 1000 family 2179 quest 525", result.Log);
        Assert.Contains("spawned=True", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family2179Quest525DeathSkipsWhenRateMissesOrHandkerchiefOwned()
    {
        var rateMiss = await KillMap1000Family2179CapriceMonsterAsync(forcedRate: false);
        Assert.DoesNotContain(ParseMonsterSpawns(rateMiss.Sent), packet => packet.MonsterId == 2_163U);
        Assert.DoesNotContain(ParseNpcReplies(rateMiss.Sent), packet => packet.Text.Contains("Caprice~Leader", StringComparison.Ordinal));
        Assert.Contains("Core.Rate(20)=false", rateMiss.Log);

        var alreadyOwned = await KillMap1000Family2179CapriceMonsterAsync(
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 721_782),
            ]);
        Assert.DoesNotContain(ParseMonsterSpawns(alreadyOwned.Sent), packet => packet.MonsterId == 2_163U);
        Assert.DoesNotContain(ParseNpcReplies(alreadyOwned.Sent), packet => packet.Text.Contains("Caprice~Leader", StringComparison.Ordinal));
        Assert.Contains("Bud Villager Handkerchief already in inventory", alreadyOwned.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family55434417Quest1498DeathIncrementsAngryBladeGhostObjective()
    {
        var result = await KillMap1000Family55434417AngryBladeGhostMonsterAsync(objective0: 1);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_498U);
        Assert.Equal([2U], quest.Intentions);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a AngryBladeGhost.");
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_498U && packet.Intentions.SequenceEqual([2U]));
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet => packet.Text.Contains("Sculptor~He", StringComparison.Ordinal));
        Assert.Contains("map 1000 family 55434417 quest 1498", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family55434417Quest1498DeathShowsCompletionDialogAtThree()
    {
        var result = await KillMap1000Family55434417AngryBladeGhostMonsterAsync(objective0: 2);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_498U);
        Assert.Equal([3U], quest.Intentions);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a AngryBladeGhost.");
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.Text == "Sculptor~He~has~finished~the~inscription!~Go~visit~him,~right~away.");
        Assert.Contains("completed=True", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family76BladeGhostDeathAppliesItemRewardBranches()
    {
        var result = await KillMap1000Family76BladeGhostMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_803, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_801, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_506, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_500, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 721_846, stackSize: 4),
            ]);

        Assert.Equal(
            5,
            result.UpdatedSession.PlayerItems.InventoryItems
                .Where(entry => entry.ItemId == 721_846U)
                .Sum(entry => entry.StackSize));
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_843U);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 720_968U);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_407U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 721_846U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 721_843U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 720_968U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_407U);
        Assert.Equal(4, ParseStringPackets(result.Sent).Count(packet =>
            packet.Id == LegacyStringId.Effect &&
            packet.Strings.SequenceEqual(["relive1"])));
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You~picked~up~a~Blade~Ghost~Fang~and~put~it~in~your~inventory.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You~obtained~the~Stone~Spell.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~the~Level~83~Blade~Ghost~and~obtained~an~Antidote.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~a~Level~83~Blade~Ghost~and~cut~its~ear.");
        Assert.Contains("questId=1803", result.Log);
        Assert.Contains("questId=1801", result.Log);
        Assert.Contains("questId=1506", result.Log);
        Assert.Contains("questId=1500", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family76BladeGhostDeathIncrementsQuest1499AndShowsCompletion()
    {
        var result = await KillMap1000Family76BladeGhostMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_499, PlayerQuestStatus.Accepted, 0, [29U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_499U);
        Assert.Equal([30U], quest.Intentions);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a Level 83 Blade Ghost.");
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_499U && packet.Intentions.SequenceEqual([30U]));
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.Text == "You`ve~killed~30~Level~83~Blade~Ghosts.~Go~report~to~Spring~General~Xu.");
        Assert.Contains("questId=1499", result.Log);
        Assert.Contains("completed=True", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family76BladeGhostDeathReportsFullInventoryForItemBranch()
    {
        var result = await KillMap1000Family76BladeGhostMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_801, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(930_000 + index)))
                .ToArray());

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_843U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 721_843U);
        Assert.DoesNotContain(ParseStringPackets(result.Sent), packet => packet.Strings.SequenceEqual(["relive1"]));
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=\"inventory full\"", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family76BladeGhostDeathSkipsRateMissAndOwnedCaps()
    {
        var rateMiss = await KillMap1000Family76BladeGhostMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_803, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceFangRate: false);

        Assert.DoesNotContain(rateMiss.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_846U);
        Assert.DoesNotContain(ParseNpcReplies(rateMiss.Sent), packet => packet.Text.Contains("Blade~Ghost~Fang", StringComparison.Ordinal));
        Assert.Contains("Core.Rate(50)=false", rateMiss.Log);

        var alreadyOwned = await KillMap1000Family76BladeGhostMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_803, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_801, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 721_846, stackSize: 5),
                CreateInventoryItem(uid: 2, itemId: 721_843),
            ]);

        Assert.Equal(
            5,
            alreadyOwned.UpdatedSession.PlayerItems.InventoryItems
                .Where(entry => entry.ItemId == 721_846U)
                .Sum(entry => entry.StackSize));
        Assert.Single(alreadyOwned.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_843U);
        Assert.DoesNotContain(ParseGameItems(alreadyOwned.Sent), packet => packet.ItemId is 721_846U or 721_843U);
        Assert.Contains("item count already reached 5", alreadyOwned.Log);
        Assert.Contains("Stone Spell already in inventory", alreadyOwned.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family17BladeGhostDeathAppliesInventoryRewardBranches()
    {
        var result = await KillMap1000Family17BladeGhostMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_501, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_508, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_497, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_413U);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_414U);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_409U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_413U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_414U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_409U);
        Assert.Equal(3, ParseStringPackets(result.Sent).Count(packet =>
            packet.Id == LegacyStringId.Effect &&
            packet.Strings.SequenceEqual(["relive1"])));
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~the~Blade~Ghost~and~cut~its~ear.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~the~Blade~Ghost~and~obtained~a~Straight~Hook.~Deliver~it~to~Elder~Jiang.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~the~Blade~Ghost~and~found~a~Sharp~Axe.~Deliver~it~to~Sculptor~He.");
        Assert.Contains("questId=1501", result.Log);
        Assert.Contains("questId=1508", result.Log);
        Assert.Contains("questId=1497", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family17BladeGhostDeathAppliesObjectiveBranchesAndBluntSword()
    {
        var result = await KillMap1000Family17BladeGhostMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_802, PlayerQuestStatus.Accepted, 0, [199U]),
                new PlayerQuestSnapshotEntry(1_493, PlayerQuestStatus.Accepted, 0, [49U]),
            ]);

        Assert.Equal([200U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_802U).Intentions);
        Assert.Equal([50U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_493U).Intentions);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 721_845U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 721_845U);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve~killed~the~Blade~Ghost.");
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_802U && packet.Intentions.SequenceEqual([200U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_493U && packet.Intentions.SequenceEqual([50U]));
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~200~Blade~Ghosts~and~found~a~Blunt~Sword!");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~enough~Blade~Ghosts.~Go~report~to~Spring~General~Xu.");
        Assert.Contains("questId=1802", result.Log);
        Assert.Contains("questId=1493", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family17BladeGhostDeathDropsSharpBladeFloorItem()
    {
        var result = await KillMap1000Family17BladeGhostMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_494, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var floorItem = Assert.Single(ParseFloorItems(result.Sent), packet => packet.ItemId == 711_406U);
        Assert.Equal((byte)14, floorItem.PlusOrColor);
        Assert.Equal((uint)KillerUid, floorItem.ItemOwnerUid);
        Assert.Equal(LegacyFloorItemDropType.Visible, floorItem.DropType);
        Assert.True(result.FloorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.True(runtimeEntry.OwnerOnly);
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~the~Blade~Ghost~and~obtained~a~Sharp~Blade.");
        Assert.Contains(ParseStringPackets(result.Sent), packet => packet.Strings.SequenceEqual(["relive1"]));
        Assert.Contains("questId=1494", result.Log);
        Assert.Contains("floor drop", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family17BladeGhostDeathHandlesRateMissOwnedSharpBladesAndFullInventory()
    {
        var rateMiss = await KillMap1000Family17BladeGhostMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_508, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceStraightHookRate: false);
        Assert.DoesNotContain(rateMiss.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_414U);
        Assert.Contains("Core.Rate(25)=false", rateMiss.Log);

        var fullInventory = await KillMap1000Family17BladeGhostMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_501, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(930_000 + index)))
                .ToArray());
        Assert.DoesNotContain(fullInventory.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_413U);
        Assert.Contains(ParseNpcReplies(fullInventory.Sent), packet => packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=\"inventory full\"", fullInventory.Log);

        var alreadyOwnedSharpBlades = await KillMap1000Family17BladeGhostMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_494, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceSharpBladeRate: false,
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 711_406, stackSize: 5),
            ]);
        Assert.Contains(
            ParseMessages(alreadyOwnedSharpBlades.Sent),
            packet => packet.Content[3] == "You`ve gathered 5 Sharp Blades. Hurry to deliver them to Spring Vice General Ou!");
        Assert.DoesNotContain(ParseFloorItems(alreadyOwnedSharpBlades.Sent), packet => packet.ItemId == 711_406U);
    }

    [Theory]
    [InlineData(0, 721_952, "You Maple Stone came out from the Rock Monster`s body. Pick it up.")]
    [InlineData(1, 721_953, "A Ocean Stone came out from the Rock Monster`s body. Pick it up.")]
    [InlineData(2, 721_951, "A Leaf Stone came out from the Rock Monster`s body. Pick it up.")]
    public async Task HandlerMap1000Family75RockMonsterDeathUsesSourceStoneChoice(
        int stoneChoice,
        int expectedItemId,
        string expectedMessage)
    {
        var result = await KillMap1000Family75RockMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_797, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            stoneChoice: stoneChoice);

        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == (uint)expectedItemId);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == (uint)expectedItemId);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == expectedMessage);
        Assert.Contains("questId=1797", result.Log);
        Assert.Contains($"stoneChoice={stoneChoice}", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family75RockMonsterDeathAppliesInventoryRewardBranches()
    {
        var result = await KillMap1000Family75RockMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_796, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_485, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_491, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 710_581, stackSize: 9),
            ]);

        Assert.Equal(
            10,
            result.UpdatedSession.PlayerItems.InventoryItems
                .Where(entry => entry.ItemId == 710_581U)
                .Sum(entry => entry.StackSize));
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_402U);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_405U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 710_581U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_402U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_405U);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "A Marble came out from the Rock Monster`s body. Check your inventory.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "Deliver~the~Monster~Tail~to~Convoy~Vice~Leader~Ling.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~the~Level~78~Rock~Monster~and~obtained~a~Thunder~Stone.~Deliver~it~to~Ironsmith~Li.");
        Assert.Contains("questId=1796", result.Log);
        Assert.Contains("questId=1485", result.Log);
        Assert.Contains("questId=1491", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family75RockMonsterDeathIncrementsQuest1484AndShowsCompletion()
    {
        var result = await KillMap1000Family75RockMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_484, PlayerQuestStatus.Accepted, 0, [29U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_484U);
        Assert.Equal([30U], quest.Intentions);
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_484U && packet.Intentions.SequenceEqual([30U]));
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.Text == "You`ve~killed~30~Level~78~Rock~Monsters.~Go~report~to~Convoy~Leader~Gu.");
        Assert.Contains("questId=1484", result.Log);
        Assert.Contains("completed=True", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family75RockMonsterDeathHandlesRateMissOwnedMarblesAndFullInventory()
    {
        var rateMiss = await KillMap1000Family75RockMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_491, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceThunderStoneRate: false);

        Assert.DoesNotContain(rateMiss.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_405U);
        Assert.DoesNotContain(ParseNpcReplies(rateMiss.Sent), packet => packet.Text.Contains("Thunder~Stone", StringComparison.Ordinal));
        Assert.Contains("Core.Rate(20)=false", rateMiss.Log);

        var fullInventory = await KillMap1000Family75RockMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_485, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(930_000 + index)))
                .ToArray());

        Assert.DoesNotContain(fullInventory.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_402U);
        Assert.Contains(ParseNpcReplies(fullInventory.Sent), packet => packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=\"inventory full\"", fullInventory.Log);

        var alreadyOwnedMarbles = await KillMap1000Family75RockMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_796, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 710_581, stackSize: 10),
            ]);

        Assert.Equal(
            10,
            alreadyOwnedMarbles.UpdatedSession.PlayerItems.InventoryItems
                .Where(entry => entry.ItemId == 710_581U)
                .Sum(entry => entry.StackSize));
        Assert.DoesNotContain(ParseGameItems(alreadyOwnedMarbles.Sent), packet => packet.ItemId == 710_581U);
        Assert.Contains(ParseMessages(alreadyOwnedMarbles.Sent), packet => packet.Content[3] == "You've collected 10 Marbles. Take them to Farmer Lynn!");
        Assert.Contains("item count already reached 10", alreadyOwnedMarbles.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family51RockMonsterDeathAppliesObjectiveAndInventoryRewards()
    {
        var result = await KillMap1000Family51RockMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_479, PlayerQuestStatus.Accepted, 0, [49U]),
                new PlayerQuestSnapshotEntry(1_501, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_478, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_479U);
        Assert.Equal([50U], quest.Intentions);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_412U);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 720_970U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_412U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 720_970U);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve~killed~the~Rock~Monster.");
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_479U && packet.Intentions.SequenceEqual([50U]));
        Assert.Contains(ParseStringPackets(result.Sent), packet => packet.Strings.SequenceEqual(["relive1"]));
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~50~Rock~Monsters.~Go~report~to~General~ZhuGe.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~the~Rock~Monster~and~obtained~Rock~Monster`s~Tail.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~the~Rock~Monster~and~obtained~a~slabstone.");
        Assert.Contains("questId=1479", result.Log);
        Assert.Contains("questId=1501", result.Log);
        Assert.Contains("questId=1478", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family51RockMonsterDeathDropsSourceFloorItems()
    {
        var result = await KillMap1000Family51RockMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_480, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_481, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var colorfulStone = Assert.Single(ParseFloorItems(result.Sent), packet => packet.ItemId == 711_349U);
        Assert.Equal((byte)14, colorfulStone.PlusOrColor);
        Assert.Equal((uint)KillerUid, colorfulStone.ItemOwnerUid);
        Assert.Equal(LegacyFloorItemDropType.Visible, colorfulStone.DropType);
        Assert.True(result.FloorItems.TryGetVisibleEntry(colorfulStone.Uid, out var colorfulRuntimeEntry));
        Assert.True(colorfulRuntimeEntry.OwnerOnly);

        var grindstone = Assert.Single(ParseFloorItems(result.Sent), packet => packet.ItemId == 711_399U);
        Assert.Equal((byte)14, grindstone.PlusOrColor);
        Assert.Equal((uint)KillerUid, grindstone.ItemOwnerUid);
        Assert.Equal(LegacyFloorItemDropType.Visible, grindstone.DropType);
        Assert.True(result.FloorItems.TryGetVisibleEntry(grindstone.Uid, out var grindstoneRuntimeEntry));
        Assert.True(grindstoneRuntimeEntry.OwnerOnly);

        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~the~Rock~Monster~and~obtained~a~Colorful~Stone.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~the~Rock~Monster~and~obtained~a~Grindstone.");
        Assert.Contains("questId=1480", result.Log);
        Assert.Contains("questId=1481", result.Log);
        Assert.Contains("floor drop", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family51RockMonsterDeathHandlesRateMissAndFullInventory()
    {
        var rateMiss = await KillMap1000Family51RockMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_480, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_481, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceColorfulStoneRate: false,
            forceGrindstoneRate: false);

        Assert.DoesNotContain(ParseFloorItems(rateMiss.Sent), packet => packet.ItemId is 711_349U or 711_399U);
        Assert.Contains("Core.Rate(20)=false", rateMiss.Log);
        Assert.Contains("Core.Rate(30)=false", rateMiss.Log);

        var fullInventory = await KillMap1000Family51RockMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_501, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(930_000 + index)))
                .ToArray());

        Assert.DoesNotContain(fullInventory.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_412U);
        Assert.DoesNotContain(ParseGameItems(fullInventory.Sent), packet => packet.ItemId == 711_412U);
        Assert.DoesNotContain(ParseStringPackets(fullInventory.Sent), packet => packet.Strings.SequenceEqual(["relive1"]));
        Assert.Contains(ParseNpcReplies(fullInventory.Sent), packet => packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=\"inventory full\"", fullInventory.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family74HillMonsterDeathDropsSecretLetterWhenQuest1477NotFinished()
    {
        var result = await KillMap1000Family74HillMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_477, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var floorItem = Assert.Single(ParseFloorItems(result.Sent), packet => packet.ItemId == 720_975U);
        Assert.Equal((byte)14, floorItem.PlusOrColor);
        Assert.Equal((uint)KillerUid, floorItem.ItemOwnerUid);
        Assert.Equal(LegacyFloorItemDropType.Visible, floorItem.DropType);
        Assert.True(result.FloorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.True(runtimeEntry.OwnerOnly);
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~the~Hill~Monster~and~received~a~Secret~Letter.");
        Assert.Contains("questId=1477", result.Log);
        Assert.Contains("floor drop", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family74HillMonsterDeathAppliesObjectiveBranches()
    {
        var result = await KillMap1000Family74HillMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_471, PlayerQuestStatus.Accepted, 0, [29U]),
                new PlayerQuestSnapshotEntry(1_476, PlayerQuestStatus.Accepted, 0, [40U, 9U]),
                new PlayerQuestSnapshotEntry(1_472, PlayerQuestStatus.Accepted, 0, [19U]),
            ],
            forceSecretLetterRate: false);

        Assert.Equal([30U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_471U).Intentions);
        Assert.Equal([40U, 10U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_476U).Intentions);
        Assert.Equal([20U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_472U).Intentions);
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_471U && packet.Intentions.SequenceEqual([30U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_476U && packet.Intentions.SequenceEqual([40U, 10U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_472U && packet.Intentions.SequenceEqual([20U]));
        Assert.True(ParseMessages(result.Sent).Count(packet => packet.Content[3] == "You`ve~killed~a~Level~73~Hill~Monster.") >= 3);
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~30~Hill~Monsters~L73.~Go~report~to~Han~Cheng.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~40~Hill~Monsters~and~10~Hill~Monsters~L73.~Go~report~to~Han~Cheng.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~20~Level~73~Hill~Monsters.~Go~report~to~Ironsmith~Li.");
        Assert.Contains("questId=1471", result.Log);
        Assert.Contains("questId=1476", result.Log);
        Assert.Contains("questId=1472", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family74HillMonsterDeathAppliesArmorReward()
    {
        var result = await KillMap1000Family74HillMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_473, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceSecretLetterRate: false);

        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_416U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_416U);
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~a~Level~73~Hill~Monster~and~obtained~an~armor.");
        Assert.Contains("questId=1473", result.Log);
        Assert.Contains("itemId=711416", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family74HillMonsterDeathHandlesRateMissFinishedSecretLetterAndFullInventory()
    {
        var rateMiss = await KillMap1000Family74HillMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_477, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_473, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceSecretLetterRate: false,
            forceArmorRate: false);

        Assert.DoesNotContain(ParseFloorItems(rateMiss.Sent), packet => packet.ItemId == 720_975U);
        Assert.DoesNotContain(rateMiss.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_416U);
        Assert.Contains("Core.Rate(5)=false", rateMiss.Log);
        Assert.Contains("Core.Rate(20)=false", rateMiss.Log);

        var finishedSecretLetter = await KillMap1000Family74HillMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_477, PlayerQuestStatus.Finished, 0, [0U]),
            ]);
        Assert.DoesNotContain(ParseFloorItems(finishedSecretLetter.Sent), packet => packet.ItemId == 720_975U);
        Assert.Contains("quest already finished", finishedSecretLetter.Log);

        var fullInventory = await KillMap1000Family74HillMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_473, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceSecretLetterRate: false,
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(930_000 + index)))
                .ToArray());

        Assert.DoesNotContain(fullInventory.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_416U);
        Assert.DoesNotContain(ParseGameItems(fullInventory.Sent), packet => packet.ItemId == 711_416U);
        Assert.Contains(ParseNpcReplies(fullInventory.Sent), packet => packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=\"inventory full\"", fullInventory.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family14SandMonsterDeathAppliesInventoryRewards()
    {
        var result = await KillMap1000Family14SandMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_458, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_501, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_453, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_341U);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_410U);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_338U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_341U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_410U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_338U);
        Assert.Equal(2, ParseStringPackets(result.Sent).Count(packet => packet.Strings.SequenceEqual(["relive1"])));
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~the~Sand~Monster~and~received~a~Sandbag.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~the~Sand~Monster~and~received~a~Sand~Pill.");
        Assert.Contains("questId=1458", result.Log);
        Assert.Contains("questId=1501", result.Log);
        Assert.Contains("questId=1453", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family14SandMonsterDeathAppliesObjectiveBranches()
    {
        var result = await KillMap1000Family14SandMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_464, PlayerQuestStatus.Accepted, 0, [39U]),
                new PlayerQuestSnapshotEntry(1_451, PlayerQuestStatus.Accepted, 0, [49U]),
            ]);

        Assert.Equal([40U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_464U).Intentions);
        Assert.Equal([50U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_451U).Intentions);
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_464U && packet.Intentions.SequenceEqual([40U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_451U && packet.Intentions.SequenceEqual([50U]));
        Assert.True(ParseMessages(result.Sent).Count(packet => packet.Content[3] == "You've~killed~a~Sand~Monster.") >= 2);
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~40~Sand~Monsters.~After~you~kill~another~10~Level~68~Sand~Monsters,~you~can~go~report~to~Ke~Yulun.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet => packet.Text == "You`ve~killed~50~Sand~Monsters.~Go~report~to~the~DC~Vice~Captain.");
        Assert.Contains("questId=1464", result.Log);
        Assert.Contains("questId=1451", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family14SandMonsterDeathHandlesRateMissAndFullInventory()
    {
        var rateMiss = await KillMap1000Family14SandMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_458, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_501, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_453, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceSandbagRate: false,
            forceSandPillRate: false,
            forceQuest1453Rate: false);

        Assert.DoesNotContain(rateMiss.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId is 711_341U or 711_410U or 711_338U);
        Assert.DoesNotContain(ParseGameItems(rateMiss.Sent), packet => packet.ItemId is 711_341U or 711_410U or 711_338U);
        Assert.DoesNotContain(ParseStringPackets(rateMiss.Sent), packet => packet.Strings.SequenceEqual(["relive1"]));
        Assert.Contains("Core.Rate(30)=false", rateMiss.Log);
        Assert.Contains("Core.Rate(40)=false", rateMiss.Log);
        Assert.Contains("Core.Rate(20)=false", rateMiss.Log);

        var fullInventory = await KillMap1000Family14SandMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_458, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(930_000 + index)))
                .ToArray());

        Assert.DoesNotContain(fullInventory.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_341U);
        Assert.DoesNotContain(ParseGameItems(fullInventory.Sent), packet => packet.ItemId == 711_341U);
        Assert.DoesNotContain(ParseStringPackets(fullInventory.Sent), packet => packet.Strings.SequenceEqual(["relive1"]));
        Assert.Contains(ParseNpcReplies(fullInventory.Sent), packet => packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=\"inventory full\"", fullInventory.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family73Level68SandMonsterDeathAppliesObjectiveBranches()
    {
        var result = await KillMap1000Family73Level68SandMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_464, PlayerQuestStatus.Accepted, 0, [40U, 9U]),
                new PlayerQuestSnapshotEntry(1_463, PlayerQuestStatus.Accepted, 0, [7U]),
                new PlayerQuestSnapshotEntry(1_460, PlayerQuestStatus.Accepted, 0, [29U]),
            ]);

        Assert.Equal([40U, 10U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_464U).Intentions);
        Assert.Equal([8U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_463U).Intentions);
        Assert.Equal([30U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_460U).Intentions);
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_464U && packet.Intentions.SequenceEqual([40U, 10U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_463U && packet.Intentions.SequenceEqual([8U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_460U && packet.Intentions.SequenceEqual([30U]));
        Assert.True(ParseMessages(result.Sent).Count(packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "You've killed a Level 68 Sand Monster.") >= 3);
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            packet.Text == "You`ve~killed~40~Sand~Monsters~and~10~Level~68~Sand~Monsters.~Go~report~to~KeYulun.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            packet.Text == "You`ve~killed~30~Level~68~Sand~Monsters.~Go~report~to~the~DC~Vice~Captain.");
        Assert.Contains("questId=1464", result.Log);
        Assert.Contains("questId=1463", result.Log);
        Assert.Contains("questId=1460", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family73Level68SandMonsterDeathDropsSandEssenceFloorItem()
    {
        var result = await KillMap1000Family73Level68SandMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_461, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var sandEssence = Assert.Single(ParseFloorItems(result.Sent), packet => packet.ItemId == 711_343U);
        Assert.InRange(sandEssence.X, (ushort)166, (ushort)174);
        Assert.InRange(sandEssence.Y, (ushort)226, (ushort)234);
        Assert.Equal((byte)14, sandEssence.PlusOrColor);
        Assert.Equal((uint)KillerUid, sandEssence.ItemOwnerUid);
        Assert.Equal(LegacyFloorItemDropType.Visible, sandEssence.DropType);
        Assert.True(result.FloorItems.TryGetVisibleEntry(sandEssence.Uid, out var runtimeEntry));
        Assert.True(runtimeEntry.OwnerOnly);
        Assert.Equal(711_343U, runtimeEntry.Packet.ItemId);
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            packet.Text == "You`ve~killed~the~Level~68~Sand~Monster~and~received~a~Sand~Essence.");
        Assert.Contains("questId=1461", result.Log);
        Assert.Contains("floor drop", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family73Level68SandMonsterDeathSkipsSandEssenceWhenRateMisses()
    {
        var result = await KillMap1000Family73Level68SandMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_461, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceSandEssenceRate: false);

        Assert.DoesNotContain(ParseFloorItems(result.Sent), packet => packet.ItemId == 711_343U);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet =>
            packet.Text == "You`ve~killed~the~Level~68~Sand~Monster~and~received~a~Sand~Essence.");
        Assert.Contains("Core.Rate(20)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family72SnakemanL63DeathDropsQuest1341FloorItem()
    {
        var result = await KillMap1020Family72SnakemanL63MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_341, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var floorItem = Assert.Single(ParseFloorItems(result.Sent), packet => packet.ItemId == 720_858U);
        Assert.InRange(floorItem.X, (ushort)166, (ushort)174);
        Assert.InRange(floorItem.Y, (ushort)226, (ushort)234);
        Assert.Equal((byte)14, floorItem.PlusOrColor);
        Assert.Equal((uint)KillerUid, floorItem.ItemOwnerUid);
        Assert.Equal(LegacyFloorItemDropType.Visible, floorItem.DropType);
        Assert.True(result.FloorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.True(runtimeEntry.OwnerOnly);
        Assert.Equal(720_858U, runtimeEntry.Packet.ItemId);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet =>
            packet.Text.Contains("Snakeman", StringComparison.OrdinalIgnoreCase) ||
            packet.Text.Contains("720858", StringComparison.Ordinal));
        Assert.DoesNotContain(ParseMessages(result.Sent), packet =>
            packet.Content.Any(text =>
                text.Contains("Snakeman", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("720858", StringComparison.Ordinal)));
        Assert.Contains("questId=1341", result.Log);
        Assert.Contains("floor drop", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family72SnakemanL63DeathSkipsQuest1341DropWhenRateMisses()
    {
        var result = await KillMap1020Family72SnakemanL63MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_341, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceQuest1341Rate: false);

        Assert.DoesNotContain(ParseFloorItems(result.Sent), packet => packet.ItemId == 720_858U);
        Assert.Contains("Core.Rate(20)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family72SnakemanL63DeathCompletesQuest1343Objective()
    {
        var result = await KillMap1020Family72SnakemanL63MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_343, PlayerQuestStatus.Accepted, 0, [49U]),
            ],
            forceQuest1341Rate: false);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_343U);
        Assert.Equal([50U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_343U);
        Assert.Equal([50U], objectiveFrame.Intentions);
        Assert.Contains(ParseMessages(result.Sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "You killed 50 Snakemen L63.");
        Assert.Contains("questId=1343", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family71ThunderApeL58DeathCompletesQuest1325Objective()
    {
        var result = await KillMap1020Family71ThunderApeL58MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_325, PlayerQuestStatus.Accepted, 0, [49U]),
            ],
            forceQuest1328Rate: false);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_325U);
        Assert.Equal([50U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_325U);
        Assert.Equal([50U], objectiveFrame.Intentions);
        Assert.Contains(ParseMessages(result.Sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "You killed 50 Thunder Apes L58.");
        Assert.Contains("questId=1325", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family71ThunderApeL58DeathDropsQuest1328FloorItem()
    {
        var result = await KillMap1020Family71ThunderApeL58MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_328, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var floorItem = Assert.Single(ParseFloorItems(result.Sent), packet => packet.ItemId == 720_850U);
        Assert.InRange(floorItem.X, (ushort)166, (ushort)174);
        Assert.InRange(floorItem.Y, (ushort)226, (ushort)234);
        Assert.Equal((byte)14, floorItem.PlusOrColor);
        Assert.Equal((uint)KillerUid, floorItem.ItemOwnerUid);
        Assert.Equal(LegacyFloorItemDropType.Visible, floorItem.DropType);
        Assert.True(result.FloorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.True(runtimeEntry.OwnerOnly);
        Assert.Equal(720_850U, runtimeEntry.Packet.ItemId);
        Assert.Empty(ParseNpcReplies(result.Sent));
        Assert.Contains(ParseMessages(result.Sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "A strange item dropped on the ground! Pick it up to see that is! ");
        Assert.Contains("questId=1328", result.Log);
        Assert.Contains("floor drop", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family71ThunderApeL58DeathSkipsQuest1328DropWhenRateMisses()
    {
        var result = await KillMap1020Family71ThunderApeL58MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_328, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceQuest1328Rate: false);

        Assert.DoesNotContain(ParseFloorItems(result.Sent), packet => packet.ItemId == 720_850U);
        Assert.DoesNotContain(ParseMessages(result.Sent), packet =>
            packet.Content.Count > 3 &&
            packet.Content[3] == "A strange item dropped on the ground! Pick it up to see that is! ");
        Assert.Contains("Core.Rate(20)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family12ThunderApeDeathCompletesQuest1324Objective()
    {
        var result = await KillMap1020Family12ThunderApeMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_324, PlayerQuestStatus.Accepted, 0, [59U]),
            ],
            forceQuest1327Rate: false,
            forceQuest1329Rate: false);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_324U);
        Assert.Equal([60U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_324U);
        Assert.Equal([60U], objectiveFrame.Intentions);
        Assert.Contains(ParseMessages(result.Sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "You killed 60 Thunder Apes.");
        Assert.Contains("questId=1324", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family12ThunderApeDeathDropsQuest1327FloorItem()
    {
        var result = await KillMap1020Family12ThunderApeMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_327, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceQuest1329Rate: false);

        var floorItem = Assert.Single(ParseFloorItems(result.Sent), packet => packet.ItemId == 711_353U);
        Assert.InRange(floorItem.X, (ushort)166, (ushort)174);
        Assert.InRange(floorItem.Y, (ushort)226, (ushort)234);
        Assert.Equal((byte)14, floorItem.PlusOrColor);
        Assert.Equal((uint)KillerUid, floorItem.ItemOwnerUid);
        Assert.Equal(LegacyFloorItemDropType.Visible, floorItem.DropType);
        Assert.True(result.FloorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.True(runtimeEntry.OwnerOnly);
        Assert.Equal(711_353U, runtimeEntry.Packet.ItemId);
        Assert.Contains(ParseMessages(result.Sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "A strange item dropped on the ground! Pick it up to see that is! ");
        Assert.Contains("questId=1327", result.Log);
        Assert.Contains("floor drop", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family12ThunderApeDeathSkipsQuest1327DropWhenRateMisses()
    {
        var result = await KillMap1020Family12ThunderApeMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_327, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceQuest1327Rate: false,
            forceQuest1329Rate: false);

        Assert.DoesNotContain(ParseFloorItems(result.Sent), packet => packet.ItemId == 711_353U);
        Assert.DoesNotContain(ParseMessages(result.Sent), packet =>
            packet.Content.Count > 3 &&
            packet.Content[3] == "A strange item dropped on the ground! Pick it up to see that is! ");
        Assert.Contains("Core.Rate(20)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family12ThunderApeDeathQuest1329AddsBlazePill()
    {
        var result = await KillMap1020Family12ThunderApeMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_329, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceQuest1327Rate: false);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_329U);
        Assert.Equal([1U], quest.Intentions);
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet =>
            packet.Uid == 1_329U &&
            packet.Intentions.SequenceEqual([1U]));
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 720_851U);
        Assert.Contains(ParseGameItems(result.Sent), packet =>
            packet.ItemId == 720_851U &&
            packet.Mode == LegacyGameItemPacket.AddItemMode &&
            packet.StackSize == 1);
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            packet.Text == "You received the Blaze Pill!");
        Assert.Contains("questId=1329", result.Log);
        Assert.Contains("itemId=720851", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family12ThunderApeDeathQuest1329ReportsFullInventory()
    {
        var result = await KillMap1020Family12ThunderApeMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_329, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceQuest1327Rate: false,
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(950_000 + index)))
                .ToArray());

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_329U);
        Assert.Equal([0U], quest.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_329U);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 720_851U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 720_851U);
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=\"inventory full\"", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family12ThunderApeDeathQuest1329SkipsWhenRateMisses()
    {
        var result = await KillMap1020Family12ThunderApeMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_329, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceQuest1327Rate: false,
            forceQuest1329Rate: false);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_329U);
        Assert.Equal([0U], quest.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_329U);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 720_851U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 720_851U);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet => packet.Text.Contains("Blaze Pill", StringComparison.Ordinal));
        Assert.Contains("Core.Rate(25)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family11GiantApeDeathCompletesQuest1316Objective()
    {
        var result = await KillMap1020Family11GiantApeMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_316, PlayerQuestStatus.Accepted, 0, [49U]),
            ],
            forceQuest1363Rate: false);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_316U);
        Assert.Equal([50U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_316U);
        Assert.Equal([50U], objectiveFrame.Intentions);
        Assert.Contains(ParseMessages(result.Sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "You killed 50 Giant Apes.");
        Assert.Contains("questId=1316", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family11GiantApeDeathQuest1363AddsApeClaw()
    {
        var result = await KillMap1020Family11GiantApeMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_363, PlayerQuestStatus.Accepted, 0, [0U, 0U, 0U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_363U);
        Assert.Equal([1U, 0U, 0U], quest.Intentions);
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet =>
            packet.Uid == 1_363U &&
            packet.Intentions.SequenceEqual([1U, 0U, 0U]));
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_357U);
        Assert.Contains(ParseGameItems(result.Sent), packet =>
            packet.ItemId == 711_357U &&
            packet.Mode == LegacyGameItemPacket.AddItemMode &&
            packet.StackSize == 1);
        Assert.Contains(ParseMessages(result.Sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "You received 1 Ape Claw.");
        Assert.Contains("questId=1363", result.Log);
        Assert.Contains("itemId=711357", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family11GiantApeDeathQuest1363AddsApeBone()
    {
        var result = await KillMap1020Family11GiantApeMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_363, PlayerQuestStatus.Accepted, 0, [1U, 0U, 0U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_363U);
        Assert.Equal([1U, 1U, 0U], quest.Intentions);
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet =>
            packet.Uid == 1_363U &&
            packet.Intentions.SequenceEqual([1U, 1U, 0U]));
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_358U);
        Assert.Contains(ParseGameItems(result.Sent), packet =>
            packet.ItemId == 711_358U &&
            packet.Mode == LegacyGameItemPacket.AddItemMode &&
            packet.StackSize == 1);
        Assert.Contains(ParseMessages(result.Sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "You received 1 Ape Bone.");
        Assert.Contains("questId=1363", result.Log);
        Assert.Contains("itemId=711358", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family11GiantApeDeathQuest1363AddsApeSkin()
    {
        var result = await KillMap1020Family11GiantApeMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_363, PlayerQuestStatus.Accepted, 0, [1U, 1U, 0U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_363U);
        Assert.Equal([1U, 1U, 1U], quest.Intentions);
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet =>
            packet.Uid == 1_363U &&
            packet.Intentions.SequenceEqual([1U, 1U, 1U]));
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_359U);
        Assert.Contains(ParseGameItems(result.Sent), packet =>
            packet.ItemId == 711_359U &&
            packet.Mode == LegacyGameItemPacket.AddItemMode &&
            packet.StackSize == 1);
        Assert.Contains(ParseMessages(result.Sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "You received 1 Ape Skin.");
        Assert.Contains("questId=1363", result.Log);
        Assert.Contains("itemId=711359", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family11GiantApeDeathQuest1363SkipsWhenObjectivesComplete()
    {
        var result = await KillMap1020Family11GiantApeMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_363, PlayerQuestStatus.Accepted, 0, [1U, 1U, 1U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_363U);
        Assert.Equal([1U, 1U, 1U], quest.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_363U);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item =>
            item.ItemId is 711_357U or 711_358U or 711_359U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet =>
            packet.ItemId is 711_357U or 711_358U or 711_359U);
        Assert.DoesNotContain(ParseMessages(result.Sent), packet =>
            packet.Content.Count > 3 &&
            packet.Content[3].StartsWith("You received 1 Ape ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandlerMap1020Family11GiantApeDeathQuest1363ReportsFullInventory()
    {
        var result = await KillMap1020Family11GiantApeMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_363, PlayerQuestStatus.Accepted, 0, [0U, 0U, 0U]),
            ],
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(970_000 + index)))
                .ToArray());

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_363U);
        Assert.Equal([0U, 0U, 0U], quest.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_363U);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item =>
            item.ItemId is 711_357U or 711_358U or 711_359U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet =>
            packet.ItemId is 711_357U or 711_358U or 711_359U);
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            packet.Text == "Please make 1 more space in your inventory.");
        Assert.DoesNotContain(ParseMessages(result.Sent), packet =>
            packet.Content.Count > 3 &&
            packet.Content[3].StartsWith("You received 1 Ape ", StringComparison.Ordinal));
        Assert.Contains("reason=\"inventory full\"", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family11GiantApeDeathQuest1363SkipsWhenRateMisses()
    {
        var result = await KillMap1020Family11GiantApeMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_363, PlayerQuestStatus.Accepted, 0, [0U, 0U, 0U]),
            ],
            forceQuest1363Rate: false);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_363U);
        Assert.Equal([0U, 0U, 0U], quest.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_363U);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item =>
            item.ItemId is 711_357U or 711_358U or 711_359U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet =>
            packet.ItemId is 711_357U or 711_358U or 711_359U);
        Assert.DoesNotContain(ParseMessages(result.Sent), packet =>
            packet.Content.Count > 3 &&
            packet.Content[3].StartsWith("You received 1 Ape ", StringComparison.Ordinal));
        Assert.Contains("Core.Rate(30)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family14336DeathIncrementsQuest1356Objective()
    {
        var result = await KillMap1020Family14336MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_356, PlayerQuestStatus.Accepted, 0, [7U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_356U);
        Assert.Equal([8U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_356U);
        Assert.Equal([8U], objectiveFrame.Intentions);
        Assert.Contains("familyId=14336", result.Log);
        Assert.Contains("questId=1356", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family70GiantApeL58DeathCollectsQuest1753Smell()
    {
        var result = await KillMap1020Family70GiantApeL58MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_753, PlayerQuestStatus.Accepted, 0, [0U, 0U, 9U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_753U);
        Assert.Equal([0U, 0U, 10U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_753U);
        Assert.Equal([0U, 0U, 10U], objectiveFrame.Intentions);
        Assert.Contains(ParseMessages(result.Sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "You,ve~collected~some~smells.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            packet.Text == "You,ve collected the smell of Gaint Ape!");
        Assert.Contains("familyId=70", result.Log);
        Assert.Contains("questId=1753", result.Log);
    }

    [Fact]
    public async Task HandlerMap1011Family106RockMonsterDeathCollectsQuest1753Smell()
    {
        var result = await KillMap1011Family106RockMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_753, PlayerQuestStatus.Accepted, 0, [9U, 0U, 0U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_753U);
        Assert.Equal([10U, 0U, 0U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_753U);
        Assert.Equal([10U, 0U, 0U], objectiveFrame.Intentions);
        Assert.Contains(ParseMessages(result.Sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "You,ve~collected~some~smells.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            packet.Text == "You,ve collected the smell of Rock Monster!");
        Assert.Contains("familyId=106", result.Log);
        Assert.Contains("questId=1753", result.Log);
    }

    [Fact]
    public async Task HandlerMap1011Family11007BanditDeathRateHitQuest1103CompletesAndLogsAutoRoute()
    {
        var result = await KillMap1011Family11007BanditMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_103, PlayerQuestStatus.Accepted, 0, [9U]),
            ]);

        var floorItem = Assert.Single(ParseFloorItems(result.Sent), packet => packet.ItemId == 721_263U);
        Assert.InRange(floorItem.X, (ushort)166, (ushort)174);
        Assert.InRange(floorItem.Y, (ushort)226, (ushort)234);
        Assert.Equal((byte)14, floorItem.PlusOrColor);
        Assert.Equal((uint)KillerUid, floorItem.ItemOwnerUid);
        Assert.Equal(LegacyFloorItemDropType.Visible, floorItem.DropType);
        Assert.True(result.FloorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.True(runtimeEntry.OwnerOnly);
        Assert.Equal(721_263U, runtimeEntry.Packet.ItemId);
        Assert.NotNull(runtimeEntry.Item);
        Assert.Equal(721_263U, runtimeEntry.Item.ItemId);
        Assert.Equal(77, runtimeEntry.Item.Durability);
        Assert.Equal(77, runtimeEntry.Item.MaximumDurability);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_103U);
        Assert.Equal([10U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_103U);
        Assert.Equal([10U], objectiveFrame.Intentions);
        Assert.Contains("map1011-family11007-quest1103-autopatcher", result.Log);
        Assert.Contains("You`ve~killed~10~Bandits.~Go~tell~Yu~Jing~about~it.", result.Log);
        Assert.Contains("finishNpcId=8267", result.Log);
        Assert.Contains("finishMapId=1011", result.Log);
        Assert.Contains("finishX=222", result.Log);
        Assert.Contains("finishY=333", result.Log);
        Assert.Contains("sourceDurabilityItemId=729094", result.Log);
    }

    [Fact]
    public async Task HandlerMap1011Family11007BanditDeathRateMissStillIncrementsQuest1103()
    {
        var result = await KillMap1011Family11007BanditMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_103, PlayerQuestStatus.Accepted, 0, [4U]),
            ],
            forceBanditRate: false);

        Assert.DoesNotContain(ParseFloorItems(result.Sent), packet => packet.ItemId == 721_263U);
        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_103U);
        Assert.Equal([5U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_103U);
        Assert.Equal([5U], objectiveFrame.Intentions);
        Assert.Contains("Core.Rate(4)=false", result.Log);
        Assert.DoesNotContain("map1011-family11007-quest1103-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap1011Family11007BanditDeathNoAcceptedQuestStillDropsRateHitFloorItem()
    {
        var result = await KillMap1011Family11007BanditMonsterAsync([]);

        var floorItem = Assert.Single(ParseFloorItems(result.Sent), packet => packet.ItemId == 721_263U);
        Assert.Equal((byte)14, floorItem.PlusOrColor);
        Assert.Equal((uint)KillerUid, floorItem.ItemOwnerUid);
        Assert.Equal(LegacyFloorItemDropType.Visible, floorItem.DropType);
        Assert.True(result.FloorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.True(runtimeEntry.OwnerOnly);
        Assert.Equal(721_263U, runtimeEntry.Packet.ItemId);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_103U);
        Assert.DoesNotContain("map1011-family11007-quest1103-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap1011Family7BanditDeathQuest1114AddsGrainAndLogsAutoRouteAtTen()
    {
        var result = await KillMap1011Family7BanditMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_114, PlayerQuestStatus.Accepted, 0, [9U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_114U);
        Assert.Equal([10U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_114U);
        Assert.Equal([10U], objectiveFrame.Intentions);

        var grain = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_322U);
        Assert.Equal((ushort)1, grain.StackSize);
        Assert.Contains(ParseGameItems(result.Sent), packet =>
            packet.ItemId == 711_322U &&
            packet.Mode == LegacyGameItemPacket.AddItemMode &&
            packet.StackSize == 1);
        Assert.Contains(ParseMessages(result.Sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "You've killed the Bandit and received a bag of grain!");
        Assert.Contains("map1011-family7-quest1114-autopatcher", result.Log);
        Assert.Contains("You`ve~collected~the~bags~of~10~Bandits.~Hurry~to~report~back~to~the~ForestGuvernor!", result.Log);
        Assert.Contains("finishNpcId=8266", result.Log);
        Assert.Contains("finishMapId=1011", result.Log);
        Assert.Contains("finishX=227", result.Log);
        Assert.Contains("finishY=360", result.Log);
    }

    [Fact]
    public async Task HandlerMap1011Family11008BanditLeaderDeathQuest1118IncrementsAndLogsAutoRoute()
    {
        var result = await KillMap1011Family11008BanditLeaderMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_118, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_118U);
        Assert.Equal([1U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_118U);
        Assert.Equal([1U], objectiveFrame.Intentions);
        Assert.Contains("map1011-family11008-quest1118-autopatcher", result.Log);
        Assert.Contains("You`ve~killed~the~Bandit~Leader.~Hurry~and~return~to~the~Bandit~Boss.", result.Log);
        Assert.Contains("finishNpcId=8272", result.Log);
        Assert.Contains("finishMapId=1011", result.Log);
        Assert.Contains("finishX=227", result.Log);
        Assert.Contains("finishY=404", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family14334DeathSpawnsFiveApeLeadersWhenQuest1749MultiplierReached()
    {
        var result = await KillMap1020Family14334ApeLeaderTriggerMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_749, PlayerQuestStatus.Accepted, 0, [0U, 9U]),
            ],
            questMultiple: 2,
            forceApeLeaderRate: false);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_749U);
        Assert.Equal([0U, 10U], quest.Intentions);
        Assert.True(result.UpdatedSession.Character.Attributes.TryGetValue("QuestMultiple", out var questMultiple));
        Assert.Equal("1", questMultiple);
        Assert.Equal(5, ParseMonsterSpawns(result.Sent).Count(packet => packet.MonsterId == 14_334U));
        Assert.DoesNotContain(ParseMonsterSpawns(result.Sent), packet => packet.MonsterId == 14_335U);
        Assert.Contains("spawnedMonsterId=14334", result.Log);
        Assert.Contains("spawnedCount=5", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family14334DeathSpawnsApeLeaderWhenQuest1749RateHits()
    {
        var result = await KillMap1020Family14334ApeLeaderTriggerMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_749, PlayerQuestStatus.Accepted, 0, [0U, 0U]),
            ]);

        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            packet.Text == "The Ape Leader has appeared! Be extra cautious!");
        Assert.Single(ParseMonsterSpawns(result.Sent), packet => packet.MonsterId == 14_335U);
        Assert.Contains("apeLeaderMonsterId=14335", result.Log);
        Assert.Contains("apeLeaderSpawned=True", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family14334DeathSkipsApeLeaderWhenQuest1749RateMisses()
    {
        var result = await KillMap1020Family14334ApeLeaderTriggerMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_749, PlayerQuestStatus.Accepted, 0, [0U, 0U]),
            ],
            forceApeLeaderRate: false);

        Assert.DoesNotContain(ParseMonsterSpawns(result.Sent), packet => packet.MonsterId == 14_335U);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet =>
            packet.Text == "The Ape Leader has appeared! Be extra cautious!");
        Assert.Contains("Core.Rate(20)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family10DeathAppliesQuestBranchesWhenRatesHit()
    {
        var result = await KillMap1020Family10MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_311, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_303, PlayerQuestStatus.Accepted, 0, [2U]),
                new PlayerQuestSnapshotEntry(1_314, PlayerQuestStatus.Accepted, 0, [4U]),
                new PlayerQuestSnapshotEntry(1_746, PlayerQuestStatus.Accepted, 0, [6U]),
            ]);

        var quest1311 = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_311U);
        Assert.Equal([0U], quest1311.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_311U);

        var floorItem = Assert.Single(ParseFloorItems(result.Sent), packet => packet.ItemId == 711_352U);
        Assert.InRange(floorItem.X, (ushort)166, (ushort)174);
        Assert.InRange(floorItem.Y, (ushort)226, (ushort)234);
        Assert.Equal((byte)14, floorItem.PlusOrColor);
        Assert.Equal((uint)KillerUid, floorItem.ItemOwnerUid);
        Assert.Equal(LegacyFloorItemDropType.Visible, floorItem.DropType);
        Assert.True(result.FloorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.True(runtimeEntry.OwnerOnly);
        Assert.Equal(711_352U, runtimeEntry.Packet.ItemId);
        Assert.DoesNotContain(ParseMessages(result.Sent), packet =>
            packet.Content.Count > 3 &&
            packet.Content[3] == "A strange item dropped on the ground! Pick it up to see that is! ");

        var quest1303 = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_303U);
        Assert.Equal([3U], quest1303.Intentions);
        var quest1314 = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_314U);
        Assert.Equal([5U], quest1314.Intentions);
        var quest1746 = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_746U);
        Assert.Equal([7U], quest1746.Intentions);
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet =>
            packet.Uid == 1_303U &&
            packet.Intentions.SequenceEqual([3U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet =>
            packet.Uid == 1_314U &&
            packet.Intentions.SequenceEqual([5U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet =>
            packet.Uid == 1_746U &&
            packet.Intentions.SequenceEqual([7U]));
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 721_699U);
        Assert.Contains(ParseGameItems(result.Sent), packet =>
            packet.ItemId == 721_699U &&
            packet.Mode == LegacyGameItemPacket.AddItemMode &&
            packet.StackSize == 1);
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            packet.Text == "You`ve~found~a~Decayed~Heart!");
        Assert.Contains("questId=1311", result.Log);
        Assert.Contains("sourceDurabilityItemId=729976", result.Log);
        Assert.Contains("questId=1303", result.Log);
        Assert.Contains("questId=1314", result.Log);
        Assert.Contains("questId=1746", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family10DeathSkipsRateGatedBranchesWhenRatesMiss()
    {
        var result = await KillMap1020Family10MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_311, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_746, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceQuest1311Rate: false,
            forceQuest1746Rate: false);

        Assert.DoesNotContain(ParseFloorItems(result.Sent), packet => packet.ItemId == 711_352U);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 721_699U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 721_699U);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_746U);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet =>
            packet.Text == "You`ve~found~a~Decayed~Heart!");
        Assert.Contains("Core.Rate(30)=false", result.Log);
        Assert.Contains("Core.Rate(15)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family8305DeathAddsMonkeyKingTail()
    {
        var result = await KillMap1020Family8305MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_305, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_305U);
        Assert.Equal([1U], quest.Intentions);
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet =>
            packet.Uid == 1_305U &&
            packet.Intentions.SequenceEqual([1U]));
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            packet.Text == "You~received~the~Monkey~King~Tail!");
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 720_863U);
        Assert.Contains(ParseGameItems(result.Sent), packet =>
            packet.ItemId == 720_863U &&
            packet.Mode == LegacyGameItemPacket.AddItemMode &&
            packet.StackSize == 1);
        Assert.Contains("questId=1305", result.Log);
        Assert.Contains("itemId=720863", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family69DeathIncrementsQuest1304Objective()
    {
        var result = await KillMap1020Family69MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_304, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_304U);
        Assert.Equal([1U], quest.Intentions);
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet =>
            packet.Uid == 1_304U &&
            packet.Intentions.SequenceEqual([1U]));
        Assert.Empty(ParseNpcReplies(result.Sent));
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 720_863U);
        Assert.Contains("questId=1304", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family11223DeathAppliesAllAcceptedQuestBranchesWhenRateHits()
    {
        var result = await KillMap1020Family11223VultureMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_306, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_301, PlayerQuestStatus.Accepted, 0, [4U]),
                new PlayerQuestSnapshotEntry(1_736, PlayerQuestStatus.Accepted, 0, [0U, 2U]),
            ]);

        var quest1306 = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_306U);
        Assert.Equal([0U], quest1306.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_306U);

        var quest1301 = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_301U);
        Assert.Equal([5U], quest1301.Intentions);
        var quest1736 = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_736U);
        Assert.Equal([0U, 3U], quest1736.Intentions);
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet =>
            packet.Uid == 1_301U &&
            packet.Intentions.SequenceEqual([5U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet =>
            packet.Uid == 1_736U &&
            packet.Intentions.SequenceEqual([0U, 3U]));

        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 729_976U);
        Assert.Contains(ParseGameItems(result.Sent), packet =>
            packet.ItemId == 729_976U &&
            packet.Mode == LegacyGameItemPacket.AddItemMode &&
            packet.StackSize == 1);
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            packet.Text == "You~received~a~Vulture~Feather.~Climb~up~to~the~City~Wall~at~(554,606)~to~burn~it!");
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            packet.Text == "You`ve~found~a~Vulture~Feather.");

        var floorItem = Assert.Single(ParseFloorItems(result.Sent), packet => packet.ItemId == 729_976U);
        Assert.Equal((byte)14, floorItem.PlusOrColor);
        Assert.Equal((uint)KillerUid, floorItem.ItemOwnerUid);
        Assert.Equal(LegacyFloorItemDropType.Visible, floorItem.DropType);
        Assert.True(result.FloorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.True(runtimeEntry.OwnerOnly);
        Assert.Equal(729_976U, runtimeEntry.Packet.ItemId);
        Assert.Contains("questId=1306", result.Log);
        Assert.Contains("questId=1301", result.Log);
        Assert.Contains("questId=1736", result.Log);
        Assert.Contains("floor drop", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family11223DeathQuest1306FullInventoryStillAppliesIndependentBranches()
    {
        var result = await KillMap1020Family11223VultureMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_306, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_301, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_736, PlayerQuestStatus.Accepted, 0, [0U, 0U]),
            ],
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(960_000 + index)))
                .ToArray());

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 729_976U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 729_976U);
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            packet.Text == "You`ve~found~a~Vulture~Feather.");
        Assert.Contains(ParseFloorItems(result.Sent), packet => packet.ItemId == 729_976U);
        var quest1301 = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_301U);
        Assert.Equal([1U], quest1301.Intentions);
        var quest1736 = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_736U);
        Assert.Equal([0U, 1U], quest1736.Intentions);
        Assert.Contains("reason=\"inventory full\"", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family11223DeathRateMissSkipsQuest1736AndFloorDrop()
    {
        var result = await KillMap1020Family11223VultureMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_306, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_301, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_736, PlayerQuestStatus.Accepted, 0, [0U, 0U]),
            ],
            forceVultureFeatherRate: false);

        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 729_976U);
        Assert.Contains(ParseGameItems(result.Sent), packet =>
            packet.ItemId == 729_976U &&
            packet.Mode == LegacyGameItemPacket.AddItemMode);
        var quest1301 = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_301U);
        Assert.Equal([1U], quest1301.Intentions);
        var quest1736 = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_736U);
        Assert.Equal([0U, 0U], quest1736.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_736U);
        Assert.DoesNotContain(ParseFloorItems(result.Sent), packet => packet.ItemId == 729_976U);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet =>
            packet.Text == "You`ve~found~a~Vulture~Feather.");
        Assert.Contains("Core.Rate(15)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family13SnakemanDeathAppliesObjectiveBranches()
    {
        var result = await KillMap1020Family13SnakemanMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_346, PlayerQuestStatus.Accepted, 0, [39U]),
                new PlayerQuestSnapshotEntry(1_345, PlayerQuestStatus.Accepted, 0, [19U]),
                new PlayerQuestSnapshotEntry(1_360, PlayerQuestStatus.Accepted, 0, [59U]),
                new PlayerQuestSnapshotEntry(1_335, PlayerQuestStatus.Accepted, 0, [7U]),
            ],
            forceQuest1341Rate: false,
            forceQuest1333Rate: false);

        Assert.Equal([40U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_346U).Intentions);
        Assert.Equal([20U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_345U).Intentions);
        Assert.Equal([60U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_360U).Intentions);
        Assert.Equal([8U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_335U).Intentions);
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_346U && packet.Intentions.SequenceEqual([40U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_345U && packet.Intentions.SequenceEqual([20U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_360U && packet.Intentions.SequenceEqual([60U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_335U && packet.Intentions.SequenceEqual([8U]));
        Assert.Contains(ParseMessages(result.Sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "You killed 40 Snakeman.");
        Assert.Contains(ParseMessages(result.Sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "You killed 20 Snakeman.");
        Assert.Contains(ParseMessages(result.Sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "You killed 60 Snakeman.");
        Assert.Contains("questId=1346", result.Log);
        Assert.Contains("questId=1345", result.Log);
        Assert.Contains("questId=1360", result.Log);
        Assert.Contains("questId=1335", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family13SnakemanDeathCollectsQuest1753Smell()
    {
        var result = await KillMap1020Family13SnakemanMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_753, PlayerQuestStatus.Accepted, 0, [3U, 0U, 0U]),
            ],
            forceQuest1341Rate: false,
            forceQuest1333Rate: false,
            forceQuest1783Rate: false);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_753U);
        Assert.Equal([4U, 0U, 0U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_753U);
        Assert.Equal([4U, 0U, 0U], objectiveFrame.Intentions);
        Assert.Contains(ParseMessages(result.Sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "You,ve collected the smell of Snake Man!");
        Assert.Contains("questId=1753", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family13SnakemanDeathQuest1783AddsVenomousFangAndLogsAutoRoute()
    {
        var result = await KillMap1020Family13SnakemanMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_783, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceQuest1341Rate: false,
            forceQuest1333Rate: false);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_783U);
        Assert.Equal([1U], quest.Intentions);
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet =>
            packet.Uid == 1_783U &&
            packet.Intentions.SequenceEqual([1U]));
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 729_978U);
        Assert.Contains(ParseGameItems(result.Sent), packet =>
            packet.ItemId == 729_978U &&
            packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains("map1020-family13-quest1783-autopatcher", result.Log);
        Assert.Contains("finishNpcId=1611", result.Log);
        Assert.Contains("finishX=345", result.Log);
        Assert.Contains("finishY=251", result.Log);
        Assert.Contains("questId=1783", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family13SnakemanDeathQuest1783SkipsWhenRateMisses()
    {
        var result = await KillMap1020Family13SnakemanMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_783, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceQuest1341Rate: false,
            forceQuest1333Rate: false,
            forceQuest1783Rate: false);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_783U);
        Assert.Equal([0U], quest.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_783U);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 729_978U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 729_978U);
        Assert.Contains("Core.Rate(15)=false", result.Log);
        Assert.DoesNotContain("map1020-family13-quest1783-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family13SnakemanDeathDropsQuest1341And1333FloorItems()
    {
        var result = await KillMap1020Family13SnakemanMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_341, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_333, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var floorItems = ParseFloorItems(result.Sent);
        var quest1341Drop = Assert.Single(floorItems, packet => packet.ItemId == 720_857U);
        Assert.InRange(quest1341Drop.X, (ushort)166, (ushort)174);
        Assert.InRange(quest1341Drop.Y, (ushort)226, (ushort)234);
        Assert.Equal((byte)14, quest1341Drop.PlusOrColor);
        Assert.Equal((uint)KillerUid, quest1341Drop.ItemOwnerUid);
        Assert.Equal(LegacyFloorItemDropType.Visible, quest1341Drop.DropType);
        Assert.True(result.FloorItems.TryGetVisibleEntry(quest1341Drop.Uid, out var quest1341RuntimeEntry));
        Assert.True(quest1341RuntimeEntry.OwnerOnly);
        Assert.Equal(720_857U, quest1341RuntimeEntry.Packet.ItemId);

        var quest1333Drop = Assert.Single(floorItems, packet => packet.ItemId == 720_854U);
        Assert.InRange(quest1333Drop.X, (ushort)166, (ushort)174);
        Assert.InRange(quest1333Drop.Y, (ushort)226, (ushort)234);
        Assert.Equal((byte)14, quest1333Drop.PlusOrColor);
        Assert.Equal((uint)KillerUid, quest1333Drop.ItemOwnerUid);
        Assert.Equal(LegacyFloorItemDropType.Visible, quest1333Drop.DropType);
        Assert.True(result.FloorItems.TryGetVisibleEntry(quest1333Drop.Uid, out var quest1333RuntimeEntry));
        Assert.True(quest1333RuntimeEntry.OwnerOnly);
        Assert.Equal(720_854U, quest1333RuntimeEntry.Packet.ItemId);

        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet =>
            packet.Text.Contains("720857", StringComparison.Ordinal) ||
            packet.Text.Contains("720854", StringComparison.Ordinal) ||
            packet.Text.Contains("Snakeman", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(ParseMessages(result.Sent), packet =>
            packet.Content.Any(text =>
                text.Contains("720857", StringComparison.Ordinal) ||
                text.Contains("720854", StringComparison.Ordinal) ||
                text.Contains("Snakeman", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("questId=1341", result.Log);
        Assert.Contains("questId=1333", result.Log);
        Assert.Contains("floor drop", result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family13SnakemanDeathSkipsDropsWhenRatesMiss()
    {
        var result = await KillMap1020Family13SnakemanMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_341, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_333, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceQuest1341Rate: false,
            forceQuest1333Rate: false);

        Assert.DoesNotContain(ParseFloorItems(result.Sent), packet => packet.ItemId is 720_857U or 720_854U);
        var rateMissCount = result.Log.Split("Core.Rate(20)=false", StringSplitOptions.None).Length - 1;
        Assert.True(rateMissCount >= 2, result.Log);
    }

    [Fact]
    public async Task HandlerMap1020Family13SnakemanDeathQuest1337StacksOwnedCloudRidingScroll()
    {
        var result = await KillMap1020Family13SnakemanMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_337, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceQuest1341Rate: false,
            forceQuest1333Rate: false,
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 720_856, stackSize: 2),
            ]);

        var scrolls = result.UpdatedSession.PlayerItems.InventoryItems
            .Where(item => item.ItemId == 720_856U)
            .ToArray();
        Assert.Equal(3, scrolls.Sum(item => item.StackSize == 0 ? 1 : item.StackSize));
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            packet.Text == "You~received~the~Cloud~Riding~Scroll!");
        Assert.Contains(ParseGameItems(result.Sent), packet =>
            packet.ItemId == 720_856U &&
            packet.Mode is LegacyGameItemPacket.AddItemMode or LegacyGameItemPacket.UpdateItemMode &&
            packet.StackSize is 1 or 3);
        Assert.Contains("questId=1337", result.Log);
        Assert.Contains("itemId=720856", result.Log);
    }


    [Fact]
    public async Task HandlerMap1000Family15HillMonsterDeathAppliesHeartInventoryReward()
    {
        var result = await KillMap1000Family15HillMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_501, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_411U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_411U);
        Assert.Contains(ParseStringPackets(result.Sent), packet =>
            packet.Id == LegacyStringId.Effect &&
            packet.Strings.SequenceEqual(["relive1"]));
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.Text == "You`ve~killed~the~Hill~Monster~and~received~a~Hill~Monster`s~Heart.");
        Assert.Contains("questId=1501", result.Log);
        Assert.Contains("itemId=711411", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family15HillMonsterDeathHandlesHeartRateMissAndFullInventory()
    {
        var rateMiss = await KillMap1000Family15HillMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_501, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceHeartRate: false);

        Assert.DoesNotContain(rateMiss.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_411U);
        Assert.DoesNotContain(ParseGameItems(rateMiss.Sent), packet => packet.ItemId == 711_411U);
        Assert.DoesNotContain(ParseStringPackets(rateMiss.Sent), packet => packet.Strings.SequenceEqual(["relive1"]));
        Assert.Contains("Core.Rate(40)=false", rateMiss.Log);

        var fullInventory = await KillMap1000Family15HillMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_501, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(930_000 + index)))
                .ToArray());

        Assert.DoesNotContain(fullInventory.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_411U);
        Assert.DoesNotContain(ParseGameItems(fullInventory.Sent), packet => packet.ItemId == 711_411U);
        Assert.DoesNotContain(ParseStringPackets(fullInventory.Sent), packet => packet.Strings.SequenceEqual(["relive1"]));
        Assert.Contains(ParseNpcReplies(fullInventory.Sent), packet => packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=\"inventory full\"", fullInventory.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family15HillMonsterDeathAppliesObjectiveBranches()
    {
        var result = await KillMap1000Family15HillMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_476, PlayerQuestStatus.Accepted, 0, [39U, 10U]),
                new PlayerQuestSnapshotEntry(1_466, PlayerQuestStatus.Accepted, 0, [49U]),
                new PlayerQuestSnapshotEntry(1_465, PlayerQuestStatus.Accepted, 0, [19U]),
            ],
            forceHeartRate: false,
            forceHammerRate: false,
            forceHeavyAxeRate: false);

        Assert.Equal([40U, 10U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_476U).Intentions);
        Assert.Equal([50U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_466U).Intentions);
        Assert.Equal([20U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_465U).Intentions);
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_476U && packet.Intentions.SequenceEqual([40U, 10U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_466U && packet.Intentions.SequenceEqual([50U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_465U && packet.Intentions.SequenceEqual([20U]));
        Assert.True(ParseMessages(result.Sent).Count(packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "You've killed a Hill Monster.") >= 2);
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.Text == "You`ve~killed~40~Hill~Monsters~and~10~Hill~Monsters~L73.~Go~report~to~Han~Cheng.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.Text == "You`ve~killed~50~Hill~Monsters.~Go~report~to~General~ZhuGe.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.Text == "You`ve~killed~20~Hill~Monsters.~Go~report~to~General~ZhuGe.");
        Assert.Contains("questId=1476", result.Log);
        Assert.Contains("questId=1466", result.Log);
        Assert.Contains("questId=1465", result.Log);
    }

    [Fact]
    public async Task HandlerMap1000Family15HillMonsterDeathDropsHammerAndHeavyAxeFloorItems()
    {
        var result = await KillMap1000Family15HillMonsterMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_467, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_474, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceHeartRate: false);

        var hammer = Assert.Single(ParseFloorItems(result.Sent), packet => packet.ItemId == 711_344U);
        Assert.InRange(hammer.X, (ushort)166, (ushort)174);
        Assert.InRange(hammer.Y, (ushort)226, (ushort)234);
        Assert.Equal((byte)14, hammer.PlusOrColor);
        Assert.Equal((uint)KillerUid, hammer.ItemOwnerUid);
        Assert.Equal(LegacyFloorItemDropType.Visible, hammer.DropType);
        Assert.True(result.FloorItems.TryGetVisibleEntry(hammer.Uid, out var hammerRuntimeEntry));
        Assert.True(hammerRuntimeEntry.OwnerOnly);

        var heavyAxe = Assert.Single(ParseFloorItems(result.Sent), packet => packet.ItemId == 711_415U);
        Assert.InRange(heavyAxe.X, (ushort)166, (ushort)174);
        Assert.InRange(heavyAxe.Y, (ushort)226, (ushort)234);
        Assert.Equal((byte)14, heavyAxe.PlusOrColor);
        Assert.Equal((uint)KillerUid, heavyAxe.ItemOwnerUid);
        Assert.Equal(LegacyFloorItemDropType.Visible, heavyAxe.DropType);
        Assert.True(result.FloorItems.TryGetVisibleEntry(heavyAxe.Uid, out var heavyAxeRuntimeEntry));
        Assert.True(heavyAxeRuntimeEntry.OwnerOnly);

        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.Text == "You`ve~killed~the~Hill~Monster~and~received~a~Hammer.");
        Assert.Contains(ParseNpcReplies(result.Sent), packet =>
            packet.Text == "You`ve~killed~the~monster~and~received~a~Heavy~Axe.");
        Assert.Contains("questId=1467", result.Log);
        Assert.Contains("questId=1474", result.Log);
        Assert.Contains("floor drop", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family79Quest1642DeathAddsShotSaberAndMessages()
    {
        var result = await KillMap1015Family79Quest1642MonsterAsync();

        var shotSaber = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_474U);
        Assert.Equal((ushort)1, shotSaber.StackSize);
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content[3] == "You`ve killed a Banditti.");
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content[3] == "You received a Shot Saber!");
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 711_474U &&
                packet.Mode == LegacyGameItemPacket.AddItemMode &&
                packet.StackSize == 1);
        Assert.Contains("map 1015 family 79 quest 1642 Shot Saber reward", result.Log);
        Assert.Contains("shotSaberCountBefore=0", result.Log);
        Assert.Contains("shotSaberCountAfter=1", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family79Quest1642DeathSendsKillNoticeWhenRateMisses()
    {
        var result = await KillMap1015Family79Quest1642MonsterAsync(forcedRate: false);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_474U);
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content[3] == "You`ve killed a Banditti.");
        Assert.DoesNotContain(ParseMessages(result.Sent), packet => packet.Content[3] == "You received a Shot Saber!");
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_474U);
        Assert.Contains("Core.Rate(40)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family79Quest1642DeathStacksShotSaberUntilTen()
    {
        var result = await KillMap1015Family79Quest1642MonsterAsync(
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 711_474, stackSize: 9),
            ]);

        var shotSaber = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_474U);
        Assert.Equal((ushort)10, shotSaber.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 711_474U &&
                packet.Mode == LegacyGameItemPacket.UpdateItemMode &&
                packet.StackSize == 10);
        Assert.Contains("shotSaberCountBefore=9", result.Log);
        Assert.Contains("shotSaberCountAfter=10", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family79Quest1642DeathReportsFullInventoryAfterKillNotice()
    {
        var result = await KillMap1015Family79Quest1642MonsterAsync(
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(840_000 + index)))
                .ToArray());

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_474U);
        Assert.Contains(
            ParseMessages(result.Sent),
            packet => packet.Content[3] == "You`ve killed a Banditti.");
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_474U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=\"inventory full\"", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family79Quest1642DeathSkipsRewardWhenTenOwnedOrQuestNotAccepted()
    {
        var enoughShotSabers = await KillMap1015Family79Quest1642MonsterAsync(
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 711_474, stackSize: 10),
            ]);
        var questFinished = await KillMap1015Family79Quest1642MonsterAsync(
            questStatus: PlayerQuestStatus.Finished);

        Assert.Equal((ushort)10, Assert.Single(enoughShotSabers.UpdatedSession.PlayerItems.InventoryItems).StackSize);
        Assert.Contains(
            ParseMessages(enoughShotSabers.Sent),
            packet => packet.Content[3] == "You`ve killed a Banditti.");
        Assert.DoesNotContain(ParseMessages(enoughShotSabers.Sent), packet => packet.Content[3] == "You received a Shot Saber!");
        Assert.DoesNotContain(ParseGameItems(enoughShotSabers.Sent), packet => packet.ItemId == 711_474U);
        Assert.Contains("Shot Saber count already reached 10", enoughShotSabers.Log);
        Assert.DoesNotContain(questFinished.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_474U);
        Assert.DoesNotContain(ParseMessages(questFinished.Sent), packet => packet.Content[3].Contains("Banditti", StringComparison.Ordinal));
        Assert.DoesNotContain(ParseGameItems(questFinished.Sent), packet => packet.ItemId == 711_474U);
        Assert.DoesNotContain("quest 1642 Shot Saber reward", questFinished.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family79Quest1641DeathIncrementsBanditL98Objective()
    {
        var result = await KillMap1015Family79BanditL98MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_641, PlayerQuestStatus.Accepted, 0, [4U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_641U);
        Assert.Equal([5U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_641U);
        Assert.Equal([5U], objectiveFrame.Intentions);
        Assert.Single(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a BanditL98.");
        Assert.Contains("quest 1641 BanditL98 objective", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family79Quest1647DeathIncrementsSecondBanditL98Objective()
    {
        var result = await KillMap1015Family79BanditL98MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_647, PlayerQuestStatus.Accepted, 0, [7U, 2U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_647U);
        Assert.Equal([7U, 3U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_647U);
        Assert.Equal([7U, 3U], objectiveFrame.Intentions);
        Assert.Single(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a BanditL98.");
        Assert.Contains("quest 1647 BanditL98 objective", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family79BanditL98DeathAppliesBothAcceptedQuestBranches()
    {
        var result = await KillMap1015Family79BanditL98MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_641, PlayerQuestStatus.Accepted, 0, [1U]),
                new PlayerQuestSnapshotEntry(1_647, PlayerQuestStatus.Accepted, 0, [3U, 5U]),
            ]);

        Assert.Equal([2U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_641U).Intentions);
        Assert.Equal([3U, 6U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_647U).Intentions);
        Assert.Equal(2, ParseMessages(result.Sent).Count(packet => packet.Content[3] == "You`ve killed a BanditL98."));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_641U && packet.Intentions.SequenceEqual([2U]));
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_647U && packet.Intentions.SequenceEqual([3U, 6U]));
    }

    [Fact]
    public async Task HandlerMap1015Family79BanditL98DeathSkipsFinishedQuestBranches()
    {
        var result = await KillMap1015Family79BanditL98MonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_641, PlayerQuestStatus.Finished, 0, [4U]),
                new PlayerQuestSnapshotEntry(1_647, PlayerQuestStatus.Finished, 0, [7U, 2U]),
            ]);

        Assert.Equal([4U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_641U).Intentions);
        Assert.Equal([7U, 2U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_647U).Intentions);
        Assert.DoesNotContain(ParseMessages(result.Sent), packet => packet.Content[3].Contains("BanditL98", StringComparison.Ordinal));
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid is 1_641U or 1_647U);
        Assert.DoesNotContain("BanditL98 objective", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family84Quest1651DeathAddsYarnBallAndMessages()
    {
        var result = await KillMap1015Family84BandittiMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_651, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var item = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_476U);
        Assert.Equal((ushort)1, item.StackSize);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a Banditti.");
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You received 1 Yarn Ball!");
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 711_476U &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains("questId=1651", result.Log);
        Assert.Contains("Yarn Ball", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family84Quest1652DeathAddsFlowerWineAndMessages()
    {
        var result = await KillMap1015Family84BandittiMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_652, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var item = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_477U);
        Assert.Equal((ushort)1, item.StackSize);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a Banditti.");
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You received Flower Wine!");
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 711_477U &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains("questId=1652", result.Log);
        Assert.Contains("Flower Wine", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family84BandittiDeathAppliesBothRewardBranches()
    {
        var result = await KillMap1015Family84BandittiMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_651, PlayerQuestStatus.Accepted, 0, [0U]),
                new PlayerQuestSnapshotEntry(1_652, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_476U);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_477U);
        Assert.Equal(2, ParseMessages(result.Sent).Count(packet => packet.Content[3] == "You`ve killed a Banditti."));
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_476U);
        Assert.Contains(ParseGameItems(result.Sent), packet => packet.ItemId == 711_477U);
    }

    [Fact]
    public async Task HandlerMap1015Family84BandittiDeathSendsKillNoticeWhenRateMisses()
    {
        var result = await KillMap1015Family84BandittiMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_651, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceYarnRate: false);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_476U);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a Banditti.");
        Assert.DoesNotContain(ParseMessages(result.Sent), packet => packet.Content[3] == "You received 1 Yarn Ball!");
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_476U);
        Assert.Contains("Core.Rate(40)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family84BandittiDeathReportsFullInventoryAfterKillNotice()
    {
        var result = await KillMap1015Family84BandittiMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_651, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(850_000 + index)))
                .ToArray());

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_476U);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a Banditti.");
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_476U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=\"inventory full\"", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family84BandittiDeathSkipsOwnedOrFinishedBranches()
    {
        var alreadyOwned = await KillMap1015Family84BandittiMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_651, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 711_476),
            ]);
        var questFinished = await KillMap1015Family84BandittiMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_651, PlayerQuestStatus.Finished, 0, [0U]),
            ]);

        Assert.Equal(1, alreadyOwned.UpdatedSession.PlayerItems.InventoryItems.Count(entry => entry.ItemId == 711_476U));
        Assert.Contains(ParseMessages(alreadyOwned.Sent), packet => packet.Content[3] == "You`ve killed a Banditti.");
        Assert.DoesNotContain(ParseMessages(alreadyOwned.Sent), packet => packet.Content[3] == "You received 1 Yarn Ball!");
        Assert.DoesNotContain(ParseGameItems(alreadyOwned.Sent), packet => packet.ItemId == 711_476U);
        Assert.Contains("item already in inventory", alreadyOwned.Log);
        Assert.DoesNotContain(questFinished.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_476U);
        Assert.DoesNotContain(ParseMessages(questFinished.Sent), packet => packet.Content[3].Contains("Banditti", StringComparison.Ordinal));
        Assert.DoesNotContain(ParseGameItems(questFinished.Sent), packet => packet.ItemId == 711_476U);
        Assert.DoesNotContain("family 84 Banditti item reward", questFinished.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family84Quest1650DeathIncrementsBandittiObjective()
    {
        var result = await KillMap1015Family84BandittiMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_650, PlayerQuestStatus.Accepted, 0, [12U]),
            ]);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_650U);
        Assert.Equal([13U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_650U);
        Assert.Equal([13U], objectiveFrame.Intentions);
        Assert.Single(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a Banditti.");
        Assert.Contains("quest 1650 Banditti objective", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family84Quest1645DeathAddsRoyalSwordAndMessages()
    {
        var result = await KillMap1015Family84BandittiMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_645, PlayerQuestStatus.Accepted, 0, [0U]),
            ]);

        var item = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_475U);
        Assert.Equal((ushort)1, item.StackSize);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a Banditti.");
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You received the Royal Sword!");
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 711_475U &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains("questId=1645", result.Log);
        Assert.Contains("Royal Sword", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family84Quest1645DeathSendsKillNoticeWhenRateMisses()
    {
        var result = await KillMap1015Family84BandittiMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_645, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceRoyalSwordRate: false);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_475U);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a Banditti.");
        Assert.DoesNotContain(ParseMessages(result.Sent), packet => packet.Content[3] == "You received the Royal Sword!");
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_475U);
        Assert.Contains("Core.Rate(40)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family84Quest1645DeathReportsFullInventoryAfterKillNotice()
    {
        var result = await KillMap1015Family84BandittiMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_645, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(860_000 + index)))
                .ToArray());

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_475U);
        Assert.Contains(ParseMessages(result.Sent), packet => packet.Content[3] == "You`ve killed a Banditti.");
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_475U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=\"inventory full\"", result.Log);
    }

    [Fact]
    public async Task HandlerMap1015Family84Quest1645DeathSkipsOwnedOrFinishedBranches()
    {
        var alreadyOwned = await KillMap1015Family84BandittiMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_645, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            inventoryItems:
            [
                CreateInventoryItem(uid: 1, itemId: 711_475),
            ]);
        var questFinished = await KillMap1015Family84BandittiMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_645, PlayerQuestStatus.Finished, 0, [0U]),
            ]);

        Assert.Equal(1, alreadyOwned.UpdatedSession.PlayerItems.InventoryItems.Count(entry => entry.ItemId == 711_475U));
        Assert.Contains(ParseMessages(alreadyOwned.Sent), packet => packet.Content[3] == "You`ve killed a Banditti.");
        Assert.DoesNotContain(ParseMessages(alreadyOwned.Sent), packet => packet.Content[3] == "You received the Royal Sword!");
        Assert.DoesNotContain(ParseGameItems(alreadyOwned.Sent), packet => packet.ItemId == 711_475U);
        Assert.Contains("item already in inventory", alreadyOwned.Log);
        Assert.DoesNotContain(questFinished.UpdatedSession.PlayerItems.InventoryItems, entry => entry.ItemId == 711_475U);
        Assert.DoesNotContain(ParseMessages(questFinished.Sent), packet => packet.Content[3].Contains("Banditti", StringComparison.Ordinal));
        Assert.DoesNotContain(ParseGameItems(questFinished.Sent), packet => packet.ItemId == 711_475U);
    }

    [Fact]
    public async Task HandlerMap1780Family12314463DeathSendsReliveEffectAndBezoarDrop()
    {
        var result = await KillMap1780Family12314463KidSiezerMonsterAsync();

        Assert.Contains(
            ParseStringPackets(result.Sent),
            packet => packet.Uid == KillerUid &&
                packet.Id == LegacyStringId.Effect &&
                packet.Strings.SequenceEqual(["relive1"]));
        var floorItem = Assert.Single(ParseFloorItems(result.Sent), packet => packet.ItemId == 721_714U);
        Assert.Equal(LegacyFloorItemDropType.Visible, floorItem.DropType);
        Assert.Equal(KillerUid, floorItem.ItemOwnerUid);
        Assert.True(result.FloorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.Equal(FloorItemRuntimePickupKind.Item, runtimeEntry.Kind);
        Assert.Contains("map 1780 KidSiezer Bezoar drop", result.Log);
        Assert.Contains("itemId=721714", result.Log);
        Assert.Contains("effect=relive1", result.Log);
    }

    [Fact]
    public async Task HandlerMap1780Family12314462DeathSpawnsKidSiezerAndDialog()
    {
        var result = await KillMap1780Family12314462KidHunterMonsterAsync();

        var spawn = Assert.Single(ParseMonsterSpawns(result.Sent), packet => packet.MonsterId == 12_314_463U);
        Assert.Equal((ushort)500, spawn.X);
        Assert.Equal((ushort)501, spawn.Y);
        Assert.Equal("KidSiezer", spawn.Names[0]);
        Assert.True(result.MonsterStates.TryGetDynamicMonsterByUid(
            1_780,
            0,
            spawn.Uid,
            out var spawnedInstance,
            out var spawnedFamily));
        Assert.Equal(12_314_463U, spawnedInstance.MonsterId);
        Assert.Equal(12_314_463, spawnedFamily.Id);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "The~Kid~Siezer~appeared.~Go~kill~it.");
        Assert.Contains("map1780-kidsiezer-spawn-item-packet", result.Log);
        Assert.Contains("map 1780 KidSiezer spawn branch", result.Log);
        Assert.Contains("spawned=True", result.Log);
    }

    [Fact]
    public async Task HandlerMap1780Family12314462DeathSkipsWhenKidSiezerAlreadyVisible()
    {
        var result = await KillMap1780Family12314462KidHunterMonsterAsync(preRegisterKidSiezer: true);

        Assert.DoesNotContain(ParseMonsterSpawns(result.Sent), packet => packet.MonsterId == 12_314_463U);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet => packet.Text.Contains("Kid~Siezer", StringComparison.Ordinal));
        Assert.DoesNotContain("map1780-kidsiezer-spawn-item-packet", result.Log);
        Assert.Contains("visible KidSiezer already in screen", result.Log);
    }

    [Fact]
    public async Task HandlerMap1780OtherFamilyDeathSpawnsKidHunterAndDialog()
    {
        var result = await KillMap1780OtherFamilyMonsterAsync();

        var spawn = Assert.Single(ParseMonsterSpawns(result.Sent), packet => packet.MonsterId == 12_314_462U);
        Assert.Equal((ushort)500, spawn.X);
        Assert.Equal((ushort)501, spawn.Y);
        Assert.Equal("KidHunter", spawn.Names[0]);
        Assert.True(result.MonsterStates.TryGetDynamicMonsterByUid(
            1_780,
            0,
            spawn.Uid,
            out var spawnedInstance,
            out var spawnedFamily));
        Assert.Equal(12_314_462U, spawnedInstance.MonsterId);
        Assert.Equal(12_314_462, spawnedFamily.Id);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "The~Kid~Hunter~appeared.~Go~kill~it.");
        Assert.Contains("map1780-kidhunter-spawn-item-packet", result.Log);
        Assert.Contains("map 1780 KidHunter spawn branch", result.Log);
        Assert.Contains("spawned=True", result.Log);
    }

    [Fact]
    public async Task HandlerMap1780OtherFamilyDeathSkipsKidHunterWhenRateMisses()
    {
        var result = await KillMap1780OtherFamilyMonsterAsync(forcedRate: false);

        Assert.DoesNotContain(ParseMonsterSpawns(result.Sent), packet => packet.MonsterId == 12_314_462U);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet => packet.Text.Contains("Kid~Hunter", StringComparison.Ordinal));
        Assert.DoesNotContain("map1780-kidhunter-spawn-item-packet", result.Log);
        Assert.Contains("Core.Rate(10)=false", result.Log);
    }

    [Fact]
    public async Task HandlerMap1780OtherFamilyDeathSkipsWhenKidHunterAlreadyVisible()
    {
        var result = await KillMap1780OtherFamilyMonsterAsync(preRegisterKidHunter: true);

        Assert.DoesNotContain(ParseMonsterSpawns(result.Sent), packet => packet.MonsterId == 12_314_462U);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet => packet.Text.Contains("Kid~Hunter", StringComparison.Ordinal));
        Assert.DoesNotContain("map1780-kidhunter-spawn-item-packet", result.Log);
        Assert.Contains("visible KidHunter already in screen", result.Log);
    }

    [Fact]
    public async Task HandlerMap1780OtherFamilyDeathSkipsWhenBezoarOwned()
    {
        var result = await KillMap1780OtherFamilyMonsterAsync(existingBezoar: true);

        Assert.DoesNotContain(ParseMonsterSpawns(result.Sent), packet => packet.MonsterId == 12_314_462U);
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet => packet.Text.Contains("Kid~Hunter", StringComparison.Ordinal));
        Assert.DoesNotContain("map1780-kidhunter-spawn-item-packet", result.Log);
        Assert.Contains("Bezoar already in inventory", result.Log);
    }

    [Fact]
    public async Task HandlerGenericBossDeathTreatsGenerateBossFamilyAsSourceSilentNoDrop()
    {
        var bossFamily = CreateBossFamily(id: 9_902, name: "GenericBoss");
        var spawn = CreateBossSpawn(bossFamily);
        var worldData = CreateWorldDataCatalog(bossFamily, spawn);
        var boss = LegacyMonsterInstance.FromGenerator(
            spawn,
            bossFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(KillerUid, "LastHit");
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            boss,
            log);

        Assert.Empty(ParseFloorItems(killerSent));
        var output = log.ToString();
        Assert.Contains("branch=\"MonsterRole.cs GenerateBossFamily()\"", output);
        Assert.DoesNotContain("boss-family monster death drops are source-backed but not modeled", output);
    }

    private static void AssertBossHarmRankMail(LegacyMailboxEntry entry, int rank)
    {
        Assert.Equal("STR_SYSTEM_NAME@@", entry.Title);
        Assert.Equal("STR_SYSTEM_NAME@@", entry.SenderName);
        Assert.Equal("STR_SYSTEM_NAME@@", entry.Content);
        Assert.Equal(1U, entry.Id);
        Assert.Equal(100_000UL, entry.Money);
        Assert.Equal((uint)LegacyBossDeathRewardPolicy.GetHarmRankMailConquerPoints(rank), entry.EMoney);
        Assert.Equal(0U, entry.ItemId);
        Assert.Equal(1U, entry.EmoneyRecordType);
        Assert.Equal((uint)rank, entry.Flag);
        Assert.True(entry.HasClaimableRewards);
    }

    private static async Task KillBossAsync(
        ConquerTcpConnection connection,
        WorldSession session,
        WorldDataCatalog worldData,
        WorldSessionStore sessions,
        MonsterRuntimeStateStore monsterStates,
        LegacyMailboxStore mailboxes,
        LegacyMonsterInstance boss,
        TextWriter log,
        FloorItemRuntimeStateStore? floorItems = null,
        int damage = 200_000,
        TeamSessionStateStore? teamStates = null,
        string? playerQuestsRoot = null)
    {
        PrimeMonsterForServerAuthoritativeFinalHit(monsterStates, boss);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                TeamStates: teamStates,
                MonsterStates: monsterStates,
                FloorItems: floorItems,
                Mailboxes: mailboxes,
                PlayerQuestsRoot: playerQuestsRoot),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: KillerUid,
                OpponentUid: boss.Uid,
                X: boss.X,
                Y: boss.Y,
                AttackType: MsgAttackType.Physical,
                Damage: damage,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));
    }

    private static void PrimeMonsterForServerAuthoritativeFinalHit(
        MonsterRuntimeStateStore monsterStates,
        LegacyMonsterInstance monster)
    {
        var now = DateTimeOffset.UtcNow;
        if (!monsterStates.TryGetVisibleInstance(monster, now, out var visibleMonster) ||
            visibleMonster.Hitpoints <= 1)
        {
            return;
        }

        monsterStates.ApplyDamage(visibleMonster, visibleMonster.Hitpoints - 1, now);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)> KillFamily3319Async(
        string rewardBranch,
        IReadOnlyDictionary<string, string>? attributes = null,
        long classCode = 142,
        IReadOnlyList<InnerPowerRankEntry>? innerPowerRanks = null)
    {
        var family = CreateBossFamily(id: 3_319, name: "Family3319", boss: false);
        var spawn = CreateBossSpawn(family);
        var worldData = CreateWorldDataCatalog(family, spawn, innerPowerRanks: innerPowerRanks);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            family,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var runtimeAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyFamily3319RewardBranch"] = rewardBranch,
            ["LegacyMonsterDropRoll"] = "40",
        };

        if (attributes is not null)
        {
            foreach (var (key, value) in attributes)
            {
                runtimeAttributes[key] = value;
            }
        }

        var session = CreateSession(
            KillerUid,
            "LastHit",
            classCode: classCode,
            attributes: runtimeAttributes);
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)> KillFamily8418Async(
        string rewardBranch,
        IReadOnlyDictionary<string, string>? attributes = null,
        IReadOnlyList<ItemType>? itemTypes = null)
    {
        var family = CreateBossFamily(id: 8_418, name: "MapHuantMonster", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 10_342);
        var worldData = CreateWorldDataCatalog(family, spawn, items: itemTypes);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            family,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var runtimeAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyFamily8418RewardBranch"] = rewardBranch,
            ["LegacyMonsterDropRoll"] = "40",
        };

        if (attributes is not null)
        {
            foreach (var (key, value) in attributes)
            {
                runtimeAttributes[key] = value;
            }
        }

        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_342,
            attributes: runtimeAttributes);
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)> KillHouseDemonAsync(
        int familyId,
        string rewardBranch,
        ulong? dynamicId = null)
    {
        var familyAttributes = new Dictionary<string, string>(
            CreateBossFamily(id: familyId, name: "HouseDemon", boss: false).Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["life"] = "1",
        };
        var family = new Monster(familyId, "HouseDemon", familyAttributes, []);
        var dynamicIdValue = dynamicId ?? KillerUid;
        var spawn = CreateBossSpawn(family, mapId: 10_098) with
        {
            Attributes = new Dictionary<string, string>
            {
                ["DynamicID"] = dynamicIdValue.ToString(CultureInfo.InvariantCulture),
            },
        };
        var worldData = CreateWorldDataCatalog(family, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            family,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_098,
            attributes: new Dictionary<string, string>
            {
                ["DynamicID"] = dynamicIdValue.ToString(CultureInfo.InvariantCulture),
                ["LegacyHouseDemonRewardBranch"] = rewardBranch,
                ["LegacyMonsterDropRoll"] = "40",
            });
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)> KillSpiritBeadQuest2375MonsterAsync(
        byte level,
        bool boss,
        uint maxLife,
        bool accepted = true)
    {
        var attributes = new Dictionary<string, string>
        {
            ["life"] = maxLife.ToString(CultureInfo.InvariantCulture),
            ["lookface"] = "900",
            ["level"] = level.ToString(CultureInfo.InvariantCulture),
            ["defense"] = "0",
            ["defense2"] = "10000",
            ["attack_min"] = "1",
            ["attack_max"] = "1",
            ["attack_range"] = "0",
            ["Boss"] = boss ? "1" : "0",
        };
        var family = new Monster(5_237, "SpiritBeadMonster", attributes, []);
        var spawn = CreateBossSpawn(family, mapId: 10_015);
        var worldData = CreateWorldDataCatalog(family, spawn);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_015,
            attributes: new Dictionary<string, string>
            {
                ["DailySpiritBeadCount"] = "7",
                ["LegacyMonsterDropRoll"] = "40",
            });
        if (accepted)
        {
            session = WithAcceptedQuest(session, 2_375);
        }

        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<Family4212Quest6126Result> KillFamily4212Quest6126MonsterAsync(
        bool killerAccepted,
        bool includeTeamMember = false,
        bool teammateAccepted = false,
        int teammateMapId = 10_015,
        ulong teammateDynamicId = 0)
    {
        var attributes = new Dictionary<string, string>
        {
            ["life"] = "1",
            ["lookface"] = "900",
            ["level"] = "138",
            ["defense"] = "0",
            ["defense2"] = "10000",
            ["attack_min"] = "1",
            ["attack_max"] = "1",
            ["attack_range"] = "0",
            ["Boss"] = "0",
        };
        var family = new Monster(4_212, "Family4212", attributes, []);
        var spawn = CreateBossSpawn(family, mapId: 10_015);
        var worldData = CreateWorldDataCatalog(family, spawn);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_015,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        if (killerAccepted)
        {
            killerSession = WithAcceptedQuest(killerSession, 6_126);
        }

        sessions.Set(killerConnection, killerSession);

        var teammateSent = new List<byte[]>();
        ConquerTcpConnection? teammateConnection = null;
        TeamSessionStateStore? teamStates = null;
        if (includeTeamMember)
        {
            const uint teammateUid = 8_888;
            teammateConnection = CreateConnection(teammateSent);
            var teammateAttributes = new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            };
            if (teammateDynamicId != 0)
            {
                teammateAttributes["DynamicID"] = teammateDynamicId.ToString(CultureInfo.InvariantCulture);
            }

            var teammateSession = CreateSession(
                teammateUid,
                "Teammate",
                mapId: teammateMapId,
                attributes: teammateAttributes);
            if (teammateAccepted)
            {
                teammateSession = WithAcceptedQuest(teammateSession, 6_126);
            }

            sessions.Set(teammateConnection, teammateSession);
            teamStates = new TeamSessionStateStore();
            teamStates.CreateTeam(killerConnection, KillerUid);
            Assert.True(teamStates.TryAddMember(killerConnection, teammateConnection, teammateUid));
        }

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue,
            teamStates: teamStates);

        Assert.True(sessions.TryGet(killerConnection, out var updatedKillerSession));
        Assert.NotNull(updatedKillerSession);
        WorldSession? updatedTeammateSession = null;
        if (teammateConnection is not null)
        {
            Assert.True(sessions.TryGet(teammateConnection, out updatedTeammateSession));
            Assert.NotNull(updatedTeammateSession);
        }

        return new Family4212Quest6126Result(
            updatedKillerSession!,
            killerSent,
            updatedTeammateSession,
            teammateSent,
            log.ToString(),
            worldData);
    }

    private sealed record Family4212Quest6126Result(
        WorldSession UpdatedKillerSession,
        List<byte[]> KillerSent,
        WorldSession? UpdatedTeammateSession,
        List<byte[]> TeammateSent,
        string Log,
        WorldDataCatalog WorldData);

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)> KillMap3071Family2700MonsterAsync(
        string rewardBranch,
        bool fullInventory = false)
    {
        var attributes = new Dictionary<string, string>
        {
            ["life"] = "1",
            ["lookface"] = "900",
            ["level"] = "138",
            ["defense"] = "0",
            ["defense2"] = "10000",
            ["attack_min"] = "1",
            ["attack_max"] = "1",
            ["attack_range"] = "0",
            ["Boss"] = "0",
        };
        var family = new Monster(2_700, "Map3071Family2700", attributes, []);
        var spawn = CreateBossSpawn(family, mapId: 3_071);
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: [CreateItemType(711_609)]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        var floorItems = new FloorItemRuntimeStateStore();
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_071,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacyMap3071Family2700RewardBranch"] = rewardBranch,
            });
        if (fullInventory)
        {
            var fillerItems = Enumerable.Range(1, 40)
                .Select(index => new PlayerItemSnapshotItem
                {
                    Uid = checked((uint)index),
                    ItemId = checked((uint)(100_000 + index)),
                    StackSize = 1,
                })
                .ToArray();
            session = session with
            {
                PlayerItems = new PlayerItemSnapshot(fillerItems, [], [], [], []),
            };
        }

        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems: floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)> KillMap3071Family7022MonsterAsync(
        string rewardBranch,
        bool fullInventory = false)
    {
        var attributes = new Dictionary<string, string>
        {
            ["life"] = "1",
            ["lookface"] = "900",
            ["level"] = "138",
            ["defense"] = "0",
            ["defense2"] = "10000",
            ["attack_min"] = "1",
            ["attack_max"] = "1",
            ["attack_range"] = "0",
            ["Boss"] = "0",
        };
        var family = new Monster(7_022, "Map3071Family7022", attributes, []);
        var spawn = CreateBossSpawn(family, mapId: 3_071);
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(711_609),
                CreateItemType(711_610),
                CreateItemType(711_611),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_071,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacyMap3071Family7022RewardBranch"] = rewardBranch,
            });
        if (fullInventory)
        {
            var fillerItems = Enumerable.Range(1, 40)
                .Select(index => new PlayerItemSnapshotItem
                {
                    Uid = checked((uint)index),
                    ItemId = checked((uint)(100_000 + index)),
                    StackSize = 1,
                })
                .ToArray();
            session = session with
            {
                PlayerItems = new PlayerItemSnapshot(fillerItems, [], [], [], []),
            };
        }

        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)> KillMap3935StarStonePackMonsterAsync(
        int familyId)
    {
        var attributes = new Dictionary<string, string>
        {
            ["life"] = "1",
            ["lookface"] = "900",
            ["level"] = "138",
            ["defense"] = "0",
            ["defense2"] = "10000",
            ["attack_min"] = "1",
            ["attack_max"] = "1",
            ["attack_range"] = "0",
            ["Boss"] = "0",
        };
        var family = new Monster(familyId, "Map3935StarStone", attributes, []);
        var spawn = CreateBossSpawn(family, mapId: 3_935);
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: [CreateItemType(3_600_031)]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_935,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)> KillMap3935SeniorDragonSoulMonsterAsync(
        bool questAccepted)
    {
        var attributes = new Dictionary<string, string>
        {
            ["life"] = "1",
            ["lookface"] = "900",
            ["level"] = "138",
            ["defense"] = "0",
            ["defense2"] = "10000",
            ["attack_min"] = "1",
            ["attack_max"] = "1",
            ["attack_range"] = "0",
            ["Boss"] = "0",
        };
        var family = new Monster(40_120, "Map3935SeniorDragonSoul", attributes, []);
        var spawn = CreateBossSpawn(family, mapId: 3_935);
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: [CreateItemType(3_600_027)]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_935,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        if (questAccepted)
        {
            session = WithAcceptedQuest(session, 35_034);
        }

        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)> KillMap3935BeastQuest35025MonsterAsync(
        int familyId,
        bool accepted,
        uint objective0 = 0)
    {
        var attributes = new Dictionary<string, string>
        {
            ["life"] = "1",
            ["lookface"] = "900",
            ["level"] = "138",
            ["defense"] = "0",
            ["defense2"] = "10000",
            ["attack_min"] = "1",
            ["attack_max"] = "1",
            ["attack_range"] = "0",
            ["Boss"] = "0",
        };
        var family = new Monster(familyId, "Map3935Beast", attributes, []);
        var spawn = CreateBossSpawn(family, mapId: 3_935);
        var worldData = CreateWorldDataCatalog(family, spawn);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_935,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        if (accepted)
        {
            session = session with
            {
                Character = session.Character with
                {
                    PlayerQuests = new PlayerQuestSnapshot(
                        [
                            new PlayerQuestSnapshotEntry(
                                35_025,
                                PlayerQuestStatus.Accepted,
                                0,
                                [objective0]),
                        ]),
                },
            };
        }

        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData, MonsterRuntimeStateStore MonsterStates)>
        KillMap3935BeastSpawnLoopMonsterAsync(
            int playerX,
            int playerY,
            int spawnedMonsterId,
            string spawnedName,
            bool forcedRate = true,
            bool preRegisterVisible = false)
    {
        var triggerAttributes = new Dictionary<string, string>
        {
            ["life"] = "1",
            ["lookface"] = "901",
            ["level"] = "138",
            ["defense"] = "0",
            ["defense2"] = "10000",
            ["attack_min"] = "1",
            ["attack_max"] = "1",
            ["attack_range"] = "0",
            ["Boss"] = "0",
        };
        var triggerFamily = new Monster(40_900, "Map3935BeastSpawnTrigger", triggerAttributes, []);
        var spawnedFamily = CreateBossFamily(spawnedMonsterId, spawnedName, boss: false);
        var spawn = CreateBossSpawn(triggerFamily, mapId: 3_935) with
        {
            BoundX = playerX,
            BoundY = playerY,
            SourceRelativePath = "3935/Map3935BeastSpawnTrigger/1.ini",
        };
        var worldData = CreateWorldDataCatalog(triggerFamily, spawn, extraMonsters: [spawnedFamily]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, triggerFamily, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        if (preRegisterVisible)
        {
            monsterStates.RegisterDynamicMonster(
                new LegacyMonsterInstance(
                    Uid: 900_000,
                    MapId: 3_935,
                    X: checked((ushort)playerX),
                    Y: checked((ushort)playerY),
                    Mesh: 900,
                    Level: 138,
                    Hitpoints: 200_000,
                    MaxLife: 200_000,
                    MonsterId: checked((uint)spawnedMonsterId),
                    Name: spawnedName,
                    Boss: false,
                    GeneratorId: 900_000,
                    SpawnIndex: 0,
                    DynamicId: 0),
                spawnedFamily);
        }

        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 3_935,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap3935BeastSpawnRate"] = forcedRate ? "1" : "0",
                }),
            playerX,
            playerY);
        session = session with
        {
            Character = session.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            35_025,
                            PlayerQuestStatus.Accepted,
                            0,
                            [0U]),
                    ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, monsterStates);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData, string PlayerQuestsRoot)>
        KillMap3935Quest35028MonsterAsync(uint objective0)
    {
        var family = CreateBossFamily(id: 40_901, name: "Map3935Quest35028Enemy", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 3_935);
        var worldData = CreateWorldDataCatalog(family, spawn);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        var playerQuestsRoot = Path.Combine(rootDirectory, "PlayersQuests-35028");
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_935,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        session = session with
        {
            Character = session.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            35_028,
                            PlayerQuestStatus.Accepted,
                            0,
                            [objective0]),
                    ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue,
            playerQuestsRoot: playerQuestsRoot);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, playerQuestsRoot);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)>
        KillMap3935Quest35007MonsterAsync(uint objective0, uint strikePoints)
    {
        var family = CreateBossFamily(id: 40_902, name: "Map3935Quest35007Enemy", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 3_935);
        var worldData = CreateWorldDataCatalog(family, spawn);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_935,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacyMap3935Quest35007StrikePoints"] = strikePoints.ToString(CultureInfo.InvariantCulture),
            });
        session = session with
        {
            Character = session.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            35_007,
                            PlayerQuestStatus.Accepted,
                            0,
                            [objective0]),
                    ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)>
        KillMap3998Family40903Quest3634MonsterAsync(
            uint objective0,
            PlayerQuestStatus status = PlayerQuestStatus.Accepted)
    {
        var family = CreateBossFamily(id: 40_903, name: "AngerRat", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 3_998);
        var worldData = CreateWorldDataCatalog(family, spawn);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_998,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        session = session with
        {
            Character = session.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            3_634,
                            status,
                            0,
                            [objective0]),
                    ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)>
        KillMap3998Family40900Quest3636MonsterAsync(
            bool forcedRate = true,
            ushort existingFragments = 0,
            bool fullInventory = false,
            PlayerQuestStatus status = PlayerQuestStatus.Accepted)
    {
        var family = CreateBossFamily(id: 40_900, name: "RuneFragmentCarrier", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 3_998);
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: [CreateItemType(3_008_752)]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_998,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacyMap3998Family40900RuneFragmentRate"] = forcedRate ? "1" : "0",
            });
        var inventoryItems = new List<PlayerItemSnapshotItem>();
        if (existingFragments != 0)
        {
            inventoryItems.Add(new PlayerItemSnapshotItem
            {
                Uid = 1,
                ItemId = 3_008_752,
                StackSize = existingFragments,
            });
        }

        if (fullInventory)
        {
            var firstUid = inventoryItems.Count == 0
                ? 1
                : checked((int)inventoryItems.Max(item => item.Uid) + 1);
            inventoryItems.AddRange(
                Enumerable.Range(firstUid, 40 - inventoryItems.Count)
                    .Select(index => new PlayerItemSnapshotItem
                    {
                        Uid = checked((uint)index),
                        ItemId = checked((uint)(100_000 + index)),
                        StackSize = 1,
                    }));
        }

        if (inventoryItems.Count != 0)
        {
            session = session with
            {
                PlayerItems = new PlayerItemSnapshot(inventoryItems.ToArray(), [], [], [], []),
            };
        }

        session = session with
        {
            Character = session.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            3_636,
                            status,
                            0,
                            [0U]),
                    ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)>
        KillMap3998Family40902Quest3638MonsterAsync(
            bool existingHammer = false,
            bool fullInventory = false,
            PlayerQuestStatus status = PlayerQuestStatus.Accepted)
    {
        var family = CreateBossFamily(id: 40_902, name: "VioletBatKing", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 3_998);
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: [CreateItemType(3_008_750)]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_998,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        var inventoryItems = new List<PlayerItemSnapshotItem>();
        if (existingHammer)
        {
            inventoryItems.Add(new PlayerItemSnapshotItem
            {
                Uid = 1,
                ItemId = 3_008_750,
                StackSize = 1,
            });
        }

        if (fullInventory)
        {
            var firstUid = inventoryItems.Count == 0
                ? 1
                : checked((int)inventoryItems.Max(item => item.Uid) + 1);
            inventoryItems.AddRange(
                Enumerable.Range(firstUid, 40 - inventoryItems.Count)
                    .Select(index => new PlayerItemSnapshotItem
                    {
                        Uid = checked((uint)index),
                        ItemId = checked((uint)(100_000 + index)),
                        StackSize = 1,
                    }));
        }

        if (inventoryItems.Count != 0)
        {
            session = session with
            {
                PlayerItems = new PlayerItemSnapshot(inventoryItems.ToArray(), [], [], [], []),
            };
        }

        session = session with
        {
            Character = session.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            3_638,
                            status,
                            0,
                            [0U]),
                    ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)>
        KillMap3998Family40904Quest3641MonsterAsync(
            uint objective0,
            PlayerQuestStatus status = PlayerQuestStatus.Accepted)
    {
        var family = CreateBossFamily(id: 40_904, name: "LavaScorpion", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 3_998);
        var worldData = CreateWorldDataCatalog(family, spawn);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_998,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        session = session with
        {
            Character = session.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            3_641,
                            status,
                            0,
                            [objective0]),
                    ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)>
        KillMap3998Family40908Quest3644MonsterAsync(
            ushort existingStones = 0,
            bool fullInventory = false,
            PlayerQuestStatus status = PlayerQuestStatus.Accepted)
    {
        var family = CreateBossFamily(id: 40_908, name: "BuildingStoneCarrier", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 3_998);
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: [CreateItemType(3_008_742)]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_998,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        var inventoryItems = new List<PlayerItemSnapshotItem>();
        if (existingStones != 0)
        {
            inventoryItems.Add(new PlayerItemSnapshotItem
            {
                Uid = 1,
                ItemId = 3_008_742,
                StackSize = existingStones,
            });
        }

        if (fullInventory)
        {
            var firstUid = inventoryItems.Count == 0
                ? 1
                : checked((int)inventoryItems.Max(item => item.Uid) + 1);
            inventoryItems.AddRange(
                Enumerable.Range(firstUid, 40 - inventoryItems.Count)
                    .Select(index => new PlayerItemSnapshotItem
                    {
                        Uid = checked((uint)index),
                        ItemId = checked((uint)(100_000 + index)),
                        StackSize = 1,
                    }));
        }

        if (inventoryItems.Count != 0)
        {
            session = session with
            {
                PlayerItems = new PlayerItemSnapshot(inventoryItems.ToArray(), [], [], [], []),
            };
        }

        session = session with
        {
            Character = session.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            3_644,
                            status,
                            0,
                            [0U]),
                    ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)>
        KillMap3998Family40907Quest3648MonsterAsync(
            ushort existingOres = 0,
            bool fullInventory = false,
            PlayerQuestStatus status = PlayerQuestStatus.Accepted)
    {
        var family = CreateBossFamily(id: 40_907, name: "StarOreCarrier", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 3_998);
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: [CreateItemType(3_008_748)]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_998,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        var inventoryItems = new List<PlayerItemSnapshotItem>();
        if (existingOres != 0)
        {
            inventoryItems.Add(new PlayerItemSnapshotItem
            {
                Uid = 1,
                ItemId = 3_008_748,
                StackSize = existingOres,
            });
        }

        if (fullInventory)
        {
            var firstUid = inventoryItems.Count == 0
                ? 1
                : checked((int)inventoryItems.Max(item => item.Uid) + 1);
            inventoryItems.AddRange(
                Enumerable.Range(firstUid, 40 - inventoryItems.Count)
                    .Select(index => new PlayerItemSnapshotItem
                    {
                        Uid = checked((uint)index),
                        ItemId = checked((uint)(100_000 + index)),
                        StackSize = 1,
                    }));
        }

        if (inventoryItems.Count != 0)
        {
            session = session with
            {
                PlayerItems = new PlayerItemSnapshot(inventoryItems.ToArray(), [], [], [], []),
            };
        }

        session = session with
        {
            Character = session.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            3_648,
                            status,
                            0,
                            [0U]),
                    ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)>
        KillMap3998Family40905Quest3645MonsterAsync(
            bool forcedRate = true,
            bool existingWheel = false,
            bool fullInventory = false,
            PlayerQuestStatus status = PlayerQuestStatus.Accepted)
    {
        var family = CreateBossFamily(id: 40_905, name: "ClawedRockDevil", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 3_998);
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: [CreateItemType(3_008_743)]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_998,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacyMap3998Family40905WheelOfNatureRate"] = forcedRate ? "1" : "0",
            });
        var inventoryItems = new List<PlayerItemSnapshotItem>();
        if (existingWheel)
        {
            inventoryItems.Add(new PlayerItemSnapshotItem
            {
                Uid = 1,
                ItemId = 3_008_743,
                StackSize = 1,
            });
        }

        if (fullInventory)
        {
            var firstUid = inventoryItems.Count == 0
                ? 1
                : checked((int)inventoryItems.Max(item => item.Uid) + 1);
            inventoryItems.AddRange(
                Enumerable.Range(firstUid, 40 - inventoryItems.Count)
                    .Select(index => new PlayerItemSnapshotItem
                    {
                        Uid = checked((uint)index),
                        ItemId = checked((uint)(100_000 + index)),
                        StackSize = 1,
                    }));
        }

        if (inventoryItems.Count != 0)
        {
            session = session with
            {
                PlayerItems = new PlayerItemSnapshot(inventoryItems.ToArray(), [], [], [], []),
            };
        }

        session = session with
        {
            Character = session.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            3_645,
                            status,
                            0,
                            [0U]),
                    ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)>
        KillMap3998Family40906Quest3646MonsterAsync(
            bool forcedRate = true,
            ushort existingFragments = 0,
            bool existingEarthForce = false,
            bool fullInventory = false,
            PlayerQuestStatus status = PlayerQuestStatus.Accepted)
    {
        var family = CreateBossFamily(id: 40_906, name: "EarthForceCarrier", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 3_998);
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(3_008_754),
                CreateItemType(3_008_745),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 3_998,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacyMap3998Family40906EarthForceRate"] = forcedRate ? "1" : "0",
            });
        var inventoryItems = new List<PlayerItemSnapshotItem>();
        if (existingFragments != 0)
        {
            inventoryItems.Add(new PlayerItemSnapshotItem
            {
                Uid = 1,
                ItemId = 3_008_754,
                StackSize = existingFragments,
            });
        }

        if (existingEarthForce)
        {
            inventoryItems.Add(new PlayerItemSnapshotItem
            {
                Uid = inventoryItems.Count == 0 ? 1U : 2U,
                ItemId = 3_008_745,
                StackSize = 1,
            });
        }

        if (fullInventory)
        {
            var firstUid = inventoryItems.Count == 0
                ? 1
                : checked((int)inventoryItems.Max(item => item.Uid) + 1);
            inventoryItems.AddRange(
                Enumerable.Range(firstUid, 40 - inventoryItems.Count)
                    .Select(index => new PlayerItemSnapshotItem
                    {
                        Uid = checked((uint)index),
                        ItemId = checked((uint)(100_000 + index)),
                        StackSize = 1,
                    }));
        }

        if (inventoryItems.Count != 0)
        {
            session = session with
            {
                PlayerItems = new PlayerItemSnapshot(inventoryItems.ToArray(), [], [], [], []),
            };
        }

        session = session with
        {
            Character = session.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            3_646,
                            status,
                            0,
                            [0U]),
                    ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)>
        KillMap1002Family1PheasantMonsterAsync(
            PlayerQuestStatus? quest1307Status = PlayerQuestStatus.Accepted,
            uint quest1307Objective = 0,
            PlayerQuestStatus? quest824Status = null,
            uint quest824Objective = 0)
    {
        var family = CreateBossFamily(id: 1, name: "Pheasant", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_002);
        var worldData = CreateWorldDataCatalog(family, spawn);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_002,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        var quests = new List<PlayerQuestSnapshotEntry>();
        if (quest1307Status is { } status1307)
        {
            quests.Add(new PlayerQuestSnapshotEntry(
                1_307,
                status1307,
                0,
                [quest1307Objective]));
        }

        if (quest824Status is { } status824)
        {
            quests.Add(new PlayerQuestSnapshotEntry(
                824,
                status824,
                0,
                [quest824Objective]));
        }

        if (quests.Count != 0)
        {
            session = session with
            {
                Character = session.Character with
                {
                    PlayerQuests = new PlayerQuestSnapshot(quests),
                },
            };
        }

        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)>
        KillMap1002Family3RobinMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            ushort existingFeathers = 0,
            bool includeFeatherItemType = true)
    {
        var family = CreateBossFamily(id: 3, name: "Robin", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_002);
        var xuLiangAnchor = new QuestNpcAnchor(
            "Finish",
            "FinishNpcId",
            Variant: null,
            NpcId: 7_992,
            MapId: 1_002,
            X: 368,
            Y: 643,
            Name: "XuLiang",
            MapName: "WindPlain",
            RawValue: "7992,1002,368,643,XuLiang,WindPlain",
            SourceLine: 4_304,
            Fields: ["7992", "1002", "368", "643", "XuLiang", "WindPlain"]);
        var artisanLuoAnchor = new QuestNpcAnchor(
            "Finish",
            "FinishNpcId",
            Variant: null,
            NpcId: 4_467,
            MapId: 1_002,
            X: 342,
            Y: 640,
            Name: "ArtisanLuo",
            MapName: "WindPlain",
            RawValue: "4467,1002,342,640,ArtisanLuo,WindPlain",
            SourceLine: 5_278,
            Fields: ["4467", "1002", "342", "640", "ArtisanLuo", "WindPlain"]);
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: includeFeatherItemType ? [CreateItemType(711_238)] : [],
            questMissions:
            [
                CreateQuestMission(
                    2_365,
                    intentAmount: 2,
                    finishNpcAnchors: [xuLiangAnchor]),
                CreateQuestMission(
                    1_010,
                    intentAmount: 1,
                    finishNpcAnchors: [artisanLuoAnchor]),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_002,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        if (existingFeathers != 0)
        {
            session = session with
            {
                PlayerItems = new PlayerItemSnapshot(
                    [
                        new PlayerItemSnapshotItem
                        {
                            Uid = 1,
                            ItemId = 711_238,
                            StackSize = existingFeathers,
                        },
                    ],
                    [],
                    [],
                    [],
                    []),
            };
        }

        session = session with
        {
            Character = session.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)>
        KillMap1002Family4ApparitionMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool existingHandkerchief = false,
            bool includeHandkerchiefItemType = true)
    {
        var family = CreateBossFamily(id: 4, name: "Apparition", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_002);
        var ruHuaAnchor = new QuestNpcAnchor(
            "Finish",
            "FinishNpcId",
            Variant: null,
            NpcId: 7_993,
            MapId: 1_002,
            X: 495,
            Y: 622,
            Name: "RuHua",
            MapName: "WindPlain",
            RawValue: "7993,1002,495,622,RuHua,WindPlain",
            SourceLine: 4_317,
            Fields: ["7993", "1002", "495", "622", "RuHua", "WindPlain"]);
        var fortunetellerAnchor = new QuestNpcAnchor(
            "Finish",
            "FinishNpcId",
            Variant: null,
            NpcId: 600_050,
            MapId: 1_002,
            X: 489,
            Y: 622,
            Name: "Fortuneteller",
            MapName: "WindPlain",
            RawValue: "600050,1002,489,622,Fortuneteller,WindPlain",
            SourceLine: 4_326,
            Fields: ["600050", "1002", "489", "622", "Fortuneteller", "WindPlain"]);
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: includeHandkerchiefItemType ? [CreateItemType(711_241)] : [],
            questMissions:
            [
                CreateQuestMission(
                    1_013,
                    intentAmount: 1,
                    finishNpcAnchors: [ruHuaAnchor]),
                CreateQuestMission(
                    1_016,
                    intentAmount: 1,
                    finishNpcAnchors: [fortunetellerAnchor]),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_002,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        if (existingHandkerchief)
        {
            session = session with
            {
                PlayerItems = new PlayerItemSnapshot(
                    [
                        new PlayerItemSnapshotItem
                        {
                            Uid = 1,
                            ItemId = 711_241,
                            StackSize = 1,
                        },
                    ],
                    [],
                    [],
                    [],
                    []),
            };
        }

        session = session with
        {
            Character = session.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)>
        KillMap1013DynamicFamily14333Quest1748MonsterAsync(
            uint objective0,
            PlayerQuestStatus status = PlayerQuestStatus.Accepted,
            ulong dynamicId = 2_468)
    {
        var family = CreateBossFamily(id: 14_333, name: "FranticMonkey", boss: false);
        var spawnAttributes = new Dictionary<string, string>();
        if (dynamicId != 0)
        {
            spawnAttributes["DynamicID"] = dynamicId.ToString(CultureInfo.InvariantCulture);
        }

        var spawn = CreateBossSpawn(family, mapId: 1_013) with
        {
            Attributes = spawnAttributes,
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            questMissions:
            [
                CreateQuestMission(1_748, intentAmount: 1),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var attributes = new Dictionary<string, string>
        {
            ["LegacyMonsterDropRoll"] = "40",
        };
        if (dynamicId != 0)
        {
            attributes["DynamicID"] = dynamicId.ToString(CultureInfo.InvariantCulture);
        }

        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_013,
            attributes: attributes);
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(
                        1_748,
                        status,
                        0,
                        [objective0]),
                ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        FloorItemRuntimeStateStore FloorItems)> KillMap1015Family18BirdmanMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceGroundDropRate = true,
            bool forceBirdBloodRate = true,
            bool forceGoldenBeakRate = true,
            bool forceBirdmanFeathersRate = true,
            bool forceBirdmanArmRate = true,
            bool forceToolBagRate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 18, name: "Birdman", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_015) with
        {
            BoundX = 830,
            BoundY = 584,
            SourceRelativePath = "1015/Birdman/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(729_094),
                CreateItemType(721_906),
                CreateItemType(711_464),
                CreateItemType(720_986),
                CreateItemType(711_438),
                CreateItemType(711_439),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_015,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacyMap1015Family18Ground729094Rate"] = forceGroundDropRate ? "1" : "0",
                ["LegacyMap1015Family18BirdBloodRate"] = forceBirdBloodRate ? "1" : "0",
                ["LegacyMap1015Family18GoldenBeakRate"] = forceGoldenBeakRate ? "1" : "0",
                ["LegacyMap1015Family18BirdmanFeathersRate"] = forceBirdmanFeathersRate ? "1" : "0",
                ["LegacyMap1015Family18BirdmanArmRate"] = forceBirdmanArmRate ? "1" : "0",
                ["LegacyMap1015Family18ToolBagRate"] = forceToolBagRate ? "1" : "0",
            });
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, floorItems);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)>
        KillMap1000Family2163CapriceLeaderMonsterAsync(
            PlayerQuestStatus questStatus,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 2_163, name: "CapriceLeader", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_000) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1000/CapriceLeader/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(721_777),
                CreateItemType(721_782),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_000,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            170,
            230);
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(
                        525,
                        questStatus,
                        0,
                        [0U]),
                ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        MonsterRuntimeStateStore MonsterStates)> KillMap1000Family2179CapriceMonsterAsync(
            bool forcedRate = true,
            PlayerQuestStatus questStatus = PlayerQuestStatus.Accepted,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 2_179, name: "Caprice", boss: false);
        var spawnedFamily = CreateBossFamily(id: 2_163, name: "CapriceLeader", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_000) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1000/Caprice/1.txt",
            Attributes = new Dictionary<string, string>
            {
                ["DynamicID"] = "2468",
            },
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: [CreateItemType(721_782)],
            extraMonsters: [spawnedFamily]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_000,
                attributes: new Dictionary<string, string>
                {
                    ["DynamicID"] = "2468",
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1000Family2179CapriceLeaderRate"] = forcedRate ? "1" : "0",
                }),
            170,
            230);
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(
                        525,
                        questStatus,
                        0,
                        [0U]),
                ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, monsterStates);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)>
        KillMap1000Family55434417AngryBladeGhostMonsterAsync(
            uint objective0,
            PlayerQuestStatus questStatus = PlayerQuestStatus.Accepted)
    {
        var family = CreateBossFamily(id: 55_434_417, name: "AngryBladeGhost", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_000) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1000/AngryBladeGhost/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            questMissions:
            [
                CreateQuestMission(1_498, intentAmount: 3),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_000,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            170,
            230);
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(
                        1_498,
                        questStatus,
                        0,
                        [objective0]),
                ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)>
        KillMap1000Family76BladeGhostMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceFangRate = true,
            bool forceStoneSpellRate = true,
            bool forceAntidoteRate = true,
            bool forceEarRate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 76, name: "Level83BladeGhost", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_000) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1000/Level83BladeGhost/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(721_846),
                CreateItemType(721_843),
                CreateItemType(720_968),
                CreateItemType(711_407),
            ],
            questMissions:
            [
                CreateQuestMission(1_499, intentAmount: 30),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_000,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1000Family76BladeGhostFangRate"] = forceFangRate ? "1" : "0",
                    ["LegacyMap1000Family76StoneSpellRate"] = forceStoneSpellRate ? "1" : "0",
                    ["LegacyMap1000Family76AntidoteRate"] = forceAntidoteRate ? "1" : "0",
                    ["LegacyMap1000Family76BladeGhostEarRate"] = forceEarRate ? "1" : "0",
                }),
            170,
            230);
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        FloorItemRuntimeStateStore FloorItems)> KillMap1000Family17BladeGhostMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceBladeGhostEarRate = true,
            bool forceStraightHookRate = true,
            bool forceSharpBladeRate = true,
            bool forceSharpAxeRate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 17, name: "BladeGhost", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_000) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1000/BladeGhost/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(721_845),
                CreateItemType(711_413),
                CreateItemType(711_414),
                CreateItemType(711_406),
                CreateItemType(711_409),
            ],
            questMissions:
            [
                CreateQuestMission(1_802, intentAmount: 200),
                CreateQuestMission(1_493, intentAmount: 50),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_000,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1000Family17BladeGhostEarRate"] = forceBladeGhostEarRate ? "1" : "0",
                    ["LegacyMap1000Family17StraightHookRate"] = forceStraightHookRate ? "1" : "0",
                    ["LegacyMap1000Family17SharpBladeRate"] = forceSharpBladeRate ? "1" : "0",
                    ["LegacyMap1000Family17SharpAxeRate"] = forceSharpAxeRate ? "1" : "0",
                }),
            170,
            230);
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, floorItems);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1000Family14SandMonsterMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceSandbagRate = true,
            bool forceSandPillRate = true,
            bool forceQuest1453Rate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 14, name: "SandMonster", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_000) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1000/SandMonster/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(711_341),
                CreateItemType(711_410),
                CreateItemType(711_338),
            ],
            questMissions:
            [
                CreateQuestMission(1_464, intentAmount: 40),
                CreateQuestMission(1_451, intentAmount: 50),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_000,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1000Family14SandbagRate"] = forceSandbagRate ? "1" : "0",
                    ["LegacyMap1000Family14SandPillRate"] = forceSandPillRate ? "1" : "0",
                    ["LegacyMap1000Family14Quest1453Rate"] = forceQuest1453Rate ? "1" : "0",
                }),
            170,
            230);
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        FloorItemRuntimeStateStore FloorItems)> KillMap1000Family73Level68SandMonsterMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceSandEssenceRate = true)
    {
        var family = CreateBossFamily(id: 73, name: "Level68SandMonster", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_000) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1000/SandMonsterL68/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(711_343),
            ],
            questMissions:
            [
                CreateQuestMission(1_464, intentAmount: 2),
                CreateQuestMission(1_463, intentAmount: 1),
                CreateQuestMission(1_460, intentAmount: 1),
                CreateQuestMission(1_461, intentAmount: 1),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_000,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1000Family73SandEssenceRate"] = forceSandEssenceRate ? "1" : "0",
                }),
            170,
            230);
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, floorItems);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1020Family14336MonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests)
    {
        var family = CreateBossFamily(id: 14_336, name: "Family14336", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_020) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1020/Family14336/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            questMissions:
            [
                CreateQuestMission(1_356, intentAmount: 1),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_020,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            170,
            230);
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        MonsterRuntimeStateStore MonsterStates)> KillMap1020Family14334ApeLeaderTriggerMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            uint questMultiple = 1,
            bool forceApeLeaderRate = true)
    {
        var family = CreateBossFamily(id: 14_334, name: "ApeLeaderTrigger", boss: false);
        var leaderFamily = CreateBossFamily(id: 14_335, name: "ApeLeader", boss: true);
        var spawn = CreateBossSpawn(family, mapId: 1_020) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1020/ApeLeaderTrigger/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            extraMonsters: [leaderFamily],
            questMissions:
            [
                CreateQuestMission(1_749, intentAmount: 2),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_020,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1020Family14334ApeLeaderRate"] = forceApeLeaderRate ? "1" : "0",
                    ["QuestMultiple"] = questMultiple.ToString(CultureInfo.InvariantCulture),
                }),
            monster.X,
            monster.Y);
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, monsterStates);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        FloorItemRuntimeStateStore FloorItems)> KillMap1020Family10MonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceQuest1311Rate = true,
            bool forceQuest1746Rate = true)
    {
        var family = CreateBossFamily(id: 10, name: "Map1020Family10", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_020) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1020/Family10/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(711_352),
                CreateItemType(729_976),
                CreateItemType(721_699),
            ],
            questMissions:
            [
                CreateQuestMission(1_311, intentAmount: 1),
                CreateQuestMission(1_303, intentAmount: 1),
                CreateQuestMission(1_314, intentAmount: 1),
                CreateQuestMission(1_746, intentAmount: 1),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_020,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1020Family10Quest1311Rate"] = forceQuest1311Rate ? "1" : "0",
                    ["LegacyMap1020Family10Quest1746Rate"] = forceQuest1746Rate ? "1" : "0",
                }),
            monster.X,
            monster.Y);
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, floorItems);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1020Family8305MonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests)
    {
        var family = CreateBossFamily(id: 8_305, name: "MonkeyKing", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_020) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1020/MonkeyKing/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(720_863),
            ],
            questMissions:
            [
                CreateQuestMission(1_305, intentAmount: 1),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_020,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            monster.X,
            monster.Y);
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1020Family69MonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests)
    {
        var family = CreateBossFamily(id: 69, name: "Map1020Family69", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_020) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1020/Family69/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            questMissions:
            [
                CreateQuestMission(1_304, intentAmount: 1),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_020,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            monster.X,
            monster.Y);
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        FloorItemRuntimeStateStore FloorItems)> KillMap1020Family11223VultureMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceVultureFeatherRate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 11_223, name: "Vulture", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_020) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1020/Vulture/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(729_976),
            ],
            questMissions:
            [
                CreateQuestMission(1_306, intentAmount: 1),
                CreateQuestMission(1_301, intentAmount: 1),
                CreateQuestMission(1_736, intentAmount: 2),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_020,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1020Family11223VultureFeatherRate"] = forceVultureFeatherRate ? "1" : "0",
                }),
            monster.X,
            monster.Y);
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, floorItems);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1020Family70GiantApeL58MonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests)
    {
        var family = CreateBossFamily(id: 70, name: "GiantApeL58", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_020) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1020/GiantApeL58/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            questMissions:
            [
                CreateQuestMission(1_753, intentAmount: 3),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_020,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            170,
            230);
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1011Family106RockMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests)
    {
        var family = CreateBossFamily(id: 106, name: "RockMonster", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_011) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1011/RockMonster/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            questMissions:
            [
                CreateQuestMission(1_753, intentAmount: 3),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_011,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            170,
            230);
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        FloorItemRuntimeStateStore FloorItems)> KillMap1011Family11007BanditMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceBanditRate = true)
    {
        var family = CreateBossFamily(id: 11_007, name: "Bandit", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_011) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1011/Bandit/1.txt",
        };
        var yuJingAnchor = new QuestNpcAnchor(
            "Finish",
            "FinishNpcId",
            Variant: null,
            NpcId: 8_267,
            MapId: 1_011,
            X: 222,
            Y: 333,
            Name: "YuJing",
            MapName: "PhoenixCastle",
            RawValue: "8267,1011,222,333,YuJing,PhoenixCastle",
            SourceLine: 1_103,
            Fields: ["8267", "1011", "222", "333", "YuJing", "PhoenixCastle"]);
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(721_263),
                CreateItemType(729_094, durability: 77),
            ],
            questMissions:
            [
                CreateQuestMission(
                    1_103,
                    intentAmount: 1,
                    finishNpcAnchors: [yuJingAnchor]),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_011,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1011Family11007BanditDropRate"] = forceBanditRate ? "1" : "0",
                }),
            170,
            230);
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, floorItems);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1011Family7BanditMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests)
    {
        var family = CreateBossFamily(id: 7, name: "Bandit", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_011) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1011/Bandit7/1.txt",
        };
        var forestGovernorAnchor = new QuestNpcAnchor(
            "Finish",
            "FinishNpcId",
            Variant: null,
            NpcId: 8_266,
            MapId: 1_011,
            X: 227,
            Y: 360,
            Name: "ForestGuvernor",
            MapName: "PhoenixCastle",
            RawValue: "8266,1011,227,360,ForestGuvernor,PhoenixCastle",
            SourceLine: 1_114,
            Fields: ["8266", "1011", "227", "360", "ForestGuvernor", "PhoenixCastle"]);
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(711_322),
            ],
            questMissions:
            [
                CreateQuestMission(
                    1_114,
                    intentAmount: 1,
                    finishNpcAnchors: [forestGovernorAnchor]),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_011,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            170,
            230);
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1011Family11008BanditLeaderMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests)
    {
        var family = CreateBossFamily(id: 11_008, name: "BanditLeader", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_011) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1011/BanditLeader/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            questMissions:
            [
                CreateQuestMission(1_118, intentAmount: 1),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_011,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            170,
            230);
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        FloorItemRuntimeStateStore FloorItems)> KillMap1020Family12ThunderApeMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceQuest1327Rate = true,
            bool forceQuest1329Rate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 12, name: "ThunderApe", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_020) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1020/ThunderApe/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(711_353),
                CreateItemType(720_851),
            ],
            questMissions:
            [
                CreateQuestMission(1_324, intentAmount: 1),
                CreateQuestMission(1_327, intentAmount: 1),
                CreateQuestMission(1_329, intentAmount: 1),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_020,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1020Family12Quest1327Rate"] = forceQuest1327Rate ? "1" : "0",
                    ["LegacyMap1020Family12Quest1329Rate"] = forceQuest1329Rate ? "1" : "0",
                }),
            170,
            230);
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, floorItems);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        FloorItemRuntimeStateStore FloorItems)> KillMap1020Family11GiantApeMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceQuest1363Rate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 11, name: "GiantApe", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_020) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1020/GiantApe/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(711_357),
                CreateItemType(711_358),
                CreateItemType(711_359),
            ],
            questMissions:
            [
                CreateQuestMission(1_316, intentAmount: 1),
                CreateQuestMission(1_363, intentAmount: 3),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_020,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1020Family11Quest1363Rate"] = forceQuest1363Rate ? "1" : "0",
                }),
            170,
            230);
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, floorItems);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        FloorItemRuntimeStateStore FloorItems)> KillMap1020Family71ThunderApeL58MonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceQuest1328Rate = true)
    {
        var family = CreateBossFamily(id: 71, name: "ThunderApeL58", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_020) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1020/ThunderApeL58/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(720_850),
            ],
            questMissions:
            [
                CreateQuestMission(1_325, intentAmount: 1),
                CreateQuestMission(1_328, intentAmount: 1),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_020,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1020Family71Quest1328Rate"] = forceQuest1328Rate ? "1" : "0",
                }),
            170,
            230);
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, floorItems);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        FloorItemRuntimeStateStore FloorItems)> KillMap1020Family13SnakemanMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceQuest1341Rate = true,
            bool forceQuest1333Rate = true,
            bool forceQuest1783Rate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 13, name: "Snakeman", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_020) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1020/Snakeman/1.txt",
        };
        var generalJuddAnchor = new QuestNpcAnchor(
            "Finish",
            "FinishNpcId",
            Variant: null,
            NpcId: 1_611,
            MapId: 1_002,
            X: 345,
            Y: 251,
            Name: "GeneralJudd",
            MapName: "TwinCity",
            RawValue: "1611,1002,345,251,GeneralJudd,TwinCity",
            SourceLine: 5_892,
            Fields: ["1611", "1002", "345", "251", "GeneralJudd", "TwinCity"]);
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(729_978),
                CreateItemType(720_854),
                CreateItemType(720_856),
                CreateItemType(720_857),
            ],
            questMissions:
            [
                CreateQuestMission(1_333, intentAmount: 1),
                CreateQuestMission(1_335, intentAmount: 1),
                CreateQuestMission(1_337, intentAmount: 1),
                CreateQuestMission(1_341, intentAmount: 1),
                CreateQuestMission(1_345, intentAmount: 1),
                CreateQuestMission(1_346, intentAmount: 1),
                CreateQuestMission(1_360, intentAmount: 1),
                CreateQuestMission(1_753, intentAmount: 3),
                CreateQuestMission(
                    1_783,
                    intentAmount: 1,
                    finishNpcAnchors: [generalJuddAnchor]),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_020,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1020Family13Quest1341Rate"] = forceQuest1341Rate ? "1" : "0",
                    ["LegacyMap1020Family13Quest1333Rate"] = forceQuest1333Rate ? "1" : "0",
                    ["LegacyMap1020Family13Quest1783Rate"] = forceQuest1783Rate ? "1" : "0",
                }),
            170,
            230);
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, floorItems);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        FloorItemRuntimeStateStore FloorItems)> KillMap1020Family72SnakemanL63MonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceQuest1341Rate = true)
    {
        var family = CreateBossFamily(id: 72, name: "SnakemanL63", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_020) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1020/SnakemanL63/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(720_858),
            ],
            questMissions:
            [
                CreateQuestMission(1_341, intentAmount: 1),
                CreateQuestMission(1_343, intentAmount: 1),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_020,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1020Family72Quest1341Rate"] = forceQuest1341Rate ? "1" : "0",
                }),
            170,
            230);
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, floorItems);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        FloorItemRuntimeStateStore FloorItems)> KillMap1000Family15HillMonsterMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceHeartRate = true,
            bool forceHammerRate = true,
            bool forceHeavyAxeRate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 15, name: "HillMonster", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_000) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1000/HillMonster/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(711_411),
                CreateItemType(711_344),
                CreateItemType(711_415),
            ],
            questMissions:
            [
                CreateQuestMission(1_476, intentAmount: 2),
                CreateQuestMission(1_466, intentAmount: 1),
                CreateQuestMission(1_465, intentAmount: 1),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_000,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1000Family15HeartRate"] = forceHeartRate ? "1" : "0",
                    ["LegacyMap1000Family15HammerRate"] = forceHammerRate ? "1" : "0",
                    ["LegacyMap1000Family15HeavyAxeRate"] = forceHeavyAxeRate ? "1" : "0",
                }),
            170,
            230);
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, floorItems);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        FloorItemRuntimeStateStore FloorItems)> KillMap1000Family74HillMonsterMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceSecretLetterRate = true,
            bool forceArmorRate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 74, name: "Level73HillMonster", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_000) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1000/Level73HillMonster/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(720_975),
                CreateItemType(711_416),
            ],
            questMissions:
            [
                CreateQuestMission(1_471, intentAmount: 30),
                CreateQuestMission(1_476, intentAmount: 2),
                CreateQuestMission(1_472, intentAmount: 20),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_000,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1000Family74SecretLetterRate"] = forceSecretLetterRate ? "1" : "0",
                    ["LegacyMap1000Family74ArmorRate"] = forceArmorRate ? "1" : "0",
                }),
            170,
            230);
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, floorItems);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1000Family75RockMonsterMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceStoneRate = true,
            int stoneChoice = 0,
            bool forceMarbleRate = true,
            bool forceMonsterTailRate = true,
            bool forceThunderStoneRate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 75, name: "Level78RockMonster", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_000) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1000/Level78RockMonster/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(721_952),
                CreateItemType(721_953),
                CreateItemType(721_951),
                CreateItemType(710_581),
                CreateItemType(711_402),
                CreateItemType(711_405),
            ],
            questMissions:
            [
                CreateQuestMission(1_484, intentAmount: 30),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_000,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1000Family75StoneRate"] = forceStoneRate ? "1" : "0",
                    ["LegacyMap1000Family75StoneChoice"] = stoneChoice.ToString(CultureInfo.InvariantCulture),
                    ["LegacyMap1000Family75MarbleRate"] = forceMarbleRate ? "1" : "0",
                    ["LegacyMap1000Family75MonsterTailRate"] = forceMonsterTailRate ? "1" : "0",
                    ["LegacyMap1000Family75ThunderStoneRate"] = forceThunderStoneRate ? "1" : "0",
                }),
            170,
            230);
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        FloorItemRuntimeStateStore FloorItems)> KillMap1000Family51RockMonsterMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceRockMonsterTailRate = true,
            bool forceColorfulStoneRate = true,
            bool forceGrindstoneRate = true,
            bool forceSlabstoneRate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 51, name: "RockMonster", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_000) with
        {
            BoundX = 170,
            BoundY = 230,
            SourceRelativePath = "1000/RockMonster/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(711_412),
                CreateItemType(711_349),
                CreateItemType(711_399),
                CreateItemType(720_970),
            ],
            questMissions:
            [
                CreateQuestMission(1_479, intentAmount: 50),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_000,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1000Family51RockMonsterTailRate"] = forceRockMonsterTailRate ? "1" : "0",
                    ["LegacyMap1000Family51ColorfulStoneRate"] = forceColorfulStoneRate ? "1" : "0",
                    ["LegacyMap1000Family51GrindstoneRate"] = forceGrindstoneRate ? "1" : "0",
                    ["LegacyMap1000Family51SlabstoneRate"] = forceSlabstoneRate ? "1" : "0",
                }),
            170,
            230);
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, floorItems);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1015Family77BirdmanL88MonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceBirdBloodRate = true,
            bool forceBirdBeakRate = true,
            bool forceSharpClawRate = true,
            bool forceBirdmanFeatherRate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 77, name: "BirdmanL88", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_015) with
        {
            BoundX = 830,
            BoundY = 584,
            SourceRelativePath = "1015/BirdmanL88/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(721_906),
                CreateItemType(711_445),
                CreateItemType(711_446),
                CreateItemType(711_449),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_015,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacyMap1015Family77BirdBloodRate"] = forceBirdBloodRate ? "1" : "0",
                ["LegacyMap1015Family77BirdBeakRate"] = forceBirdBeakRate ? "1" : "0",
                ["LegacyMap1015Family77SharpClawRate"] = forceSharpClawRate ? "1" : "0",
                ["LegacyMap1015Family77BirdmanFeatherRate"] = forceBirdmanFeatherRate ? "1" : "0",
            });
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1015Family4431179HawkLeaderMonsterAsync()
    {
        var family = CreateBossFamily(id: 4_431_179, name: "HawkLeader", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_015) with
        {
            BoundX = 830,
            BoundY = 584,
            SourceRelativePath = "1015/HawkLeader/1.txt",
        };
        var worldData = CreateWorldDataCatalog(family, spawn);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_015,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(1_629, PlayerQuestStatus.Accepted, 0, [4U]),
                ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1015Family78HawKingL93MonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceHawkClawRate = true,
            bool forceGallbladerrRate = true,
            bool forceQuest1628Rate = true,
            bool forceHawkBloodRate = true,
            bool forceHawkClawFangRate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 78, name: "HawKingL93", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_015) with
        {
            BoundX = 830,
            BoundY = 584,
            SourceRelativePath = "1015/HawKingL93/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(721_910),
                CreateItemType(721_909),
                CreateItemType(720_993),
                CreateItemType(711_456),
                CreateItemType(711_457),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_015,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacyMap1015Family78HawkClawRate"] = forceHawkClawRate ? "1" : "0",
                ["LegacyMap1015Family78GallbladerrRate"] = forceGallbladerrRate ? "1" : "0",
                ["LegacyMap1015Family78Quest1628Rate"] = forceQuest1628Rate ? "1" : "0",
                ["LegacyMap1015Family78HawkBloodRate"] = forceHawkBloodRate ? "1" : "0",
                ["LegacyMap1015Family78HawkClawFangRate"] = forceHawkClawFangRate ? "1" : "0",
            });
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1015Family84BandittiMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceYarnRate = true,
            bool forceFlowerWineRate = true,
            bool forceRoyalSwordRate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 84, name: "Banditti", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_015) with
        {
            BoundX = 830,
            BoundY = 584,
            SourceRelativePath = "1015/Banditti84/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(711_476),
                CreateItemType(711_477),
                CreateItemType(711_475),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_015,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacyMap1015Family84YarnBallRate"] = forceYarnRate ? "1" : "0",
                ["LegacyMap1015Family84FlowerWineRate"] = forceFlowerWineRate ? "1" : "0",
                ["LegacyMap1015Family84RoyalSwordRate"] = forceRoyalSwordRate ? "1" : "0",
            });
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1015Family79BanditL98MonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests)
    {
        var family = CreateBossFamily(id: 79, name: "BanditL98", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_015) with
        {
            BoundX = 830,
            BoundY = 584,
            SourceRelativePath = "1015/BanditL98/1.txt",
        };
        var worldData = CreateWorldDataCatalog(family, spawn);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_015,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1015Family79Quest1642MonsterAsync(
            bool forcedRate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null,
            PlayerQuestStatus questStatus = PlayerQuestStatus.Accepted)
    {
        var family = CreateBossFamily(id: 79, name: "Banditti", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_015) with
        {
            BoundX = 830,
            BoundY = 584,
            SourceRelativePath = "1015/Banditti/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: [CreateItemType(711_474)]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_015,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacyMap1015Family79ShotSaberRate"] = forcedRate ? "1" : "0",
            });
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(
                        1_642,
                        questStatus,
                        0,
                        [0U]),
                ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1015TokenMonsterAsync(
            int familyId,
            uint questId,
            uint itemId,
            string rateAttribute,
            bool forcedRate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null,
            PlayerQuestStatus questStatus = PlayerQuestStatus.Accepted)
    {
        var family = CreateBossFamily(id: familyId, name: $"Map1015Family{familyId.ToString(CultureInfo.InvariantCulture)}", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_015) with
        {
            BoundX = 830,
            BoundY = 584,
            SourceRelativePath = $"1015/Family{familyId.ToString(CultureInfo.InvariantCulture)}/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: [CreateItemType(checked((int)itemId))]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_015,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
                [rateAttribute] = forcedRate ? "1" : "0",
            });
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(
                        questId,
                        questStatus,
                        0,
                        [0U]),
                ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        MonsterRuntimeStateStore MonsterStates)> KillMap1015Family55BanditL97MonsterAsync(
            uint objective0,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null,
            PlayerQuestStatus questStatus = PlayerQuestStatus.Accepted,
            bool preRegisterBanditLeader = false,
            bool forceFoodSupplyRate = true,
            bool forceBaconRate = true,
            IReadOnlyList<PlayerQuestSnapshotEntry>? quests = null)
    {
        var family = CreateBossFamily(id: 55, name: "BanditL97", boss: false);
        var spawnedFamily = CreateBossFamily(id: 2_224, name: "BanditLeaderL97", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_015) with
        {
            BoundX = 830,
            BoundY = 584,
            SourceRelativePath = "1015/BanditL97/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(710_562),
                CreateItemType(711_468),
                CreateItemType(711_467),
            ],
            extraMonsters: [spawnedFamily]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        if (preRegisterBanditLeader)
        {
            monsterStates.RegisterDynamicMonster(
                new LegacyMonsterInstance(
                    Uid: 900_000,
                    MapId: 1_015,
                    X: 831,
                    Y: 585,
                    Mesh: 900,
                    Level: 138,
                    Hitpoints: 200_000,
                    MaxLife: 200_000,
                    MonsterId: 2_224,
                    Name: "BanditLeaderL97",
                    Boss: false,
                    GeneratorId: 900_000,
                    SpawnIndex: 0,
                    DynamicId: 0),
                spawnedFamily);
        }

        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_015,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1015Family55FoodSupplyRate"] = forceFoodSupplyRate ? "1" : "0",
                    ["LegacyMap1015Family55BaconRate"] = forceBaconRate ? "1" : "0",
                }),
            830,
            584);
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    quests ??
                    [
                        new PlayerQuestSnapshotEntry(
                            1_820,
                            questStatus,
                            0,
                            [objective0]),
                    ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, monsterStates);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1015Family19HawKingMonsterAsync(
            IReadOnlyList<PlayerQuestSnapshotEntry> quests,
            bool forceHawkClawRate = true,
            bool forceGallbladerrRate = true,
            bool forceFeatherRate = true,
            bool forceOldBookRate = true,
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 19, name: "HawKing", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_015) with
        {
            BoundX = 830,
            BoundY = 584,
            SourceRelativePath = "1015/HawKing/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items:
            [
                CreateItemType(721_910),
                CreateItemType(721_909),
                CreateItemType(711_454),
                CreateItemType(720_987),
                CreateItemType(720_988),
                CreateItemType(720_989),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_015,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacyMap1015Family19HawkClawRate"] = forceHawkClawRate ? "1" : "0",
                ["LegacyMap1015Family19GallbladerrRate"] = forceGallbladerrRate ? "1" : "0",
                ["LegacyMap1015Family19FeatherRate"] = forceFeatherRate ? "1" : "0",
                ["LegacyMap1015Family19OldBookRate"] = forceOldBookRate ? "1" : "0",
            });
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(quests),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1015Family2224RedTokenMonsterAsync(
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null,
            PlayerQuestStatus questStatus = PlayerQuestStatus.Accepted)
    {
        var family = CreateBossFamily(id: 2_224, name: "BanditLeaderL97", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_015) with
        {
            BoundX = 830,
            BoundY = 584,
            SourceRelativePath = "1015/BanditLeaderL97/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: [CreateItemType(710_562)]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_015,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(
                        1_820,
                        questStatus,
                        0,
                        [0U]),
                ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1015Family2156TroutElfMonsterAsync(
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null,
            PlayerQuestStatus questStatus = PlayerQuestStatus.Accepted)
    {
        var family = CreateBossFamily(id: 2_156, name: "TroutElf", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_015) with
        {
            BoundX = 830,
            BoundY = 584,
            SourceRelativePath = "1015/TroutElf/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: [CreateItemType(721_778)]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_015,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(
                        522,
                        questStatus,
                        0,
                        [0U]),
                ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1015Family2157BloodyShawnMonsterAsync(
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null,
            PlayerQuestStatus questStatus = PlayerQuestStatus.Accepted)
    {
        var family = CreateBossFamily(id: 2_157, name: "BloodyShawn", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_015) with
        {
            BoundX = 830,
            BoundY = 584,
            SourceRelativePath = "1015/BloodyShawn/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: [CreateItemType(721_781)]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_015,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(
                        523,
                        questStatus,
                        0,
                        [0U]),
                ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData)> KillMap1015Family2171JadeCourtesanMonsterAsync(
            IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var family = CreateBossFamily(id: 2_171, name: "JadeCourtesan", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_015) with
        {
            BoundX = 830,
            BoundY = 584,
            SourceRelativePath = "1015/JadeCourtesan/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: [CreateItemType(710_217)]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_015,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        var session = baseSession with
        {
            PlayerItems = new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(
                        408,
                        PlayerQuestStatus.Accepted,
                        0,
                        [9U]),
                ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        FloorItemRuntimeStateStore FloorItems)> KillMap1780Family12314463KidSiezerMonsterAsync()
    {
        var family = CreateBossFamily(id: 12_314_463, name: "KidSiezer", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_780);
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: [CreateItemType(721_714)]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_780,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            floorItems,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, floorItems);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        MonsterRuntimeStateStore MonsterStates)> KillMap1780Family12314462KidHunterMonsterAsync(
            bool preRegisterKidSiezer = false)
    {
        var family = CreateBossFamily(id: 12_314_462, name: "KidHunter", boss: false);
        var kidSiezerFamily = CreateBossFamily(id: 12_314_463, name: "KidSiezer", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_780) with
        {
            BoundX = 500,
            BoundY = 501,
            SourceRelativePath = "1780/KidHunter/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            extraMonsters: [kidSiezerFamily]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        if (preRegisterKidSiezer)
        {
            monsterStates.RegisterDynamicMonster(
                new LegacyMonsterInstance(
                    Uid: 900_000,
                    MapId: 1_780,
                    X: 500,
                    Y: 501,
                    Mesh: 900,
                    Level: 138,
                    Hitpoints: 200_000,
                    MaxLife: 200_000,
                    MonsterId: 12_314_463,
                    Name: "KidSiezer",
                    Boss: false,
                    GeneratorId: 900_000,
                    SpawnIndex: 0,
                    DynamicId: 0),
                kidSiezerFamily);
        }

        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_780,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                }),
            500,
            501);
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, monsterStates);
    }

    private async Task<(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        WorldDataCatalog WorldData,
        MonsterRuntimeStateStore MonsterStates)> KillMap1780OtherFamilyMonsterAsync(
            bool forcedRate = true,
            bool preRegisterKidHunter = false,
            bool existingBezoar = false)
    {
        var family = CreateBossFamily(id: 12_314_460, name: "Map1780OtherMonster", boss: false);
        var kidHunterFamily = CreateBossFamily(id: 12_314_462, name: "KidHunter", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_780) with
        {
            BoundX = 500,
            BoundY = 501,
            SourceRelativePath = "1780/OtherMonster/1.txt",
        };
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            extraMonsters: [kidHunterFamily]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        if (preRegisterKidHunter)
        {
            monsterStates.RegisterDynamicMonster(
                new LegacyMonsterInstance(
                    Uid: 900_000,
                    MapId: 1_780,
                    X: 500,
                    Y: 501,
                    Mesh: 900,
                    Level: 138,
                    Hitpoints: 200_000,
                    MaxLife: 200_000,
                    MonsterId: 12_314_462,
                    Name: "KidHunter",
                    Boss: false,
                    GeneratorId: 900_000,
                    SpawnIndex: 0,
                    DynamicId: 0),
                kidHunterFamily);
        }

        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = WithPosition(
            CreateSession(
                KillerUid,
                "LastHit",
                mapId: 1_780,
                attributes: new Dictionary<string, string>
                {
                    ["LegacyMonsterDropRoll"] = "40",
                    ["LegacyMap1780KidHunterSpawnRate"] = forcedRate ? "1" : "0",
                }),
            500,
            501);
        if (existingBezoar)
        {
            session = session with
            {
                PlayerItems = new PlayerItemSnapshot(
                    [
                        new PlayerItemSnapshotItem
                        {
                            Uid = 1,
                            ItemId = 721_714,
                            StackSize = 1,
                        },
                    ],
                    [],
                    [],
                    [],
                    []),
            };
        }

        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData, monsterStates);
    }

    private static IReadOnlyList<(int Rank, uint Uid, string Name, uint Damage)> CreateRankedHunters()
    {
        return Enumerable.Range(1, 10)
            .Select(rank => (
                Rank: rank,
                Uid: checked((uint)(1_000 + rank)),
                Name: FormattableString.Invariant($"Hunter{rank}"),
                Damage: checked((uint)(10_000 - (rank * 500)))))
            .ToArray();
    }

    private static Monster CreateBossFamily(int id = BossFamilyId, string name = "MailboxBoss", bool boss = true)
    {
        return new Monster(
            id,
            name,
            new Dictionary<string, string>
            {
                ["life"] = "200000",
                ["lookface"] = "900",
                ["level"] = "138",
                ["defense"] = "0",
                ["defense2"] = "10000",
                ["attack_min"] = "1",
                ["attack_max"] = "1",
                ["attack_range"] = "0",
                ["Boss"] = boss ? "1" : "0",
            },
            []);
    }

    private static MonsterSpawn CreateBossSpawn(Monster bossFamily, int mapId = 1_015)
    {
        return new MonsterSpawn(
            GeneratorId: 1,
            MapId: mapId,
            MonsterTypeId: bossFamily.Id,
            BoundX: 830,
            BoundY: 584,
            BoundWidth: 1,
            BoundHeight: 1,
            MaxNpc: 1,
            RestSeconds: 5,
            MaxPerGenerator: 1,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: null,
            SourceMonsterName: bossFamily.Name,
            SourceRelativePath: "1015/1.txt",
            Attributes: new Dictionary<string, string>());
    }

    private static InnerPowerRankEntry CreateInnerPowerRow(int characterId, string characterName, int potency)
    {
        var fields = new[]
        {
            characterName,
            characterId.ToString(CultureInfo.InvariantCulture),
            potency.ToString(CultureInfo.InvariantCulture),
            "0",
            string.Empty,
        };

        return new InnerPowerRankEntry(
            characterName,
            characterId,
            RankingFields: fields.Skip(2).Take(fields.Length - 3).ToArray(),
            SourceFile: "fixture://InnerPower.txt",
            LineNumber: 1,
            RawLine: string.Join('/', fields),
            Fields: fields);
    }

    private static Dictionary<string, string> DeityLandInventoryRewardsDisabledAttributes(
        params (string Key, string Value)[] overrides)
    {
        var attributes = new Dictionary<string, string>
        {
            ["LegacyDeityLandReward4050001Rate"] = "0",
            ["LegacyDeityLandReward4060001Rate"] = "0",
            ["LegacyDeityLandRedRuneFragmentRate"] = "0",
            ["LegacyDeityLandYellowRuneFragmentRate"] = "0",
            ["LegacyDeityLandReward3306368Rate"] = "0",
            ["LegacyDeityLandReward3330770Rate"] = "0",
            ["LegacyDeityLandReward3316793Rate"] = "0",
            ["LegacyDeityLandReward3009102Rate"] = "0",
            ["LegacyDeityLandReward3009103Rate"] = "0",
            ["LegacyDeityLandReward3009104Rate"] = "0",
            ["LegacyDeityLandReward4040001Rate"] = "0",
            ["LegacyDeityLandReward3303483Rate"] = "0",
            ["LegacyDeityLandReward3307083Rate"] = "0",
            ["LegacyDeityLandReward3304961Rate"] = "0",
            ["LegacyDeityLandInventoryRelicFirstRate"] = "0",
            ["LegacyDeityLandInventoryRelicSecondRate"] = "0",
            ["LegacyDeityLandRareChestKeyFirstRate"] = "0",
            ["LegacyDeityLandRareChestKeySecondRate"] = "0",
            ["LegacyDeityLandEpicChestKeyFirstRate"] = "0",
            ["LegacyDeityLandEpicChestKeySecondRate"] = "0",
        };

        foreach (var (key, value) in overrides)
        {
            attributes[key] = value;
        }

        return attributes;
    }

    private static Dictionary<string, string> CreateEpicTrojanCompletionAttributes()
    {
        return new Dictionary<string, string>
        {
            ["DynamicID"] = "2468",
            ["LegacyMonsterDropRoll"] = "40",
            ["StageEpicTrojanQuest"] = "52",
            ["EpicTrojanMrMirrorPrograss"] = "18",
            ["EpicTrojanEvilArrayPoints"] = "7",
            ["EpicTrojanAbysalStage"] = "3",
            ["EpicTrijanKillGhostReaver"] = "1",
            ["ChangeEpicTrojan"] = "9",
            ["ChangeArrayEpicTrojan"] = "8",
            ["ChangeMr_MirrorEpicTrojan"] = "7",
            ["ChangeGeneralPakEpicTrojan"] = "6",
            ["CanChangeEpicMaterial"] = "0",
            ["CanChangeArrayEpicMaterial"] = "0",
            ["CanChangeMr_MirrorEpicMaterial"] = "0",
            ["CanChangGeneralPakMaterial"] = "0",
        };
    }

    private static WorldSession CreateSession(
        uint uid,
        string name,
        int mapId = 1_015,
        long classCode = 142,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return new WorldSession(
            AccountId: uid,
            Username: name.ToLowerInvariant(),
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: name,
                Body: 1_003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: mapId,
                X: 830,
                Y: 584,
                PreviousMapId: mapId,
                PreviousMapX: 830,
                PreviousMapY: 584,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 5_000,
                MinMana: 500,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "fixture://character",
                Attributes: attributes is null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static void WriteCharacterSnapshotForSession(string usersRoot, WorldSession session)
    {
        var character = session.Character;
        var attributeLines = string.Join(
            Environment.NewLine,
            character.Attributes.Select(pair => pair.Key + "=" + pair.Value));
        File.WriteAllText(
            Path.Combine(usersRoot, character.Uid.ToString(CultureInfo.InvariantCulture) + ".ini"),
            $$"""
            [Character]
            UID={{character.Uid}}
            Body={{character.Body}}
            Face={{character.Face}}
            Name={{character.Name}}
            Class={{character.Class}}
            Map={{character.MapId}}
            X={{character.X}}
            Y={{character.Y}}
            FirstClass={{character.FirstClass}}
            SecoundeClass={{character.SecondClass}}
            Reborn={{character.Reborn}}
            Level={{character.Level}}
            Haire={{character.Hair}}
            Experience={{character.Experience}}
            PMap={{character.PreviousMapId}}
            PMapX={{character.PreviousMapX}}
            PMapY={{character.PreviousMapY}}
            Agility={{character.Agility}}
            Strength={{character.Strength}}
            Vitaliti={{character.Vitality}}
            Spirit={{character.Spirit}}
            Atributes={{character.AttributePoints}}
            MinHitPoints={{character.MinHitPoints}}
            MinMana={{character.MinMana}}
            MaxMana={{character.MaxMana}}
            ConquerPoints={{character.ConquerPoints}}
            BankCps={{character.BankConquerPoints}}
            BoundConquerPoints={{character.BoundConquerPoints}}
            Money={{character.Money}}
            PkPoints={{character.PkPoints}}
            Online={{character.Online}}
            ExpBallUsed={{character.ExpBallUsed}}
            {{attributeLines}}
            """);
    }

    private static WorldSession WithAcceptedQuest(WorldSession session, uint questId)
    {
        return session with
        {
            Character = session.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            questId,
                            PlayerQuestStatus.Accepted,
                            0,
                            [0U]),
                    ]),
            },
        };
    }

    private static WorldSession WithPosition(WorldSession session, int x, int y)
    {
        return session with
        {
            Character = session.Character with
            {
                X = x,
                Y = y,
                PreviousMapX = x,
                PreviousMapY = y,
            },
        };
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

    private static byte[] CreateEmptyFrame(ushort opcode)
    {
        return new PacketWriter(opcode, payloadLength: 0).ToArray();
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static List<LegacyMailListPacket> ParseMailLists(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMailListPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMailListPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacyMsgMessagePacket> ParseMessages(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMsgMessagePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacyStringPacket> ParseStringPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyStringPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyStringPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<MsgActionPacket> ParseActions(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgActionPacket>();
        foreach (var bytes in frames)
        {
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacyChiInfoPacket> ParseChiInfoPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyChiInfoPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyChiInfoPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacyFloorItemPacket> ParseFloorItems(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyFloorItemPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyFloorItemPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacyGameItemPacket> ParseGameItems(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyGameItemPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<MsgItemUsagePacket> ParseItemUsage(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgItemUsagePacket>();
        foreach (var bytes in frames)
        {
            if (MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacyMonsterSpawnPacket> ParseMonsterSpawns(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMonsterSpawnPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMonsterSpawnPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<MsgQuestDataObjectiveFrame> ParseQuestObjectiveFrames(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgQuestDataObjectiveFrame>();
        foreach (var bytes in frames)
        {
            if (MsgQuestDataPacket.TryParseObjectiveFrame(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacyQuestListFrame> ParseQuestListFrames(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyQuestListFrame>();
        foreach (var bytes in frames)
        {
            if (LegacyQuestListPacket.TryParseReviewFrame(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static void AssertDropCount(IReadOnlyList<LegacyFloorItemPacket> floorItems, uint itemId, int count)
    {
        Assert.Equal(count, floorItems.Count(item => item.ItemId == itemId));
    }

    private static WorldDataCatalog CreateWorldDataCatalog(
        Monster monster,
        MonsterSpawn spawn,
        IReadOnlyList<ItemType>? items = null,
        IReadOnlyList<SpiritRate>? spiritRates = null,
        IReadOnlyList<Monster>? extraMonsters = null,
        IReadOnlyList<QuestMission>? questMissions = null,
        IReadOnlyList<XuanbaoRandomAttribute>? xuanbaoRandomAttributes = null,
        IReadOnlyList<InnerPowerRankEntry>? innerPowerRanks = null,
        IReadOnlyList<MagicType>? magic = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-13T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://legacy-boss-harm-rank-mail",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items ?? [],
            itemAddBonuses: [],
            levelExperience: [],
            runeLevelExperience: [],
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: [],
            furniture: [],
            hairColorTypes: [],
            hairFaceStorageTypes: [],
            coatStorageTypes: [],
            coatStorageAttributes: [],
            coatColorRules: [],
            xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [],
            xuanbaoRandomAttributes: xuanbaoRandomAttributes ?? [],
            christmasNpcs: [],
            tutorTypes: [],
            tutorBattleLimitTypes: [],
            beastAttributes: [],
            fateExperience: [],
            spiritRates: spiritRates ?? [],
            hundredWeapons: [],
            gouyuTypes: [],
            gouyuImmortals: [],
            specialEffectAttributes: [],
            daoqiDictTypes: [],
            syndicateFormationTypes: [],
            syndicateSkillTypes: [],
            disdainRules: [],
            jiangHuAttributes: [],
            jiangHuCultivateStatuses: [],
            refineryBoxes: [],
            refinerySouls: [],
            refineryMaterials: [],
            stateCountFiles: [],
            binarySeedFiles: [],
            emptySeedFiles: [],
            backupSeedFiles: [],
            pokerTables: [],
            rouletteTables: [],
            npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, "", 0, []), [], "fixture://activity"),
            meltingTypeRules: [],
            lotteryPrizes: [],
            arenaRecords: [],
            elitePkRecords: [],
            teamElitePkRecords: [],
            skillTeamPkRecords: [],
            classPkWarRecords: [],
            combatGearRanks: [],
            prestigeRanks: [],
            innerPowerRanks: innerPowerRanks ?? [],
            runeRankings: [],
            staticStatues: [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: questMissions ?? [],
            quizQuestions: [],
            magic: magic ?? [],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: extraMonsters is null ? [monster] : [monster, .. extraMonsters],
            monsterSpawns: [spawn],
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://neigong", 0, "", new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [],
            shopItems: [],
            exchangeShopGoods: [],
            exchangeShopGoodsEx: [],
            exchangeShopLevels: [],
            honorShopItems: [],
            honorShopRecommendations: [],
            racePointShopItems: [],
            racePointShopRecommendations: [],
            goldenLeagueShopItems: [],
            goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(),
            booths: [],
            boothItems: []);
    }

    private static QuestMission CreateQuestMission(
        int missionId,
        int intentAmount,
        IReadOnlyList<QuestNpcAnchor>? beginNpcAnchors = null,
        IReadOnlyList<QuestNpcAnchor>? finishNpcAnchors = null)
    {
        return new QuestMission(
            SectionId: missionId,
            SourceLine: missionId,
            TypeId: 21_972,
            MissionId: missionId,
            Name: null,
            LevelMin: null,
            LevelMax: null,
            Auto: null,
            First: null,
            PrerequisiteRaw: null,
            PrerequisiteMissionIds: [],
            MapId: null,
            Profession: null,
            Sex: null,
            FinishTime: null,
            ActivityBeginTime: null,
            ActivityEndTime: null,
            CompleteFlag: null,
            Prize: null,
            IntentionDescription: null,
            IntentAmount: intentAmount,
            Content: null,
            BeginNpcAnchors: beginNpcAnchors ?? [],
            FinishNpcAnchors: finishNpcAnchors ?? [],
            Intentions: Enumerable.Range(0, intentAmount)
                .Select(index => new QuestIntention(
                    Position: index,
                    Variant: null,
                    Key: FormattableString.Invariant($"Intention{index}"),
                    Text: "",
                    SourceLine: missionId))
                .ToArray(),
            PrizeItems: [],
            Entries: []);
    }

    private static IReadOnlyList<ItemType> CreateGenerateSoulsItemTypes()
    {
        return
        [
            CreateItemType(800_001, purificationLevel: 7),
            CreateItemType(800_002, purificationLevel: 7),
            CreateItemType(729_305, purificationLevel: 7),
            CreateItemType(700_001, purificationLevel: 6),
            CreateItemType(700_002, purificationLevel: 6),
            CreateItemType(350_001),
            CreateItemType(370_001),
        ];
    }

    private static IReadOnlyList<XuanbaoRandomAttribute> CreateRelicRandomAttributes()
    {
        return
        [
            XuanbaoRandomAttribute(4_100_001, LegacyChiAttributeType.Immunity, min: 110, max: 201),
            XuanbaoRandomAttribute(4_100_001, LegacyChiAttributeType.Breakthrough, min: 30, max: 101),
            XuanbaoRandomAttribute(4_100_001, LegacyChiAttributeType.AddMagicAttack, min: 600, max: 901),
        ];
    }

    private static IReadOnlyList<ItemType> CreateQueenOfEvilItemTypes()
    {
        return
        [
            CreateItemType(3_333_403),
            CreateItemType(3_335_128),
            CreateItemType(3_321_098),
            CreateItemType(3_321_107),
            CreateItemType(4_030_001),
            CreateItemType(4_030_101),
            CreateItemType(4_030_201),
            CreateItemType(4_030_301),
            CreateItemType(4_030_401),
            CreateItemType(4_030_002),
        ];
    }

    private static IReadOnlyList<ItemType> CreateDragonFurnaceItemTypes()
    {
        return
        [
            CreateItemType(4_200_001),
            CreateItemType(4_200_002),
        ];
    }

    private static IReadOnlyList<SpiritRate> CreateDragonFurnaceSpiritRates()
    {
        return
        [
            CreateSpiritRate(76, 4_200_001, oldType: 0, isItem: false, newAnimaId: 5),
            CreateSpiritRate(77, 4_200_001, oldType: 1, isItem: true, newAnimaId: 4_200_002),
            CreateSpiritRate(78, 4_200_001, oldType: 2, isItem: true, newAnimaId: 4_200_003),
        ];
    }

    private static SpiritRate CreateSpiritRate(
        int id,
        int itemTypeId,
        int oldType,
        bool isItem,
        int newAnimaId)
    {
        var fields = new[]
        {
            id.ToString(CultureInfo.InvariantCulture),
            "1",
            itemTypeId.ToString(CultureInfo.InvariantCulture),
            oldType.ToString(CultureInfo.InvariantCulture),
            isItem ? "1" : "0",
            newAnimaId.ToString(CultureInfo.InvariantCulture),
            "0",
            "0",
            "",
        };

        return new SpiritRate(
            id,
            1,
            itemTypeId,
            [oldType, isItem ? 1 : 0, newAnimaId, 0, 0],
            "fixture://spirit_rate.txt",
            id,
            string.Join("@@", fields),
            fields);
    }

    private static ItemType CreateItemType(int id, uint purificationLevel = 0, uint durability = 0)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = id.ToString(CultureInfo.InvariantCulture);
        fields[1] = FormattableString.Invariant($"Item{id}");
        fields[ItemType.PurificationLevelFieldIndex] = purificationLevel.ToString(CultureInfo.InvariantCulture);
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(id, fields[1], fields);
    }

    private static PlayerItemSnapshotItem CreateInventoryItem(uint uid, uint itemId, ushort stackSize = 1)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            StackSize = stackSize,
        };
    }

    private static XuanbaoRandomAttribute XuanbaoRandomAttribute(
        int itemTypeId,
        LegacyChiAttributeType attributeType,
        int min,
        int max)
    {
        var raw = FormattableString.Invariant($"{itemTypeId}@@{(byte)attributeType}@@{min}@@{max}@@");
        return new XuanbaoRandomAttribute(
            itemTypeId,
            (byte)attributeType,
            min,
            max,
            "fixture://xuanbao_rand_attr.txt",
            itemTypeId,
            raw,
            raw.Split("@@"));
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData($"fixture://{name}", [], [], [], info: null, pole: null, conductors: []);
    }
}
