namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Fact]
    public Task HandlerRelicsDecomposeDeclaredPrefixGrantsNineRowsAndPublishesRank() =>
        LegacyOwnedRelicDecomposeTests.RunMigrationCaseAsync(LegacyRelicDecomposeMigrationCase.Ordinary);

    [Fact]
    public Task HandlerRelicsDecomposeIgnoresMissingSurplusOutsideDeclaredPrefix() =>
        LegacyOwnedRelicDecomposeTests.RunMigrationCaseAsync(LegacyRelicDecomposeMigrationCase.MissingSurplus);

    [Fact]
    public Task HandlerRelicsDecomposeMissingDeclaredUidRejectsBeforeAllMutation() =>
        LegacyOwnedRelicDecomposeTests.RunMigrationCaseAsync(LegacyRelicDecomposeMigrationCase.MissingDeclared);

    [Fact]
    public Task HandlerRelicsDecomposeDuplicateOneUnitRefusesBeforeMutation() =>
        LegacyOwnedRelicDecomposeTests.RunMigrationCaseAsync(LegacyRelicDecomposeMigrationCase.DuplicateOne);

    [Fact]
    public Task HandlerRelicsDecomposeDuplicateTwoUnitRefusesBeforeMutation() =>
        LegacyOwnedRelicDecomposeTests.RunMigrationCaseAsync(LegacyRelicDecomposeMigrationCase.DuplicateTwo);

    [Fact]
    public Task HandlerRelicsDecomposeMissingCrystalMetadataPreservesItemsAndRank() =>
        LegacyOwnedRelicDecomposeTests.RunMigrationCaseAsync(LegacyRelicDecomposeMigrationCase.NoMetadata);

    [Fact]
    public Task HandlerRelicsDecomposeFullInventoryDoesNotGrantNineRewards() =>
        LegacyOwnedRelicDecomposeTests.RunMigrationCaseAsync(LegacyRelicDecomposeMigrationCase.Full);

    [Fact]
    public Task HandlerRelicsDecomposeZeroCountRefusesWithoutMutation() =>
        LegacyOwnedRelicDecomposeTests.RunMigrationCaseAsync(LegacyRelicDecomposeMigrationCase.Zero);

    [Fact]
    public Task HandlerRelicsDecomposeFiftyCountRefusesWithoutMutation() =>
        LegacyOwnedRelicDecomposeTests.RunMigrationCaseAsync(LegacyRelicDecomposeMigrationCase.Fifty);

    [Fact]
    public Task HandlerRelicsDecomposeMaximumCountRefusesWithoutMutation() =>
        LegacyOwnedRelicDecomposeTests.RunMigrationCaseAsync(LegacyRelicDecomposeMigrationCase.Maximum);

    [Fact]
    public Task HandlerRelicsDecomposeEmptyArgsPreservesItemsAndRank() =>
        LegacyOwnedRelicDecomposeTests.RunMigrationCaseAsync(LegacyRelicDecomposeMigrationCase.EmptyWithCatalog);

}
