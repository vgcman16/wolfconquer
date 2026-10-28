using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCharacterSpellsJournalTests
{
    public static IEnumerable<object[]> AmbiguousCharacterImages()
    {
        foreach (var target in new[] { "before", "after" })
        foreach (var mutation in new[] { "duplicate-section", "duplicate-uid", "duplicate-class-same",
            "duplicate-class-different", "malformed-line", "empty-key", "missing-section", "zero-uid", "overflow-uid" })
            yield return [target, mutation];
    }

    [Theory]
    [MemberData(nameof(AmbiguousCharacterImages))]
    public Task AmbiguousOrOutOfRangeCharacterInputRefusesBeforePublishingAJournal(string target, string mutation) => RunJournalAsync(fixture =>
    {
        var changes = Changes(fixture);
        var image = target == "before" ? changes[0].Before! : changes[0].After;
        var text = Encoding.Latin1.GetString(image);
        var uid = fixture.Original.Character.Uid.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var line = mutation switch
        {
            "duplicate-uid" => "uid=" + uid,
            "duplicate-class-same" => "CLASS=" + (target == "before" ? "15" : "1005"),
            "duplicate-class-different" => "Class=2005",
            "malformed-line" => "This active Character row has no equals sign",
            "empty-key" => "=42",
            _ => null,
        };
        if (line is not null)
        {
            var other = text.IndexOf("[Other]", StringComparison.Ordinal);
            Assert.True(other > 0);
            text = text.Insert(other, line + "\n");
        }
        else text = mutation switch
        {
            "duplicate-section" => text + "\n[Character]\nUID=" + uid + "\n",
            "missing-section" => text.Replace("[Character]", "; missing required section", StringComparison.Ordinal),
            "zero-uid" => text.Replace("UID=" + uid, "UID=0", StringComparison.Ordinal),
            "overflow-uid" => text.Replace("UID=" + uid, "UID=4294967296", StringComparison.Ordinal),
            _ => throw new InvalidOperationException("Unrecognized owned Character image mutation."),
        };
        var malformed = Encoding.Latin1.GetBytes(text);
        changes[0] = target == "before" ? changes[0] with { Before = malformed } : changes[0] with { After = malformed };
        if (target == "before")
        {
            // Make the actual owned file match the submitted preimage, so a
            // generic before-image mismatch cannot mask parser admission.
            File.WriteAllBytes(fixture.CharacterPath, malformed);
            fixture.ExpectImage(fixture.CharacterPath, malformed);
        }

        Assert.False(LegacyTradeJournal.TryCommitCharacterSpells(fixture.Users, fixture.SpellsRoot, changes, out _));

        fixture.RequireFilesUnchanged();
        RequireNoExposure(fixture);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    });
}
