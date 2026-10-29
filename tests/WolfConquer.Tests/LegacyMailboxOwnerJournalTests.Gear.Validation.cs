using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMailboxOwnerJournalTests
{
    [Theory]
    [InlineData("missing-before")]
    [InlineData("duplicate-kind")]
    [InlineData("foreign-uid")]
    [InlineData("partial-image")]
    [InlineData("huge-items")]
    [InlineData("huge-jades")]
    [InlineData("unknown-key")]
    [InlineData("bad-utf8")]
    [InlineData("bad-scalar")]
    public void V5InvalidRequiredGearInputCannotPublishOrChangeAnyImage(string fault) => Run(fixture =>
    {
        var changes = SeedGearJournal(fixture);
        var gear = changes[3];
        var text = Encoding.UTF8.GetString(gear.After);
        changes[3] = fault switch
        {
            "missing-before" => gear with { Before = null },
            "foreign-uid" => gear with { FileId = gear.FileId + 1 },
            "partial-image" => gear with { After = gear.After[..^1] },
            "huge-items" => gear with { After = Encoding.UTF8.GetBytes(text.Replace("Count=1" + Environment.NewLine,
                "Count=2147483647" + Environment.NewLine, StringComparison.Ordinal)) },
            "huge-jades" => gear with { After = Encoding.UTF8.GetBytes(text.Replace("[BlackJadeBag]" + Environment.NewLine + "Count=2",
                "[BlackJadeBag]" + Environment.NewLine + "Count=2147483647", StringComparison.Ordinal)) },
            "unknown-key" => gear with { After = Encoding.UTF8.GetBytes(text + "Unowned=1" + Environment.NewLine) },
            "bad-utf8" => gear with { After = [.. gear.After, 0xff] },
            "bad-scalar" => gear with { After = Encoding.UTF8.GetBytes(text.Replace("PirateExp=37", "PirateExp=invalid", StringComparison.Ordinal)) },
            _ => gear,
        };
        if (fault == "duplicate-kind") changes[1] = changes[1] with { Kind = LegacyTradeJournalFileKind.CombatGear };
        else if (fault == "missing-before") Assert.Null(changes[3].Before);
        else if (fault == "foreign-uid") Assert.NotEqual(gear.FileId, changes[3].FileId);
        else Assert.False(gear.After.AsSpan().SequenceEqual(changes[3].After));
        var files = fixture.CaptureFiles();

        Assert.False(LegacyTradeJournal.TryCommitMailboxGearOwner(fixture.Users, fixture.Items, fixture.Mailbox,
            GearRoot(fixture), changes, out var reason));

        Assert.StartsWith("tradeJournalInputInvalid:", reason);
        fixture.AssertFiles(files);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    });

    [Theory]
    [InlineData("unknown-domain-field")]
    [InlineData("foreign-domain")]
    [InlineData("target-order")]
    [InlineData("old-version-with-gear")]
    [InlineData("partial-gear-record")]
    public void V5MalformedPendingRecordFencesBeforeAnyTargetWrite(string fault) => Run(fixture =>
    {
        var changes = SeedGearJournal(fixture);
        Assert.False(LegacyTradeJournal.TryCommitMailboxGearOwner(fixture.Users, fixture.Items, fixture.Mailbox,
            GearRoot(fixture), changes, LegacyJournalInterruption.StopAfter(0), out _));
        var record = JsonNode.Parse(File.ReadAllText(fixture.Pending))!;
        switch (fault)
        {
            case "unknown-domain-field": record["MailboxGearDomain"]!["Injected"] = "not accepted"; break;
            case "foreign-domain": record["MailboxGearDomain"]!["CombatGear"] = Path.Combine(fixture.Root, "NeverCreated"); break;
            case "target-order":
                var array = record["Changes"]!.AsArray();
                var reversed = array.Reverse().Select(row => row!.DeepClone()).ToArray();
                array.Clear();
                foreach (var row in reversed) array.Add(row);
                break;
            case "old-version-with-gear": record["Version"] = 4; break;
            case "partial-gear-record":
                record["Changes"]![3]!["After"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("[CombatGear]\nCount=0\n"));
                break;
        }
        File.WriteAllText(fixture.Pending, record.ToJsonString()); // Owned serialized fault only.
        var files = fixture.CaptureFiles();
        var state = GearRecovery(fixture, GearRoot(fixture));

        Assert.False(state.TryEnsureInitialized(out var reason));

        Assert.StartsWith("tradeJournalRecoveryRequired:invalidOrUnreadable:", reason);
        Assert.True(state.IsBlocked);
        fixture.AssertFiles(files);
        AssertGearImages(fixture, changes, after: false);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "NeverCreated")));
    });

    [Theory]
    [InlineData("missing")]
    [InlineData("same")]
    [InlineData("ancestor")]
    [InlineData("relative")]
    public void V5CommitNeedsAnExplicitExistingDisjointGearRoot(string fault) => Run(fixture =>
    {
        var changes = SeedGearJournal(fixture);
        var configured = fault switch
        {
            "missing" => Path.Combine(fixture.Root, "MissingGear"),
            "same" => fixture.Items,
            "ancestor" => fixture.Root,
            _ => "PlayersCombatGear",
        };
        var files = fixture.CaptureFiles();
        Assert.False(LegacyTradeJournal.IsMailboxGearDomainConfigured(fixture.Users, fixture.Items, fixture.Mailbox, configured));
        Assert.False(LegacyTradeJournal.TryCommitMailboxGearOwner(fixture.Users, fixture.Items, fixture.Mailbox,
            configured, changes, out _));
        fixture.AssertFiles(files);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    });

    [Fact]
    public void V5MissingActualGearLeafRejectsWithoutCreatingOrChangingOtherImages() => Run(fixture =>
    {
        var changes = SeedGearJournal(fixture);
        File.Delete(GearTarget(fixture, changes[3]));
        var files = fixture.CaptureFiles();
        Assert.False(LegacyTradeJournal.TryCommitMailboxGearOwner(fixture.Users, fixture.Items, fixture.Mailbox,
            GearRoot(fixture), changes, out _));
        fixture.AssertFiles(files);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    });

    [Fact]
    public void V5RealCanonicalEmptyGearImageIsSupportedWithoutInventingMissingStorage() => Run(fixture =>
    {
        var changes = SeedGearJournal(fixture, emptyGear: true);
        Assert.Equal(GearBytes(CombatGearSnapshot.Empty), changes[3].Before);
        Assert.True(LegacyTradeJournal.TryCommitMailboxGearOwner(fixture.Users, fixture.Items, fixture.Mailbox,
            GearRoot(fixture), changes, out var reason), reason);
        AssertGearImages(fixture, changes, after: true);
    });

    [Fact]
    public void V5ReadinessIsNotInferredByExistingTripleRootOwner() => Run(fixture =>
    {
        SeedGearJournal(fixture);
        var old = new LegacyTradeRecoveryState(fixture.Users, fixture.Items, null, fixture.Mailbox);
        Assert.True(old.TryEnsureInitialized(out var reason), reason);
        Assert.True(old.IsReadyForMailbox(fixture.Users, fixture.Items, fixture.Mailbox));
        Assert.False(old.IsReadyForMailboxGear(fixture.Users, fixture.Items, fixture.Mailbox, GearRoot(fixture)));
        var configured = GearRecovery(fixture, GearRoot(fixture));
        Assert.True(configured.TryEnsureInitialized(out reason), reason);
        Assert.True(configured.IsReadyForMailboxGear(fixture.Users, fixture.Items, fixture.Mailbox, GearRoot(fixture)));
    });

    [Fact]
    public void V4RecordShapeAndRecoveryRemainUnchangedWithNewOptionalGearConfiguration() => Run(fixture =>
    {
        var changes = SeedGearJournal(fixture);
        Assert.False(LegacyTradeJournal.TryCommitMailboxOwner(fixture.Users, fixture.Items, fixture.Mailbox,
            changes.Take(3).ToArray(), LegacyJournalInterruption.StopAfter(0), out _));
        var record = JsonNode.Parse(File.ReadAllText(fixture.Pending))!.AsObject();
        Assert.Equal(new[] { "Version", "TransactionId", "Changes", "MailboxDomain" }, record.Select(pair => pair.Key));
        Assert.Equal(4, record["Version"]!.GetValue<int>());
        Assert.True(GearRecovery(fixture, GearRoot(fixture)).TryEnsureInitialized(out var reason), reason);
        foreach (var change in changes.Take(3)) Assert.Equal(change.After, File.ReadAllBytes(fixture.Target(change)));
        fixture.AssertClaimedImages();
        Assert.Equal(changes[3].Before, File.ReadAllBytes(GearTarget(fixture, changes[3])));
    });
}
