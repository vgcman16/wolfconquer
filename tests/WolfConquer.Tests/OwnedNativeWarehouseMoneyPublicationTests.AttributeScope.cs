using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedNativeWarehouseMoneyPublicationTests
{
    [Theory]
    [InlineData("77")]
    [InlineData("88")]
    public Task ScopedWarehouseWitnessRetainsRawValuesButActualActorPublicationRejectsAmbiguousDynamicId(string second) =>
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

            // Warehouse-local DATA accepts these unrelated rows. DynamicID is
            // nevertheless owned by the joint actor projection, so the actual
            // Store must reject its duplicate spelling before publication.
            var error = Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection, proposed));
            Assert.Equal("ordinaryActorScalarStateInvalidOrAmbiguous", error.Message);

            Assert.Same(before, f.Current);
            Assert.Equal(native, WarehouseNativeTuple(f)); Assert.Equal(first, f.Sent.Count);
            Assert.NotNull(f.Native.OrdinarySpellBinding); f.Data.AssertFiles(files);
            Assert.False(f.Native.OrdinarySpellBinding!.IsRetired);
            Assert.False(f.Runtime!.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            // Complete Character persistence must still refuse this ambiguity;
            // native projection may not silently merge or discard either row.
            Assert.False(LegacyWarehouseMoneyState.TryCopyPersistenceFields(proof.CapturedFields, out var saved));
            Assert.Null(saved);
            fields["dynamicid"] = "99";
            Assert.False(proof.IsCurrentPlain());
        });

    [Fact]
    public Task ActualStorePublicationPreservesTrulyOpaqueCaseDistinctFieldsWithoutChangingWarehouseOrFiles() =>
        WithWarehouseFrame("555", null, async f =>
        {
            await f.LoginAsync();
            var before = f.Current;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var fields = new Dictionary<string, string>(before.Character.Attributes, StringComparer.Ordinal)
            { ["Other"] = "first", ["other"] = "second" };
            var proposed = before with { Character = before.Character with { Attributes = fields } };
            var proof = new LegacyWarehouseMoneyState.NativeValueProof(proposed.Character);
            Assert.Equal("first", proof.CapturedFields["Other"]);
            Assert.Equal("second", proof.CapturedFields["other"]);
            var files = f.Data.Files(); var native = WarehouseNativeTuple(f); int first = f.Sent.Count;
            try
            {
                f.Store.Set(f.Connection, proposed);

                Assert.NotSame(before, f.Current); Assert.Same(fields, f.Current.Character.Attributes);
                Assert.Equal("first", f.Current.Character.Attributes["Other"]);
                Assert.Equal("second", f.Current.Character.Attributes["other"]);
                Assert.True(proof.IsCurrentPlain());
                Assert.Equal(native, WarehouseNativeTuple(f)); Assert.Equal(first, f.Sent.Count);
                Assert.Same(binding, f.Native.OrdinarySpellBinding); Assert.False(binding.IsRetired);
                Assert.False(f.Runtime!.Recovery.IsBlocked); f.Data.AssertFiles(files);
                Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
                fields["other"] = "third";
                Assert.False(proof.IsCurrentPlain());
            }
            finally
            {
                // Restore ordinary persistence input before actual owner drain.
                // The accepted in-memory compatibility is asserted above.
                fields.Remove("other");
            }
        });
}
