using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyDragonWarRuntimeReviveTests
{
    private const int LegacyDragonWarMapId = 700;

    [Fact]
    public void CreateDueRuntimeUpdatesRevivesDeadTournamentPlayersAfterSourceDelay()
    {
        var store = new LegacyDragonWarRuntimeStateStore();
        var openedAt = new DateTimeOffset(2026, 6, 25, 19, 0, 0, TimeSpan.Zero);
        var deadAt = openedAt.AddSeconds(80);
        var session = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["MaxHitpoints"] = "61229",
                ["MaxMana"] = "300",
            });

        store.Open(openedAt);
        Assert.True(store.TryJoin(session, out var joined));
        store.SetProcess(LegacyDragonWarProcess.Alive);

        var death = LegacyPlayerDeathState.ApplyDeath(
            joined.Character,
            deadAt,
            deadX: (ushort)joined.Character.X,
            deadY: (ushort)joined.Character.Y);
        var deadSession = joined with { Character = death.Character };

        var tooEarly = store.CreateDueRuntimeUpdates(deadAt.AddSeconds(4), [deadSession]);

        Assert.Empty(tooEarly.SessionUpdates);

        var revivedTick = store.CreateDueRuntimeUpdates(deadAt.AddSeconds(5), [deadSession]);
        var revived = Assert.Single(revivedTick.SessionUpdates).Session;

        Assert.Equal(LegacyDragonWarMapId, revived.Character.MapId);
        Assert.Equal(deadSession.Character.MapId, revived.Character.PreviousMapId);
        Assert.Equal(deadSession.Character.X, revived.Character.PreviousMapX);
        Assert.Equal(deadSession.Character.Y, revived.Character.PreviousMapY);
        Assert.True(revived.Character.MinHitPoints > 0);
        Assert.Equal(61_229, revived.Character.MinHitPoints);
        Assert.True(revived.Character.Attributes.TryGetValue("DynamicID", out var dynamicId));
        Assert.Equal(joined.Character.Attributes["DynamicID"], dynamicId);
        Assert.False(HasLegacyStatusFlag(revived.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.False(HasLegacyStatusFlag(revived.Character.Attributes, LegacyPlayerDeathState.GhostStatusFlag));
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
