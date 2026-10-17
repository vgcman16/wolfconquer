using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyDragonWarRuntimeStartTests
{
    private const int LegacyDragonWarMapId = 700;
    private const int LegacyDragonSwingFlag = 0xA0;
    private const int UnrelatedStatusFlag = 0x93;
    private const uint FirstRightCornerChatType = 2_108;

    [Fact]
    public void CreateDueUpdatesStartsIdleDragonWarAndGivesFirstParticipantDragonSwing()
    {
        var store = new LegacyDragonWarRuntimeStateStore();
        var openedAt = new DateTimeOffset(2026, 6, 25, 19, 0, 0, TimeSpan.Zero);
        var sessions = new[]
        {
            CreateSession(
                uid: 1,
                name: "Alpha",
                attributes: new Dictionary<string, string>
                {
                    ["StatusFlags"] = BuildStatusFlags(UnrelatedStatusFlag),
                }),
            CreateSession(uid: 2, name: "Bravo"),
        };
        store.Open(openedAt);

        var joinedSessions = sessions.Select(session =>
        {
            Assert.True(store.TryJoin(session, out var joined));
            return joined;
        }).ToList();

        var startTick = store.CreateDueRuntimeUpdates(openedAt.AddSeconds(61), joinedSessions);

        Assert.Contains(
            startTick.Updates,
            update => update.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                update.Message == "DragonWar has started! signup are now closed.");

        var mutatedSessions = startTick.SessionUpdates.Select(update => update.Session).ToList();
        var mutatedSession = Assert.Single(mutatedSessions);

        Assert.Contains(joinedSessions, session => session.Character.Uid == mutatedSession.Character.Uid);
        Assert.Equal("5", mutatedSession.Character.Attributes["DragonWarHits"]);
        Assert.True(HasLegacyStatusFlag(mutatedSession.Character.Attributes, LegacyDragonSwingFlag));
        Assert.True(HasLegacyStatusFlag(mutatedSession.Character.Attributes, UnrelatedStatusFlag));

        foreach (var unchangedSession in joinedSessions.Where(session => session.Character.Uid != mutatedSession.Character.Uid))
        {
            Assert.False(HasLegacyStatusFlag(unchangedSession.Character.Attributes, LegacyDragonSwingFlag));
            Assert.Equal("0", unchangedSession.Character.Attributes["DragonWarHits"]);
        }

        List<WorldSession> sessionsAfterStart = joinedSessions
            .Select(session => session.Character.Uid == mutatedSession.Character.Uid ? mutatedSession : session)
            .ToList();
        var scoreboardUpdates = store.CreateDueRuntimeUpdates(openedAt.AddSeconds(64), sessionsAfterStart).Updates;

        Assert.Contains(
            scoreboardUpdates,
            update => update.ChatType == FirstRightCornerChatType &&
                update.Message == "DragonWar Score: ");
    }

    [Fact]
    public void CreateDueUpdatesPreservesExistingStatusVectorWordsWhenAddingDragonSwing()
    {
        var store = new LegacyDragonWarRuntimeStateStore();
        var openedAt = new DateTimeOffset(2026, 6, 25, 19, 0, 0, TimeSpan.Zero);
        var session = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(4, 63, 64, UnrelatedStatusFlag, 191),
            });
        store.Open(openedAt);
        Assert.True(store.TryJoin(session, out var joined));

        var startTick = store.CreateDueRuntimeUpdates(openedAt.AddSeconds(61), [joined]);
        var mutatedSession = Assert.Single(startTick.SessionUpdates.Select(update => update.Session));

        Assert.True(HasLegacyStatusFlag(mutatedSession.Character.Attributes, LegacyDragonSwingFlag));
        Assert.True(HasLegacyStatusFlag(mutatedSession.Character.Attributes, 4));
        Assert.True(HasLegacyStatusFlag(mutatedSession.Character.Attributes, 63));
        Assert.True(HasLegacyStatusFlag(mutatedSession.Character.Attributes, 64));
        Assert.True(HasLegacyStatusFlag(mutatedSession.Character.Attributes, UnrelatedStatusFlag));
        Assert.True(HasLegacyStatusFlag(mutatedSession.Character.Attributes, 191));
    }

    private static WorldSession CreateSession(
        long uid = 1_293_600,
        string name = "HeroOne",
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return new WorldSession(
            AccountId: uid,
            Username: name,
            Character: CreateSnapshot(uid, name, attributes ?? new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static CharacterSnapshot CreateSnapshot(
        long uid,
        string name,
        IReadOnlyDictionary<string, string> attributes)
    {
        return new CharacterSnapshot(
            Uid: uid,
            Name: name,
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: 7051,
            FirstClass: 0,
            SecondClass: 0,
            Reborn: 0,
            Level: 140,
            Experience: 0,
            MapId: 1002,
            X: 705,
            Y: 577,
            PreviousMapId: 1002,
            PreviousMapX: 705,
            PreviousMapY: 577,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: 61_229,
            MinMana: 0,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 0,
            SourceFile: "",
            Attributes: new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var values = new ulong[4];
        foreach (var flag in flags)
        {
            var offset = flag / 64;
            if ((uint)offset >= values.Length)
            {
                continue;
            }

            values[offset] |= 1UL << (flag % 64);
        }

        return string.Join(',', values);
    }

    private static bool HasLegacyStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
    {
        return attributes.TryGetValue("StatusFlags", out var raw) &&
            HasLegacyStatusFlag(raw, flag);
    }

    private static bool HasLegacyStatusFlag(string statusFlags, int flag)
    {
        if (statusFlags.Contains('/', StringComparison.Ordinal))
        {
            var slashParts = statusFlags.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var wordIndex = flag / 32;
            return wordIndex < slashParts.Length &&
                ulong.TryParse(slashParts[wordIndex], out var word) &&
                (word & (1UL << (flag % 32))) != 0;
        }

        var parts = statusFlags.Split(',', StringSplitOptions.TrimEntries);
        var offset = flag / 64;
        return offset < parts.Length &&
            ulong.TryParse(parts[offset], out var value) &&
            (value & (1UL << (flag % 64))) != 0;
    }
}
