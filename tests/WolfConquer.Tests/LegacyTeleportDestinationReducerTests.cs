using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeleportDestinationReducerTests
{
    private const long OriginalPreviousMapId = 900;
    private const long OriginalPreviousMapX = 90;
    private const long OriginalPreviousMapY = 91;
    private const ulong OriginalPreviousDynamicId = 777;

    [Fact]
    public void MissingMapFallsBackToTwinCityCoordinatesButRetainsRequestedDynamicId()
    {
        var source = Character(mapId: 1_015, x: 830, y: 584, dynamicId: 44);

        var result = LegacyTeleportDestinationReducer.Reduce(
            source,
            new LegacyTeleportDestination(99_999, 12, 13, DynamicId: 88),
            requestedMapExists: false);

        Assert.Equal(1, result.TransitionCount);
        Assert.Null(result.Map6000Correction);
        Assert.Equal(new LegacyTeleportDestination(1_002, 410, 354, 88), result.FirstTransition.Destination);
        AssertCharacterLocation(result.FinalCharacter, mapId: 1_002, x: 410, y: 354, dynamicId: 88);
        AssertHistory(result.FinalCharacter, mapId: 1_015, x: 830, y: 584, dynamicId: 0);
    }

    [Fact]
    public void MissingMapFallbackCanUseSameScopeBranchAfterTwinCityNormalization()
    {
        var source = Character(mapId: 1_002, x: 430, y: 378, dynamicId: 44);

        var result = LegacyTeleportDestinationReducer.Reduce(
            source,
            new LegacyTeleportDestination(99_999, 12, 13),
            requestedMapExists: false);

        AssertCharacterLocation(result.FinalCharacter, mapId: 1_002, x: 410, y: 354, dynamicId: 0);
        AssertOriginalHistory(result.FinalCharacter);
    }

    [Fact]
    public void SameMapAndDynamicOnlyMoveCoordinatesAndPreserveAllHistory()
    {
        var source = Character(mapId: 1_015, x: 830, y: 584, dynamicId: 44);

        var result = LegacyTeleportDestinationReducer.Reduce(
            source,
            new LegacyTeleportDestination(1_015, 801, 550, DynamicId: 44),
            requestedMapExists: true);

        AssertCharacterLocation(result.FinalCharacter, mapId: 1_015, x: 801, y: 550, dynamicId: 44);
        AssertOriginalHistory(result.FinalCharacter);
        Assert.Same(source.Attributes, result.FinalCharacter.Attributes);
    }

    [Fact]
    public void SameMapWithDifferentDynamicCapturesCurrentScopeInHistory()
    {
        var source = Character(mapId: 1_015, x: 830, y: 584, dynamicId: 44);

        var result = LegacyTeleportDestinationReducer.Reduce(
            source,
            new LegacyTeleportDestination(1_015, 801, 550, DynamicId: 45),
            requestedMapExists: true);

        AssertCharacterLocation(result.FinalCharacter, mapId: 1_015, x: 801, y: 550, dynamicId: 45);
        AssertHistory(result.FinalCharacter, mapId: 1_015, x: 830, y: 584, dynamicId: 44);
    }

    [Fact]
    public void DifferentMapWithSameDynamicCapturesCurrentScopeInHistory()
    {
        var source = Character(mapId: 1_015, x: 830, y: 584, dynamicId: 44);

        var result = LegacyTeleportDestinationReducer.Reduce(
            source,
            new LegacyTeleportDestination(1_000, 500, 650, DynamicId: 44),
            requestedMapExists: true);

        AssertCharacterLocation(result.FinalCharacter, mapId: 1_000, x: 500, y: 650, dynamicId: 44);
        AssertHistory(result.FinalCharacter, mapId: 1_015, x: 830, y: 584, dynamicId: 44);
    }

    [Fact]
    public void TwinCityZeroesCurrentDynamicBeforeSameScopeComparison()
    {
        var source = Character(mapId: 1_002, x: 430, y: 378, dynamicId: 44);

        var result = LegacyTeleportDestinationReducer.Reduce(
            source,
            new LegacyTeleportDestination(1_002, 410, 354),
            requestedMapExists: true);

        AssertCharacterLocation(result.FinalCharacter, mapId: 1_002, x: 410, y: 354, dynamicId: 0);
        AssertOriginalHistory(result.FinalCharacter);
    }

    [Fact]
    public void TwinCityNormalizationForcesChangedBranchWhenRequestedDynamicIsNonzero()
    {
        var source = Character(mapId: 1_002, x: 430, y: 378, dynamicId: 44);

        var result = LegacyTeleportDestinationReducer.Reduce(
            source,
            new LegacyTeleportDestination(1_002, 410, 354, DynamicId: 44),
            requestedMapExists: true);

        AssertCharacterLocation(result.FinalCharacter, mapId: 1_002, x: 410, y: 354, dynamicId: 44);
        AssertHistory(result.FinalCharacter, mapId: 1_002, x: 430, y: 378, dynamicId: 0);
    }

    [Fact]
    public void TwinCityNormalizationIsCapturedAfterChangingFromAnotherMap()
    {
        var source = Character(mapId: 1_015, x: 830, y: 584, dynamicId: 44);

        var result = LegacyTeleportDestinationReducer.Reduce(
            source,
            new LegacyTeleportDestination(1_002, 410, 354),
            requestedMapExists: true);

        AssertCharacterLocation(result.FinalCharacter, mapId: 1_002, x: 410, y: 354, dynamicId: 0);
        AssertHistory(result.FinalCharacter, mapId: 1_015, x: 830, y: 584, dynamicId: 0);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("DynamicID", "not-a-number")]
    [InlineData("DynamicId", "-1")]
    public void MissingOrMalformedCurrentDynamicIsTreatedAsZero(string? key, string? value)
    {
        var attributes = BaseAttributes();
        if (key is not null)
        {
            attributes[key] = Assert.IsType<string>(value);
        }

        var source = Character(mapId: 1_015, x: 830, y: 584, attributes: attributes);

        var result = LegacyTeleportDestinationReducer.Reduce(
            source,
            new LegacyTeleportDestination(1_000, 500, 650),
            requestedMapExists: true);

        AssertCharacterLocation(result.FinalCharacter, mapId: 1_000, x: 500, y: 650, dynamicId: 0);
        AssertHistory(result.FinalCharacter, mapId: 1_015, x: 830, y: 584, dynamicId: 0);
    }

    [Fact]
    public void DynamicIdAliasIsReadAndChangedBranchWritesCanonicalAttributeNames()
    {
        var attributes = BaseAttributes();
        attributes["DynamicId"] = "44";
        attributes["pdinamycid"] = "123";
        attributes["Unrelated"] = "kept";
        var source = Character(mapId: 1_015, x: 830, y: 584, attributes: attributes);

        var result = LegacyTeleportDestinationReducer.Reduce(
            source,
            new LegacyTeleportDestination(1_000, 500, 650, DynamicId: 88),
            requestedMapExists: true);

        Assert.Contains("DynamicID", result.FinalCharacter.Attributes.Keys);
        Assert.Contains("PDinamycID", result.FinalCharacter.Attributes.Keys);
        Assert.DoesNotContain("DynamicId", result.FinalCharacter.Attributes.Keys);
        Assert.DoesNotContain("pdinamycid", result.FinalCharacter.Attributes.Keys);
        Assert.Equal("kept", result.FinalCharacter.Attributes["Unrelated"]);
        AssertCharacterLocation(result.FinalCharacter, mapId: 1_000, x: 500, y: 650, dynamicId: 88);
        AssertHistory(result.FinalCharacter, mapId: 1_015, x: 830, y: 584, dynamicId: 44);
    }

    [Fact]
    public void ValidPrisonDestinationProducesOnlyFirstTransition()
    {
        var source = Character(mapId: 1_002, x: 430, y: 378, dynamicId: 0);

        var result = LegacyTeleportDestinationReducer.Reduce(
            source,
            new LegacyTeleportDestination(6_000, 80, 90),
            requestedMapExists: true,
            firstDestinationLocationIsValid: true);

        Assert.Equal(1, result.TransitionCount);
        Assert.Null(result.Map6000Correction);
        AssertCharacterLocation(result.FinalCharacter, mapId: 6_000, x: 80, y: 90, dynamicId: 0);
        AssertHistory(result.FinalCharacter, mapId: 1_002, x: 430, y: 378, dynamicId: 0);
    }

    [Fact]
    public void InvalidPrisonDestinationProducesSecondTransitionToSourceCorrectionPoint()
    {
        var source = Character(mapId: 1_002, x: 430, y: 378, dynamicId: 0);

        var result = LegacyTeleportDestinationReducer.Reduce(
            source,
            new LegacyTeleportDestination(6_000, 80, 90),
            requestedMapExists: true,
            firstDestinationLocationIsValid: false);

        Assert.Equal(2, result.TransitionCount);
        AssertCharacterLocation(result.FirstTransition.Character, mapId: 6_000, x: 80, y: 90, dynamicId: 0);
        var correction = Assert.IsType<LegacyTeleportDestinationTransition>(result.Map6000Correction);
        Assert.Equal(new LegacyTeleportDestination(6_000, 32, 73), correction.Destination);
        AssertCharacterLocation(correction.Character, mapId: 6_000, x: 32, y: 73, dynamicId: 0);
        AssertHistory(correction.Character, mapId: 1_002, x: 430, y: 378, dynamicId: 0);
        Assert.Same(correction.Character, result.FinalCharacter);
    }

    [Fact]
    public void InvalidDynamicPrisonDestinationCorrectionCapturesFirstTransitionAsHistory()
    {
        var source = Character(mapId: 1_015, x: 830, y: 584, dynamicId: 7);

        var result = LegacyTeleportDestinationReducer.Reduce(
            source,
            new LegacyTeleportDestination(6_000, 80, 90, DynamicId: 42),
            requestedMapExists: true,
            firstDestinationLocationIsValid: false);

        AssertCharacterLocation(result.FirstTransition.Character, mapId: 6_000, x: 80, y: 90, dynamicId: 42);
        AssertHistory(result.FirstTransition.Character, mapId: 1_015, x: 830, y: 584, dynamicId: 7);
        AssertCharacterLocation(result.FinalCharacter, mapId: 6_000, x: 32, y: 73, dynamicId: 0);
        AssertHistory(result.FinalCharacter, mapId: 6_000, x: 80, y: 90, dynamicId: 42);
    }

    [Fact]
    public void InvalidSameScopeStaticPrisonDestinationPreservesHistoryAcrossBothTransitions()
    {
        var source = Character(mapId: 6_000, x: 40, y: 50, dynamicId: 0);

        var result = LegacyTeleportDestinationReducer.Reduce(
            source,
            new LegacyTeleportDestination(6_000, 80, 90),
            requestedMapExists: true,
            firstDestinationLocationIsValid: false);

        Assert.Equal(2, result.TransitionCount);
        AssertOriginalHistory(result.FirstTransition.Character);
        AssertOriginalHistory(result.FinalCharacter);
        AssertCharacterLocation(result.FinalCharacter, mapId: 6_000, x: 32, y: 73, dynamicId: 0);
    }

    [Fact]
    public void InvalidSameScopeDynamicPrisonDestinationCapturesInvalidPointOnCorrection()
    {
        var source = Character(mapId: 6_000, x: 40, y: 50, dynamicId: 42);

        var result = LegacyTeleportDestinationReducer.Reduce(
            source,
            new LegacyTeleportDestination(6_000, 80, 90, DynamicId: 42),
            requestedMapExists: true,
            firstDestinationLocationIsValid: false);

        AssertOriginalHistory(result.FirstTransition.Character);
        AssertCharacterLocation(result.FinalCharacter, mapId: 6_000, x: 32, y: 73, dynamicId: 0);
        AssertHistory(result.FinalCharacter, mapId: 6_000, x: 80, y: 90, dynamicId: 42);
    }

    [Fact]
    public void UnavailablePrisonMapFallsBackWithoutPrisonCorrection()
    {
        var source = Character(mapId: 1_015, x: 830, y: 584, dynamicId: 7);

        var result = LegacyTeleportDestinationReducer.Reduce(
            source,
            new LegacyTeleportDestination(6_000, 80, 90, DynamicId: 42),
            requestedMapExists: false,
            firstDestinationLocationIsValid: false);

        Assert.Equal(1, result.TransitionCount);
        Assert.Null(result.Map6000Correction);
        Assert.Equal(new LegacyTeleportDestination(1_002, 410, 354, 42), result.FirstTransition.Destination);
        AssertCharacterLocation(result.FinalCharacter, mapId: 1_002, x: 410, y: 354, dynamicId: 42);
        AssertHistory(result.FinalCharacter, mapId: 1_015, x: 830, y: 584, dynamicId: 0);
    }

    [Fact]
    public void ReduceDoesNotMutateSourceSnapshotOrAttributeDictionary()
    {
        var attributes = BaseAttributes();
        attributes["DynamicID"] = "44";
        attributes["Unrelated"] = "kept";
        var source = Character(mapId: 1_015, x: 830, y: 584, attributes: attributes);

        var result = LegacyTeleportDestinationReducer.Reduce(
            source,
            new LegacyTeleportDestination(1_000, 500, 650, DynamicId: 88),
            requestedMapExists: true);

        Assert.Equal(1_015, source.MapId);
        Assert.Equal(830, source.X);
        Assert.Equal(584, source.Y);
        Assert.Equal("44", attributes["DynamicID"]);
        Assert.Equal(OriginalPreviousDynamicId.ToString(CultureInfo.InvariantCulture), attributes["PDinamycID"]);
        Assert.False(ReferenceEquals(source.Attributes, result.FinalCharacter.Attributes));
        Assert.Equal("kept", result.FinalCharacter.Attributes["Unrelated"]);
    }

    private static CharacterSnapshot Character(
        long mapId,
        long x,
        long y,
        ulong? dynamicId = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        var resolvedAttributes = attributes is null ? BaseAttributes() : new Dictionary<string, string>(attributes);
        if (dynamicId.HasValue)
        {
            resolvedAttributes["DynamicID"] = dynamicId.Value.ToString(CultureInfo.InvariantCulture);
        }

        return new CharacterSnapshot(
            Uid: 1_293_600,
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
            MapId: mapId,
            X: x,
            Y: y,
            PreviousMapId: OriginalPreviousMapId,
            PreviousMapX: OriginalPreviousMapX,
            PreviousMapY: OriginalPreviousMapY,
            Strength: 0,
            Agility: 36,
            Vitality: 447,
            Spirit: 0,
            AttributePoints: 12,
            MinHitPoints: 61_258,
            MinMana: 5_000,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 7,
            SourceFile: "",
            Attributes: resolvedAttributes,
            PaidNobility: new Dictionary<string, string>());
    }

    private static Dictionary<string, string> BaseAttributes()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PDinamycID"] = OriginalPreviousDynamicId.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static void AssertCharacterLocation(
        CharacterSnapshot character,
        long mapId,
        long x,
        long y,
        ulong dynamicId)
    {
        Assert.Equal(mapId, character.MapId);
        Assert.Equal(x, character.X);
        Assert.Equal(y, character.Y);
        Assert.Equal(dynamicId, ReadAttribute(character, "DynamicID", "DynamicId"));
    }

    private static void AssertHistory(
        CharacterSnapshot character,
        long mapId,
        long x,
        long y,
        ulong dynamicId)
    {
        Assert.Equal(mapId, character.PreviousMapId);
        Assert.Equal(x, character.PreviousMapX);
        Assert.Equal(y, character.PreviousMapY);
        Assert.Equal(dynamicId, ReadAttribute(character, "PDinamycID"));
    }

    private static void AssertOriginalHistory(CharacterSnapshot character)
    {
        AssertHistory(
            character,
            OriginalPreviousMapId,
            OriginalPreviousMapX,
            OriginalPreviousMapY,
            OriginalPreviousDynamicId);
    }

    private static ulong ReadAttribute(CharacterSnapshot character, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (character.Attributes.TryGetValue(key, out var raw) &&
                ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        throw new Xunit.Sdk.XunitException($"Missing numeric attribute: {string.Join(", ", keys)}");
    }
}
