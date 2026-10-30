using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStartupItemSpellRecoveryTests
{
    [Theory]
    [InlineData(0, 1, false)] [InlineData(1, 1, false)] [InlineData(2, 1, false)] [InlineData(3, 1, false)]
    [InlineData(0, 2, false)] [InlineData(1, 2, false)] [InlineData(2, 2, false)] [InlineData(3, 2, false)]
    [InlineData(0, 1, true)] [InlineData(1, 1, true)] [InlineData(2, 1, true)] [InlineData(3, 1, true)]
    [InlineData(0, 2, true)] [InlineData(1, 2, true)] [InlineData(2, 2, true)] [InlineData(3, 2, true)]
    public Task GenuineBookConsumptionAndDefaultLearningRecoverEachPrefixWithExistingOrActuallyAbsentSpells(
        int boundary, ushort stack, bool missingSpells) => WithFixture(async f =>
        {
            await f.StageAsync(boundary); await f.CloseAsync(); f.AssertLeaseAvailable();
            var runtime = f.Open(); f.AssertRecoveredImages();
            Assert.Throws<IOException>(() => new FileStream(f.Data.OwnerLease, FileMode.Open,
                FileAccess.ReadWrite, FileShare.None).Dispose());
            await f.AssertFreshFrameAsync(runtime); f.AssertRecoveredImages();
            await f.AssertReplayedBookRequestDoesNotConsumeAsync();
            await f.CloseAsync(); f.AssertLeaseAvailable(); var recovered = f.Data.Files();
            f.Open(); f.AssertRecoveredImages(); f.Data.AssertFiles(recovered);
        }, stack, missingSpells: missingSpells);

    [Theory]
    [InlineData(0, 1)] [InlineData(1, 1)] [InlineData(2, 1)] [InlineData(3, 1)]
    [InlineData(0, 2)] [InlineData(1, 2)] [InlineData(2, 2)] [InlineData(3, 2)]
    public Task ADirectoryAbsentBeforeRealBootstrapAndAdmissionIsCreatedOnlyByTheGenuineV9WriteLifecycle(int boundary, ushort stack) =>
        WithFixture(async f =>
        {
            Assert.False(Directory.Exists(f.Data.Spells)); Assert.Null(f.Before[f.SpellsPath]);
            await f.StageAsync(boundary);
            Assert.Equal(boundary != 0, Directory.Exists(f.Data.Spells));
            if (boundary == 0) Assert.False(File.Exists(f.SpellsPath));
            await f.CloseAsync(); var runtime = f.Open(); f.AssertRecoveredImages();
            Assert.True(Directory.Exists(f.Data.Spells)); await f.AssertFreshFrameAsync(runtime);
            await f.AssertReplayedBookRequestDoesNotConsumeAsync();
            await f.CloseAsync(); var recovered = f.Data.Files();
            f.Open(); f.AssertRecoveredImages(); f.Data.AssertFiles(recovered);
        }, stack, missingSpellsDirectory: true);

    [Theory]
    [InlineData(1, 1, false)] [InlineData(2, 1, false)] [InlineData(3, 1, false)]
    [InlineData(1, 2, false)] [InlineData(2, 2, false)] [InlineData(3, 2, false)]
    [InlineData(3, 1, true)] [InlineData(3, 2, true)]
    public Task RealBookStagesAreFlushedByTheActualPreparedWriterBeforeOwnedRecovery(int ordinal, ushort stack, bool missingSpells) =>
        WithFixture(async f =>
        {
            await f.StopAfterStageFlushedAsync(ordinal);
            var stages = f.OrderedTargets.Select(f.StagePath).ToArray();
            for (var index = 0; index < f.OrderedTargets.Count; index++)
            {
                var path = f.OrderedTargets[index];
                if (f.Before[path] is { } before) Assert.Equal(before, File.ReadAllBytes(path));
                else Assert.False(File.Exists(path));
                if (index < ordinal) Assert.Equal(f.After[path], File.ReadAllBytes(stages[index]));
                else Assert.False(File.Exists(stages[index]));
            }
            await f.CloseAsync(); f.Open(); f.AssertRecoveredImages();
            Assert.All(stages, path => Assert.False(File.Exists(path)));
            await f.CloseAsync(); var recovered = f.Data.Files();
            f.Open(); f.AssertRecoveredImages(); f.Data.AssertFiles(recovered);
        }, stack, missingSpells: missingSpells);

    [Theory]
    [InlineData("rune-identity", 0, false)] [InlineData("rune-identity", 1, false)]
    [InlineData("rune-identity", 2, false)] [InlineData("rune-identity", 3, false)]
    [InlineData("rune-equip", 0, false)] [InlineData("rune-equip", 1, false)]
    [InlineData("rune-equip", 2, false)] [InlineData("rune-equip", 3, false)]
    [InlineData("rune-identity", 2, true)] [InlineData("rune-equip", 2, true)]
    public Task ActualRuneOperationsRecoverWithTheirRecordedDependenciesAndExactOwnedGrantOrIdentityOutcomes(
        string mode, int boundary, bool missingSpells) => WithFixture(async f =>
        {
            await f.StageAsync(boundary); await f.CloseAsync();
            var runtime = f.Open(); f.AssertRecoveredImages(); await f.AssertFreshFrameAsync(runtime);
            await f.CloseAsync(); var recovered = f.Data.Files();
            f.Open(); f.AssertRecoveredImages(); f.Data.AssertFiles(recovered);
        }, mode: mode, missingSpells: missingSpells);

    [Theory]
    [InlineData("rune-identity", 1, false)] [InlineData("rune-identity", 2, false)] [InlineData("rune-identity", 3, false)]
    [InlineData("rune-equip", 1, false)] [InlineData("rune-equip", 2, false)] [InlineData("rune-equip", 3, false)]
    [InlineData("rune-identity", 3, true)] [InlineData("rune-equip", 3, true)]
    public Task ActualRecordedRuneStageFlushesAndExplicitMissingBeforeSpellsUseTheirCapturedRootCustody(
        string mode, int ordinal, bool missingSpells) => WithFixture(async f =>
        {
            await f.StopAfterStageFlushedAsync(ordinal);
            var stages = f.OrderedTargets.Select(f.StagePath).ToArray();
            await f.CloseAsync(); f.Open(); f.AssertRecoveredImages();
            Assert.All(stages, path => Assert.False(File.Exists(path)));
        }, mode: mode, missingSpells: missingSpells);

    [Fact]
    public Task AnActualAlreadyStoredRuneUnequipCanProduceAndRecoverAnExactNoOpIntent() => WithFixture(async f =>
    {
        await f.StageAsync(0);
        foreach (var (path, before) in f.Before) Assert.Equal(before, f.After[path]);
        await f.CloseAsync(); f.Open(); f.AssertRecoveredImages();
    }, mode: "rune-noop");

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task GenuineRuneEquipCanDisplaceFirstSameIdAndThenRequestedSlotInProducerOrder(bool flushed) => WithFixture(async f =>
    {
        if (flushed) await f.StopAfterStageFlushedAsync(3); else await f.StageAsync(2);
        var rows = PlayerItemSnapshotLoader.Parse(f.After[f.ItemsPath]).RuneItems;
        Assert.Equal((ushort)102, Assert.Single(rows, row => row.Uid == LegacyOwnedStartupItemSpellRecoveryFixture.ItemUid).Position);
        Assert.All(rows.Where(row => row.Uid != LegacyOwnedStartupItemSpellRecoveryFixture.ItemUid), row => Assert.Equal((ushort)212, row.Position));
        await f.CloseAsync(); var runtime = f.Open(); f.AssertRecoveredImages(); await f.AssertFreshFrameAsync(runtime);
        await f.CloseAsync(); var recovered = f.Data.Files(); f.Open(); f.AssertRecoveredImages(); f.Data.AssertFiles(recovered);
    }, mode: "rune-displace");

    [Theory]
    [InlineData("rune-identity", "raw-zero")] [InlineData("rune-equip", "raw-zero")]
    [InlineData("rune-identity", "bound")] [InlineData("rune-equip", "bound")]
    [InlineData("rune-identity", "inscribed")] [InlineData("rune-equip", "inscribed")]
    public Task GenuineRuneRulesPreserveRawZeroCountAndDoNotImportTradeOnlyRestrictions(string mode, string flags) =>
        WithFixture(async f =>
        { await f.StageAsync(2); await f.CloseAsync(); f.Open(); f.AssertRecoveredImages(); }, mode: mode, runeFlags: flags);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public Task ARealThreeTargetFlushedStageMayRetainOnlyItsActualAfterImagePrefix(int ordinal) => WithFixture(async f =>
    {
        await f.StopAfterStageFlushedAsync(3);
        var path = f.StagePath(f.OrderedTargets[ordinal]); var bytes = File.ReadAllBytes(path);
        File.WriteAllBytes(path, bytes.AsSpan(0, ordinal == 0 ? 0 : bytes.Length / 2).ToArray());
        await f.CloseAsync(); f.Open(); f.AssertRecoveredImages(); Assert.False(File.Exists(path));
    });

    [Theory]
    [InlineData("bound", 1)] [InlineData("bound", 2)] [InlineData("inscribed", 1)] [InlineData("inscribed", 2)]
    public Task GenuineBookRulesDoNotImportTradeOrVendorBoundAndInscribedRestrictions(string flags, ushort stack) =>
        WithFixture(async f =>
        { await f.StageAsync(2); await f.CloseAsync(); f.Open(); f.AssertRecoveredImages(); }, stack, bookFlags: flags);

    [Fact]
    public Task GenericRegistryOpenAndBootstrapStillRefuseTheSupportedV9Intent() => WithFixture(async f =>
    {
        await f.StageAsync(2); await f.CloseAsync(); var files = f.Data.Files();
        var opened = LegacyItemUidRegistry.TryOpen(f.Data.RegistryOptions(), f.Data.ExpectedDomain, out var registry, out var reason);
        if (registry is not null) f.Own(registry);
        Assert.False(opened, reason); Assert.Null(registry); Assert.NotEmpty(reason);
        var bootstrapped = LegacyItemUidRegistry.TryBootstrap(f.Data.RegistryOptions(), out var bootstrap, out reason);
        if (bootstrap is not null) f.Own(bootstrap);
        Assert.False(bootstrapped, reason); Assert.Null(bootstrap); Assert.NotEmpty(reason);
        f.Data.AssertFiles(files); f.AssertLeaseAvailable();
    });
}
