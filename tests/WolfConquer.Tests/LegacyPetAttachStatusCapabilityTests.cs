using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPetAttachStatusCapabilityTests
{
    [Fact]
    public void ClassifyThundercloudPetAttachStatusAsDedicatedRuntimeSlice()
    {
        Assert.Equal(89, (int)LegacyMagicSort.PetAttachStatus);

        var info = LegacyMagicSortCapabilities.Classify(
            LegacyMagicSort.PetAttachStatus,
            12_970);

        Assert.Equal(LegacyMagicSortCapability.StateAndBuffRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("thundercloud-pet-attach-status-runtime", info.NextSeam);
        Assert.Contains("RevengeTail removal", info.Reason);
        Assert.Contains("PetAttachStatus.cs", info.SourceEvidence);
        Assert.Contains("no Source7770", info.SourceEvidence);
    }

    [Theory]
    [InlineData(12_969)]
    [InlineData(12_971)]
    public void KeepAdjacentPetAttachStatusSpellIdsUnsupported(ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(
            LegacyMagicSort.PetAttachStatus,
            spellId);

        Assert.False(info.Supported);
    }
}
