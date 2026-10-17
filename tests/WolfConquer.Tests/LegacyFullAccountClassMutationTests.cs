using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFullAccountClassMutationTests : IDisposable
{
    private const long AccountId = 1_293_870;
    private const uint CharacterUid = 1_293_870;
    private const uint NpcId = 465_123;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-full-account-class-" + Guid.NewGuid().ToString("N"));

    public static IEnumerable<object[]> ClassCases()
    {
        yield return Case(1, 2_005, 2_000, 2_051, false, 1_095, 1_020, 1_090, 1_025, 1_045, 1_046, 1_020, 1_015, 1_040);
        yield return Case(2, 1_005, 1_000, 1_051, true, 1_095, 1_020, 1_090, 1_046, 1_045);
        yield return Case(3, 13_005, 13_002, 13_051, true, 1_095, 1_020, 1_090, 10_309, 1_010, 1_000, 1_005);
        yield return Case(4, 14_005, 14_002, 14_051, true, 1_095, 1_090, 1_020, 10_309, 1_010, 1_000, 1_005);
        yield return Case(5, 4_005, 4_000, 4_051, true, 1_095, 1_020, 1_090, 8_002);
        yield return Case(6, 5_005, 5_000, 5_051, true, 1_095, 1_020, 12_080, 12_070, 6_011, 6_001);
        yield return Case(7, 6_005, 6_000, 6_051, true, 1_095, 1_020, 1_090, 10_430, 10_415, 10_490, 10_390);
        yield return Case(8, 7_005, 7_000, 7_051, true, 1_095, 1_020, 1_090, 11_050, 11_110);
        yield return Case(9, 8_005, 8_000, 8_051, true, 1_095, 1_020, 1_090, 12_240, 12_290, 12_270, 12_340, 12_330, 12_320, 12_210, 12_220, 12_230);
        yield return Case(10, 16_005, 16_000, 16_051, true, 1_095, 1_020, 1_090, 12_980, 12_870, 13_050, 13_060, 13_040);
        yield return Case(11, 9_005, 9_000, 9_051, true, 1_095, 1_090, 1_020, 15_750, 15_800, 15_700, 15_770, 15_720, 15_670, 15_680, 15_690);
    }

    [Theory]
    [MemberData(nameof(ClassCases))]
    public async Task EveryClassOptionPersistsExactSourceClassAndSpellTableBeforeCurrentWirePackets(
        byte optionId,
        uint targetClass,
        uint familyMinimum,
        uint familyMaximum,
        bool clearExistingSpells,
        ushort[] sourceSpellIds)
    {
        var originalSpells = Spells(
            Spell(1_020, level: 7, experience: 888, previousLevel: 6, soulLevel: 4, useSpellSoul: 3),
            Spell(9_999, level: 5, experience: 444, previousLevel: 2, soulLevel: 1, useSpellSoul: 1));
        var result = await InvokeAsync(
            optionId,
            classCode: 15,
            reborn: 0,
            level: 140,
            attributePoints: 10,
            originalSpells,
            observerCount: 1);

        Assert.True(result.Handled);
        Assert.Equal("commit", result.ExposureOrder[0]);
        Assert.Equal("disconnect", result.ExposureOrder[^1]);
        Assert.True(result.Disconnected);
        Assert.NotNull(result.PersistedAtCommit);
        Assert.Equal(targetClass, result.UpdatedSession.Character.Class);
        Assert.Equal(2, result.UpdatedSession.Character.Reborn);
        Assert.Equal(140, result.UpdatedSession.Character.Level);
        Assert.Equal(0, result.UpdatedSession.Character.Agility);
        Assert.Equal(0, result.UpdatedSession.Character.Strength);
        Assert.Equal(1, result.UpdatedSession.Character.Vitality);
        Assert.Equal(0, result.UpdatedSession.Character.Spirit);
        Assert.Equal(910, result.UpdatedSession.Character.AttributePoints);
        Assert.Equal(50, result.UpdatedSession.Character.MinHitPoints);
        AssertCharacterCoreEqual(result.UpdatedSession.Character, result.ReloadedCharacter);
        AssertCharacterCoreEqual(result.UpdatedSession.Character, result.PersistedAtCommit!.Character);
        AssertSpellSnapshotsEqual(result.UpdatedSession.PlayerSpells, result.PersistedSpells);
        AssertSpellSnapshotsEqual(result.UpdatedSession.PlayerSpells, result.PersistedAtCommit.Spells);
        Assert.Contains("Sentinel=keep-me", result.PersistedCharacterText, StringComparison.Ordinal);
        Assert.Contains("[Other]", result.PersistedCharacterText, StringComparison.Ordinal);
        Assert.All(result.ObserverSent, Assert.Empty);

        var expectedPersisted = BuildExpectedSpells(originalSpells, sourceSpellIds, clearExistingSpells);
        AssertSpellSnapshotsEqual(expectedPersisted, result.UpdatedSession.PlayerSpells);
        if (!clearExistingSpells)
        {
            var retained = Assert.Single(result.UpdatedSession.PlayerSpells.Spells, spell => spell.Id == 9_999);
            Assert.Equal(5, retained.Level);
            Assert.Equal(444, retained.Experience);
            var reset = Assert.Single(result.UpdatedSession.PlayerSpells.Spells, spell => spell.Id == 1_020);
            Assert.Equal(0, reset.Level);
            Assert.Equal(0, reset.Experience);
            Assert.Equal(2, sourceSpellIds.Count(id => id == 1_020));
        }

        var index = 0;
        AssertScalarUpdate(result.ClaimantSent[index++], LegacyMsgUpdateDataType.Agility, 0);
        AssertScalarUpdate(result.ClaimantSent[index++], LegacyMsgUpdateDataType.Strength, 0);
        AssertScalarUpdate(result.ClaimantSent[index++], LegacyMsgUpdateDataType.Vitality, 1);
        AssertScalarUpdate(result.ClaimantSent[index++], LegacyMsgUpdateDataType.Spirit, 0);
        AssertScalarUpdate(result.ClaimantSent[index++], LegacyMsgUpdateDataType.Atributes, 910);

        if (clearExistingSpells)
        {
            foreach (var originalSpell in originalSpells.Spells)
            {
                Assert.True(MsgActionPacket.TryParse(ReadFrame(result.ClaimantSent[index++]), out var remove));
                Assert.Equal(CharacterUid, remove.ObjId);
                Assert.Equal((MsgActionType)109, remove.Type);
                Assert.Equal(originalSpell.Id, remove.DwParam);
            }
        }

        AssertScalarUpdate(result.ClaimantSent[index++], LegacyMsgUpdateDataType.Class, targetClass);
        foreach (var spellId in sourceSpellIds)
        {
            Assert.True(LegacySpellPacket.TryParse(ReadFrame(result.ClaimantSent[index++]), out var spell));
            Assert.Equal(LegacySpellPacket.Opcode, ReadFrame(result.ClaimantSent[index - 1]).Header.Opcode);
            Assert.Equal(spellId, spell.Id);
            Assert.Equal(0, spell.Level);
            Assert.Equal(0, spell.Experience);
            Assert.Equal(0, spell.Type);
            Assert.Equal(0U, spell.UseSoul);
        }

        Assert.Equal(result.ClaimantSent.Count, index);
        Assert.Contains($"optionId={optionId}", result.Log, StringComparison.Ordinal);
        Assert.Contains($"class=15->{targetClass}", result.Log, StringComparison.Ordinal);
        Assert.Contains($"clearSpells={clearExistingSpells}", result.Log, StringComparison.Ordinal);
        Assert.Contains("wire=7899", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
        Assert.InRange(targetClass, familyMinimum, familyMaximum);
    }

    [Theory]
    [MemberData(nameof(ClassCases))]
    public async Task ExactSourceClassFamilyBoundsWinBeforeInfrastructure(
        byte optionId,
        uint targetClass,
        uint familyMinimum,
        uint familyMaximum,
        bool clearExistingSpells,
        ushort[] sourceSpellIds)
    {
        foreach (var currentClass in new[] { familyMinimum, familyMaximum })
        {
            var result = await InvokeAsync(
                optionId,
                classCode: currentClass,
                reborn: 0,
                level: 140,
                attributePoints: 10,
                Spells(Spell(777)),
                includeSessions: false,
                includeUsersRoot: false,
                includePlayerSpellsRoot: false,
                includeWorldData: false);

            Assert.True(result.Handled);
            Assert.False(result.Disconnected);
            Assert.Null(result.PersistedAtCommit);
            Assert.Same(result.OriginalSession, result.UpdatedSession);
            Assert.Equal(result.OriginalCharacterBytes, result.PersistedCharacterBytes);
            Assert.Equal(result.OriginalSpellBytes, result.PersistedSpellBytes);
            var reply = AssertMessageBox(Assert.Single(result.ClaimantSent));
            Assert.Equal("Error..!", reply.Text);
            Assert.Contains("reason=source-gate", result.Log, StringComparison.Ordinal);
            Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
        }

        Assert.InRange(targetClass, familyMinimum, familyMaximum);
        Assert.NotEmpty(sourceSpellIds);
        Assert.Equal(optionId != 1, clearExistingSpells);
    }

    [Fact]
    public async Task RebornLevelGateWinsBeforeInfrastructure()
    {
        var result = await InvokeAsync(
            optionId: 2,
            classCode: 15,
            reborn: 1,
            level: 2,
            attributePoints: 10,
            Spells(Spell(777)),
            includeSessions: false,
            includeUsersRoot: false,
            includePlayerSpellsRoot: false,
            includeWorldData: false);

        Assert.Equal("Error..!", AssertMessageBox(Assert.Single(result.ClaimantSent)).Text);
        Assert.False(result.Disconnected);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(15, 119, 0, 910, true, 0)]
    [InlineData(15, 120, 0, 970, false, 60)]
    [InlineData(13_002, 109, 0, 910, true, 0)]
    [InlineData(13_002, 110, 0, 1_000, false, 90)]
    [InlineData(15, 1, 2, 1_327, false, 417)]
    public async Task UpdateLevelBranchesMatchSourceThresholdsAndCurrentWireOrder(
        long classCode,
        long level,
        long reborn,
        long expectedAttributes,
        bool expectSourceStats,
        long expectedLevelBonus)
    {
        var originalSpells = Spells(Spell(700), Spell(701));
        var result = await InvokeAsync(
            optionId: 2,
            classCode,
            reborn,
            level,
            attributePoints: 10,
            originalSpells,
            observerCount: 1);

        Assert.Equal(140, result.UpdatedSession.Character.Level);
        Assert.Equal(expectedAttributes, result.UpdatedSession.Character.AttributePoints);
        Assert.Equal(2, result.UpdatedSession.Character.Reborn);
        Assert.Equal(1_005, result.UpdatedSession.Character.Class);
        Assert.True(result.UpdatedSession.Character.MinHitPoints > 0);
        AssertCharacterCoreEqual(result.UpdatedSession.Character, result.ReloadedCharacter);
        Assert.True(result.Disconnected);
        Assert.Equal("commit", result.ExposureOrder[0]);
        Assert.Equal("disconnect", result.ExposureOrder[^1]);

        var index = 5;
        foreach (var spell in originalSpells.Spells)
        {
            Assert.True(MsgActionPacket.TryParse(ReadFrame(result.ClaimantSent[index++]), out var remove));
            Assert.Equal((MsgActionType)109, remove.Type);
            Assert.Equal(spell.Id, remove.DwParam);
        }

        AssertScalarUpdate(result.ClaimantSent[index++], LegacyMsgUpdateDataType.Level, 140);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(result.ClaimantSent[index++]), out var levelAction));
        Assert.Equal((MsgActionType)92, levelAction.Type);
        Assert.Equal(140U, levelAction.PositionX);
        Assert.Equal(CharacterUid, levelAction.ObjId);

        if (expectSourceStats)
        {
            AssertScalarUpdate(result.ClaimantSent[index++], LegacyMsgUpdateDataType.Strength, 0);
            AssertScalarUpdate(result.ClaimantSent[index++], LegacyMsgUpdateDataType.Agility, 0);
            AssertScalarUpdate(result.ClaimantSent[index++], LegacyMsgUpdateDataType.Spirit, 0);
            AssertScalarUpdate(result.ClaimantSent[index++], LegacyMsgUpdateDataType.Vitality, 1);
        }
        else
        {
            AssertScalarUpdate(result.ClaimantSent[index++], LegacyMsgUpdateDataType.Atributes, (ulong)expectedAttributes);
        }

        var classPacketIndex = result.ClaimantSent.Count - 6;
        Assert.True(classPacketIndex > index);
        AssertScalarUpdate(result.ClaimantSent[classPacketIndex], LegacyMsgUpdateDataType.Class, 1_005);
        var observerAction = Assert.Single(Assert.Single(result.ObserverSent));
        Assert.True(MsgActionPacket.TryParse(ReadFrame(observerAction), out var peerLevelAction));
        Assert.Equal((MsgActionType)92, peerLevelAction.Type);
        Assert.Contains($"levelBonus={expectedLevelBonus}", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LevelAbove140SendsLevelActionAndHealWithoutExtraLevelAttributePacket()
    {
        var result = await InvokeAsync(
            optionId: 2,
            classCode: 15,
            reborn: 0,
            level: 141,
            attributePoints: 10,
            Spells(Spell(700)),
            observerCount: 1);

        Assert.Equal(910, result.UpdatedSession.Character.AttributePoints);
        var levelIndex = 6;
        AssertScalarUpdate(result.ClaimantSent[levelIndex], LegacyMsgUpdateDataType.Level, 140);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(result.ClaimantSent[levelIndex + 1]), out var action));
        Assert.Equal((MsgActionType)92, action.Type);
        AssertScalarUpdate(result.ClaimantSent[levelIndex + 2], LegacyMsgUpdateDataType.MaxHitpoints,
            ParseSingleUpdate(result.ClaimantSent[levelIndex + 2]).Value);
        Assert.Contains("levelBonus=0", result.Log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("sessions", "session-store-missing")]
    [InlineData("users", "users-root-missing")]
    [InlineData("spells", "player-spells-root-missing")]
    [InlineData("world", "world-data-missing-for-update-level")]
    public async Task MissingMutationOwnerFencesBeforePersistencePacketsOrDisconnect(
        string missing,
        string expectedReason)
    {
        var result = await InvokeAsync(
            optionId: 2,
            classCode: 15,
            reborn: 2,
            level: 1,
            attributePoints: 10,
            Spells(Spell(700)),
            includeSessions: missing != "sessions",
            includeUsersRoot: missing != "users",
            includePlayerSpellsRoot: missing != "spells",
            includeWorldData: missing != "world");

        Assert.True(result.Handled);
        Assert.Empty(result.ClaimantSent);
        Assert.False(result.Disconnected);
        Assert.Null(result.PersistedAtCommit);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(result.OriginalCharacterBytes, result.PersistedCharacterBytes);
        Assert.Equal(result.OriginalSpellBytes, result.PersistedSpellBytes);
        Assert.Contains("PARITY-GAP", result.Log, StringComparison.Ordinal);
        Assert.Contains($"reason={expectedReason}", result.Log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PersistenceLock.Character)]
    [InlineData(PersistenceLock.Spells)]
    public async Task EitherLockedPersistenceFileRollsBackBothBeforeExposure(PersistenceLock persistenceLock)
    {
        var result = await InvokeAsync(
            optionId: 2,
            classCode: 15,
            reborn: 0,
            level: 140,
            attributePoints: 10,
            Spells(Spell(700), Spell(701)),
            persistenceLock: persistenceLock);

        Assert.Empty(result.ClaimantSent);
        Assert.False(result.Disconnected);
        Assert.Null(result.PersistedAtCommit);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(result.OriginalCharacterBytes, result.PersistedCharacterBytes);
        Assert.Equal(result.OriginalSpellBytes, result.PersistedSpellBytes);
        Assert.Contains("transaction-write-failed:IOException", result.Log, StringComparison.Ordinal);
        Assert.Contains("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AttributeOverflowFencesBeforeDurableOrWireMutation()
    {
        var result = await InvokeAsync(
            optionId: 2,
            classCode: 15,
            reborn: 0,
            level: 140,
            attributePoints: 65_000,
            Spells(Spell(700)));

        Assert.Empty(result.ClaimantSent);
        Assert.False(result.Disconnected);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(result.OriginalCharacterBytes, result.PersistedCharacterBytes);
        Assert.Equal(result.OriginalSpellBytes, result.PersistedSpellBytes);
        Assert.Contains("reason=explicit-attribute-bonus-overflow", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DuplicatePersistedSpellIdsFenceInsteadOfInventingDictionaryOrder()
    {
        var result = await InvokeAsync(
            optionId: 1,
            classCode: 15,
            reborn: 0,
            level: 140,
            attributePoints: 10,
            Spells(Spell(700), Spell(700, level: 2)));

        Assert.Empty(result.ClaimantSent);
        Assert.False(result.Disconnected);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Contains("reason=duplicate-player-spell-id", result.Log, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        byte optionId,
        long classCode,
        long reborn,
        long level,
        long attributePoints,
        PlayerSpellSnapshot originalSpells,
        bool includeSessions = true,
        bool includeUsersRoot = true,
        bool includePlayerSpellsRoot = true,
        bool includeWorldData = true,
        int observerCount = 0,
        PersistenceLock persistenceLock = PersistenceLock.None)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(invocationRoot, "Users");
        var playerSpellsRoot = Path.Combine(invocationRoot, "PlayersSpells");
        Directory.CreateDirectory(usersRoot);
        var characterPath = Path.Combine(usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        WriteCharacter(characterPath, classCode, reborn, level, attributePoints);
        var spellLoader = new PlayerSpellSnapshotLoader(playerSpellsRoot);
        spellLoader.SaveByCharacterUid(CharacterUid, originalSpells);
        var spellPath = Path.Combine(playerSpellsRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var originalCharacterBytes = File.ReadAllBytes(characterPath);
        var originalSpellBytes = File.ReadAllBytes(spellPath);

        var characterLoader = new CharacterSnapshotLoader(usersRoot);
        var character = Assert.IsType<CharacterSnapshot>(characterLoader.LoadByAccountId(AccountId));
        var session = new WorldSession(
            AccountId,
            "full-account-class-test",
            character,
            PlayerItemSnapshot.Empty,
            originalSpells,
            PlayerProficiencySnapshot.Empty,
            CombatGear: CombatGearSnapshot.Empty,
            UsersRoot: includeUsersRoot ? usersRoot : null);
        var claimantSent = new List<byte[]>();
        var observerSent = new List<IReadOnlyList<byte[]>>();
        var exposureOrder = new List<string>();
        var disconnected = false;
        var claimant = new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9_958),
            (frame, _) =>
            {
                var bytes = frame.ToArray();
                claimantSent.Add(bytes);
                exposureOrder.Add("send:" + ReadFrame(bytes).Header.Opcode.ToString(CultureInfo.InvariantCulture));
                return ValueTask.CompletedTask;
            },
            _ =>
            {
                disconnected = true;
                exposureOrder.Add("disconnect");
                return ValueTask.CompletedTask;
            });
        PersistedState? persistedAtCommit = null;
        var sessions = new WorldSessionStore((connection, previous, _) =>
        {
            if (!ReferenceEquals(connection, claimant) || previous is null)
            {
                return;
            }

            persistedAtCommit = new PersistedState(
                Assert.IsType<CharacterSnapshot>(characterLoader.LoadByAccountId(AccountId)),
                spellLoader.LoadByCharacterUid(CharacterUid));
            exposureOrder.Add("commit");
        });
        sessions.Set(claimant, session);

        for (var index = 0; index < observerCount; index++)
        {
            var sent = new List<byte[]>();
            var observer = new ConquerTcpConnection(
                null,
                new IPEndPoint(IPAddress.Loopback, 9_959 + index),
                (frame, _) =>
                {
                    sent.Add(frame.ToArray());
                    return ValueTask.CompletedTask;
                });
            sessions.Set(
                observer,
                session with
                {
                    AccountId = AccountId + index + 1,
                    Character = character with
                    {
                        Uid = CharacterUid + (uint)index + 1,
                        Name = "FullAccountClassObserver" + index.ToString(CultureInfo.InvariantCulture),
                    },
                    UsersRoot = null,
                });
            observerSent.Add(sent);
        }

        FileStream? locked = persistenceLock switch
        {
            PersistenceLock.Character => OpenPersistenceLock(characterPath),
            PersistenceLock.Spells => OpenPersistenceLock(spellPath),
            _ => null,
        };

        bool handled;
        using var log = new StringWriter();
        try
        {
            handled = await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
                new WorldPacketHandlerContext(
                    claimant,
                    log,
                    Session: session,
                    WorldData: includeWorldData ? LegacyItemWorldDataFixture.Create() : null,
                    Sessions: includeSessions ? sessions : null,
                    PlayerSpellsRoot: includePlayerSpellsRoot ? playerSpellsRoot : null),
                session,
                NpcId,
                optionId,
                input: string.Empty,
                CancellationToken.None);
        }
        finally
        {
            locked?.Dispose();
        }

        Assert.True(sessions.TryGet(claimant, out var stored));
        return new InvokeResult(
            handled,
            session,
            Assert.IsType<WorldSession>(stored),
            persistedAtCommit,
            Assert.IsType<CharacterSnapshot>(characterLoader.LoadByAccountId(AccountId)),
            spellLoader.LoadByCharacterUid(CharacterUid),
            claimantSent,
            observerSent,
            exposureOrder,
            disconnected,
            log.ToString(),
            originalCharacterBytes,
            originalSpellBytes,
            File.ReadAllBytes(characterPath),
            File.ReadAllBytes(spellPath),
            File.ReadAllText(characterPath, Encoding.Latin1));
    }

    private static void WriteCharacter(
        string path,
        long classCode,
        long reborn,
        long level,
        long attributePoints)
    {
        File.WriteAllText(
            path,
            $$"""
            [Character]
            UID={{CharacterUid}}
            Name=FullAccountClassHero
            Body=1003
            Face=0
            Haire=0
            Class={{classCode}}
            FirstClass=13005
            SecoundeClass=2005
            Reborn={{reborn}}
            Level={{level}}
            Experience=777
            Map=1002
            X=430
            Y=378
            PMap=1002
            PMapX=430
            PMapY=378
            Strength=11
            Agility=12
            Vitaliti=13
            Spirit=14
            Atributes={{attributePoints}}
            MinHitPoints=50
            MinMana=25
            MaxMana=25
            Money=100
            ConquerPoints=200
            BoundConquerPoints=300
            BankCps=400
            PkPoints=0
            Online=1
            Sentinel=keep-me

            [Other]
            Note=preserve-this-section
            """,
            Encoding.Latin1);
    }

    private static FileStream OpenPersistenceLock(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    private static object[] Case(
        byte optionId,
        uint targetClass,
        uint familyMinimum,
        uint familyMaximum,
        bool clearExistingSpells,
        params ushort[] spellIds)
    {
        return [optionId, targetClass, familyMinimum, familyMaximum, clearExistingSpells, spellIds];
    }

    private static PlayerSpellSnapshot Spells(params PlayerSpellSnapshotEntry[] spells)
    {
        return new PlayerSpellSnapshot(spells);
    }

    private static PlayerSpellSnapshotEntry Spell(
        ushort id,
        ushort level = 0,
        int experience = 0,
        byte previousLevel = 0,
        byte soulLevel = 0,
        byte useSpellSoul = 0)
    {
        return new PlayerSpellSnapshotEntry(id, level, experience, previousLevel, soulLevel, useSpellSoul);
    }

    private static PlayerSpellSnapshot BuildExpectedSpells(
        PlayerSpellSnapshot original,
        IReadOnlyList<ushort> sourceSpellIds,
        bool clearExistingSpells)
    {
        var entries = clearExistingSpells
            ? new List<PlayerSpellSnapshotEntry>()
            : original.Spells.ToList();
        foreach (var spellId in sourceSpellIds)
        {
            var reset = Spell(spellId);
            var index = entries.FindIndex(spell => spell.Id == spellId);
            if (index >= 0)
            {
                entries[index] = reset;
            }
            else
            {
                entries.Add(reset);
            }
        }

        return new PlayerSpellSnapshot(entries);
    }

    private static void AssertCharacterCoreEqual(CharacterSnapshot expected, CharacterSnapshot actual)
    {
        Assert.Equal(expected.Class, actual.Class);
        Assert.Equal(expected.FirstClass, actual.FirstClass);
        Assert.Equal(expected.SecondClass, actual.SecondClass);
        Assert.Equal(expected.Reborn, actual.Reborn);
        Assert.Equal(expected.Level, actual.Level);
        Assert.Equal(expected.Experience, actual.Experience);
        Assert.Equal(expected.Strength, actual.Strength);
        Assert.Equal(expected.Agility, actual.Agility);
        Assert.Equal(expected.Vitality, actual.Vitality);
        Assert.Equal(expected.Spirit, actual.Spirit);
        Assert.Equal(expected.AttributePoints, actual.AttributePoints);
        Assert.Equal(expected.MinHitPoints, actual.MinHitPoints);
    }

    private static void AssertSpellSnapshotsEqual(PlayerSpellSnapshot expected, PlayerSpellSnapshot actual)
    {
        Assert.Equal(
            PlayerSpellSnapshotLoader.ToLegacyBytes(expected),
            PlayerSpellSnapshotLoader.ToLegacyBytes(actual));
    }

    private static void AssertScalarUpdate(byte[] bytes, LegacyMsgUpdateDataType type, ulong value)
    {
        var update = ParseSingleUpdate(bytes);
        Assert.Equal(type, update.Type);
        Assert.Equal(value, update.Value);
    }

    private static LegacyMsgUpdateEntry ParseSingleUpdate(byte[] bytes)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(CharacterUid, packet.Uid);
        return Assert.Single(packet.Updates);
    }

    private static LegacyNpcReplyPacket AssertMessageBox(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        return reply;
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

    public enum PersistenceLock
    {
        None,
        Character,
        Spells,
    }

    private sealed record PersistedState(CharacterSnapshot Character, PlayerSpellSnapshot Spells);

    private sealed record InvokeResult(
        bool Handled,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        PersistedState? PersistedAtCommit,
        CharacterSnapshot ReloadedCharacter,
        PlayerSpellSnapshot PersistedSpells,
        IReadOnlyList<byte[]> ClaimantSent,
        IReadOnlyList<IReadOnlyList<byte[]>> ObserverSent,
        IReadOnlyList<string> ExposureOrder,
        bool Disconnected,
        string Log,
        byte[] OriginalCharacterBytes,
        byte[] OriginalSpellBytes,
        byte[] PersistedCharacterBytes,
        byte[] PersistedSpellBytes,
        string PersistedCharacterText);
}
