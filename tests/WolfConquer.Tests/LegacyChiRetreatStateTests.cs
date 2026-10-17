using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyChiRetreatStateTests
{
    [Theory]
    [InlineData(LegacyChiPowerType.Dragon, "DragonTime", "RetreatedDragon")]
    [InlineData(LegacyChiPowerType.Phoenix, "PhoenixTime", "RetreatedPhoenix")]
    [InlineData(LegacyChiPowerType.Tiger, "TigerTime", "RetreatedTiger")]
    [InlineData(LegacyChiPowerType.Turtle, "TurtleTime", "RetreatedTurtle")]
    public void CancelClearsSelectedRetreatStorage(
        LegacyChiPowerType powerType,
        string timeKey,
        string retreatedKey)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [timeKey] = "1",
            [retreatedKey] = "9-90|10-100|11-110|12-120",
        };
        var character = CreateCharacter(attributes);

        Assert.True(
            LegacyChiRetreatState.TryCancelRetreat(
                character,
                powerType,
                out var updated,
                out var reason));

        Assert.Equal("cancelled", reason);
        Assert.Equal("0", updated.Attributes[timeKey]);
        Assert.Equal(string.Empty, updated.Attributes[retreatedKey]);
    }

    [Fact]
    public void CancelMissingRetreatLeavesCharacterUnchanged()
    {
        var character = CreateCharacter(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.False(
            LegacyChiRetreatState.TryCancelRetreat(
                character,
                LegacyChiPowerType.Dragon,
                out var updated,
                out var reason));

        Assert.Same(character, updated);
        Assert.Equal("retreatMissing", reason);
    }

    [Theory]
    [InlineData(LegacyChiPowerType.Dragon, "DragonTime", "RetreatedDragon")]
    [InlineData(LegacyChiPowerType.Phoenix, "PhoenixTime", "RetreatedPhoenix")]
    [InlineData(LegacyChiPowerType.Tiger, "TigerTime", "RetreatedTiger")]
    [InlineData(LegacyChiPowerType.Turtle, "TurtleTime", "RetreatedTurtle")]
    public void SealCopiesCurrentAttributesWithoutLockBits(
        LegacyChiPowerType powerType,
        string timeKey,
        string retreatedKey)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PowerKey(powerType)] = "1/100/1/10/0/2/20/1/3/30/0/4/40/1/",
            [timeKey] = "1",
            [retreatedKey] = "9-90|10-100|11-110|12-120",
        };
        var character = CreateCharacter(attributes);

        Assert.True(
            LegacyChiRetreatState.TrySealCurrentPower(
                character,
                powerType,
                DateTime.Now,
                out var updated,
                out var reason));

        Assert.Equal("sealed", reason);
        Assert.Equal("1-10|2-20|3-30|4-40", updated.Attributes[retreatedKey]);
        Assert.Equal("1", updated.Attributes[timeKey]);
        Assert.Equal(attributes[PowerKey(powerType)], updated.Attributes[PowerKey(powerType)]);
    }

    [Fact]
    public void ExpiredRetreatDoesNotChangeStoredPower()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dragon"] = "1/100/1/10/0/2/20/0/3/30/0/4/40/0/",
            ["DragonTime"] = DateTime.Now.AddMinutes(-1).ToBinary().ToString(),
            ["RetreatedDragon"] = "9-90|10-100|11-110|12-120",
        };
        var character = CreateCharacter(attributes);

        Assert.False(
            LegacyChiRetreatState.TrySealCurrentPower(
                character,
                LegacyChiPowerType.Dragon,
                DateTime.Now,
                out var updated,
                out var reason));

        Assert.Same(character, updated);
        Assert.Equal("retreatExpired", reason);
        Assert.Equal("9-90|10-100|11-110|12-120", updated.Attributes["RetreatedDragon"]);
    }

    [Fact]
    public void MalformedRetreatTimeFailsWithoutChangingStoredPower()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dragon"] = "1/100/1/10/0/2/20/0/3/30/0/4/40/0/",
            ["DragonTime"] = long.MaxValue.ToString(),
            ["RetreatedDragon"] = "9-90|10-100|11-110|12-120",
        };
        var character = CreateCharacter(attributes);

        Assert.False(
            LegacyChiRetreatState.TrySealCurrentPower(
                character,
                LegacyChiPowerType.Dragon,
                DateTime.Now,
                out var updated,
                out var reason));

        Assert.Same(character, updated);
        Assert.Equal("retreatTimeInvalid", reason);
    }

    [Theory]
    [InlineData(LegacyChiPowerType.Dragon, "DragonTime", "RetreatedDragon", "Dragon")]
    [InlineData(LegacyChiPowerType.Phoenix, "PhoenixTime", "RetreatedPhoenix", "Pheonix")]
    [InlineData(LegacyChiPowerType.Tiger, "TigerTime", "RetreatedTiger", "Tiger")]
    [InlineData(LegacyChiPowerType.Turtle, "TurtleTime", "RetreatedTurtle", "Turtle")]
    public void RestoreReplacesOnlySelectedPowerAndClearsRestoredLocks(
        LegacyChiPowerType powerType,
        string timeKey,
        string retreatedKey,
        string powerKey)
    {
        var originalPower = "1/777/1/10/1/2/20/0/3/30/1/4/40/0/";
        var attributes = CreateAllPowerAttributes();
        attributes[powerKey] = originalPower;
        attributes[timeKey] = "1";
        attributes[retreatedKey] = "9-90|10-100|11-110|12-120";
        attributes["UnrelatedSentinel"] = "unchanged";
        var character = CreateCharacter(attributes);
        var untouchedPowers = attributes
            .Where(entry => IsPowerKey(entry.Key) && !string.Equals(entry.Key, powerKey, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

        Assert.True(
            LegacyChiRetreatState.TryRestoreRetreatedPower(
                character,
                powerType,
                DateTime.Now,
                out var updated,
                out var reason));

        Assert.Equal("restored", reason);
        Assert.NotSame(character, updated);
        Assert.Equal("1/777/9/90/0/10/100/0/11/110/0/12/120/0/", updated.Attributes[powerKey]);
        Assert.Equal(originalPower, character.Attributes[powerKey]);
        Assert.Equal("1", updated.Attributes[timeKey]);
        Assert.Equal("9-90|10-100|11-110|12-120", updated.Attributes[retreatedKey]);
        Assert.Equal("unchanged", updated.Attributes["UnrelatedSentinel"]);
        foreach (var untouchedPower in untouchedPowers)
        {
            Assert.Equal(untouchedPower.Value, updated.Attributes[untouchedPower.Key]);
        }
    }

    [Fact]
    public void RestoreAcceptsUnexpiredRetreatAndPreservesPhoenixAlias()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Phoenix"] = "1/4321/1/10/1/2/20/1/3/30/1/4/40/1/",
            ["PhoenixTime"] = DateTime.Now.AddMinutes(5).ToBinary().ToString(CultureInfo.InvariantCulture),
            ["RetreatedPhoenix"] = "5-50|6-60|7-70|8-80",
        };
        var character = CreateCharacter(attributes);

        Assert.True(
            LegacyChiRetreatState.TryRestoreRetreatedPower(
                character,
                LegacyChiPowerType.Phoenix,
                DateTime.Now,
                out var updated,
                out var reason));

        Assert.Equal("restored", reason);
        Assert.Equal("1/4321/5/50/0/6/60/0/7/70/0/8/80/0/", updated.Attributes["Phoenix"]);
        Assert.False(updated.Attributes.ContainsKey("Pheonix"));
    }

    [Theory]
    [InlineData(null, "1-10|2-20|3-30|4-40", "retreatMissing")]
    [InlineData("0", "1-10|2-20|3-30|4-40", "retreatMissing")]
    [InlineData("not-a-time", "1-10|2-20|3-30|4-40", "retreatTimeInvalid")]
    [InlineData("1", null, "retreatedPowerMissing")]
    [InlineData("1", "", "retreatedPowerMissing")]
    [InlineData("1", "1-10|2-20|3-30", "retreatedPowerInvalid")]
    [InlineData("1", "1-10|2-20|3-30|4-40|5-50", "retreatedPowerInvalid")]
    [InlineData("1", "bad-10|2-20|3-30|4-40", "retreatedPowerInvalid")]
    [InlineData("1", "1-70000|2-20|3-30|4-40", "retreatedPowerInvalid")]
    [InlineData("1", "1-10|2-20|3-30|4", "retreatedPowerInvalid")]
    public void RestoreRejectsMissingOrMalformedRetreatWithoutMutation(
        string? retreatTime,
        string? retreatedPower,
        string expectedReason)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dragon"] = "1/100/1/10/1/2/20/1/3/30/1/4/40/1/",
            ["UnrelatedSentinel"] = "unchanged",
        };
        if (retreatTime is not null)
        {
            attributes["DragonTime"] = retreatTime;
        }

        if (retreatedPower is not null)
        {
            attributes["RetreatedDragon"] = retreatedPower;
        }

        AssertRestoreFailureLeavesCharacterUnchanged(attributes, expectedReason);
    }

    [Fact]
    public void RestoreRejectsInvalidBinaryRetreatTimeWithoutMutation()
    {
        var attributes = ValidDragonRestoreAttributes();
        attributes["DragonTime"] = long.MaxValue.ToString(CultureInfo.InvariantCulture);

        AssertRestoreFailureLeavesCharacterUnchanged(attributes, "retreatTimeInvalid");
    }

    [Fact]
    public void RestoreRejectsExpiredRetreatWithoutMutation()
    {
        var attributes = ValidDragonRestoreAttributes();
        attributes["DragonTime"] = DateTime.Now.AddMinutes(-1).ToBinary().ToString(CultureInfo.InvariantCulture);

        AssertRestoreFailureLeavesCharacterUnchanged(attributes, "retreatExpired");
    }

    [Fact]
    public void RestoreRejectsLockedCurrentPowerWithoutMutation()
    {
        var attributes = ValidDragonRestoreAttributes();
        attributes["Dragon"] = "0/100/1/10/1/2/20/1/3/30/1/4/40/1/";

        AssertRestoreFailureLeavesCharacterUnchanged(attributes, "powerLockedOrMissing");
    }

    [Fact]
    public void RestoreRejectsInvalidPowerTypeWithoutMutation()
    {
        var attributes = ValidDragonRestoreAttributes();
        var character = CreateCharacter(attributes);

        Assert.False(
            LegacyChiRetreatState.TryRestoreRetreatedPower(
                character,
                LegacyChiPowerType.None,
                DateTime.Now,
                out var updated,
                out var reason));

        Assert.Same(character, updated);
        Assert.Equal("powerTypeInvalid", reason);
    }

    private static void AssertRestoreFailureLeavesCharacterUnchanged(
        Dictionary<string, string> attributes,
        string expectedReason)
    {
        var character = CreateCharacter(attributes);
        var before = character.Attributes.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.OrdinalIgnoreCase);

        Assert.False(
            LegacyChiRetreatState.TryRestoreRetreatedPower(
                character,
                LegacyChiPowerType.Dragon,
                DateTime.Now,
                out var updated,
                out var reason));

        Assert.Same(character, updated);
        Assert.Equal(expectedReason, reason);
        Assert.Equal(before, updated.Attributes);
    }

    private static Dictionary<string, string> ValidDragonRestoreAttributes()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dragon"] = "1/100/1/10/1/2/20/1/3/30/1/4/40/1/",
            ["DragonTime"] = "1",
            ["RetreatedDragon"] = "5-50|6-60|7-70|8-80",
            ["UnrelatedSentinel"] = "unchanged",
        };
    }

    private static Dictionary<string, string> CreateAllPowerAttributes()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dragon"] = "1/101/1/11/0/2/21/0/3/31/0/4/41/0/",
            ["Pheonix"] = "1/102/1/12/0/2/22/0/3/32/0/4/42/0/",
            ["Tiger"] = "1/103/1/13/0/2/23/0/3/33/0/4/43/0/",
            ["Turtle"] = "1/104/1/14/0/2/24/0/3/34/0/4/44/0/",
        };
    }

    private static bool IsPowerKey(string key)
    {
        return key is "Dragon" or "Pheonix" or "Phoenix" or "Tiger" or "Turtle";
    }

    private static string PowerKey(LegacyChiPowerType powerType)
    {
        return powerType == LegacyChiPowerType.Phoenix ? "Pheonix" : powerType.ToString();
    }

    private static CharacterSnapshot CreateCharacter(IReadOnlyDictionary<string, string> attributes)
    {
        return new CharacterSnapshot(
            Uid: 1,
            Name: "Chi",
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: 10,
            FirstClass: 10,
            SecondClass: 0,
            Reborn: 1,
            Level: 140,
            Experience: 0,
            MapId: 1002,
            X: 430,
            Y: 378,
            PreviousMapId: 1002,
            PreviousMapX: 430,
            PreviousMapY: 378,
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
            SourceFile: "fixture",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
    }
}
