using System.Collections.ObjectModel;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeOwnedStaminaTests
{
    // DATA ONLY: admission projection does not create an owner or save a file.
    [Theory]
    [InlineData(null, 0)]
    [InlineData("0", 0)]
    [InlineData("37", 37)]
    [InlineData("65535", 65535)]
    public void AdmissionUsesNativeZeroOnlyForAbsenceAndKeepsPresentValidCompatibility(string? saved, int expected)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal) { ["Opaque"] = "retain" };
        if (saved is not null) attributes.Add("sTaMiNa", saved);
        var source = Session(attributes);
        var final = LegacyStaminaState.ForAdmission(source);
        Assert.Equal(checked((ushort)expected), LegacyStaminaState.RequireCurrent(final.Character));
        Assert.Equal(expected.ToString(CultureInfo.InvariantCulture), final.Character.Attributes["Stamina"]);
        Assert.Contains(final.Character.Attributes.Keys, key => key == "Stamina");
        Assert.Equal("retain", final.Character.Attributes["Opaque"]);
        Assert.NotSame(source.Character.Attributes, final.Character.Attributes);
        Assert.Equal(saved is null ? 1 : 2, attributes.Count);
        if (saved is null) Assert.Throws<InvalidOperationException>(() => LegacyStaminaState.RequireCurrent(source.Character));
        else Assert.Equal(saved, attributes["sTaMiNa"]);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("65536")]
    [InlineData("not-a-number")]
    [InlineData("")]
    [InlineData("1.5")]
    public void MalformedOrUnrepresentableRuntimeInputCannotBecomeAdmissionFunds(string text)
    {
        var source = Session(new Dictionary<string, string> { ["Stamina"] = text });
        Assert.Throws<InvalidOperationException>(() => LegacyStaminaState.ForAdmission(source));
        Assert.Throws<InvalidOperationException>(() => LegacyStaminaState.RequireCurrent(source.Character));
        Assert.Equal(text, source.Character.Attributes["Stamina"]);
    }

    [Fact]
    public void PresentNullIsNotAbsence()
    {
        // Saved text cannot express present-null: this is a DATA refusal only.
        var source = Session(new Dictionary<string, string> { ["Stamina"] = null! });
        Assert.Throws<InvalidOperationException>(() => LegacyStaminaState.ForAdmission(source));
        Assert.Null(source.Character.Attributes["Stamina"]);
    }

    [Fact]
    public void DuplicateCaseAliasesRefuseEvenWhenValuesAgree()
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal) { ["Stamina"] = "12", ["stamina"] = "12" };
        Assert.Throws<InvalidOperationException>(() => LegacyStaminaState.ForAdmission(Session(attributes)));
        Assert.Equal(2, attributes.Count);
    }

    [Fact]
    public void BclReadOnlyWrapperCannotConcealCustomBackingWithADifferentIndexedValue()
    {
        // DATA negative: the old enum-only witness saw72 while the holder's
        // actual runtime lookup could report99 through this custom backing.
        var raw = new SplitViewAttributes { ["Stamina"] = "72" };
        var wrapper = new ReadOnlyDictionary<string, string>(raw);
        Assert.Equal("72", wrapper.Single().Value); Assert.Equal("99", wrapper["Stamina"]);
        Assert.Throws<InvalidOperationException>(() => new LegacyStaminaState.NativeValueProof(Session(wrapper).Character));
        Assert.Equal("72", raw.Single().Value);
    }

    [Theory]
    [InlineData("Stamina")]
    [InlineData("Opaque")]
    public void FinalNativeProofObservesChangesAnywhereInItsActualBclMap(string changed)
    {
        var attributes = new Dictionary<string, string> { ["Stamina"] = "12", ["Opaque"] = "before" };
        var character = Session(new ReadOnlyDictionary<string, string>(attributes)).Character;
        var proof = new LegacyStaminaState.NativeValueProof(character);
        Assert.True(proof.IsCurrentPlain());
        attributes[changed] = "13";
        Assert.False(proof.IsCurrentPlain());
        Assert.Equal((ushort)12, proof.Value);
    }

    [Fact]
    public void WholeUnboundComponentRetainsNumericOnlySetterWithoutFullLoadingOutput()
    {
        var client = new LegacyNativeGameClientRuntime();
        Assert.Equal((ushort)0, client.Player.Stamina);
        client.FullLoading = true;
        try
        {
            client.Player.Stamina = ushort.MaxValue;
            Assert.Equal(ushort.MaxValue, client.Player.Stamina);
            lock (client.SpellAdmissionSyncRoot)
            lock (client.Player.SpellAdmissionSyncRoot)
                client.Player.Stamina = 0;
            Assert.Equal((ushort)0, client.Player.Stamina);
            Assert.Null(client.OrdinarySpellBinding);
        }
        finally { client.FullLoading = false; }
    }

    [Fact]
    public Task ActualOwnedStorePublishesRuntimeStaminaAndRefusesInvalidJointHpInput() => Run(f =>
    {
        var before = f.Current(); var native = before.NativeGameClient!; var files = f.Data.Files();
        Assert.Equal((ushort)37, native.Player.Stamina);
        var attributes = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["Stamina"] = "72" };
        f.Store.Set(f.Connection(OwnedEquipmentBehaviorFixture.Actor), before with
        { Character = before.Character with { Attributes = attributes } }, out var final);
        Assert.Same(final, f.Current()); Assert.Equal((ushort)72, native.Player.Stamina);
        Assert.Equal((ushort)72, LegacyStaminaState.RequireCurrent(final.Character));
        Assert.Equal(before.Character.MinHitPoints, final.Character.MinHitPoints);
        Assert.Equal(before.Character.MinMana, final.Character.MinMana);
        f.Data.AssertFiles(files); Assert.Empty(f.ActorFrames);
        var invalid = new Dictionary<string, string>(final.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["Stamina"] = "65536" };
        Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection(OwnedEquipmentBehaviorFixture.Actor), final with
        { Character = final.Character with { MinHitPoints = 2, Attributes = invalid } }));
        Assert.Same(final, f.Current()); Assert.Equal((ushort)72, native.Player.Stamina);
        Assert.Equal(checked((int)before.Character.MinHitPoints), native.Player.HitPoints);
        f.Data.AssertFiles(files); Assert.Empty(f.ActorFrames); return Task.CompletedTask;
    });

    [Fact]
    public Task ActualAttachedAndRetiredPlayerCannotUseSetterOrNativeRefinementGrant() => Run(f =>
    {
        var current = f.Current(); var client = current.NativeGameClient!; var player = client.Player;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding);
        var files = f.Data.Files(); var vector = player.BitVector;
        var words = vector.bits; var registrations = vector.ArrayFlags;
        var wordBytes = words.ToArray(); var rows = registrations.ToArray();
        Assert.Throws<InvalidOperationException>(() => player.Stamina = 100);
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        lock (vector.SyncRoot)
        {
            var actual = new LegacyNativeRefinementBindings(client, player, vector, player.View,
                LegacyNativePerfectionEffects.RefinementKind.Stamina);
            Assert.True(actual.IsCurrent());
            Assert.False(vector.TryPrepareRefinement(actual, out var prepared)); Assert.Null(prepared);
        }
        Assert.False(LegacyNativePerfectionEffects.TryLightOfStamina(client));
        Assert.Same(current, f.Current()); Assert.Equal((ushort)37, player.Stamina);
        Assert.Same(words, vector.bits); Assert.Equal(wordBytes, vector.bits);
        Assert.Same(registrations, vector.ArrayFlags); Assert.Equal(rows, vector.ArrayFlags.ToArray());
        Assert.True(f.Store.TryRemove(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out var retired));
        Assert.Same(current, retired); Assert.True(binding.IsRetired);
        Assert.Same(binding, player.AttachedVitalsBinding);
        Assert.Throws<InvalidOperationException>(() => player.Stamina = 100);
        Assert.Equal((ushort)37, player.Stamina); f.Data.AssertFiles(files); Assert.Empty(f.ActorFrames);
        return Task.CompletedTask;
    });

    private static WorldSession Session(IReadOnlyDictionary<string, string> attributes) => new(1, "DataOnly",
        OwnedEquipmentBehaviorFixture.Character() with { Attributes = attributes }, PlayerItemSnapshot.Empty,
        PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty);

    private static Task Run(Func<OwnedEquipmentBehaviorFixture, Task> body)
    {
        var character = OwnedEquipmentBehaviorFixture.Character(minHitPoints: 1) with
        { Attributes = new Dictionary<string, string> { ["Stamina"] = "37" } };
        return OwnedEquipmentBehaviorFixture.RunAsync(new(character, PlayerItemSnapshot.Empty),
            OwnedEquipmentBehaviorFixture.Catalog(), f => { f.ActorFrames.Clear(); return body(f); });
    }

    private sealed class SplitViewAttributes : Dictionary<string, string>, IDictionary<string, string>
    {
        string IDictionary<string, string>.this[string key]
        { get => key == "Stamina" ? "99" : base[key]; set => base[key] = value; }
        bool IDictionary<string, string>.TryGetValue(string key, out string value)
        {
            if (key == "Stamina") { value = "99"; return true; }
            return base.TryGetValue(key, out value!);
        }
    }
}
