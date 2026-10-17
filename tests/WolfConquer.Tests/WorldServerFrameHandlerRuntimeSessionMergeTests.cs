using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class WorldServerFrameHandlerRuntimeSessionMergeTests
{
    private const string SkyTowerActiveChallengeIdAttribute = "SkyTowerActiveChallengeId";
    private const string ConcurrentOnlyAttribute = "ConcurrentOnly";

    [Fact]
    public void StaminaMergeRetriesAgainstNewerSessionAndPreservesUnrelatedState()
    {
        var publications = new List<(WorldSession? Previous, WorldSession Current)>();
        var sessions = new WorldSessionStore(
            (_, previous, current) => publications.Add((previous, current)));
        var connection = new ConquerTcpConnection(null, null);
        var staleSession = CreateSession(
            username: "stale-owner",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Stamina"] = "20",
                ["XPCount"] = "4",
                [SkyTowerActiveChallengeIdAttribute] = "100",
            });
        sessions.Set(connection, staleSession);

        var newerAttributes = new Dictionary<string, string>(
            staleSession.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["Stamina"] = "35",
            ["XPCount"] = "9",
            [SkyTowerActiveChallengeIdAttribute] = "7341",
            [ConcurrentOnlyAttribute] = "keep-me",
        };
        var newerSession = staleSession with
        {
            Username = "newer-owner",
            PendingBankConquerPoints = 2_500,
            Character = staleSession.Character with { Attributes = newerAttributes },
        };
        sessions.Set(connection, newerSession);
        publications.Clear();

        var merged = WorldServerFrameHandler.TryMergeLegacyRuntimeStaminaUpdate(
            sessions,
            connection,
            staleSession,
            stamina: 77);

        Assert.True(merged);
        var publication = Assert.Single(publications);
        Assert.Same(newerSession, publication.Previous);
        Assert.True(sessions.TryGet(connection, out var currentSession));
        var current = Assert.IsType<WorldSession>(currentSession);
        Assert.Same(current, publication.Current);
        Assert.Equal("newer-owner", current.Username);
        Assert.Equal((uint)2_500, current.PendingBankConquerPoints);
        Assert.Equal("77", current.Character.Attributes["Stamina"]);
        Assert.Equal("9", current.Character.Attributes["XPCount"]);
        Assert.Equal("7341", current.Character.Attributes[SkyTowerActiveChallengeIdAttribute]);
        Assert.Equal("keep-me", current.Character.Attributes[ConcurrentOnlyAttribute]);
    }

    [Fact]
    public void XpStateMergeRetriesAgainstNewerSessionAndPreservesUnrelatedState()
    {
        var publications = new List<(WorldSession? Previous, WorldSession Current)>();
        var sessions = new WorldSessionStore(
            (_, previous, current) => publications.Add((previous, current)));
        var connection = new ConquerTcpConnection(null, null);
        var staleSession = CreateSession(
            username: "stale-owner",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Stamina"] = "20",
                ["XPCount"] = "3",
                ["StatusFlags"] = "0/",
                [SkyTowerActiveChallengeIdAttribute] = "100",
            });
        sessions.Set(connection, staleSession);

        var newerAttributes = new Dictionary<string, string>(
            staleSession.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["Stamina"] = "88",
            ["XPCount"] = "5",
            ["StatusFlags"] = "2/",
            [SkyTowerActiveChallengeIdAttribute] = "9812",
            [ConcurrentOnlyAttribute] = "keep-me-too",
        };
        var newerSession = staleSession with
        {
            Username = "newer-xp-owner",
            LegacyCatalogSelectedItemId = 4_321,
            Character = staleSession.Character with { Attributes = newerAttributes },
        };
        sessions.Set(connection, newerSession);
        publications.Clear();

        var merged = WorldServerFrameHandler.TryMergeLegacyRuntimeXpStateUpdate(
            sessions,
            connection,
            staleSession,
            xpCount: 42,
            xpListActive: true);

        Assert.True(merged);
        var publication = Assert.Single(publications);
        Assert.Same(newerSession, publication.Previous);
        Assert.True(sessions.TryGet(connection, out var currentSession));
        var current = Assert.IsType<WorldSession>(currentSession);
        Assert.Same(current, publication.Current);
        Assert.Equal("newer-xp-owner", current.Username);
        Assert.Equal((uint)4_321, current.LegacyCatalogSelectedItemId);
        Assert.Equal("42", current.Character.Attributes["XPCount"]);
        Assert.Equal("88", current.Character.Attributes["Stamina"]);
        Assert.Equal("9812", current.Character.Attributes[SkyTowerActiveChallengeIdAttribute]);
        Assert.Equal("keep-me-too", current.Character.Attributes[ConcurrentOnlyAttribute]);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(current.Character.Attributes, flag: 1));
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(
            current.Character.Attributes,
            LegacyRuntimeUpdateTicker.XpListFlag));
    }

    private static WorldSession CreateSession(
        string username,
        IReadOnlyDictionary<string, string> attributes)
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: username,
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "RuntimeMerge",
                Body: 1_003,
                Face: 0,
                Hair: 0,
                Class: 10,
                FirstClass: 10,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: 300,
                Y: 278,
                PreviousMapId: 1_002,
                PreviousMapX: 300,
                PreviousMapY: 278,
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
                Online: 1,
                SourceFile: "1293600.ini",
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }
}
