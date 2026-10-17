using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("LegacySuperGuildWarRuntime")]
public sealed class LegacySuperGuildWarBackendTests
{
    [Theory]
    [InlineData(516_076U, 191, 142, 1_537UL, 1_527UL)]
    [InlineData(516_078U, 166, 224, 1_511UL, 1_551UL)]
    public async Task WinnerGuildOfficerCanOpenAndCloseGate(uint npcId, int x, int y, ulong openMesh, ulong closedMesh)
    {
        WorldPacketHandlers.ResetLegacySuperGuildWarStateForTests();
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 3_868, x: x, y: y, guildId: 77, guildRank: 1);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            WorldData: CreateWorldDataCatalog(
                CreateSuperGuildWarInfo(winnerGuildId: 77),
                [CreateGateSobNpc(npcId, x, y, (int)closedMesh)]),
            Sessions: sessions);

        var opened = await WorldPacketHandlers.TryHandleLegacySuperGuildWarGateNpcAsync(
            context,
            session,
            npcId,
            optionId: 1,
            CancellationToken.None);

        Assert.True(opened);
        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == npcId &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Mesh && entry.Value == openMesh));
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "Gate is already opened.");
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);

        sent.Clear();
        var closed = await WorldPacketHandlers.TryHandleLegacySuperGuildWarGateNpcAsync(
            context,
            session,
            npcId,
            optionId: 2,
            CancellationToken.None);

        Assert.True(closed);
        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == npcId &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Mesh && entry.Value == closedMesh));
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "Gate is already closed.");
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WinnerGuildMemberCanEnterThroughGate()
    {
        WorldPacketHandlers.ResetLegacySuperGuildWarStateForTests();
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_002, x: 430, y: 378, guildId: 77, guildRank: 6);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            WorldData: CreateWorldDataCatalog(CreateSuperGuildWarInfo(winnerGuildId: 77)),
            Sessions: sessions);

        var handled = await WorldPacketHandlers.TryHandleLegacySuperGuildWarGateNpcAsync(
            context,
            session,
            516_080,
            optionId: 3,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(3_868, updatedSession.Character.MapId);
        Assert.Equal(287, updatedSession.Character.X);
        Assert.Equal(223, updatedSession.Character.Y);
        Assert.Contains(ParseMsgActions(sent), action => action.Type == MsgActionType.Teleport);
        Assert.Contains("WorldServer sent LegacyNpc teleport transition", log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc SuperGuildWarGate", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonWinnerGuildCannotMutateGate()
    {
        WorldPacketHandlers.ResetLegacySuperGuildWarStateForTests();
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 3_868, x: 166, y: 224, guildId: 88, guildRank: 1);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            WorldData: CreateWorldDataCatalog(
                CreateSuperGuildWarInfo(winnerGuildId: 77),
                [CreateGateSobNpc(516_078, 166, 224, 1_551)]),
            Sessions: sessions);

        var handled = await WorldPacketHandlers.TryHandleLegacySuperGuildWarGateNpcAsync(
            context,
            session,
            516_078,
            optionId: 1,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseMsgUpdates(sent));
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text is not null && reply.Text.StartsWith("Sorry,", StringComparison.Ordinal));
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void RewardClaimsAreBoundedPerWinnerAndRank()
    {
        var state = new LegacySuperGuildWarStateStore();
        var superGuildWarInfo = CreateSuperGuildWarInfo(winnerGuildId: 77, leaderReward: 1, deputyReward: 2);

        var firstLeader = state.TryClaimReward(1_001, guildId: 77, guildRank: 1, superGuildWarInfo);
        var secondLeader = state.TryClaimReward(1_002, guildId: 77, guildRank: 1, superGuildWarInfo);
        var duplicateLeader = state.TryClaimReward(1_001, guildId: 77, guildRank: 1, superGuildWarInfo);
        var firstDeputy = state.TryClaimReward(2_001, guildId: 77, guildRank: 2, superGuildWarInfo);
        var secondDeputy = state.TryClaimReward(2_002, guildId: 77, guildRank: 2, superGuildWarInfo);
        var thirdDeputy = state.TryClaimReward(2_003, guildId: 77, guildRank: 2, superGuildWarInfo);
        var outsider = state.TryClaimReward(3_001, guildId: 88, guildRank: 1, superGuildWarInfo);

        Assert.Equal(LegacySuperGuildWarRewardClaimStatus.Granted, firstLeader.Status);
        Assert.Equal(LegacySuperGuildWarRewardKind.Leader, firstLeader.Kind);
        Assert.Equal(0, firstLeader.RemainingAfterClaim);
        Assert.Equal(LegacySuperGuildWarRewardClaimStatus.Exhausted, secondLeader.Status);
        Assert.Equal(LegacySuperGuildWarRewardClaimStatus.AlreadyClaimed, duplicateLeader.Status);
        Assert.Equal(LegacySuperGuildWarRewardClaimStatus.Granted, firstDeputy.Status);
        Assert.Equal(1, firstDeputy.RemainingAfterClaim);
        Assert.Equal(LegacySuperGuildWarRewardClaimStatus.Granted, secondDeputy.Status);
        Assert.Equal(0, secondDeputy.RemainingAfterClaim);
        Assert.Equal(LegacySuperGuildWarRewardClaimStatus.Exhausted, thirdDeputy.Status);
        Assert.Equal(LegacySuperGuildWarRewardClaimStatus.NotWinnerGuild, outsider.Status);
    }

    private static WorldDataCatalog CreateWorldDataCatalog(
        GuildWarData superGuildWarInfo,
        IReadOnlyList<SobNpc>? sobNpcs = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-20T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://legacy-super-guild-war-backend",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [],
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
            xuanbaoRandomAttributes: [],
            christmasNpcs: [],
            tutorTypes: [],
            tutorBattleLimitTypes: [],
            beastAttributes: [],
            fateExperience: [],
            spiritRates: [],
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
            innerPowerRanks: [],
            runeRankings: [],
            staticStatues: [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: CreateSuperGuildWarInfo(winnerGuildId: 0),
            superGuildWarInfo: superGuildWarInfo,
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: [],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: sobNpcs ?? [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: [],
            monsterSpawns: [],
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

    private static GuildWarData CreateSuperGuildWarInfo(
        int? winnerGuildId,
        int? leaderReward = 1,
        int? deputyReward = 7,
        IReadOnlyDictionary<int, int>? leaderTop = null,
        IReadOnlyDictionary<int, int>? deputyTop = null)
    {
        return new GuildWarData(
            "fixture://SuperGuildWarInfo.ini",
            [],
            [],
            [],
            new GuildWarInfoData(
                winnerGuildId,
                Name: winnerGuildId is > 0 ? "WinnerGuild" : null,
                LeaderReward: leaderReward,
                DeputiLeaderReward: deputyReward,
                LeaderTop: leaderTop ?? new Dictionary<int, int>(),
                DeputiTop: deputyTop ?? new Dictionary<int, int>()),
            pole: null,
            conductors: []);
    }

    private static SobNpc CreateGateSobNpc(uint npcId, int x, int y, int mesh)
    {
        return new SobNpc(
            Id: checked((int)npcId),
            Name: "SuperGuildWarGate",
            Kind: 26,
            Lookface: mesh,
            MapId: 3_868,
            X: x,
            Y: y,
            Life: 10_000_000,
            MaxLife: 10_000_000,
            Unknown9: 1,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: checked((int)npcId),
            RawLine: "",
            Fields: []);
    }

    private static WorldSession CreateSession(
        long mapId,
        long x,
        long y,
        ulong guildId,
        ulong guildRank)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = "0",
            ["GuildID"] = guildId.ToString(CultureInfo.InvariantCulture),
            ["GuildRank"] = guildRank.ToString(CultureInfo.InvariantCulture),
        };

        return new WorldSession(
            AccountId: 100,
            Username: "super-guild-war-backend-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_700,
                Name: "GateHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: mapId,
                X: x,
                Y: y,
                PreviousMapId: mapId,
                PreviousMapX: x,
                PreviousMapY: y,
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
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<MsgActionPacket> ParseMsgActions(IEnumerable<byte[]> sent)
    {
        var packets = new List<MsgActionPacket>();
        foreach (var bytes in sent)
        {
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
