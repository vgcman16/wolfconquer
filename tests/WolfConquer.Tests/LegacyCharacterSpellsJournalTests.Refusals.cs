using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCharacterSpellsJournalTests
{
    [Theory]
    [InlineData("missing-spells-owner")]
    [InlineData("items-only-owner")]
    [InlineData("different-spells-owner")]
    public Task RecoveryCannotInferTheSpellsRouteFromAnIncompleteOrForeignOwner(string owner) => RunJournalAsync(fixture =>
    {
        var changes = Changes(fixture);
        Assert.False(LegacyTradeJournal.TryCommitCharacterSpells(fixture.Users, fixture.SpellsRoot, changes,
            LegacyCharacterSpellsJournalInterruption.AfterPending, out _));
        var retained = RememberPending(fixture);
        var recovery = owner switch
        {
            "missing-spells-owner" => new LegacyTradeRecoveryState(fixture.Users, null),
            "items-only-owner" => new LegacyTradeRecoveryState(fixture.Users, fixture.SpellsRoot),
            _ => Recovery(fixture, Path.Combine(fixture.Root, "OtherSpells")),
        };

        Assert.False(recovery.IsReadyForCharacterSpells(fixture.Users, fixture.SpellsRoot));
        Assert.False(recovery.TryEnsureInitialized(out var reason));

        Assert.StartsWith("tradeJournalRecoveryRequired:", reason);
        RequireImages(fixture, changes[0].Before!, changes[1].Before!);
        Assert.Equal(retained, File.ReadAllBytes(fixture.PendingPath));
        fixture.RequireFilesUnchanged();
        RequireNoExposure(fixture);
    });

    [Theory]
    [InlineData("malformed-json")]
    [InlineData("foreign-users")]
    [InlineData("foreign-spells")]
    [InlineData("unknown-root-property")]
    [InlineData("unknown-domain-property")]
    [InlineData("duplicate-root-property")]
    [InlineData("wrong-spells-uid")]
    [InlineData("wrong-character-uid")]
    [InlineData("wrong-kind")]
    [InlineData("wrong-count")]
    [InlineData("wrong-order")]
    [InlineData("foreign-current-bytes")]
    public Task MalformedOrConflictingRetainedRecordCannotChangeAnyCurrentParticipant(string mutation) => RunJournalAsync(fixture =>
    {
        var changes = Changes(fixture);
        Assert.False(LegacyTradeJournal.TryCommitCharacterSpells(fixture.Users, fixture.SpellsRoot, changes,
            LegacyCharacterSpellsJournalInterruption.AfterPending, out _));
        var record = Pending(fixture);
        var targets = record["Changes"]!.AsArray();
        switch (mutation)
        {
            case "foreign-users": record["CharacterSpellsDomain"]!["Users"] = Path.Combine(fixture.Root, "OtherUsers"); break;
            case "foreign-spells": record["CharacterSpellsDomain"]!["Spells"] = Path.Combine(fixture.Root, "OtherSpells"); break;
            case "unknown-root-property": record["Unexpected"] = "refuse"; break;
            case "unknown-domain-property": record["CharacterSpellsDomain"]!["Unexpected"] = "refuse"; break;
            case "wrong-spells-uid": targets[1]!["FileId"] = fixture.Original.Character.Uid + 1; break;
            case "wrong-character-uid":
                var after = Encoding.Latin1.GetString(changes[0].After).Replace(
                    "UID=" + fixture.Original.Character.Uid, "UID=" + (fixture.Original.Character.Uid + 1), StringComparison.Ordinal);
                targets[0]!["After"] = Convert.ToBase64String(Encoding.Latin1.GetBytes(after));
                break;
            case "wrong-kind": targets[1]!["Kind"] = 1; break;
            case "wrong-count": targets.RemoveAt(1); break;
            case "wrong-order":
                var first = targets[0]!.DeepClone();
                targets[0] = targets[1]!.DeepClone(); targets[1] = first;
                break;
            case "foreign-current-bytes":
                var foreign = fixture.SpellBytes; foreign[8] ^= 0x80;
                File.WriteAllBytes(fixture.SpellPath, foreign);
                fixture.ExpectImage(fixture.SpellPath, foreign);
                break;
        }
        var text = mutation == "malformed-json" ? "{\"Version\":8" : record.ToJsonString();
        if (mutation == "duplicate-root-property") text = text.Insert(text.IndexOf('{') + 1, "\"Version\":8,");
        WritePending(fixture, text);
        var retained = RememberPending(fixture);
        var character = fixture.CharacterBytes; var spells = fixture.SpellBytes;

        Assert.False(Recovery(fixture).TryEnsureInitialized(out var reason));

        Assert.StartsWith("tradeJournalRecoveryRequired:", reason);
        RequireImages(fixture, character, spells);
        Assert.Equal(retained, File.ReadAllBytes(fixture.PendingPath));
        fixture.RequireFilesUnchanged();
        RequireNoExposure(fixture);
    });

    [Fact]
    public Task CharacterImageUidMustMatchTheSpellsParticipantRatherThanItsAccountFileId() => RunJournalAsync(fixture =>
    {
        var changes = Changes(fixture);
        changes[1] = changes[1] with { FileId = fixture.Original.Character.Uid + 1 };

        Assert.False(LegacyTradeJournal.TryCommitCharacterSpells(fixture.Users, fixture.SpellsRoot, changes, out _));

        fixture.RequireFilesUnchanged();
        RequireNoExposure(fixture);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    });
}
