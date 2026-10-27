namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Theory]
    [InlineData(1052L, 0L, 0L, 31461L)]
    [InlineData(1057L, 0L, 0L, 31461L)]
    [InlineData(2000L, 1052L, 0L, 29836L)]
    [InlineData(2000L, 1057L, 0L, 29836L)]
    [InlineData(2000L, 0L, 1052L, 29836L)]
    [InlineData(2000L, 0L, 1057L, 29836L)]
    public Task TargetTrojanLineageOrdinaryLoginCommitsOnlyTheMissingCycloneAndPreservesSavedFlags(
        long current, long first, long second, long runtimeHitpoints) =>
        RequireTargetOwnedSpellStartup(current, first, second, runtimeHitpoints, existingCyclone: false, expectedGrant: true);

    [Fact]
    public Task TargetTrojanLineageOrdinaryLoginKeepsExistingCycloneProgressAndOpaqueRecordBytes() =>
        RequireTargetOwnedSpellStartup(1057, 1052, 1057, 31461, existingCyclone: true, expectedGrant: false);

    [Theory]
    [InlineData(1058L, 0L, 0L, 31461L)]
    [InlineData(2000L, 1058L, 0L, 29836L)]
    [InlineData(2000L, 0L, 1058L, 29836L)]
    public Task UnconfirmedLineageOrdinaryLoginDoesNotGrantCycloneOrRewriteLearnedFlags(
        long current, long first, long second, long runtimeHitpoints) =>
        RequireTargetOwnedSpellStartup(current, first, second, runtimeHitpoints, existingCyclone: false, expectedGrant: false);
}
