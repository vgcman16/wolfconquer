using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyDeathSideEffectsParityTests
{
    private const uint CasterUid = 1_293_600;
    private const uint TargetUid = 1_293_601;
    private const ushort ThunderSpellId = 1_000;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task PlayerPlayerMagicDeathSendsLegacyDeadTalkAndRecordsDeathPositionAndStamps()
    {
        var worldData = CreateMagicCatalog(CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var origin = new DateTimeOffset(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);
        var clock = new DeathSideEffectsTimeProvider(origin);
        var deadTalks = 0;
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent, bytes =>
        {
            if (!LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message) ||
                message.ChatType != LegacyMsgMessagePacket.TalkChatType ||
                message.Content.Count <= 3 || message.Content[3] != "You are dead.")
                return;
            Assert.Equal(1, ++deadTalks);
            var atDeadTalk = Assert.Single(sessions.Snapshot(),
                active => active.Session.Character.Uid == TargetUid).Session;
            Assert.Equal(origin.AddSeconds(1), ParseTimestamp(
                atDeadTalk.Character.Attributes[LegacyPlayerDeathState.GhostStampAttribute]));
            Assert.False(atDeadTalk.Character.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
            Assert.Equal(100, atDeadTalk.Character.MinHitPoints);
            clock.Advance(TimeSpan.FromSeconds(2));
        });
        var casterSession = CreateSession();
        var targetSession = CreateSession(
            uid: TargetUid,
            name: "Target",
            x: 832,
            y: 584,
            minHitPoints: 100);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            TimeProvider: clock);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: CasterUid,
                OpponentUid: TargetUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(0, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal("832", updatedTargetSession.Character.Attributes[LegacyPlayerDeathState.DeadXAttribute]);
        Assert.Equal("584", updatedTargetSession.Character.Attributes[LegacyPlayerDeathState.DeadYAttribute]);
        var deadStamp = ParseTimestamp(updatedTargetSession.Character.Attributes[LegacyPlayerDeathState.DeadStampAttribute]);
        var ghostStamp = ParseTimestamp(updatedTargetSession.Character.Attributes[LegacyPlayerDeathState.GhostStampAttribute]);
        // Source GhostStamp precedes DeadTalk; DeadStamp is a DISTINCT later read.
        Assert.Equal(origin.AddSeconds(1), ghostStamp);
        Assert.Equal(origin.AddSeconds(2), deadStamp);
        Assert.Equal(origin.AddSeconds(2), clock.GetUtcNow());
        Assert.Equal(1, deadTalks);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(
            updatedTargetSession.Character.Attributes,
            LegacyPlayerDeathState.DeadStatusFlag));

        var deadMessage = Assert.Single(
            ParseLegacyMessages(targetSent),
            message => message.ChatType == LegacyMsgMessagePacket.TalkChatType &&
                message.Content.Count > 3 &&
                message.Content[3] == "You are dead.");
        Assert.Equal(LegacyMsgMessagePacket.RedColor, deadMessage.Color);
        Assert.Equal("SYSTEM", deadMessage.Content[0]);
        Assert.Equal("ALL", deadMessage.Content[1]);
    }

    private static WorldSession CreateSession(
        long uid = CasterUid,
        string name = "Caster",
        long x = 830,
        long y = 584,
        long minMana = 500,
        long minHitPoints = 5_000)
    {
        return new WorldSession(
            AccountId: 1,
            Username: name.ToLowerInvariant(),
            CreateCharacter(uid, name, x, y, minMana, minHitPoints),
            PlayerItemSnapshot.Empty,
            new PlayerSpellSnapshot(
            [
                new PlayerSpellSnapshotEntry(
                    ThunderSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0),
            ]),
            PlayerProficiencySnapshot.Empty);
    }

    private static CharacterSnapshot CreateCharacter(
        long uid,
        string name,
        long x,
        long y,
        long minMana,
        long minHitPoints)
    {
        return new CharacterSnapshot(
            Uid: uid,
            Name: name,
            Body: 1_003,
            Face: 0,
            Hair: 0,
            Class: 142,
            FirstClass: 142,
            SecondClass: 0,
            Reborn: 0,
            Level: 120,
            Experience: 0,
            MapId: 1_015,
            X: x,
            Y: y,
            PreviousMapId: 1_015,
            PreviousMapX: x,
            PreviousMapY: y,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 300,
            AttributePoints: 0,
            MinHitPoints: minHitPoints,
            MinMana: minMana,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://character",
            Attributes: new Dictionary<string, string>(),
            PaidNobility: new Dictionary<string, string>());
    }

    private static MagicType CreateMagicRow(
        ushort spellId,
        byte sort,
        ushort useMana,
        uint damage,
        int coldTime)
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = sort.ToString(CultureInfo.InvariantCulture);
        fields[3] = $"Spell{spellId}";
        fields[8] = "0";
        fields[9] = useMana.ToString(CultureInfo.InvariantCulture);
        fields[10] = damage.ToString(CultureInfo.InvariantCulture);
        fields[15] = "9";
        fields[33] = "1000";
        fields[47] = coldTime.ToString(CultureInfo.InvariantCulture);
        return new MagicType(spellId, spellId, $"Spell{spellId}", fields);
    }

    private static WorldDataCatalog CreateMagicCatalog(params MagicType[] magicRows)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://legacy-death-side-effects",
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
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: magicRows,
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

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData($"fixture://{name}", [], [], [], info: null, pole: null, conductors: []);
    }

    private static ConquerTcpConnection CreateSendingConnection(
        List<byte[]> sent,
        Action<byte[]>? onSend = null)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                var bytes = frame.ToArray();
                sent.Add(bytes);
                onSend?.Invoke(bytes);
                return ValueTask.CompletedTask;
            });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static List<LegacyMsgMessagePacket> ParseLegacyMessages(IEnumerable<byte[]> frames)
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

    private sealed class DeathSideEffectsTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan amount) => _utcNow += amount;
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}
