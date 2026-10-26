using Xunit.Abstractions;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeCombatGearStagedLoadTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("StageFull")]
    [InlineData("StageRankOrdering")]
    [InlineData("StageMissingGear")]
    [InlineData("StageMissingJade")]
    [InlineData("StageMissingLottery")]
    [InlineData("StageMissingCollection")]
    [InlineData("StageMissingRanks")]
    [InlineData("StageExtraGear")]
    [InlineData("StageExtraJade")]
    [InlineData("StageExtraLottery")]
    [InlineData("StageExtraCollection")]
    [InlineData("StageDuplicateJade")]
    [InlineData("StageDuplicateLottery")]
    [InlineData("StageDuplicateCollection")]
    [InlineData("StageDuplicateGearBadTail")]
    [InlineData("StageRankMissingHeader")]
    [InlineData("StageRankCountMismatch")]
    [InlineData("StageRankTruncated")]
    [InlineData("StageRankExtra")]
    [InlineData("StageRankUnknownType")]
    [InlineData("StageRankByteOverflow")]
    [InlineData("StageStaleGear")]
    [InlineData("StageStaleJade")]
    [InlineData("StageStaleLottery")]
    [InlineData("StageStaleCollection")]
    [InlineData("StageStaleRanks")]
    [InlineData("FrozenLate")]
    [InlineData("UnrelatedNinja")]
    public void CompleteRealStageAndExactRegistryPreserveSafePublication(string scenario)
    {
        var result = LegacyNativeCombatGearLoadChildFixture.Run(scenario, output);
        Assert.True(result.GetProperty("Passed").GetBoolean());
        Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
        bool ready = scenario is "StageFull" or "UnrelatedNinja";
        Assert.Equal(ready, result.GetProperty("Loaded").GetBoolean());
        Assert.Equal(ready, result.GetProperty("Ready").GetBoolean());
    }

    [Theory]
    [InlineData("ProofRawGateOnly")]
    [InlineData("ProofSetterGear")]
    [InlineData("ProofSetterJade")]
    [InlineData("ProofSetterLottery")]
    [InlineData("ProofSetterCollection")]
    [InlineData("ProofSetterRanks")]
    [InlineData("ProofSetterLocation")]
    [InlineData("ProofSetterGate")]
    [InlineData("ProofGearRow")]
    [InlineData("ProofGearSkill")]
    [InlineData("ProofGearSkillsOwner")]
    [InlineData("ProofGearMapOwner")]
    [InlineData("ProofJadeRow")]
    [InlineData("ProofLotteryRow")]
    [InlineData("ProofCollectionRow")]
    [InlineData("ProofRankShape")]
    [InlineData("ProofRankRuntime")]
    [InlineData("ProofFilesChanged")]
    [InlineData("ProofFailedReloadGear")]
    [InlineData("ProofFailedReloadJade")]
    [InlineData("ProofFailedReloadLottery")]
    [InlineData("ProofFailedReloadCollection")]
    [InlineData("ProofFailedReloadRanks")]
    public void OnlyTheCurrentActualCompletedPublicationCanAuthorizeMailboxLoading(string scenario)
    {
        var result = LegacyNativeCombatGearLoadChildFixture.Run(scenario, output);
        Assert.True(result.GetProperty("Passed").GetBoolean());
        Assert.True(result.GetProperty("Loaded").GetBoolean());
        bool ready = scenario is "ProofRankRuntime" or "ProofFilesChanged" || scenario.StartsWith("ProofFailedReload", StringComparison.Ordinal);
        Assert.Equal(ready, result.GetProperty("Ready").GetBoolean());
        Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
    }
}
