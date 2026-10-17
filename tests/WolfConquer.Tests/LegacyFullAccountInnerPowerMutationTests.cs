using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFullAccountInnerPowerMutationTests : IDisposable
{
    private const long AccountId = 1_293_875;
    private const uint CharacterUid = 1_293_875;
    private const uint NpcId = 465_123;
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-full-account-inner-power-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InsufficientBalanceKeepsExactSourceDialogBeforeInfrastructure()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(9_958, sent);
        var session = CreateSession(conquerPoints: 9, usersRoot: null);

        var handled = await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            NpcId,
            optionId: 19,
            input: string.Empty,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
            reply.Text == "You do not have 10 ConquerPoints with you.");
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.Text == "I'm just passing by." &&
            reply.OptionId == byte.MaxValue);
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(sent, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _));
    }

    [Fact]
    public async Task FixesEverySourceStageAndGongBeforeAlternatingCurrentWirePackets()
    {
        var result = await InvokeAsync(conquerPoints: 1_000);

        Assert.True(result.Handled);
        Assert.Equal("commit", result.ExposureOrder[0]);
        Assert.Equal(970, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(970, result.ReloadedCharacter.ConquerPoints);
        Assert.NotNull(result.PersistedAtCommit);
        Assert.Equal(970, result.PersistedAtCommit!.Value.Character.ConquerPoints);
        Assert.Equal(result.UpdatedCatalogRow.RawLine, result.PersistedAtCommit.Value.InnerPower.RawLine);
        Assert.Equal(result.UpdatedCatalogRow.RawLine, result.PersistedInnerPower.RawLine);
        Assert.Contains("OtherUser/1293000/5/0/", result.PersistedInnerPowerText, StringComparison.Ordinal);

        var fields = result.UpdatedCatalogRow.Fields;
        Assert.Equal("2", fields[3]);
        Assert.Equal(["1", "1", "2"], fields.Skip(4).Take(3).ToArray());
        Assert.Equal(["11", "100", "1", "5", "1"], fields.Skip(7).Take(5).ToArray());
        Assert.Equal(["99", "100", "1", "3", "0"], fields.Skip(12).Take(5).ToArray());
        Assert.Equal(["2", "1", "1"], fields.Skip(17).Take(3).ToArray());
        Assert.Equal(["21", "100", "1", "9", "0"], fields.Skip(20).Take(5).ToArray());
        Assert.Equal(string.Empty, fields[^1]);

        Assert.Equal(6, result.ClaimantSent.Count);
        Assert.Equal(3, result.ObserverSent.Count);
        var expectedBalances = new ulong[] { 990, 980, 970 };
        for (var index = 0; index < expectedBalances.Length; index++)
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(
                ReadFrame(result.ClaimantSent[index * 2]),
                out var update));
            var value = Assert.Single(update.Updates);
            Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, value.Type);
            Assert.Equal(expectedBalances[index], value.Value);
            Assert.Equal(LegacyMsgUpdatePacket.Opcode, ReadFrame(result.ClaimantSent[index * 2]).Header.Opcode);

            AssertWhiteTalk(result.ClaimantSent[index * 2 + 1]);
            AssertWhiteTalk(result.ObserverSent[index]);
        }

        Assert.DoesNotContain(result.ClaimantSent, bytes =>
            ReadFrame(bytes).Header.Opcode == WorldPacketOpcodes.LegacyInnerStrengthOpt);
        Assert.Contains("stages=2 gongs=3", result.Log, StringComparison.Ordinal);
        Assert.Contains("packetOrder=(ConquerPoints,WhiteTalk)*", result.Log, StringComparison.Ordinal);
        Assert.Contains("wire=7899", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);

        result.ClaimantSent.Clear();
        using var rootLog = new StringWriter();
        var rootHandled = await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
            result.Context with { Log = rootLog, Session = result.UpdatedSession },
            result.UpdatedSession,
            NpcId,
            optionId: 0,
            input: string.Empty,
            CancellationToken.None);
        Assert.True(rootHandled);
        Assert.DoesNotContain(ParseNpcReplies(result.ClaimantSent), reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option && reply.OptionId == 19);
    }

    [Fact]
    public async Task AggregateSourceCostUnderflowFencesBeforeDurableOrWireMutation()
    {
        var result = await InvokeAsync(conquerPoints: 10);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Empty(result.ClaimantSent);
        Assert.Empty(result.ObserverSent);
        Assert.Equal(result.OriginalCharacterBytes, result.PersistedCharacterBytes);
        Assert.Equal(result.OriginalInnerPowerBytes, result.PersistedInnerPowerBytes);
        Assert.Equal(result.OriginalCatalogRow.RawLine, result.UpdatedCatalogRow.RawLine);
        Assert.Contains("aggregate-source-cost-underflow:required=30", result.Log, StringComparison.Ordinal);
        Assert.Contains("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PersistenceLock.Character)]
    [InlineData(PersistenceLock.InnerPower)]
    public async Task EitherLockedPersistenceFileRollsBackBothBeforeExposure(PersistenceLock persistenceLock)
    {
        var result = await InvokeAsync(conquerPoints: 1_000, persistenceLock: persistenceLock);

        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Empty(result.ClaimantSent);
        Assert.Empty(result.ObserverSent);
        Assert.Equal(result.OriginalCharacterBytes, result.PersistedCharacterBytes);
        Assert.Equal(result.OriginalInnerPowerBytes, result.PersistedInnerPowerBytes);
        Assert.Equal(result.OriginalCatalogRow.RawLine, result.UpdatedCatalogRow.RawLine);
        Assert.Contains("transaction-write-failed:IOException", result.Log, StringComparison.Ordinal);
        Assert.Contains("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingSourceSlotMetadataFencesWithoutPartialUnlocks()
    {
        var result = await InvokeAsync(
            conquerPoints: 1_000,
            types:
            [
                CreateType(1, 1, 11, 5),
                CreateType(2, 1, 21, 9),
            ]);

        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Empty(result.ClaimantSent);
        Assert.Equal(result.OriginalCharacterBytes, result.PersistedCharacterBytes);
        Assert.Equal(result.OriginalInnerPowerBytes, result.PersistedInnerPowerBytes);
        Assert.Contains("inner-power-stage-1-slot-2-metadata-missing", result.Log, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        long conquerPoints,
        IReadOnlyList<NeiGongType>? types = null,
        PersistenceLock persistenceLock = PersistenceLock.None)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(invocationRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        var characterPath = Path.Combine(
            usersRoot,
            AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        var innerPowerPath = Path.Combine(invocationRoot, "InnerPower.txt");
        WriteCharacter(characterPath, conquerPoints);
        File.WriteAllText(
            innerPowerPath,
            "Count=2\r\n" +
            "OtherUser/1293000/5/0/\r\n" +
            "InnerPowerHero/1293875/7/1/1/0/2/11/2/0/1/1/99/7/0/3/0/\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var originalCharacterBytes = File.ReadAllBytes(characterPath);
        var originalInnerPowerBytes = File.ReadAllBytes(innerPowerPath);
        var parsedRows = RankingSnapshotParser.ParseInnerPowerRanksFile(innerPowerPath).ToArray();
        var originalRow = Assert.Single(parsedRows, row => row.CharacterId == CharacterUid);
        var secrets = new[] { CreateSecret(1, 2), CreateSecret(2, 1) };
        var sourceTypes = types ??
        [
            CreateType(1, 1, 11, 5),
            CreateType(1, 2, 12, 7),
            CreateType(2, 1, 21, 9),
        ];
        var worldData = CreateCatalog(parsedRows, secrets, sourceTypes);
        var characterLoader = new CharacterSnapshotLoader(usersRoot);
        var character = Assert.IsType<CharacterSnapshot>(characterLoader.LoadByAccountId(AccountId));
        var session = CreateSession(conquerPoints, usersRoot) with { Character = character };
        var claimantSent = new List<byte[]>();
        var observerSent = new List<byte[]>();
        var exposureOrder = new List<string>();
        var claimant = CreateConnection(9_958, claimantSent, exposureOrder);
        var observer = CreateConnection(9_959, observerSent);
        PersistedState? persistedAtCommit = null;
        var sessions = new WorldSessionStore((connection, previous, _) =>
        {
            if (!ReferenceEquals(connection, claimant) || previous is null)
            {
                return;
            }

            persistedAtCommit = new PersistedState(
                Assert.IsType<CharacterSnapshot>(characterLoader.LoadByAccountId(AccountId)),
                Assert.Single(
                    RankingSnapshotParser.ParseInnerPowerRanksFile(innerPowerPath),
                    row => row.CharacterId == CharacterUid));
            exposureOrder.Add("commit");
        });
        sessions.Set(claimant, session);
        sessions.Set(
            observer,
            session with
            {
                AccountId = AccountId + 1,
                Character = character with { Uid = CharacterUid + 1, Name = "InnerPowerObserver" },
                UsersRoot = null,
            });
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            claimant,
            log,
            Session: session,
            WorldData: worldData,
            Sessions: sessions);

        FileStream? locked = persistenceLock switch
        {
            PersistenceLock.Character => OpenPersistenceLock(characterPath),
            PersistenceLock.InnerPower => OpenPersistenceLock(innerPowerPath),
            _ => null,
        };

        bool handled;
        try
        {
            handled = await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
                context,
                session,
                NpcId,
                optionId: 19,
                input: string.Empty,
                CancellationToken.None);
        }
        finally
        {
            locked?.Dispose();
        }

        Assert.True(sessions.TryGet(claimant, out var stored));
        var persistedRows = RankingSnapshotParser.ParseInnerPowerRanksFile(innerPowerPath);
        return new InvokeResult(
            handled,
            session,
            Assert.IsType<WorldSession>(stored),
            persistedAtCommit,
            Assert.IsType<CharacterSnapshot>(characterLoader.LoadByAccountId(AccountId)),
            originalRow,
            Assert.Single(worldData.InnerPowerRanksByCharacterId[checked((int)CharacterUid)]),
            Assert.Single(persistedRows, row => row.CharacterId == CharacterUid),
            claimantSent,
            observerSent,
            exposureOrder,
            log.ToString(),
            originalCharacterBytes,
            originalInnerPowerBytes,
            File.ReadAllBytes(characterPath),
            File.ReadAllBytes(innerPowerPath),
            File.ReadAllText(innerPowerPath, Encoding.UTF8),
            context);
    }

    private static WorldSession CreateSession(long conquerPoints, string? usersRoot)
    {
        return new WorldSession(
            AccountId,
            "full-account-inner-power-test",
            new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "InnerPowerHero",
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
                Vitality: 1,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: 0,
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty,
            CombatGear: CombatGearSnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static void WriteCharacter(string path, long conquerPoints)
    {
        File.WriteAllText(
            path,
            $$"""
            [Character]
            UID={{CharacterUid}}
            Name=InnerPowerHero
            Body=1003
            Face=0
            Haire=0
            Class=15
            FirstClass=15
            SecoundeClass=0
            Reborn=2
            Level=140
            Experience=0
            Map=1002
            X=430
            Y=378
            PMap=1002
            PMapX=430
            PMapY=378
            Strength=0
            Agility=0
            Vitaliti=1
            Spirit=0
            Atributes=0
            MinHitPoints=1
            MinMana=0
            Money=0
            ConquerPoints={{conquerPoints}}
            BoundConquerPoints=0
            BankCps=0
            PkPoints=0
            Online=1
            Sentinel=keep-me

            [Other]
            Note=preserve-this-section
            """,
            Encoding.Latin1);
    }

    private static NeiGongSecret CreateSecret(int id, int gongCount)
    {
        return new NeiGongSecret(
            id,
            $"Stage{id}",
            string.Empty,
            gongCount,
            [],
            [],
            string.Empty,
            "NeiGongInfo.ini",
            id,
            id.ToString(CultureInfo.InvariantCulture),
            new Dictionary<string, string>());
    }

    private static NeiGongType CreateType(int stageId, int slot, int typeId, int maxLevel)
    {
        return new NeiGongType(
            stageId,
            slot,
            typeId,
            $"Gong{typeId}",
            string.Empty,
            maxLevel,
            [],
            [],
            Enumerable.Repeat(1, maxLevel).ToArray(),
            string.Empty,
            0,
            0,
            0,
            0,
            "NeiGongInfo.ini",
            slot,
            $"{stageId}-{slot}",
            new Dictionary<string, string>());
    }

    private static WorldDataCatalog CreateCatalog(
        IReadOnlyList<InnerPowerRankEntry> rows,
        IReadOnlyList<NeiGongSecret> secrets,
        IReadOnlyList<NeiGongType> types)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(DateTimeOffset.Parse("2026-07-11T00:00:00Z"), "fixture://full-account-inner-power", new Dictionary<string, int>()),
            clientConfigs: [], items: [], itemAddBonuses: [], levelExperience: [], runeLevelExperience: [], runeStorageAttributes: [], itemRefineEffects: [], itemRefineEffectEx: [], itemRefineUpgrades: [], transformInfo: [], furniture: [], hairColorTypes: [], hairFaceStorageTypes: [], coatStorageTypes: [], coatStorageAttributes: [], coatColorRules: [], xuanbaoAdditionAttributes: [], xuanbaoComposeAttributeLimits: [], xuanbaoRandomAttributes: [], christmasNpcs: [], tutorTypes: [], tutorBattleLimitTypes: [], beastAttributes: [], fateExperience: [], spiritRates: [], hundredWeapons: [], gouyuTypes: [], gouyuImmortals: [], specialEffectAttributes: [], daoqiDictTypes: [], syndicateFormationTypes: [], syndicateSkillTypes: [], disdainRules: [], jiangHuAttributes: [], jiangHuCultivateStatuses: [], refineryBoxes: [], refinerySouls: [], refineryMaterials: [], stateCountFiles: [], binarySeedFiles: [], emptySeedFiles: [], backupSeedFiles: [], pokerTables: [], rouletteTables: [], npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, string.Empty, 0, []), [], "fixture://activity"), meltingTypeRules: [], lotteryPrizes: [], arenaRecords: [], elitePkRecords: [], teamElitePkRecords: [], skillTeamPkRecords: [], classPkWarRecords: [], combatGearRanks: [], prestigeRanks: [], innerPowerRanks: rows, runeRankings: [], staticStatues: [], newPlayerRewards: [], votes: [], couplesPkRecords: [], socialAssociations: [], guildWarInfo: EmptyGuildWarData("guildwar"), superGuildWarInfo: EmptyGuildWarData("superguildwar"), classLevelStats: [], professionLevelUps: [], professionLevelBenefits: [], professionTitleBenefits: [], questInfo: new QuestInfoMetadata(null, null, null), questTaskTypes: [], questMissions: [], quizQuestions: [], magic: [], magicEffects: [], magicTypeOperations: [], titleTypes: [], combatGear: [], traps: [], npcs: [], sobNpcs: [], portals: [], maps: [], mapNames: [], monsters: [], monsterSpawns: [], lotteryDictionary: [], collectionRewardTiers: [], taskRewardTypes: [], taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(secrets.Count, [], "NeiGongInfo.ini", 1, "NeiGong", new Dictionary<string, string>()), neiGongSecrets: secrets, neiGongTypes: types, subProfessions: [], syndicateLevels: [], stageGoalInfo: new StageGoalInfo(0), stageGoalStages: [], stageGoalTasks: [], shops: [], shopItems: [], exchangeShopGoods: [], exchangeShopGoodsEx: [], exchangeShopLevels: [], honorShopItems: [], honorShopRecommendations: [], racePointShopItems: [], racePointShopRecommendations: [], goldenLeagueShopItems: [], goldenLeagueShopRecommendations: [], goldenLeagueShopConfig: new Dictionary<string, string>(), booths: [], boothItems: []);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private static ConquerTcpConnection CreateConnection(
        int port,
        List<byte[]> sent,
        List<string>? exposureOrder = null)
    {
        return new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, port),
            (frame, _) =>
            {
                var bytes = frame.ToArray();
                sent.Add(bytes);
                exposureOrder?.Add(
                    "send:" + ReadFrame(bytes).Header.Opcode.ToString(CultureInfo.InvariantCulture));
                return ValueTask.CompletedTask;
            });
    }

    private static void AssertWhiteTalk(byte[] bytes)
    {
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message));
        Assert.Equal(LegacyMsgMessagePacket.Opcode, ReadFrame(bytes).Header.Opcode);
        Assert.Equal(LegacyMsgMessagePacket.WhiteColor, message.Color);
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, message.ChatType);
        Assert.Equal(
            "Congratulations ! [ InnerPowerHero ] Has fixed his [InnerPower] For [ 10 ] ConquerPoints Enjoy With [ TestSrv ] !.",
            message.Content[3]);
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> sent)
    {
        var replies = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply))
            {
                replies.Add(reply);
            }
        }

        return replies;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static FileStream OpenPersistenceLock(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public enum PersistenceLock
    {
        None,
        Character,
        InnerPower,
    }

    private readonly record struct PersistedState(
        CharacterSnapshot Character,
        InnerPowerRankEntry InnerPower);

    private sealed record InvokeResult(
        bool Handled,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        PersistedState? PersistedAtCommit,
        CharacterSnapshot ReloadedCharacter,
        InnerPowerRankEntry OriginalCatalogRow,
        InnerPowerRankEntry UpdatedCatalogRow,
        InnerPowerRankEntry PersistedInnerPower,
        List<byte[]> ClaimantSent,
        List<byte[]> ObserverSent,
        List<string> ExposureOrder,
        string Log,
        byte[] OriginalCharacterBytes,
        byte[] OriginalInnerPowerBytes,
        byte[] PersistedCharacterBytes,
        byte[] PersistedInnerPowerBytes,
        string PersistedInnerPowerText,
        WorldPacketHandlerContext Context);
}
