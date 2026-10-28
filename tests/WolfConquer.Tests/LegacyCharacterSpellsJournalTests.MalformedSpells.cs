using System.Buffers.Binary;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Corrected-only v8 validation controls; exclude from the old-source baseline.
public sealed partial class LegacyCharacterSpellsJournalTests
{
    public static IEnumerable<object[]> MalformedSpellImages()
    {
        foreach (var target in new[] { "before", "after" })
        foreach (var mutation in new[] { "duplicate-id", "zero-id", "negative-experience" })
            yield return [target, mutation];
    }

    [Theory]
    [MemberData(nameof(MalformedSpellImages))]
    public Task InvalidRealSpellImageRefusesBeforePublishingAJournal(string target, string mutation) => RunJournalAsync(fixture =>
    {
        var changes = Changes(fixture);
        var image = (target == "before" ? changes[1].Before! : changes[1].After).ToArray();
        Assert.True(image.Length >= sizeof(int) + (2 * 12));
        Assert.True(BinaryPrimitives.ReadInt32LittleEndian(image) >= 2);

        const int firstRow = sizeof(int);
        const int secondRow = firstRow + 12;
        switch (mutation)
        {
            case "duplicate-id":
                BinaryPrimitives.WriteUInt16LittleEndian(
                    image.AsSpan(secondRow, sizeof(ushort)),
                    BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(firstRow, sizeof(ushort))));
                break;
            case "zero-id":
                BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(firstRow, sizeof(ushort)), 0);
                break;
            case "negative-experience":
                BinaryPrimitives.WriteInt32LittleEndian(
                    image.AsSpan(firstRow + sizeof(ushort) + sizeof(ushort), sizeof(int)), -1);
                break;
            default:
                throw new InvalidOperationException("Unrecognized owned Spells image mutation.");
        }

        changes[1] = target == "before"
            ? changes[1] with { Before = image }
            : changes[1] with { After = image };
        if (target == "before")
        {
            // Match the submitted preimage exactly so parser validation, rather
            // than a generic current-image mismatch, owns this refusal.
            File.WriteAllBytes(fixture.SpellPath, image);
            fixture.ExpectImage(fixture.SpellPath, image);
        }

        Assert.False(LegacyTradeJournal.TryCommitCharacterSpells(
            fixture.Users, fixture.SpellsRoot, changes, out _));

        fixture.RequireFilesUnchanged();
        RequireNoExposure(fixture);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    });

    [Fact]
    public Task UndefinedInterruptionRefusesBeforeAnyRootIoOrMutation() => RunJournalAsync(fixture =>
    {
        var absentUsers = Path.Combine(fixture.Root, "AbsentUsers");
        var absentSpells = Path.Combine(fixture.Root, "AbsentSpells");
        Assert.False(Directory.Exists(absentUsers) || File.Exists(absentUsers));
        Assert.False(Directory.Exists(absentSpells) || File.Exists(absentSpells));

        Assert.False(LegacyTradeJournal.TryCommitCharacterSpells(
            absentUsers,
            absentSpells,
            Changes(fixture),
            (LegacyCharacterSpellsJournalInterruption)int.MaxValue,
            out var reason));

        Assert.Equal("characterSpellsJournalInvalid", reason);
        Assert.False(Directory.Exists(absentUsers) || File.Exists(absentUsers));
        Assert.False(Directory.Exists(absentSpells) || File.Exists(absentSpells));
        fixture.RequireFilesUnchanged();
        RequireNoExposure(fixture);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    });
}
