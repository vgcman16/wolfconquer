using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class WorldSessionSaveSidecarPersistenceTests : IDisposable
{
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-session-save-sidecars-" + Guid.NewGuid());

    [Fact]
    public void SessionSetPersistsChangedPlayerProficienciesWithoutFailureLog()
    {
        using var log = new StringWriter();
        var handler = CreateHandler(log);
        var connection = CreateConnection();
        var session = CreateSession();
        SetSession(handler, connection, session);
        var changed = new PlayerProficiencySnapshot(
        [
            new PlayerProficiencySnapshotEntry(410, 12, 8, 123_456, 99),
        ]);

        SetSession(handler, connection, session with { PlayerProficiencies = changed });

        var persisted = new PlayerProficiencySnapshotLoader(ProficienciesRoot).LoadByCharacterUid(CharacterUid);
        Assert.Equal(
            PlayerProficiencySnapshotLoader.ToLegacyBytes(changed),
            PlayerProficiencySnapshotLoader.ToLegacyBytes(persisted));
        Assert.DoesNotContain("player proficiency persistence failed", log.ToString());
    }

    [Fact]
    public void SessionSetPersistsChangedCharacterPlayerQuestsWithoutFailureLog()
    {
        using var log = new StringWriter();
        var handler = CreateHandler(log);
        var connection = CreateConnection();
        var session = CreateSession();
        SetSession(handler, connection, session);
        var changed = new PlayerQuestSnapshot(
        [
            new PlayerQuestSnapshotEntry(35_024, PlayerQuestStatus.Accepted, 123_456, [7, 9]),
        ]);

        SetSession(
            handler,
            connection,
            session with { Character = session.Character with { PlayerQuests = changed } });

        var persisted = new PlayerQuestSnapshotLoader(QuestsRoot).LoadByCharacterUid(CharacterUid);
        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(changed), PlayerQuestSnapshotLoader.Serialize(persisted));
        Assert.DoesNotContain("player quest persistence failed", log.ToString());
    }

    [Fact]
    public void SessionSetLeavesByteIdenticalProficienciesAndQuestsUntouched()
    {
        Directory.CreateDirectory(ProficienciesRoot);
        Directory.CreateDirectory(QuestsRoot);
        var proficiencies = new PlayerProficiencySnapshot(
        [
            new PlayerProficiencySnapshotEntry(410, 12, 8, 123_456, 99),
        ]);
        var quests = new PlayerQuestSnapshot(
        [
            new PlayerQuestSnapshotEntry(35_024, PlayerQuestStatus.Accepted, 123_456, [7, 9]),
        ]);
        var proficiencyLoader = new PlayerProficiencySnapshotLoader(ProficienciesRoot);
        var questLoader = new PlayerQuestSnapshotLoader(QuestsRoot);
        proficiencyLoader.SaveByCharacterUid(CharacterUid, proficiencies);
        questLoader.SaveByCharacterUid(CharacterUid, quests);
        var proficiencyPath = Path.Combine(ProficienciesRoot, CharacterUid + ".bin");
        var questPath = Path.Combine(QuestsRoot, CharacterUid + ".bin");
        var proficiencyTimestamp = DateTime.UtcNow.AddMinutes(-5);
        var questTimestamp = DateTime.UtcNow.AddMinutes(-4);
        File.SetLastWriteTimeUtc(proficiencyPath, proficiencyTimestamp);
        File.SetLastWriteTimeUtc(questPath, questTimestamp);
        using var log = new StringWriter();
        var handler = CreateHandler(log);
        var connection = CreateConnection();
        var session = CreateSession(proficiencies, quests);
        SetSession(handler, connection, session);
        var equivalentProficiencies = new PlayerProficiencySnapshot(proficiencies.Proficiencies.ToArray());
        var equivalentQuests = new PlayerQuestSnapshot(
            quests.Quests.Select(quest => quest with { Intentions = quest.Intentions.ToArray() }).ToArray());

        using (new FileStream(proficiencyPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (new FileStream(questPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            SetSession(
                handler,
                connection,
                session with
                {
                    PlayerProficiencies = equivalentProficiencies,
                    Character = session.Character with { PlayerQuests = equivalentQuests },
                });
        }

        Assert.Equal(proficiencyTimestamp, File.GetLastWriteTimeUtc(proficiencyPath));
        Assert.Equal(questTimestamp, File.GetLastWriteTimeUtc(questPath));
        Assert.DoesNotContain("player proficiency persistence failed", log.ToString());
        Assert.DoesNotContain("player quest persistence failed", log.ToString());
    }

    [Fact]
    public void SessionSetContinuesLaterSidecarsAfterProficiencyPersistenceFailure()
    {
        Directory.CreateDirectory(_root);
        var blockedProficienciesRoot = Path.Combine(_root, "BlockedPlayersProfs");
        var combatGearRoot = Path.Combine(_root, "PlayersCombatGear");
        File.WriteAllText(blockedProficienciesRoot, "not-a-directory");
        using var log = new StringWriter();
        var handler = CreateHandler(
            log,
            proficienciesRoot: blockedProficienciesRoot,
            combatGearRoot: combatGearRoot);
        var connection = CreateConnection();
        var session = CreateSession();
        SetSession(handler, connection, session);
        var changedProficiencies = new PlayerProficiencySnapshot(
        [
            new PlayerProficiencySnapshotEntry(410, 12, 8, 123_456, 99),
        ]);
        var changedCombatGear = CombatGearSnapshot.Empty with { MasteryPoints = 77 };
        var changedQuests = new PlayerQuestSnapshot(
        [
            new PlayerQuestSnapshotEntry(35_024, PlayerQuestStatus.Accepted, 123_456, [7, 9]),
        ]);

        SetSession(
            handler,
            connection,
            session with
            {
                PlayerProficiencies = changedProficiencies,
                CombatGear = changedCombatGear,
                Character = session.Character with { PlayerQuests = changedQuests },
            });

        Assert.Equal(
            77U,
            new CombatGearSnapshotLoader(combatGearRoot)
                .LoadByCharacterUid(CharacterUid)
                .MasteryPoints);
        var persistedQuests = new PlayerQuestSnapshotLoader(QuestsRoot).LoadByCharacterUid(CharacterUid);
        Assert.Equal(
            PlayerQuestSnapshotLoader.Serialize(changedQuests),
            PlayerQuestSnapshotLoader.Serialize(persistedQuests));
        var output = log.ToString();
        Assert.Contains("player proficiency persistence failed", output);
        Assert.DoesNotContain("combat gear persistence failed", output);
        Assert.DoesNotContain("player quest persistence failed", output);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string UsersRoot => Path.Combine(_root, "Users");

    private string ProficienciesRoot => Path.Combine(_root, "PlayersProfs");

    private string QuestsRoot => Path.Combine(_root, "Quests");

    private WorldServerFrameHandler CreateHandler(
        TextWriter log,
        string? proficienciesRoot = null,
        string? combatGearRoot = null)
    {
        Directory.CreateDirectory(UsersRoot);
        var loader = new CharacterSnapshotLoader(
            UsersRoot,
            playerItemsRoot: null,
            playerSpellsRoot: null,
            proficienciesRoot ?? ProficienciesRoot,
            combatGearRoot,
            QuestsRoot);
        return new WorldServerFrameHandler(loader, log);
    }

    private static ConquerTcpConnection CreateConnection()
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958));
    }

    private static void SetSession(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        WorldSession session)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_sessions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var sessions = Assert.IsType<WorldSessionStore>(field.GetValue(handler));
        sessions.Set(connection, session);
    }

    private static WorldSession CreateSession(
        PlayerProficiencySnapshot? proficiencies = null,
        PlayerQuestSnapshot? quests = null)
    {
        var character = new CharacterSnapshot(
            Uid: CharacterUid,
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
            Money: 986_024_399,
            ConquerPoints: 639_778_904,
            BoundConquerPoints: 456,
            BankConquerPoints: 123,
            PkPoints: 0,
            Online: 1,
            SourceFile: CharacterUid + ".ini",
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
        {
            PlayerQuests = quests ?? PlayerQuestSnapshot.Empty,
        };

        return new WorldSession(
            AccountId: CharacterUid,
            Username: "user02",
            Character: character,
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: proficiencies ?? PlayerProficiencySnapshot.Empty);
    }
}
