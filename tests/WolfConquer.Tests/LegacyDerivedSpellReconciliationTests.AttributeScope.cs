using System.Collections.ObjectModel;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDerivedSpellReconciliationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DerivedMarkerProjectionPreservesUnrelatedCaseDistinctFieldsAndSourceComparer(bool wrapped)
    {
        var before = CharacterSnapshotParser.ParseText(CharacterText());
        var attributes = new Dictionary<string, string>(before.Attributes, StringComparer.Ordinal)
        { ["Opaque"] = "upper", ["opaque"] = "lower" };
        var nobility = new Dictionary<string, string>(StringComparer.Ordinal)
        { ["Other"] = "first", ["other"] = "second" };
        before = before with
        {
            Attributes = wrapped ? new ReadOnlyDictionary<string, string>(attributes) : attributes,
            PaidNobility = wrapped ? new ReadOnlyDictionary<string, string>(nobility) : nobility,
        };
        var result = LegacyDerivedSpellReconciliation.Project(before, PlayerSpellSnapshot.Empty,
            new[] { Desired(21930, 4) });

        Assert.Equal("upper", result.Character.Attributes["Opaque"]);
        Assert.Equal("lower", result.Character.Attributes["opaque"]);
        Assert.Equal("first", result.Character.PaidNobility["Other"]);
        Assert.Equal("second", result.Character.PaidNobility["other"]);
        Assert.False(result.Character.Attributes.ContainsKey("OPAQUE"));
        Assert.False(result.Character.PaidNobility.ContainsKey("OTHER"));
        Assert.Equal((ushort)4, LegacyDerivedSpellOrigins.Read(result.Character)[21930]);
        Assert.False(attributes.ContainsKey(LegacyDerivedSpellOrigins.Key));
        attributes["opaque"] = "mutated"; nobility["other"] = "mutated";
        Assert.Equal("lower", result.Character.Attributes["opaque"]);
        Assert.Equal("second", result.Character.PaidNobility["other"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("21930:4")]
    public void CaseDuplicateDerivedMarkersStillRefuseWithoutChangingInput(string marker)
    {
        var before = CharacterSnapshotParser.ParseText(CharacterText());
        var attributes = new Dictionary<string, string>(before.Attributes, StringComparer.Ordinal)
        { [LegacyDerivedSpellOrigins.Key] = marker, ["managedderivedspellorigins"] = marker };
        before = before with { Attributes = attributes };
        var saved = attributes.ToArray();

        Assert.Throws<InvalidDataException>(() => LegacyDerivedSpellOrigins.Read(before));
        Assert.Throws<InvalidDataException>(() => LegacyDerivedSpellOrigins.WithOrigins(before,
            new Dictionary<ushort, ushort>()));
        Assert.Equal(saved, attributes.ToArray());
    }
}
