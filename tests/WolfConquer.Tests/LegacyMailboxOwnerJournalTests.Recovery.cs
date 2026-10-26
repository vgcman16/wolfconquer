using System.Text.Json.Nodes;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMailboxOwnerJournalTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EveryV4InterruptionRecoversExactCharacterItemsAndMailboxImagesOnce(int replacements) => Run(fixture =>
    {
        var changes = fixture.Seed();
        var trace = new List<int>();
        Assert.False(LegacyTradeJournal.TryCommitMailboxOwner(fixture.Users, fixture.Items, fixture.Mailbox,
            changes.Reverse().ToArray(), count =>
            {
                trace.Add(count);
                return count == replacements;
            }, out var reason));
        Assert.Equal("tradeJournalRecoveryRequired:interrupted", reason);
        Assert.Equal(Enumerable.Range(0, replacements + 1), trace);
        for (var index = 0; index < changes.Length; index++)
            Assert.Equal(replacements > index ? changes[index].After : changes[index].Before,
                File.ReadAllBytes(fixture.Target(changes[index])));
        var record = JsonNode.Parse(File.ReadAllText(fixture.Pending))!;
        Assert.Equal(4, record["Version"]!.GetValue<int>());
        Assert.Equal(new[] { 0, 1, 3 }, record["Changes"]!.AsArray().Select(row => row!["Kind"]!.GetValue<int>()));
        Assert.Equal(fixture.Users, record["MailboxDomain"]!["Users"]!.GetValue<string>());
        Assert.Equal(fixture.Items, record["MailboxDomain"]!["Items"]!.GetValue<string>());
        Assert.Equal(fixture.Mailbox, record["MailboxDomain"]!["Mailbox"]!.GetValue<string>());

        var restarted = new LegacyTradeRecoveryState(fixture.Users, fixture.Items, null, fixture.Mailbox);
        Assert.False(restarted.IsReadyForMailbox(fixture.Users, fixture.Items, fixture.Mailbox));
        Assert.True(restarted.TryEnsureInitialized(out reason), reason);
        Assert.True(restarted.IsReadyForMailbox(fixture.Users, fixture.Items, fixture.Mailbox));
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
        fixture.AssertImages(changes, after: true);
        fixture.AssertClaimedImages();
        Assert.True(LegacyTradeJournal.TryRecover(fixture.Users, fixture.Items, null, fixture.Mailbox,
            out var recovered, out reason), reason);
        Assert.Equal(0, recovered);
        fixture.AssertImages(changes, after: true);
        fixture.AssertClaimedImages();
    });

    [Fact]
    public void UnknownMailboxImageFencesRecoveryBeforeAnyFurtherTargetWrite() => Run(fixture =>
    {
        var changes = fixture.Seed();
        Assert.False(LegacyTradeJournal.TryCommitMailboxOwner(fixture.Users, fixture.Items, fixture.Mailbox,
            changes, count => count == 1, out _));
        var unknown = changes[2].Before!.ToArray();
        unknown[4] ^= 1; // Different title byte; still a complete managed mailbox image.
        File.WriteAllBytes(fixture.Target(changes[2]), unknown);
        var files = fixture.CaptureFiles();
        var blocked = new LegacyTradeRecoveryState(fixture.Users, fixture.Items, null, fixture.Mailbox);
        Assert.False(blocked.TryEnsureInitialized(out var reason));
        Assert.StartsWith("tradeJournalRecoveryRequired:rollForwardFailed:", reason);
        Assert.True(blocked.IsBlocked);
        Assert.False(blocked.IsReadyForMailbox(fixture.Users, fixture.Items, fixture.Mailbox));
        fixture.AssertFiles(files);

        // An explicit fixture repair cannot make the blocked live owner replay.
        File.WriteAllBytes(fixture.Target(changes[2]), changes[2].Before!);
        var repaired = fixture.CaptureFiles();
        Assert.False(blocked.TryEnsureInitialized(out _));
        fixture.AssertFiles(repaired);
        Assert.True(new LegacyTradeRecoveryState(fixture.Users, fixture.Items, null, fixture.Mailbox)
            .TryEnsureInitialized(out reason), reason);
        fixture.AssertImages(changes, after: true);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrForeignExplicitMailboxRootCannotReplayRecordedDomain(bool foreign) => Run(fixture =>
    {
        var changes = fixture.Seed();
        Assert.False(LegacyTradeJournal.TryCommitMailboxOwner(fixture.Users, fixture.Items, fixture.Mailbox,
            changes, count => count == 0, out _));
        string? configured = null;
        if (foreign)
        {
            configured = Path.Combine(fixture.Root, "OtherMailbox");
            Directory.CreateDirectory(configured);
            File.WriteAllBytes(Path.Combine(configured, Path.GetFileName(fixture.Target(changes[2]))), changes[2].Before!);
        }
        var files = fixture.CaptureFiles();
        var wrong = new LegacyTradeRecoveryState(fixture.Users, fixture.Items, null, configured);
        Assert.False(wrong.TryEnsureInitialized(out var reason));
        if (foreign) Assert.StartsWith("tradeJournalRecoveryRequired:invalidOrUnreadable:", reason);
        else Assert.Equal("tradeJournalRecoveryRequired:mailboxRootMissing", reason);
        Assert.True(wrong.IsBlocked);
        Assert.False(wrong.IsReadyForMailbox(fixture.Users, fixture.Items, fixture.Mailbox));
        fixture.AssertFiles(files);
        Assert.True(new LegacyTradeRecoveryState(fixture.Users, fixture.Items, null, fixture.Mailbox)
            .TryEnsureInitialized(out reason), reason);
        fixture.AssertImages(changes, after: true);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
        if (foreign)
            Assert.Equal(changes[2].Before,
                File.ReadAllBytes(Path.Combine(configured!, Path.GetFileName(fixture.Target(changes[2])))));
    });
}
