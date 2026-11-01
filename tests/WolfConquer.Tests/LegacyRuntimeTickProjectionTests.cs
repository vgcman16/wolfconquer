using System.Collections.ObjectModel;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRuntimeTickProjectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DataEditsPreserveUnrelatedCaseDistinctFieldsAndSourceComparer(bool readOnly)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Other"] = "upper", ["other"] = "lower", ["stamina"] = "20", ["xpcount"] = "9",
            ["statusflags"] = "2/", ["LegacyStatusFlagRemoveOnDead.4"] = "true",
            ["LegacyStatusFlagRemoveOnDead.5"] = "false",
        };
        var character = OwnedEquipmentBehaviorFixture.Character() with
        { Attributes = readOnly ? new ReadOnlyDictionary<string, string>(fields) : fields };
        var before = fields.ToArray();
        var stamina = WorldServerFrameHandler.ProjectLegacyRuntimeStamina(character, 25);
        var projected = WorldServerFrameHandler.ProjectLegacyRuntimeXpState(stamina, 42, true);
        var proof = LegacyNativeAttributeMapProof.CaptureRaw(projected.Attributes);
        Assert.Same(StringComparer.Ordinal, proof.CapturedComparer);
        Assert.Equal("upper", proof.Fields["Other"]); Assert.Equal("lower", proof.Fields["other"]);
        Assert.Equal("25", proof.Fields["stamina"]); Assert.False(proof.Fields.ContainsKey("Stamina"));
        Assert.Equal("42", proof.Fields["xpcount"]); Assert.False(proof.Fields.ContainsKey("XPCount"));
        Assert.False(proof.Fields.ContainsKey("StatusFlags"));
        Assert.Equal("18", proof.Fields["statusflags"].Split('/')[0]);
        Assert.False(proof.Fields.ContainsKey("LegacyStatusFlagRemoveOnDead.4"));
        Assert.Equal("false", proof.Fields["LegacyStatusFlagRemoveOnDead.5"]);
        Assert.True(proof.IsCurrentPlain());
        Assert.Equal(before, fields.ToArray());
    }

    [Theory]
    [InlineData("Stamina")]
    [InlineData("XPCount")]
    [InlineData("StatusFlags")]
    [InlineData("BitVector")]
    [InlineData("LegacyStatusFlagRemoveOnDead.4")]
    public void AmbiguousUsedFieldRefusesBeforeChangingAnyData(string key)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        { [key] = "0", [key.ToLowerInvariant()] = "0", ["Other"] = "upper", ["other"] = "lower" };
        var before = fields.ToArray();
        var character = OwnedEquipmentBehaviorFixture.Character() with { Attributes = fields };
        var error = Assert.Throws<InvalidDataException>(() => key == "Stamina"
            ? WorldServerFrameHandler.ProjectLegacyRuntimeStamina(character, 5)
            : WorldServerFrameHandler.ProjectLegacyRuntimeXpState(character, 1, true));
        Assert.Equal("runtimeTickOwnedFieldAmbiguous", error.Message);
        Assert.Equal(before, fields.ToArray());
    }

    [Fact]
    public void CountOnlyDataEditDoesNotOwnOrNormalizeUnusedStatusFields()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        { ["XPCount"] = "0", ["StatusFlags"] = "2/", ["statusflags"] = "4/" };
        var character = OwnedEquipmentBehaviorFixture.Character() with { Attributes = fields };
        var projected = WorldServerFrameHandler.ProjectLegacyRuntimeXpState(character, 1, null);
        Assert.Equal("1", projected.Attributes["XPCount"]);
        Assert.Equal("2/", projected.Attributes["StatusFlags"]);
        Assert.Equal("4/", projected.Attributes["statusflags"]);
        Assert.Equal("0", fields["XPCount"]);
    }
}
