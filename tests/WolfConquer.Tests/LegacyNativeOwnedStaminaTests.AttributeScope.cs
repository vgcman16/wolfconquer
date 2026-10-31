using System.Collections.ObjectModel;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeOwnedStaminaTests
{
    // DATA: the existing real warehouse publication cases separately prove
    // this projection through an admitted owner and the actual Store.
    [Theory]
    [InlineData(false, "77")]
    [InlineData(false, "88")]
    [InlineData(true, "77")]
    [InlineData(true, "88")]
    public void ScopedStaminaProjectionPreservesUnrelatedAliasesAndWitnessesBothRawRows(bool readOnly, string second)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        { ["sTaMiNa"] = "12", ["DynamicID"] = "77", ["dynamicid"] = second };
        IReadOnlyDictionary<string, string> input = readOnly ? new ReadOnlyDictionary<string, string>(fields) : fields;
        var source = Session(input);
        var proof = new LegacyStaminaState.NativeValueProof(source.Character);
        Assert.Equal((ushort)12, proof.Value);
        Assert.Equal("77", proof.CapturedFields["DynamicID"]);
        Assert.Equal(second, proof.CapturedFields["dynamicid"]);
        Assert.True(proof.IsCurrentPlain());

        var admitted = LegacyStaminaState.ForAdmission(source);

        Assert.Equal(3, admitted.Character.Attributes.Count);
        Assert.Equal("12", admitted.Character.Attributes["Stamina"]);
        Assert.Equal("77", admitted.Character.Attributes["DynamicID"]);
        Assert.Equal(second, admitted.Character.Attributes["dynamicid"]);
        Assert.Contains(source.Character.Attributes.Keys, key => key == "sTaMiNa");
        Assert.Equal((ushort)12, LegacyStaminaState.RequireCurrent(admitted.Character));
        Assert.False(LegacyWarehouseMoneyState.TryCopyPersistenceFields(admitted.Character.Attributes, out _));
        fields["dynamicid"] = "99";
        Assert.False(proof.IsCurrentPlain());
    }
}
