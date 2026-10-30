using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyOwnedRuneGameplayFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneGameplayTests
{
    [Theory]
    [InlineData("stored")] [InlineData("inactive-set")] [InlineData("last-provider-removed")]
    [InlineData("malformed-level")] [InlineData("unmet-level")]
    public Task AClassifiedRuneCastNeedsAGenuineCurrentActiveProviderBeforeResourcesOrCooldowns(string fault) => Run(async f =>
    {
        if (fault == "inactive-set") await f.DispatchAsync(F.Equip(F.First, 122));
        else if (fault != "stored") await f.DispatchAsync(F.Equip());
        if (fault == "last-provider-removed") await f.DispatchAsync(F.Unequip());
        if (fault is "malformed-level" or "unmet-level")
        {
            var fields = Assert.IsType<string[]>(f.Catalog.Items.Single(row => row.Id == F.FirstType).Fields);
            fields[ItemType.RequiredLevelFieldIndex] = fault == "malformed-level" ? "broken" : "141";
            // The legacy native graph pins class/spell, but not this field.
            // Refusal must therefore come from the new actual caster proof.
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Native.OrdinarySpellBinding!));
        }
        Assert.Contains(f.Current.PlayerSpells.Spells, row => row.Id == F.FirstSpell);
        await f.AssertRefusedAsync(f.CastAsync);
    }, legacy: true);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task RealNormalAndAlternateProvidersPermitTheActualIronShieldFrameAndItsCosts(bool alternate) => Run(async f =>
    {
        await f.DispatchAsync(F.Equip(F.First, alternate ? (ushort)122 : (ushort)102));
        if (alternate) await f.DispatchAsync(new MsgItemUsagePacket(0, 0, MsgItemUsageAction.Alternante, 0, 0, 0, 0));
        Assert.Equal(alternate, f.Current.AlternateEquipmentActive);
        Assert.Equal(alternate, f.Native.Equipment!.Alternante);
        var before = f.Current; int first = f.Sent.Count;
        await f.CastAsync();
        Assert.Equal(before.Character.MinMana - 10, f.Current.Character.MinMana);
        Assert.Equal("95", f.Current.Character.Attributes["Stamina"]); Assert.True(f.Sent.Count > first);
        Assert.Contains("handled legacy IronShield AttachStatus magic", f.Log.ToString(), StringComparison.Ordinal);
        f.AssertConverged(); f.AssertMarker("14160:16");
    });

    [Theory]
    [InlineData("0")] [InlineData("missing")]
    public Task ProvenOrdinaryAndUnclassifiedExistingSpellsRetainTheirOriginalCastBehavior(string classification) => Run(async f =>
    {
        var before = f.Current; await f.CastAsync();
        Assert.Equal(before.Character.MinMana - 10, f.Current.Character.MinMana);
        Assert.Equal("95", f.Current.Character.Attributes["Stamina"]);
        Assert.Contains("handled legacy IronShield AttachStatus magic", f.Log.ToString(), StringComparison.Ordinal);
        f.AssertConverged(); f.AssertMarker(null);
    }, legacy: true, classification: classification);

    [Fact]
    public Task MalformedExplicitRuneClassificationCannotDowngradeTheActualCasterToOrdinary() =>
        Run(f => f.AssertRefusedAsync(f.CastAsync), legacy: true, classification: "broken");

    [Fact]
    public Task ARequiredLevelChangedAfterCaptureCannotPassTheRealStoreCostPublicationBoundary() => Run(async f =>
    {
        await f.DispatchAsync(F.Equip()); var before = f.Current;
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(before.PlayerSpells, f.Catalog, F.FirstSpell, out _, out var spell));
        Assert.Equal(true, spell.IsRuneSkill); Assert.True(LegacyRuneCastEligibility.TryCapture(f.Context, before, spell, out var eligibility));
        Assert.NotNull(eligibility);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(f.Store, f.Connection, F.Uid, out var identity));
        var fields = Assert.IsType<string[]>(f.Catalog.Items.Single(row => row.Id == F.FirstType).Fields);
        fields[ItemType.RequiredLevelFieldIndex] = "141";
        Assert.False(eligibility!.IsCurrentPlain()); var files = f.Data.Files(); int first = f.Sent.Count;
        var candidate = before with { Character = before.Character with { MinMana = before.Character.MinMana - 10 } };
        Assert.False(f.Store.TryCommitMagicCosts(f.Connection, before, identity, candidate, out var receipt, eligibility));
        Assert.Null(receipt); Assert.Same(before, f.Current); Assert.Equal(first, f.Sent.Count); f.Data.AssertFiles(files);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    });

    [Theory]
    [InlineData("[100000]\r\nIsRuneSkill=0\r\n", true, false)]
    [InlineData("[100000]\r\nIsRuneSkill=1\r\n", true, true)]
    [InlineData("[100000]\r\nIsRuneSkill=255\r\n", true, true)]
    [InlineData("[100000]\r\nName=ordinary\r\n", false, false)]
    [InlineData("[100000]\r\nIsRuneSkill=broken\r\n", false, false)]
    [InlineData("[100000]\r\nIsRuneSkill=1\r\nisruneskill=1\r\n", false, false)]
    [InlineData("[100000]\r\nIsRuneSkill=1\r\n[100000]\r\nIsRuneSkill=1\r\n", false, false)]
    public void ClassificationReadsTheActualCanonicalId00SectionAndRejectsAmbiguousLoadedEvidence(string text, bool valid, bool rune)
    {
        var data = NativeCallerWorldDataFixture.Create([], [], magicEffects: MagicEffectParser.Parse(text, "owned-MagicEffect.ini"));
        Assert.Equal(valid, LegacyRuneSpellClassification.TryCapture(data, 1000, out var proof));
        if (valid) { Assert.NotNull(proof); Assert.Equal(rune, proof!.IsRuneSkill); Assert.True(proof.IsCurrent()); }
        else Assert.Null(proof);
    }
}
