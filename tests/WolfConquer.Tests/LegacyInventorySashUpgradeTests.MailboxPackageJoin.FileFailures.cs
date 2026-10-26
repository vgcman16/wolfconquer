using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [WindowsTradeJournalFailureFact]
    public Task ActualD13PackageCharacterLeaseRefusesBeforeIntentThenSameOwnerRetries() => PackageJoinLeafFailureAsync("character");

    [WindowsTradeJournalFailureFact]
    public Task ActualD13PackageItemsLeaseRefusesBeforeIntentThenSameOwnerRetries() => PackageJoinLeafFailureAsync("items");

    [WindowsTradeJournalFailureFact]
    public Task ActualD13PackageMailboxLeaseRefusesBeforeIntentThenSameOwnerRetries() => PackageJoinLeafFailureAsync("mailbox");

    [WindowsTradeJournalFailureFact]
    public Task ActualD13PackageGearLeaseRefusesBeforeIntentThenSameOwnerRetries() => PackageJoinLeafFailureAsync("gear");

    private static Task PackageJoinLeafFailureAsync(string target) => RunPackageJoinAsync(async (fixture, join) =>
    {
        var before = CapturePackageJoin(fixture, join);
        var path = target switch
        {
            "character" => fixture.CharacterPath,
            "items" => fixture.ItemPath,
            "mailbox" => fixture.AtomicMailPath,
            "gear" => fixture.PackageGearPath,
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
        fixture.HoldPackageJoinFile(path);
        await join.FrameAsync(PackageJoinRequest(claimAll: true));
        fixture.ReleaseFiles(); // Release only after the actual registered handler is terminal.
        AssertPackageJoinUnchanged(fixture, join, before);
        AssertNoPackageJoinIntents(join);
        await join.FrameAsync(PackageJoinRequest(claimAll: true));
        AssertPackageJoinCommitted(fixture, join, before, claimAll: true, job: 10);
    });

    [Fact]
    public Task ActualD13PackageMissingRequiredGearImageRefusesWithoutIntentThenRestoredOwnerRetries() =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var saved = File.ReadAllBytes(fixture.PackageGearPath);
            File.Delete(fixture.PackageGearPath); // Only this complete owned synthetic file.
            var missing = CapturePackageJoin(fixture, join);
            await join.FrameAsync(PackageJoinRequest(claimAll: true));
            AssertPackageJoinUnchanged(fixture, join, missing);
            AssertNoPackageJoinIntents(join);
            File.WriteAllBytes(fixture.PackageGearPath, saved);
            AssertPackageJoinUnchanged(fixture, join, before);
            await join.FrameAsync(PackageJoinRequest(claimAll: true));
            AssertPackageJoinCommitted(fixture, join, before, claimAll: true, job: 10);
        });

    private sealed partial class Fixture
    {
        internal void HoldPackageJoinFile(string path) =>
            heldFiles.Add(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None));
    }
}
