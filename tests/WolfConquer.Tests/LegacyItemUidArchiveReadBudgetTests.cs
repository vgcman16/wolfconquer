using System.Text.Json;
using WolfConquer.Core.Data;
using Xunit;

namespace WolfConquer.Tests;

// These pure resource-bound controls confer no producer, archive or recovery
// authority. Genuine completed work is covered through the actual creator.
public sealed class LegacyItemUidArchiveReadBudgetTests
{
    [Fact]
    public void FullHistoryAllowsItsEntryLimitAndRefusesTheNextEntry()
    {
        var budget = new LegacyItemUidRegistry.ArchiveReadBudget();
        for (var index = 0; index < LegacyItemUidRegistry.MaximumArchiveEntries; index++)
            budget.ChargeEntry();
        var error = Assert.Throws<JsonException>(budget.ChargeEntry);
        Assert.Contains("entry resource bound", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FullHistoryChargesCompleteArchiveBytesThroughTheExactByteLimit()
    {
        var budget = new LegacyItemUidRegistry.ArchiveReadBudget();
        var completeArchiveBytes = new byte[32 * 1024];
        var count = LegacyItemUidRegistry.MaximumTotalArchiveBytes / completeArchiveBytes.Length;
        for (long index = 0; index < count; index++)
            budget.ChargeBytes(completeArchiveBytes);
        var error = Assert.Throws<JsonException>(() => budget.ChargeBytes([0]));
        Assert.Contains("byte resource bound", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusedOversizeImageCannotErasePreviouslyChargedBytes()
    {
        var budget = new LegacyItemUidRegistry.ArchiveReadBudget();
        var completeArchiveBytes = new byte[32 * 1024];
        var count = LegacyItemUidRegistry.MaximumTotalArchiveBytes / completeArchiveBytes.Length;
        for (long index = 1; index < count; index++)
            budget.ChargeBytes(completeArchiveBytes);
        Assert.Throws<JsonException>(() => budget.ChargeBytes(new byte[completeArchiveBytes.Length + 1]));
        budget.ChargeBytes(completeArchiveBytes);
        Assert.Throws<JsonException>(() => budget.ChargeBytes([0]));
    }
}
