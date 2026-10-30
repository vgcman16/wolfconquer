using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyItemRepairPersistenceTests
{
    [Theory]
    [InlineData(false, (int)LegacyItemRepairJournalInterruption.AfterPending)]
    [InlineData(false, (int)LegacyItemRepairJournalInterruption.AfterCharacter)]
    [InlineData(false, (int)LegacyItemRepairJournalInterruption.AfterItems)]
    [InlineData(true, (int)LegacyItemRepairJournalInterruption.AfterPending)]
    [InlineData(true, (int)LegacyItemRepairJournalInterruption.AfterCharacter)]
    [InlineData(true, (int)LegacyItemRepairJournalInterruption.AfterItems)]
    public void ActualSingleOwnerWalRetainsRealInterruptedPrefixAndRecoveryFinishesOnce(bool meteor, int boundary) => Run(
        meteor ? Items(Row(Target, durability: 0), Row(92, 1088001, stack: 5)) : Items(Row(Target)), f =>
    {
        var payment = meteor ? LegacyItemRepairPayment.Meteors : LegacyItemRepairPayment.Money;
        var next = Repaired(f.Before.PlayerItems, meteor ? new() { [92] = null } : null);
        var after = f.After(next, meteor ? null : 990);
        var characterBefore = File.ReadAllBytes(f.CharacterPath); var raw = f.Raw.ToArray();
        var writer = f.Prepare(after, payment); var interruption = (LegacyItemRepairJournalInterruption)boundary;
        Assert.Equal(LegacyItemRepairOutcome.RecoveryRequired, writer.TryCommit(out var reason, interruption));
        Assert.Contains("RecoveryRequired", reason, StringComparison.Ordinal); Assert.True(LegacyTradeJournal.HasPending(f.Users));
        if (interruption == LegacyItemRepairJournalInterruption.AfterPending) Assert.Equal(characterBefore, File.ReadAllBytes(f.CharacterPath));
        if (interruption != LegacyItemRepairJournalInterruption.AfterItems) Assert.Equal(raw, File.ReadAllBytes(f.ItemsPath));
        var pendingFiles = Directory.GetFiles(f.Users, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
        Assert.Equal(LegacyItemRepairOutcome.RecoveryRequired, writer.TryCommit(out var pending));
        Assert.Contains("pending", pending, StringComparison.Ordinal);
        foreach (var pair in pendingFiles) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        Assert.True(LegacyTradeJournal.TryRecover(f.Users, f.ItemsRoot, out int recovered, out reason), reason);
        Assert.Equal(1, recovered); Assert.False(LegacyTradeJournal.HasPending(f.Users));
        byte[] expected;
        if (meteor)
        {
            expected = new byte[raw.Length - 184]; raw.AsSpan(0, 4 + 184).CopyTo(expected);
            raw.AsSpan(4 + 2 * 184).CopyTo(expected.AsSpan(4 + 184));
            BinaryPrimitives.WriteInt32LittleEndian(expected, 1);
        }
        else expected = raw.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(12), 1000);
        Assert.Equal(expected, File.ReadAllBytes(f.ItemsPath));
        Assert.Equal(meteor ? 1000L : 990L, CharacterSnapshotParser.ParseText(File.ReadAllText(f.CharacterPath)).Money);
        if (meteor) Assert.Equal(characterBefore, File.ReadAllBytes(f.CharacterPath));
        var finalCharacter = File.ReadAllBytes(f.CharacterPath); var finalItems = File.ReadAllBytes(f.ItemsPath);
        Assert.True(LegacyTradeJournal.TryRecover(f.Users, f.ItemsRoot, out recovered, out reason), reason); Assert.Equal(0, recovered);
        Assert.Equal(finalCharacter, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(finalItems, File.ReadAllBytes(f.ItemsPath));
    });

    [Theory]
    [InlineData("character")]
    [InlineData("items")]
    [InlineData("missing")]
    public void PreparedWriterWithChangedOrMissingBeforeFileFencesWithoutClaimingAWrittenRepair(string fault) => Run(
        Items(Row(Target)), f =>
    {
        var writer = f.Prepare(f.After(Repaired(f.Before.PlayerItems), 990), LegacyItemRepairPayment.Money);
        if (fault == "character") File.AppendAllText(f.CharacterPath, "\r\n; external change");
        if (fault == "items") { var bytes = File.ReadAllBytes(f.ItemsPath); bytes[4 + 14] ^= 1; File.WriteAllBytes(f.ItemsPath, bytes); }
        if (fault == "missing") File.Delete(f.ItemsPath);
        var character = File.ReadAllBytes(f.CharacterPath); var items = File.Exists(f.ItemsPath) ? File.ReadAllBytes(f.ItemsPath) : null;
        Assert.Equal(LegacyItemRepairOutcome.RecoveryRequired, writer.TryCommit(out var reason));
        Assert.Contains("PrewriteBaselineUnproven", reason, StringComparison.Ordinal);
        Assert.False(LegacyTradeJournal.HasPending(f.Users)); Assert.Equal(character, File.ReadAllBytes(f.CharacterPath));
        if (items is null) Assert.False(File.Exists(f.ItemsPath)); else Assert.Equal(items, File.ReadAllBytes(f.ItemsPath));
    });

    [Fact]
    public void DuplicatePreparedWritersCannotChargeOrRepairTwice() => Run(Items(Row(Target)), f =>
    {
        var after = f.After(Repaired(f.Before.PlayerItems), 990);
        var first = f.Prepare(after, LegacyItemRepairPayment.Money); var second = f.Prepare(after, LegacyItemRepairPayment.Money);
        Assert.Equal(LegacyItemRepairOutcome.Committed, first.TryCommit(out _));
        var character = File.ReadAllBytes(f.CharacterPath); var items = File.ReadAllBytes(f.ItemsPath);
        Assert.Equal(LegacyItemRepairOutcome.RecoveryRequired, second.TryCommit(out var reason));
        Assert.Contains("PrewriteBaselineUnproven", reason, StringComparison.Ordinal);
        Assert.Equal(character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(items, File.ReadAllBytes(f.ItemsPath));
        Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });

    [Fact]
    public void CompetingRealPreparedWritersSerializeToOnePhysicalCommit() => Run(Items(Row(Target)), f =>
    {
        var after = f.After(Repaired(f.Before.PlayerItems), 990);
        var first = f.Prepare(after, LegacyItemRepairPayment.Money); var second = f.Prepare(after, LegacyItemRepairPayment.Money);
        using var start = new ManualResetEventSlim(false);
        var one = Task.Run(() => { start.Wait(); return first.TryCommit(out _); });
        var two = Task.Run(() => { start.Wait(); return second.TryCommit(out _); });
        start.Set(); var results = Task.WhenAll(one, two).GetAwaiter().GetResult();
        Assert.Single(results, result => result == LegacyItemRepairOutcome.Committed);
        Assert.Single(results, result => result == LegacyItemRepairOutcome.RecoveryRequired);
        Assert.Equal(990L, CharacterSnapshotParser.ParseText(File.ReadAllText(f.CharacterPath)).Money);
        Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });

    [Theory]
    [InlineData("same-money")]
    [InlineData("increased-money")]
    [InlineData("negative-money")]
    [InlineData("other-attribute")]
    [InlineData("identity")]
    [InlineData("source-path")]
    public void InvalidLogicalParticipantRefusesBeforeAnyWorkOrFileMutation(string fault) => Run(Items(Row(Target)), f =>
    {
        var after = f.After(Repaired(f.Before.PlayerItems), fault == "same-money" ? 1000 : fault == "increased-money" ? 1001 : fault == "negative-money" ? -1 : 990);
        if (fault == "other-attribute") ((Dictionary<string, string>)after.Character.Attributes)["Opaque"] = "changed";
        if (fault == "identity") after = after with { AccountId = after.AccountId + 1 };
        var before = fault == "source-path" ? f.Before with { Character = f.Before.Character with { SourceFile = "foreign.ini" } } : f.Before;
        var character = File.ReadAllBytes(f.CharacterPath); var raw = File.ReadAllBytes(f.ItemsPath);
        Assert.False(LegacyItemRepairPersistence.TryPrepare(before, after, f.ItemsRoot, Target, LegacyItemRepairPayment.Money, out var write, out var reason));
        Assert.Null(write); Assert.NotEmpty(reason); Assert.False(LegacyTradeJournal.HasPending(f.Users));
        Assert.Equal(character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(raw, File.ReadAllBytes(f.ItemsPath));
    });

    [Fact]
    public void InvalidInternalInterruptionRefusesOnlyAfterExactBeforeImagesAreProven() => Run(Items(Row(Target)), f =>
    {
        var writer = f.Prepare(f.After(Repaired(f.Before.PlayerItems), 990), LegacyItemRepairPayment.Money);
        var character = File.ReadAllBytes(f.CharacterPath); var raw = File.ReadAllBytes(f.ItemsPath);
        Assert.Equal(LegacyItemRepairOutcome.Rejected, writer.TryCommit(out _, (LegacyItemRepairJournalInterruption)99));
        Assert.Equal(character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(raw, File.ReadAllBytes(f.ItemsPath));
        Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });
}
