using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedNativeWarehouseMoneyPublicationTests
{
    [Theory]
    [InlineData("77")]
    [InlineData("88")]
    public Task ScopedWarehousePublicationPreservesUnrelatedCaseDistinctFieldsAndWitnessesEveryRawValue(string second) =>
        WithWarehouseFrame("555", null, async f =>
        {
            await f.LoginAsync();
            var before = f.Current;
            var fields = new Dictionary<string, string>(before.Character.Attributes, StringComparer.Ordinal)
            { ["DynamicID"] = "77", ["dynamicid"] = second };
            var proposed = before with { Character = before.Character with { Attributes = fields } };
            Assert.True(LegacyWarehouseMoneyState.TryRead(fields, out var value));
            Assert.Equal(555L, value);
            var proof = new LegacyWarehouseMoneyState.NativeValueProof(proposed.Character);
            Assert.Equal("77", proof.CapturedFields["DynamicID"]);
            Assert.Equal(second, proof.CapturedFields["dynamicid"]);
            Assert.True(proof.IsCurrentPlain());
            var files = f.Data.Files(); var native = WarehouseNativeTuple(f); int first = f.Sent.Count;

            f.Store.Set(f.Connection, proposed);

            Assert.Same(fields, f.Current.Character.Attributes);
            Assert.Equal(native, WarehouseNativeTuple(f)); Assert.Equal(first, f.Sent.Count);
            Assert.NotNull(f.Native.OrdinarySpellBinding); f.Data.AssertFiles(files);
            // Complete Character persistence must still refuse this ambiguity;
            // native projection may not silently merge or discard either row.
            Assert.False(LegacyWarehouseMoneyState.TryCopyPersistenceFields(proof.CapturedFields, out var saved));
            Assert.Null(saved);
            fields["dynamicid"] = "99";
            Assert.False(proof.IsCurrentPlain());
        });
}
