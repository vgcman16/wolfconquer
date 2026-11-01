using System.Collections.ObjectModel;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyTeleportAttributePreservationTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public void ChangedScopePreservesSupportedComparerEveryOpaqueFieldAndUniqueOwnedSpelling(int kind, bool wrapped)
    {
        var fields = Fields(kind); var before = fields.ToArray();
        var source = Character(wrapped ? new ReadOnlyDictionary<string, string>(fields) : fields);
        var result = LegacyTeleportDestinationReducer.Reduce(source, new(1000, 500, 650, 88), true);
        var final = result.FinalCharacter;
        Assert.Equal((1000L, 500L, 650L, 1015L, 830L, 584L),
            (final.MapId, final.X, final.Y, final.PreviousMapId, final.PreviousMapX, final.PreviousMapY));
        AssertScopedAttributes(final, Comparer(kind), kind != 1, "88", "44");
        Assert.Equal(before, fields.ToArray());
        Assert.NotSame(source.Attributes, final.Attributes);
        Assert.Equal(source with { MapId = 1000, X = 500, Y = 650, PreviousMapId = 1015,
            PreviousMapX = 830, PreviousMapY = 584, Attributes = final.Attributes }, final);
    }

    [Fact]
    public void CoordinateOnlyResultRetainsOriginalWrappedMapAndUntouchedPreviousScopeAliases()
    {
        var fields = Fields(0); fields.Add("PDinamycID", "777");
        var source = Character(new ReadOnlyDictionary<string, string>(fields));
        var final = LegacyTeleportDestinationReducer.Reduce(source, new(1015, 801, 550, 44), true).FinalCharacter;
        Assert.Same(source.Attributes, final.Attributes);
        Assert.Equal(source with { X = 801, Y = 550 }, final);
        Assert.Equal("777", final.Attributes["PDinamycID"]); Assert.Equal("777", final.Attributes["pdinamycid"]);
    }

    [Fact]
    public void TwinCityNormalizationPreservesHistoryAndUniqueDynamicKey()
    {
        var source = Character(Fields(0), map: 1002);
        var final = LegacyTeleportDestinationReducer.Reduce(source, new(1002, 410, 354), true).FinalCharacter;
        Assert.Equal((900L, 90L, 91L), (final.PreviousMapId, final.PreviousMapX, final.PreviousMapY));
        AssertScopedAttributes(final, StringComparer.Ordinal, true, "0", "777");
        Assert.Equal("44", source.Attributes["dynamicid"]);
    }

    [Fact]
    public void DynamicPrisonCorrectionKeepsOpaqueDataAndCapturesFirstTransitionHistory()
    {
        var source = Character(Fields(0));
        var result = LegacyTeleportDestinationReducer.Reduce(source, new(6000, 80, 90, 42), true, false);
        Assert.Equal(2, result.TransitionCount);
        AssertScopedAttributes(result.FirstTransition.Character, StringComparer.Ordinal, true, "42", "44");
        AssertScopedAttributes(result.FinalCharacter, StringComparer.Ordinal, true, "0", "42");
        Assert.Equal((6000L, 32L, 73L, 6000L, 80L, 90L), (result.FinalCharacter.MapId, result.FinalCharacter.X,
            result.FinalCharacter.Y, result.FinalCharacter.PreviousMapId, result.FinalCharacter.PreviousMapX, result.FinalCharacter.PreviousMapY));
    }

    [Theory]
    [InlineData("DynamicID", false, true)] [InlineData("DynamicID", true, true)]
    [InlineData("DynamicID", false, false)]
    [InlineData("PDinamycID", false, true)] [InlineData("PDinamycID", false, false)]
    public void DuplicateOperatedAliasesRefuseEvenWhenEqualWithoutChangingSource(string key, bool coordinateOnly, bool equal)
    {
        var fields = Fields(0); fields.Add(key, equal ? fields[key.ToLowerInvariant()] : "123"); var before = fields.ToArray();
        var source = Character(fields);
        var error = Assert.Throws<InvalidDataException>(() => LegacyTeleportDestinationReducer.Reduce(source,
            coordinateOnly ? new(1015, 801, 550, 44) : new(1000, 500, 650, 88), true));
        Assert.Equal("characterAttributeOwnedFieldAmbiguous", error.Message);
        Assert.Equal(before, fields.ToArray()); Assert.Equal((1015L, 830L, 584L), (source.MapId, source.X, source.Y));
    }

    private static IEqualityComparer<string> Comparer(int kind) => kind switch
    {
        0 => StringComparer.Ordinal, 1 => StringComparer.OrdinalIgnoreCase, 2 => EqualityComparer<string>.Default,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
    private static Dictionary<string, string> Fields(int kind)
    {
        var fields = new Dictionary<string, string>(Comparer(kind))
            { ["dynamicid"] = "44", ["pdinamycid"] = "777", ["Other"] = "upper", ["Unrelated"] = "untouched" };
        if (kind != 1) fields.Add("other", "lower"); return fields;
    }
    private static CharacterSnapshot Character(IReadOnlyDictionary<string, string> fields, long map = 1015,
        uint uid = OwnedEquipmentBehaviorFixture.Actor) => OwnedEquipmentBehaviorFixture.Character(uid, mapId: map) with
        { PreviousMapId = 900, PreviousMapX = 90, PreviousMapY = 91, Attributes = fields };
    private static void AssertScopedAttributes(CharacterSnapshot character, IEqualityComparer<string> comparer,
        bool caseDistinct, string dynamicValue, string previous)
    {
        var proof = LegacyNativeAttributeMapProof.CaptureRaw(character.Attributes);
        Assert.Same(comparer, proof.CapturedComparer); Assert.True(proof.IsCurrentPlain());
        Assert.Equal(dynamicValue, character.Attributes["dynamicid"]); Assert.Equal(previous, character.Attributes["pdinamycid"]);
        Assert.Equal("upper", character.Attributes["Other"]); Assert.Equal("untouched", character.Attributes["Unrelated"]);
        if (caseDistinct) Assert.Equal("lower", character.Attributes["other"]);
        Assert.DoesNotContain("DynamicID", character.Attributes.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("PDinamycID", character.Attributes.Keys, StringComparer.Ordinal);
    }
}
