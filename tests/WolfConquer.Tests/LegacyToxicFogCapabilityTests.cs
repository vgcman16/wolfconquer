using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyToxicFogCapabilityTests
{
    [Fact]
    public void ClassifyToxicFogAsBoundedModeledPoisonRuntimeSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Toxic, spellId: 6_001);

        Assert.Equal(LegacyMagicSortCapability.PoisonStatusRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("Toxic(27)", info.SortName);
        Assert.Equal("toxic-poison-runtime", info.NextSeam);
        Assert.Contains("bounded ToxicFog 6001 runtime slice", info.Reason);
        Assert.Contains("valid visible-player poison selection within distance < 5", info.Reason);
        Assert.Contains("PoisonLevel = ClientSpell.Level + ClientSpell.UseSpellSoul", info.Reason);
        Assert.Contains("type 1 for monsters and type 2 for players", info.Reason);
        Assert.Contains("UpdateSpell.CheckUpdate damage=1000", info.Reason);
        Assert.Contains("empty-target MsgSpellAnimation", info.Reason);
        Assert.Contains("exact poison tick damage/expiry", info.Reason);
        Assert.Contains("exact Sturdiness probabilistic edge", info.Reason);
        Assert.Contains("other poison rows are not claimed", info.Reason);
        Assert.Contains("Toxic.cs handles ClientSpell.ID 6001 after CanUseSpell.Verified", info.SourceEvidence);
        Assert.Contains("poisons non-boss visible monsters and visible players within distance < 5", info.SourceEvidence);
        Assert.Contains("type 1 for monsters and type 2 for players", info.SourceEvidence);
        Assert.Contains("MsgSpellAnimation without damage animation targets", info.SourceEvidence);
        Assert.Contains("Magic poison runtime owner", info.SuggestedNextOwner);
        Assert.Contains("exact poison tick damage/expiry", info.SuggestedNextOwner);
        Assert.Contains("non-6001 Toxic/poison rows fenced", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=Toxic(27) spellId=6001 capability=poisonStatusRuntime status=partiallyModeled supported=true nextSeam=toxic-poison-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyGenericAndOtherToxicSortRowsAsUnsupported()
    {
        var genericInfo = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Toxic);
        var otherSpellInfo = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Toxic, spellId: 6_002);

        AssertUnsupportedToxicQuery(genericInfo, expectedSpellId: 0);
        AssertUnsupportedToxicQuery(otherSpellInfo, expectedSpellId: 6_002);
    }

    private static void AssertUnsupportedToxicQuery(
        LegacyMagicSortCapabilityInfo info,
        ushort expectedSpellId)
    {
        Assert.Equal(LegacyMagicSort.Toxic, info.Sort);
        Assert.Equal(expectedSpellId, info.SpellId);
        Assert.Equal(LegacyMagicSortCapability.PoisonStatusRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.False(info.Supported);
        Assert.Equal("Toxic(27)", info.SortName);
        Assert.Equal("toxic-poison-runtime", info.NextSeam);
        Assert.Contains("Only ToxicFog spell 6001", info.Reason);
        Assert.Contains("generic or non-6001 Toxic sort query stays unsupported", info.Reason);
        Assert.Contains("other poison/Toxic rows are not claimed", info.Reason);
        Assert.Contains("review any non-6001 Toxic sort rows separately", info.SuggestedNextOwner);
        Assert.Contains(
            $"magicSort=Toxic(27) spellId={expectedSpellId} capability=poisonStatusRuntime status=missingRuntimeNotModeled supported=false nextSeam=toxic-poison-runtime",
            info.Diagnostic);
    }
}
