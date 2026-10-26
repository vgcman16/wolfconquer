using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMailboxOwnerJournalTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void V5EveryPrefixRecoversCompleteCharacterItemsMailboxGearExactlyOnce(int replacements) => Run(fixture =>
    {
        var changes = SeedGearJournal(fixture);
        var trace = new List<int>();
        Assert.False(LegacyTradeJournal.TryCommitMailboxGearOwner(fixture.Users, fixture.Items, fixture.Mailbox,
            GearRoot(fixture), changes.Reverse().ToArray(), count => { trace.Add(count); return count == replacements; }, out var reason));
        Assert.Equal("tradeJournalRecoveryRequired:interrupted", reason);
        Assert.Equal(Enumerable.Range(0, replacements + 1), trace);
        for (var index = 0; index < changes.Length; index++)
            Assert.Equal(replacements > index ? changes[index].After : changes[index].Before,
                File.ReadAllBytes(GearTarget(fixture, changes[index])));
        var record = JsonNode.Parse(File.ReadAllText(fixture.Pending))!;
        Assert.Equal(5, record["Version"]!.GetValue<int>());
        Assert.Equal(new[] { 0, 1, 3, 4 }, record["Changes"]!.AsArray().Select(row => row!["Kind"]!.GetValue<int>()));
        Assert.Equal(GearRoot(fixture), record["MailboxGearDomain"]!["CombatGear"]!.GetValue<string>());

        var restarted = GearRecovery(fixture, GearRoot(fixture));
        Assert.False(restarted.IsReadyForMailboxGear(fixture.Users, fixture.Items, fixture.Mailbox, GearRoot(fixture)));
        Assert.True(restarted.TryEnsureInitialized(out reason), reason);
        Assert.True(restarted.IsReadyForMailboxGear(fixture.Users, fixture.Items, fixture.Mailbox, GearRoot(fixture)));
        AssertGearImages(fixture, changes, after: true);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
        var files = fixture.CaptureFiles();
        Assert.True(LegacyTradeJournal.TryRecover(fixture.Users, fixture.Items, null, fixture.Mailbox,
            GearRoot(fixture), out var recovered, out reason), reason);
        Assert.Equal(0, recovered);
        fixture.AssertFiles(files);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void V5MissingOrForeignExplicitGearRootCannotReplay(bool foreign) => Run(fixture =>
    {
        var changes = SeedGearJournal(fixture);
        Assert.False(LegacyTradeJournal.TryCommitMailboxGearOwner(fixture.Users, fixture.Items, fixture.Mailbox,
            GearRoot(fixture), changes, count => count == 0, out _));
        string? configured = null;
        if (foreign)
        {
            configured = Path.Combine(fixture.Root, "OtherGear");
            new CombatGearSnapshotLoader(configured).SaveByCharacterUid(Fixture.CharacterUid, GearSnapshot());
        }
        var files = fixture.CaptureFiles();
        var wrong = GearRecovery(fixture, configured);
        Assert.False(wrong.TryEnsureInitialized(out var reason));
        Assert.StartsWith("tradeJournalRecoveryRequired:", reason);
        Assert.True(wrong.IsBlocked);
        Assert.False(wrong.IsReadyForMailboxGear(fixture.Users, fixture.Items, fixture.Mailbox, configured));
        fixture.AssertFiles(files);
        Assert.True(GearRecovery(fixture, GearRoot(fixture)).TryEnsureInitialized(out reason), reason);
        AssertGearImages(fixture, changes, after: true);
        if (foreign)
            Assert.Equal(changes[3].Before, File.ReadAllBytes(Path.Combine(configured!, Fixture.CharacterUid + ".ini")));
    });

    [Fact]
    public void V5UnknownCompleteGearImageBlocksBeforeAnyFurtherTargetWrite() => Run(fixture =>
    {
        var changes = SeedGearJournal(fixture);
        Assert.False(LegacyTradeJournal.TryCommitMailboxGearOwner(fixture.Users, fixture.Items, fixture.Mailbox,
            GearRoot(fixture), changes, count => count == 2, out _));
        var unknown = GearBytes(GearSnapshot() with { PirateExp = 999 });
        File.WriteAllBytes(GearTarget(fixture, changes[3]), unknown); // Owned full-image fault, not a replacement writer.
        var files = fixture.CaptureFiles();
        var blocked = GearRecovery(fixture, GearRoot(fixture));
        Assert.False(blocked.TryEnsureInitialized(out var reason));
        Assert.True(blocked.IsBlocked);
        Assert.StartsWith("tradeJournalRecoveryRequired:rollForwardFailed:", reason);
        fixture.AssertFiles(files);
        File.WriteAllBytes(GearTarget(fixture, changes[3]), changes[3].Before!);
        var repaired = fixture.CaptureFiles();
        Assert.False(blocked.TryEnsureInitialized(out _));
        fixture.AssertFiles(repaired);
        Assert.True(GearRecovery(fixture, GearRoot(fixture)).TryEnsureInitialized(out reason), reason);
        AssertGearImages(fixture, changes, after: true);
    });

    [Fact]
    public void V5CommitRejectsStaleReplayWithoutRepeatingAnyParticipant() => Run(fixture =>
    {
        var changes = SeedGearJournal(fixture);
        Assert.True(LegacyTradeJournal.TryCommitMailboxGearOwner(fixture.Users, fixture.Items, fixture.Mailbox,
            GearRoot(fixture), changes, out var reason), reason);
        AssertGearImages(fixture, changes, after: true);
        var files = fixture.CaptureFiles();
        Assert.False(LegacyTradeJournal.TryCommitMailboxGearOwner(fixture.Users, fixture.Items, fixture.Mailbox,
            GearRoot(fixture), changes, out _));
        fixture.AssertFiles(files);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    });

    [WindowsTradeJournalFailureFact]
    public void V5FourthTargetSharingFailureRestoresEveryPreimageThenSameComponentRetries() => Run(fixture =>
    {
        var changes = SeedGearJournal(fixture);
        var files = fixture.CaptureFiles();
        // Reads remain possible; omitting delete sharing fails the fourth replacement,
        // after the three earlier actual targets have crossed their replacement calls.
        using (var held = new FileStream(GearTarget(fixture, changes[3]), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(LegacyTradeJournal.TryCommitMailboxGearOwner(fixture.Users, fixture.Items, fixture.Mailbox,
                GearRoot(fixture), changes, out var reason));
            Assert.StartsWith("tradeJournalWriteFailed:", reason);
            fixture.AssertFiles(files);
            AssertGearImages(fixture, changes, after: false);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
        }
        Assert.True(LegacyTradeJournal.TryCommitMailboxGearOwner(fixture.Users, fixture.Items, fixture.Mailbox,
            GearRoot(fixture), changes, out var retryReason), retryReason);
        AssertGearImages(fixture, changes, after: true);
    });
}
