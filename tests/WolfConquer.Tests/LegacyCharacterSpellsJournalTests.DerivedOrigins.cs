using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.LegacyFullAccountClassMutationTests.DurableClassFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyCharacterSpellsJournalTests
{
    // Recorded DATA validation through the real owned-file v8 journal. These
    // images do not create a live spell owner or a class-reset permission.
    [Theory]
    [InlineData("new-claim")]
    [InlineData("new-level")]
    [InlineData("drop-only")]
    [InlineData("progress")]
    [InlineData("opaque")]
    [InlineData("missing-row")]
    public Task OrdinaryJournalCannotCreateOrPermanentizeDerivedClaims(string mutation) => RunJournalAsync(fixture =>
    {
        var changes = RecordedDerivedChanges(fixture, mutation != "new-claim");
        var afterCharacter = Encoding.Latin1.GetString(changes[0].After);
        var afterSpells = changes[1].After.ToArray();
        int row = afterSpells.Length - 12;
        switch (mutation)
        {
            case "new-level":
                afterCharacter = afterCharacter.Replace("21930:4", "21930:5", StringComparison.Ordinal);
                BinaryPrimitives.WriteUInt16LittleEndian(afterSpells.AsSpan(row + 2), 5);
                break;
            case "drop-only": afterCharacter = RemoveRecordedOrigin(afterCharacter); break;
            case "progress": BinaryPrimitives.WriteUInt32LittleEndian(afterSpells.AsSpan(row + 4), 1); break;
            case "opaque": afterSpells[row + 11] = 0xC7; break;
            case "missing-row": afterSpells = WithoutLastRecordedSpell(afterSpells); break;
        }
        changes[0] = changes[0] with { After = Encoding.Latin1.GetBytes(afterCharacter) };
        changes[1] = changes[1] with { After = afterSpells };

        Assert.False(LegacyTradeJournal.TryCommitCharacterSpells(fixture.Users, fixture.SpellsRoot, changes, out _));

        fixture.RequireFilesUnchanged();
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
        RequireNoExposure(fixture);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OrdinaryJournalAcceptsExactRecordedPreservationOrJointRetirementData(bool retire) => RunJournalAsync(fixture =>
    {
        var changes = RecordedDerivedChanges(fixture, true);
        if (retire)
        {
            changes[0] = changes[0] with { After = Encoding.Latin1.GetBytes(
                RemoveRecordedOrigin(Encoding.Latin1.GetString(changes[0].After))) };
            changes[1] = changes[1] with { After = WithoutLastRecordedSpell(changes[1].After) };
        }

        Assert.True(LegacyTradeJournal.TryCommitCharacterSpells(fixture.Users, fixture.SpellsRoot, changes, out var reason), reason);

        RequireImages(fixture, changes[0].After, changes[1].After);
        fixture.ExpectNoPending();
        fixture.RequireFilesUnchanged();
        RequireNoExposure(fixture);
    });

    [Fact]
    public Task RetainedOrdinaryJournalCannotDropOnlyAnOriginDuringRecovery() => RunJournalAsync(fixture =>
    {
        var changes = RecordedDerivedChanges(fixture, true);
        Assert.False(LegacyTradeJournal.TryCommitCharacterSpells(fixture.Users, fixture.SpellsRoot, changes,
            LegacyCharacterSpellsJournalInterruption.AfterPending, out _));
        RememberPending(fixture);
        var pending = Pending(fixture);
        pending["Changes"]![0]!["After"] = Convert.ToBase64String(Encoding.Latin1.GetBytes(
            RemoveRecordedOrigin(Encoding.Latin1.GetString(changes[0].After))));
        WritePending(fixture, pending.ToJsonString());
        var retained = RememberPending(fixture);

        Assert.False(Recovery(fixture).TryEnsureInitialized(out var reason));

        Assert.StartsWith("tradeJournalRecoveryRequired:", reason);
        Assert.Equal(retained, File.ReadAllBytes(fixture.PendingPath));
        fixture.RequireFilesUnchanged();
        RequireNoExposure(fixture);
    });

    private static LegacyTradeJournalChange[] RecordedDerivedChanges(Fixture fixture, bool markedBefore)
    {
        var changes = Changes(fixture);
        byte[] Mark(byte[] bytes) => Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(bytes)
            .Replace("[Other]", "ManagedDerivedSpellOrigins=21930:4\n[Other]", StringComparison.Ordinal));
        var beforeCharacter = markedBefore ? Mark(changes[0].Before!) : changes[0].Before!;
        changes[0] = changes[0] with { Before = beforeCharacter, After = Mark(changes[0].After) };
        changes[1] = changes[1] with { Before = AppendRecordedSpell(changes[1].Before!), After = AppendRecordedSpell(changes[1].After) };
        File.WriteAllBytes(fixture.CharacterPath, beforeCharacter);
        File.WriteAllBytes(fixture.SpellPath, changes[1].Before!);
        fixture.ExpectImage(fixture.CharacterPath, beforeCharacter);
        fixture.ExpectImage(fixture.SpellPath, changes[1].Before!);
        return changes;
    }

    private static byte[] AppendRecordedSpell(byte[] before)
    {
        var after = new byte[before.Length + 12];
        before.CopyTo(after, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(after, checked(BinaryPrimitives.ReadUInt32LittleEndian(before) + 1));
        BinaryPrimitives.WriteUInt16LittleEndian(after.AsSpan(before.Length), 21930);
        BinaryPrimitives.WriteUInt16LittleEndian(after.AsSpan(before.Length + 2), 4);
        return after;
    }

    private static byte[] WithoutLastRecordedSpell(byte[] before)
    {
        var after = before.AsSpan(0, before.Length - 12).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(after, checked(BinaryPrimitives.ReadUInt32LittleEndian(before) - 1));
        return after;
    }

    private static string RemoveRecordedOrigin(string text) => text.Replace(
        "ManagedDerivedSpellOrigins=21930:4\n", "", StringComparison.Ordinal);
}
