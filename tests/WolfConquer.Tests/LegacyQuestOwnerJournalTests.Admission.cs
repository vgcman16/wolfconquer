using System.Text.Json.Nodes;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyQuestOwnerJournalTests
{
    [Theory]
    [InlineData("missingQuest")]
    [InlineData("duplicateKind")]
    [InlineData("unknownKind")]
    [InlineData("zeroAccount")]
    [InlineData("negativeItems")]
    [InlineData("mismatchedUid")]
    [InlineData("nullCharacterBefore")]
    [InlineData("changedCharacter")]
    [InlineData("nullQuestBefore")]
    [InlineData("nullAfter")]
    public void InvalidV3ShapesNeverPublishOrTouchAnyRealTarget(string fault)
    {
        var changes = Seed();
        var invalid = changes.ToArray();
        switch (fault)
        {
            case "missingQuest": invalid = invalid.Take(2).ToArray(); break;
            case "duplicateKind": invalid[2] = invalid[2] with { Kind = LegacyTradeJournalFileKind.Items }; break;
            case "unknownKind": invalid[2] = invalid[2] with { Kind = (LegacyTradeJournalFileKind)17 }; break;
            case "zeroAccount": invalid[0] = invalid[0] with { FileId = 0 }; break;
            case "negativeItems": invalid[1] = invalid[1] with { FileId = -1 }; break;
            case "mismatchedUid": invalid[2] = invalid[2] with { FileId = CharacterUid + 1 }; break;
            case "nullCharacterBefore": invalid[0] = invalid[0] with { Before = null }; break;
            case "changedCharacter": invalid[0] = invalid[0] with { After = [1, 2, 3] }; break;
            case "nullQuestBefore": invalid[2] = invalid[2] with { Before = null }; break;
            case "nullAfter": invalid[2] = invalid[2] with { After = null! }; break;
        }
        var files = CaptureFiles();
        Assert.False(LegacyTradeJournal.TryCommitQuestOwner(Users, Items, Quests, invalid, out var reason));
        Assert.StartsWith("tradeJournalInputInvalid:", reason);
        Assert.False(LegacyTradeJournal.HasPending(Users));
        AssertFiles(files);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("relative")]
    [InlineData("same")]
    [InlineData("ancestor")]
    [InlineData("journal")]
    public void InvalidV3RootConfigurationCannotPublishOrChangeTargets(string fault)
    {
        var changes = Seed();
        var configuredQuests = fault switch
        {
            "missing" => Path.Combine(root, "MissingQuests"),
            "relative" => "Quests",
            "same" => Path.Combine(Items, "."),
            "ancestor" => root,
            "journal" => Path.GetDirectoryName(Pending)!,
            _ => throw new ArgumentException("Unknown root case."),
        };
        if (fault == "journal") Directory.CreateDirectory(configuredQuests);
        var files = CaptureFiles();
        Assert.False(LegacyTradeJournal.TryCommitQuestOwner(Users, Items, configuredQuests, changes, out _));
        Assert.False(LegacyTradeJournal.HasPending(Users));
        AssertFiles(files);
        Assert.False(Directory.Exists(Path.Combine(root, "MissingQuests")));
    }

    [Theory]
    [InlineData("users")]
    [InlineData("items")]
    [InlineData("quests")]
    public void ForeignConfiguredRootWithIdenticalImagesCannotReplayTheRecordedDomain(string kind)
    {
        var changes = Seed();
        Assert.False(LegacyTradeJournal.TryCommitQuestOwner(Users, Items, Quests, changes, count => count == 0, out _));
        var foreign = Path.Combine(root, "Foreign" + kind);
        Directory.CreateDirectory(foreign);
        var index = kind == "users" ? 0 : kind == "items" ? 1 : 2;
        File.WriteAllBytes(Path.Combine(foreign, Path.GetFileName(Target(changes[index]))), changes[index].Before!);
        if (kind == "users")
        {
            var journal = Path.Combine(foreign, ".managed-trade-journal");
            Directory.CreateDirectory(journal);
            File.WriteAllBytes(Path.Combine(journal, "pending.json"), File.ReadAllBytes(Pending));
        }
        var files = CaptureFiles();
        Assert.False(LegacyTradeJournal.TryRecover(kind == "users" ? foreign : Users,
            kind == "items" ? foreign : Items, kind == "quests" ? foreign : Quests,
            out var recovered, out var reason));
        Assert.Equal(0, recovered);
        Assert.StartsWith("tradeJournalRecoveryRequired:invalidOrUnreadable:", reason);
        AssertFiles(files);
    }

    [Theory]
    [InlineData("uninitialized")]
    [InlineData("pair")]
    [InlineData("foreign")]
    [InlineData("overlap")]
    [InlineData("ready")]
    public void TripleReadinessRequiresTheExplicitHealthyConfiguredQuestDomain(string state)
    {
        Seed();
        var foreign = Path.Combine(root, "OtherQuests");
        Directory.CreateDirectory(foreign);
        var recovery = state == "pair" ? new LegacyTradeRecoveryState(Users, Items)
            : new LegacyTradeRecoveryState(Users, Items, state == "foreign" ? foreign : state == "overlap" ? Items : Quests);
        var files = CaptureFiles();
        if (state != "uninitialized") Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
        Assert.Equal(state == "ready", recovery.IsReadyFor(Users, Items, Quests));
        AssertFiles(files);
    }

    [Theory]
    [InlineData("changedCharacter")]
    [InlineData("uidMismatch")]
    [InlineData("missingDomain")]
    [InlineData("foreignDomain")]
    [InlineData("extraDomain")]
    [InlineData("duplicateKind")]
    public void MalformedPublishedV3RetainsEveryTargetAndPendingEvidence(string fault)
    {
        var changes = Seed();
        Assert.False(LegacyTradeJournal.TryCommitQuestOwner(Users, Items, Quests, changes, count => count == 0, out _));
        var record = JsonNode.Parse(File.ReadAllText(Pending))!.AsObject();
        var entries = record["Changes"]!.AsArray();
        switch (fault)
        {
            case "changedCharacter": entries[0]!["After"] = Convert.ToBase64String(new byte[] { 1, 2, 3 }); break;
            case "uidMismatch": entries[2]!["FileId"] = CharacterUid + 1; break;
            case "missingDomain": record.Remove("Domain"); break;
            case "foreignDomain": record["Domain"]!["Quests"] = Path.Combine(root, "NeverConfiguredQuests"); break;
            case "extraDomain": record["Domain"]!["Other"] = "unexpected"; break;
            case "duplicateKind": entries[2]!["Kind"] = (int)LegacyTradeJournalFileKind.Items; break;
        }
        File.WriteAllText(Pending, record.ToJsonString());
        var files = CaptureFiles();
        Assert.False(LegacyTradeJournal.TryRecover(Users, Items, Quests, out var recovered, out var reason));
        Assert.Equal(0, recovered);
        Assert.StartsWith("tradeJournalRecoveryRequired:invalidOrUnreadable:", reason);
        AssertFiles(files);
        Assert.False(Directory.Exists(Path.Combine(root, "NeverConfiguredQuests")));
    }
}
