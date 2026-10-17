using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyAdminUtilityLevelProficiencyParityTests : IDisposable
{
    private const uint CharacterUid = 1_293_600;
    private const long AccountId = CharacterUid;
    private const uint StuffNpcId = 77_839;
    private const uint Stuff12NpcId = 22_224;
    private const byte StuffLevel140Option = 60;
    private const byte Stuff12MaxProficiencyOption = 149;
    private const byte Stuff12Level140Option = 162;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyAdminUtilityLevelProficiencyParityTests-" + Guid.NewGuid().ToString("N"));
    private readonly string _usersRoot;
    private readonly string _playerProficienciesRoot;

    public LegacyAdminUtilityLevelProficiencyParityTests()
    {
        _usersRoot = Path.Combine(_root, "Users");
        _playerProficienciesRoot = Path.Combine(_root, "Proficiencies");
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_playerProficienciesRoot);
    }

    [Theory]
    [InlineData(StuffNpcId, StuffLevel140Option)]
    [InlineData(Stuff12NpcId, Stuff12Level140Option)]
    [InlineData(Stuff12NpcId, Stuff12MaxProficiencyOption)]
    public async Task NonPrivilegedCharactersCannotReachMutationBranches(uint npcId, byte optionId)
    {
        var originalProficiencies = CreateMixedProficiencies();
        var result = await InvokeAsync(
            npcId,
            optionId,
            gmName: false,
            projectManager: false,
            proficiencies: originalProficiencies);

        Assert.True(result.Handled);
        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Empty(result.Sent);
        Assert.Equal(0, result.DisconnectCount);
        Assert.Equal(result.OriginalCharacterText, result.PersistedCharacterText);
        Assert.Equal(result.OriginalProficiencyBytes, result.PersistedProficiencyBytes);
        Assert.Null(result.PersistedCharacterAtCommit);
        Assert.Null(result.PersistedProficienciesAtCommit);
        Assert.Contains("ignored LegacyNpc admin utility for non-privileged character", result.Log);
    }

    [Fact]
    public async Task StuffOption60PersistsBeforeCommitAndSendsExactUpdateLevelOrder()
    {
        var result = await InvokeAsync(
            StuffNpcId,
            StuffLevel140Option,
            level: 130,
            experience: 123_456,
            attributePoints: 7,
            minHitpoints: 5);

        Assert.Equal(140, result.UpdatedSession.Character.Level);
        Assert.Equal(123_456, result.UpdatedSession.Character.Experience);
        Assert.Equal(37, result.UpdatedSession.Character.AttributePoints);
        Assert.True(result.UpdatedSession.Character.MinHitPoints > 5);
        AssertCharacterMutation(
            Assert.IsType<CharacterSnapshot>(result.PersistedCharacterAtCommit),
            result.UpdatedSession.Character);
        AssertCharacterMutation(result.ReloadedCharacter, result.UpdatedSession.Character);
        Assert.Contains("Sentinel=keep-me", result.PersistedCharacterText, StringComparison.Ordinal);
        Assert.Contains("Note=preserve-this-section", result.PersistedCharacterText, StringComparison.Ordinal);

        Assert.Equal(9, result.Sent.Count);
        AssertScalarUpdate(result.Sent[0], LegacyMsgUpdateDataType.Level, 140);
        AssertLevelAction(result.Sent[1]);
        AssertScalarUpdate(result.Sent[2], LegacyMsgUpdateDataType.Atributes, 37);
        AssertHitpointPair(
            result.Sent,
            startIndex: 3,
            checked((uint)result.UpdatedSession.Character.MinHitPoints),
            expectedCurrent: 5);
        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(result.Sent[5]), out _));
        Assert.True(LegacyUserTotalRefineLevPacket.TryParse(ReadFrame(result.Sent[6]), out _));
        AssertHitpointPair(
            result.Sent,
            startIndex: 7,
            checked((uint)result.UpdatedSession.Character.MinHitPoints),
            checked((uint)result.UpdatedSession.Character.MinHitPoints));
        Assert.Empty(ParseNpcReplies(result.Sent));
        Assert.Equal(0, result.DisconnectCount);
        Assert.Contains("attributePointsAdded=30", result.Log);
        Assert.Contains("updateOrder=Level,Action92,StatsOrAtributes,Equipment,Hitpoints,SourceDialog", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task StuffOption60At140IsIdempotentAndSendsExactMessageBox()
    {
        var result = await InvokeAsync(
            StuffNpcId,
            StuffLevel140Option,
            level: 140,
            experience: 777,
            attributePoints: 9,
            includeMutationDependencies: false);

        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Null(result.PersistedCharacterAtCommit);
        Assert.Equal(result.OriginalCharacterText, result.PersistedCharacterText);
        var reply = AssertNpcReply(Assert.Single(result.Sent), LegacyNpcReplyInteractType.MessageBox);
        Assert.Equal("Sorry, your level is already 140.", reply.Text);
        Assert.Equal(140, result.ReloadedCharacter.Level);
        Assert.Equal(777, result.ReloadedCharacter.Experience);
        Assert.Equal(9, result.ReloadedCharacter.AttributePoints);
        Assert.Equal(0, result.DisconnectCount);
        Assert.Contains("reason=alreadyLevel140", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task Stuff12Option162PreservesBelowCutoffStatsAndSendsExactThanksOutput()
    {
        var result = await InvokeAsync(
            Stuff12NpcId,
            Stuff12Level140Option,
            level: 119,
            experience: 88,
            strength: 11,
            agility: 12,
            vitality: 100,
            spirit: 14,
            attributePoints: 7,
            gmName: false,
            projectManager: true);

        Assert.Equal(140, result.ReloadedCharacter.Level);
        Assert.Equal(7, result.ReloadedCharacter.AttributePoints);
        Assert.Equal(11, result.ReloadedCharacter.Strength);
        Assert.Equal(12, result.ReloadedCharacter.Agility);
        Assert.Equal(100, result.ReloadedCharacter.Vitality);
        Assert.Equal(14, result.ReloadedCharacter.Spirit);
        AssertCharacterMutation(
            Assert.IsType<CharacterSnapshot>(result.PersistedCharacterAtCommit),
            result.UpdatedSession.Character);

        Assert.Equal(14, result.Sent.Count);
        AssertScalarUpdate(result.Sent[0], LegacyMsgUpdateDataType.Level, 140);
        AssertLevelAction(result.Sent[1]);
        AssertScalarUpdate(result.Sent[2], LegacyMsgUpdateDataType.Strength, 11);
        AssertScalarUpdate(result.Sent[3], LegacyMsgUpdateDataType.Agility, 12);
        AssertScalarUpdate(result.Sent[4], LegacyMsgUpdateDataType.Spirit, 14);
        AssertScalarUpdate(result.Sent[5], LegacyMsgUpdateDataType.Vitality, 100);
        AssertHitpointPair(
            result.Sent,
            startIndex: 6,
            checked((uint)result.UpdatedSession.Character.MinHitPoints),
            expectedCurrent: 5);
        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(result.Sent[8]), out _));
        Assert.True(LegacyUserTotalRefineLevPacket.TryParse(ReadFrame(result.Sent[9]), out _));
        AssertHitpointPair(
            result.Sent,
            startIndex: 10,
            checked((uint)result.UpdatedSession.Character.MinHitPoints),
            checked((uint)result.UpdatedSession.Character.MinHitPoints));

        var option = AssertNpcReply(result.Sent[12], LegacyNpcReplyInteractType.Option);
        Assert.Equal("Thanks!", option.Text);
        Assert.Equal(byte.MaxValue, option.OptionId);
        AssertNpcReply(result.Sent[13], LegacyNpcReplyInteractType.Finish);
        Assert.DoesNotContain(
            ParseMsgUpdates(result.Sent).SelectMany(packet => packet.Updates),
            update => update.Type == LegacyMsgUpdateDataType.Atributes);
        Assert.DoesNotContain("non-privileged", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task Stuff12Option162At140IsIdempotentAndSendsExactRejectedDialog()
    {
        var result = await InvokeAsync(
            Stuff12NpcId,
            Stuff12Level140Option,
            level: 140,
            experience: 654,
            attributePoints: 19,
            includeMutationDependencies: false);

        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Equal(result.OriginalCharacterText, result.PersistedCharacterText);
        Assert.Equal(3, result.Sent.Count);
        var text = AssertNpcReply(result.Sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("You do not have enough Conquer Points.", text.Text);
        var option = AssertNpcReply(result.Sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal("Oh!", option.Text);
        Assert.Equal(byte.MaxValue, option.OptionId);
        AssertNpcReply(result.Sent[2], LegacyNpcReplyInteractType.Finish);
        Assert.Equal(0, result.DisconnectCount);
        Assert.Contains("reason=levelNotBelow140", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task LevelPersistenceFailureRollsBackAndSendsOnlyExactFenceDialog()
    {
        var result = await InvokeAsync(
            StuffNpcId,
            StuffLevel140Option,
            level: 130,
            experience: 55,
            attributePoints: 7,
            lockCharacterFile: true);

        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Null(result.PersistedCharacterAtCommit);
        Assert.Equal(result.OriginalCharacterText, result.PersistedCharacterText);
        Assert.Equal(130, result.ReloadedCharacter.Level);
        AssertFenceDialog(result.Sent, "Level 140 admin service is disabled.");
        Assert.DoesNotContain(ParseMsgUpdates(result.Sent), packet =>
            packet.Updates.Any(update => update.Type == LegacyMsgUpdateDataType.Level));
        Assert.DoesNotContain(result.Sent, bytes => MsgActionPacket.TryParse(ReadFrame(bytes), out _));
        Assert.Equal(0, result.DisconnectCount);
        Assert.Contains("persistence failed reason=snapshotWriteFailed", result.Log);
        Assert.Contains("PARITY-GAP LegacyNpc AdminUtility", result.Log);
    }

    [Fact]
    public async Task Stuff12Option149PersistsEveryProficiencyBeforeDisconnectWithoutPackets()
    {
        var originalProficiencies = CreateMixedProficiencies();
        var result = await InvokeAsync(
            Stuff12NpcId,
            Stuff12MaxProficiencyOption,
            proficiencies: originalProficiencies);

        Assert.All(result.UpdatedSession.PlayerProficiencies.Proficiencies, proficiency =>
        {
            Assert.Equal(20U, proficiency.Level);
            Assert.Equal(0U, proficiency.Experience);
        });
        AssertProficiencyIdentityFieldsPreserved(
            originalProficiencies,
            result.UpdatedSession.PlayerProficiencies);
        AssertProficienciesEqual(
            result.UpdatedSession.PlayerProficiencies,
            Assert.IsType<PlayerProficiencySnapshot>(result.PersistedProficienciesAtCommit));
        AssertProficienciesEqual(
            result.UpdatedSession.PlayerProficiencies,
            result.ReloadedProficiencies);
        Assert.Empty(result.Sent);
        Assert.Equal(1, result.DisconnectCount);
        Assert.Equal(result.OriginalCharacterText, result.PersistedCharacterText);
        Assert.Contains("proficiencyCount=3", result.Log);
        Assert.Contains("changedCount=2", result.Log);
        Assert.Contains("output=disconnect-only", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task Stuff12Option149IsIdempotentAndStillUsesSourceDisconnect()
    {
        var proficiencies = new PlayerProficiencySnapshot(
        [
            new(410, Level: 20, PreviousLevel: 9, Experience: 0, NeededExperience: 456),
            new(999, Level: 20, PreviousLevel: 3, Experience: 0, NeededExperience: 88),
        ]);
        var result = await InvokeAsync(
            Stuff12NpcId,
            Stuff12MaxProficiencyOption,
            proficiencies: proficiencies);

        AssertProficienciesEqual(proficiencies, result.UpdatedSession.PlayerProficiencies);
        Assert.Equal(result.OriginalProficiencyBytes, result.PersistedProficiencyBytes);
        Assert.Empty(result.Sent);
        Assert.Equal(1, result.DisconnectCount);
        Assert.Contains("changedCount=0", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task ProficiencyPersistenceFailureRollsBackWithoutSessionCommitOrDisconnect()
    {
        var originalProficiencies = CreateMixedProficiencies();
        var result = await InvokeAsync(
            Stuff12NpcId,
            Stuff12MaxProficiencyOption,
            proficiencies: originalProficiencies,
            lockProficiencyFile: true);

        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Null(result.PersistedProficienciesAtCommit);
        Assert.Equal(result.OriginalProficiencyBytes, result.PersistedProficiencyBytes);
        AssertProficienciesEqual(originalProficiencies, result.ReloadedProficiencies);
        AssertFenceDialog(result.Sent, "Max weapon proficiency admin service is disabled.");
        Assert.Equal(0, result.DisconnectCount);
        Assert.Contains("max proficiency persistence failed reason=", result.Log);
        Assert.Contains("PARITY-GAP LegacyNpc AdminUtility", result.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        uint npcId,
        byte optionId,
        long level = 130,
        long experience = 123,
        long strength = 11,
        long agility = 12,
        long vitality = 100,
        long spirit = 14,
        long attributePoints = 7,
        long minHitpoints = 5,
        bool gmName = true,
        bool projectManager = false,
        PlayerProficiencySnapshot? proficiencies = null,
        bool includeMutationDependencies = true,
        bool lockCharacterFile = false,
        bool lockProficiencyFile = false)
    {
        WriteCharacterSnapshot(
            level,
            experience,
            strength,
            agility,
            vitality,
            spirit,
            attributePoints,
            minHitpoints,
            gmName,
            projectManager);
        proficiencies ??= PlayerProficiencySnapshot.Empty;
        var proficiencyLoader = new PlayerProficiencySnapshotLoader(_playerProficienciesRoot);
        proficiencyLoader.SaveByCharacterUid(CharacterUid, proficiencies);

        var characterPath = Path.Combine(_usersRoot, AccountId + ".ini");
        var proficiencyPath = Path.Combine(_playerProficienciesRoot, CharacterUid + ".bin");
        var originalCharacterText = File.ReadAllText(characterPath);
        var originalProficiencyBytes = File.ReadAllBytes(proficiencyPath);
        var characterLoader = new CharacterSnapshotLoader(_usersRoot);
        var character = Assert.IsType<CharacterSnapshot>(characterLoader.LoadByAccountId(AccountId));
        var session = new WorldSession(
            AccountId,
            "admin-utility-level-proficiency-test",
            character,
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            proficiencies,
            CombatGear: CombatGearSnapshot.Empty,
            UsersRoot: includeMutationDependencies ? _usersRoot : null);

        var sent = new List<byte[]>();
        var disconnectCount = 0;
        var connection = new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9958),
            (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            },
            _ =>
            {
                disconnectCount++;
                return ValueTask.CompletedTask;
            });
        CharacterSnapshot? persistedCharacterAtCommit = null;
        PlayerProficiencySnapshot? persistedProficienciesAtCommit = null;
        var sessions = new WorldSessionStore((_, previous, current) =>
        {
            if (previous is null)
            {
                return;
            }

            if (!ReferenceEquals(previous.Character, current.Character))
            {
                persistedCharacterAtCommit = characterLoader.LoadByAccountId(AccountId);
            }

            if (!ReferenceEquals(previous.PlayerProficiencies, current.PlayerProficiencies))
            {
                persistedProficienciesAtCommit = proficiencyLoader.LoadByCharacterUid(CharacterUid);
            }
        });
        sessions.Set(connection, session);

        FileStream? characterLock = null;
        FileStream? proficiencyLock = null;
        try
        {
            if (lockCharacterFile)
            {
                characterLock = new FileStream(characterPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }

            if (lockProficiencyFile)
            {
                proficiencyLock = new FileStream(proficiencyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }

            using var log = new StringWriter();
            var handled = await WorldPacketHandlers.TryHandleLegacyAdminUtilityNpcAsync(
                new WorldPacketHandlerContext(
                    connection,
                    log,
                    Session: session,
                    WorldData: includeMutationDependencies ? CreateWorldDataCatalog() : null,
                    Sessions: sessions,
                    PlayerProficienciesRoot: includeMutationDependencies ? _playerProficienciesRoot : null),
                session,
                npcId,
                optionId,
                CancellationToken.None);

            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            return new InvokeResult(
                handled,
                session,
                updatedSession,
                persistedCharacterAtCommit,
                persistedProficienciesAtCommit,
                sent,
                disconnectCount,
                log.ToString(),
                originalCharacterText,
                File.ReadAllText(characterPath),
                originalProficiencyBytes,
                File.ReadAllBytes(proficiencyPath),
                Assert.IsType<CharacterSnapshot>(characterLoader.LoadByAccountId(AccountId)),
                proficiencyLoader.LoadByCharacterUid(CharacterUid));
        }
        finally
        {
            proficiencyLock?.Dispose();
            characterLock?.Dispose();
        }
    }

    private void WriteCharacterSnapshot(
        long level,
        long experience,
        long strength,
        long agility,
        long vitality,
        long spirit,
        long attributePoints,
        long minHitpoints,
        bool gmName,
        bool projectManager)
    {
        var privilegeLine = projectManager ? "ProjectManager=1" : "";
        File.WriteAllText(
            Path.Combine(_usersRoot, AccountId + ".ini"),
            $$"""
            [Character]
            UID={{CharacterUid}}
            Body=1003
            Face=0
            Name={{(gmName ? "AdminUtilityTester[GM]" : "AdminUtilityTester")}}
            Class=1005
            Map=1002
            X=430
            Y=378
            FirstClass=1005
            SecoundeClass=0
            Reborn=0
            Level={{level}}
            Haire=0
            Experience={{experience}}
            PMap=1002
            PMapX=430
            PMapY=378
            Agility={{agility}}
            Strength={{strength}}
            Vitaliti={{vitality}}
            Spirit={{spirit}}
            Atributes={{attributePoints}}
            MinHitPoints={{minHitpoints}}
            MinMana=25
            MaxMana=25
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=0
            Sentinel=keep-me
            {{privilegeLine}}

            [Other]
            Note=preserve-this-section
            """);
    }

    private static PlayerProficiencySnapshot CreateMixedProficiencies()
    {
        return new PlayerProficiencySnapshot(
        [
            new(410, Level: 4, PreviousLevel: 9, Experience: 123, NeededExperience: 456),
            new(999, Level: 20, PreviousLevel: 3, Experience: 77, NeededExperience: 88),
            new(420, Level: 20, PreviousLevel: 7, Experience: 0, NeededExperience: 999),
        ]);
    }

    private static void AssertCharacterMutation(CharacterSnapshot actual, CharacterSnapshot expected)
    {
        Assert.Equal(expected.Level, actual.Level);
        Assert.Equal(expected.Experience, actual.Experience);
        Assert.Equal(expected.Strength, actual.Strength);
        Assert.Equal(expected.Agility, actual.Agility);
        Assert.Equal(expected.Vitality, actual.Vitality);
        Assert.Equal(expected.Spirit, actual.Spirit);
        Assert.Equal(expected.AttributePoints, actual.AttributePoints);
        Assert.Equal(expected.MinHitPoints, actual.MinHitPoints);
    }

    private static void AssertProficiencyIdentityFieldsPreserved(
        PlayerProficiencySnapshot original,
        PlayerProficiencySnapshot updated)
    {
        Assert.Equal(original.Proficiencies.Count, updated.Proficiencies.Count);
        for (var index = 0; index < original.Proficiencies.Count; index++)
        {
            Assert.Equal(original.Proficiencies[index].Id, updated.Proficiencies[index].Id);
            Assert.Equal(original.Proficiencies[index].PreviousLevel, updated.Proficiencies[index].PreviousLevel);
            Assert.Equal(original.Proficiencies[index].NeededExperience, updated.Proficiencies[index].NeededExperience);
        }
    }

    private static void AssertProficienciesEqual(
        PlayerProficiencySnapshot expected,
        PlayerProficiencySnapshot actual)
    {
        Assert.Equal(expected.Proficiencies.Count, actual.Proficiencies.Count);
        for (var index = 0; index < expected.Proficiencies.Count; index++)
        {
            Assert.Equal(expected.Proficiencies[index], actual.Proficiencies[index]);
        }
    }

    private static void AssertLevelAction(byte[] bytes)
    {
        Assert.True(MsgActionPacket.TryParse(ReadFrame(bytes), out var action));
        Assert.Equal(CharacterUid, action.ObjId);
        Assert.Equal((MsgActionType)92, action.Type);
        Assert.Equal(140U, action.PositionX);
    }

    private static void AssertHitpointPair(
        IReadOnlyList<byte[]> sent,
        int startIndex,
        uint expectedMax,
        uint expectedCurrent)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[startIndex]), out var maxOnly));
        var maxUpdate = Assert.Single(maxOnly.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maxUpdate.Type);
        Assert.Equal((ulong)expectedMax, maxUpdate.Value);

        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[startIndex + 1]), out var pair));
        Assert.Collection(
            pair.Updates,
            update =>
            {
                Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, update.Type);
                Assert.Equal((ulong)expectedMax, update.Value);
            },
            update =>
            {
                Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, update.Type);
                Assert.Equal((ulong)expectedCurrent, update.Value);
            });
    }

    private static void AssertScalarUpdate(
        byte[] bytes,
        LegacyMsgUpdateDataType expectedType,
        ulong expectedValue)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(CharacterUid, packet.Uid);
        var update = Assert.Single(packet.Updates);
        Assert.Equal(expectedType, update.Type);
        Assert.Equal(expectedValue, update.Value);
    }

    private static void AssertFenceDialog(IReadOnlyList<byte[]> sent, string expectedText)
    {
        Assert.Equal(4, sent.Count);
        Assert.Equal(expectedText, AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog).Text);
        var option = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal("I understand.", option.Text);
        Assert.Equal(byte.MaxValue, option.OptionId);
        Assert.Equal((ushort)285, AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Avatar).InputMaxLength);
        AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Finish);
    }

    private static LegacyNpcReplyPacket AssertNpcReply(
        byte[] bytes,
        LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply));
        Assert.Equal(expectedType, reply.InteractType);
        return reply;
    }

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> sent)
    {
        return sent
            .Select(ReadFrame)
            .Where(frame => LegacyMsgUpdatePacket.TryParse(frame, out _))
            .Select(frame =>
            {
                Assert.True(LegacyMsgUpdatePacket.TryParse(frame, out var packet));
                return packet;
            })
            .ToArray();
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> sent)
    {
        return sent
            .Select(ReadFrame)
            .Where(frame => LegacyNpcReplyPacket.TryParse(frame, out _))
            .Select(frame =>
            {
                Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var reply));
                return reply;
            })
            .ToArray();
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static WorldDataCatalog CreateWorldDataCatalog()
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-14T00:00:00Z"),
                "fixture://admin-utility-level-proficiency",
                new Dictionary<string, int>()),
            clientConfigs: [], items: [], itemAddBonuses: [], levelExperience: [], runeLevelExperience: [],
            runeStorageAttributes: [], itemRefineEffects: [], itemRefineEffectEx: [], itemRefineUpgrades: [],
            transformInfo: [], furniture: [], hairColorTypes: [], hairFaceStorageTypes: [], coatStorageTypes: [],
            coatStorageAttributes: [], coatColorRules: [], xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [], xuanbaoRandomAttributes: [], christmasNpcs: [], tutorTypes: [],
            tutorBattleLimitTypes: [], beastAttributes: [], fateExperience: [], spiritRates: [], hundredWeapons: [],
            gouyuTypes: [], gouyuImmortals: [], specialEffectAttributes: [], daoqiDictTypes: [],
            syndicateFormationTypes: [], syndicateSkillTypes: [], disdainRules: [], jiangHuAttributes: [],
            jiangHuCultivateStatuses: [], refineryBoxes: [], refinerySouls: [], refineryMaterials: [],
            stateCountFiles: [], binarySeedFiles: [], emptySeedFiles: [], backupSeedFiles: [], pokerTables: [],
            rouletteTables: [], npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(
                new ActivityTaskMetadata(0, "", 0, []),
                [],
                "fixture://activity"),
            meltingTypeRules: [], lotteryPrizes: [], arenaRecords: [], elitePkRecords: [], teamElitePkRecords: [],
            skillTeamPkRecords: [], classPkWarRecords: [], combatGearRanks: [], prestigeRanks: [],
            innerPowerRanks: [], runeRankings: [], staticStatues: [], newPlayerRewards: [], votes: [],
            couplesPkRecords: [], socialAssociations: [], guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"), classLevelStats: [], professionLevelUps: [],
            professionLevelBenefits: [], professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [], questMissions: [], quizQuestions: [], magic: [], magicEffects: [],
            magicTypeOperations: [], titleTypes: [], combatGear: [], traps: [], npcs: [], sobNpcs: [], portals: [],
            maps: [], mapNames: [], monsters: [], monsterSpawns: [], lotteryDictionary: [], collectionRewardTiers: [],
            taskRewardTypes: [], taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(
                0,
                [],
                "fixture://neigong",
                0,
                "",
                new Dictionary<string, string>()),
            neiGongSecrets: [], neiGongTypes: [], subProfessions: [], syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0), stageGoalStages: [], stageGoalTasks: [], shops: [], shopItems: [],
            exchangeShopGoods: [], exchangeShopGoodsEx: [], exchangeShopLevels: [], honorShopItems: [],
            honorShopRecommendations: [], racePointShopItems: [], racePointShopRecommendations: [],
            goldenLeagueShopItems: [], goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(), booths: [], boothItems: []);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed record InvokeResult(
        bool Handled,
        WorldSession InitialSession,
        WorldSession UpdatedSession,
        CharacterSnapshot? PersistedCharacterAtCommit,
        PlayerProficiencySnapshot? PersistedProficienciesAtCommit,
        IReadOnlyList<byte[]> Sent,
        int DisconnectCount,
        string Log,
        string OriginalCharacterText,
        string PersistedCharacterText,
        byte[] OriginalProficiencyBytes,
        byte[] PersistedProficiencyBytes,
        CharacterSnapshot ReloadedCharacter,
        PlayerProficiencySnapshot ReloadedProficiencies);
}
