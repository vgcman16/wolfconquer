using System.Collections.ObjectModel;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// DATA projection, not a native birth, attached actor or complete-file permission.
public sealed class LegacyActionStateAttributeScopeTests
{
    [Theory]
    [InlineData(false, "65786", 65786U)] [InlineData(true, "65786", 65786U)]
    [InlineData(false, "4294967295", uint.MaxValue)] [InlineData(true, "4294967295", uint.MaxValue)]
    public void UnrelatedOrdinalAliasesSurviveWhileFullWidthActionAndEveryRawRowStayWitnessed(bool readOnly, string action, uint expected)
    {
        var raw = new Dictionary<string, string>(StringComparer.Ordinal)
            { ["aCtIoN"] = action, ["Other"] = "77", ["other"] = "88" };
        IReadOnlyDictionary<string, string> input = readOnly ? new ReadOnlyDictionary<string, string>(raw) : raw;
        var source = Session(input); var proof = new LegacyActionState.NativeValueProof(source.Character);
        Assert.Equal(expected, proof.Value); Assert.True(proof.IsCurrentPlain());
        var projected = LegacyActionState.ForAdmission(source);
        Assert.Equal(3, projected.Character.Attributes.Count);
        Assert.Equal("77", projected.Character.Attributes["Other"]); Assert.Equal("88", projected.Character.Attributes["other"]);
        Assert.Equal(expected, LegacyActionState.RequireCurrent(projected.Character)); Assert.Null(projected.NativeGameClient);
        Assert.Contains("aCtIoN", raw.Keys);
        Assert.False(LegacyWarehouseMoneyState.TryCopyPersistenceFields(projected.Character.Attributes, out _));
        raw["other"] = "99"; Assert.False(proof.IsCurrentPlain());
    }

    [Theory]
    [InlineData("250", "250")] [InlineData("250", "0")]
    public void DuplicateOwnedActionNamesRefuseEvenWhenTheirValuesMatch(string first, string second)
    {
        var source = Session(new Dictionary<string, string>(StringComparer.Ordinal) { ["Action"] = first, ["action"] = second });
        Assert.Throws<InvalidOperationException>(() => LegacyActionState.ForAdmission(source));
    }

    [Fact]
    public void GenuineAbsenceProjectsZeroWithoutDiscardingOtherRowsOrWeakeningTheStrictSharedProof()
    {
        var raw = new Dictionary<string, string>(StringComparer.Ordinal) { ["Other"] = "77", ["other"] = "88" };
        Assert.Throws<InvalidOperationException>(() => new LegacyNativeAttributeMapProof(raw));
        var projected = LegacyActionState.ForAdmission(Session(raw));
        Assert.Equal(0U, LegacyActionState.RequireCurrent(projected.Character));
        Assert.Equal(3, projected.Character.Attributes.Count); Assert.Equal(2, raw.Count);
    }

    private static WorldSession Session(IReadOnlyDictionary<string, string> fields) => new(1, "ActionData",
        OwnedEquipmentBehaviorFixture.Character() with { Attributes = fields },
        PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty);
}
