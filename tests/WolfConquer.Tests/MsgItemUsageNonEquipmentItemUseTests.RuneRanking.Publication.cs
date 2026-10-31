using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Fact]
    public Task HandlerOwnedRuneComposeCommitsRankBeforeSameUidHolderChanges() =>
        AssertOwnedRuneRankPublicationAsync(RuneRankPostSendChange.SameUid);

    [Fact]
    public Task HandlerOwnedRuneComposeKeepsCommittedRankAfterSessionRemoval() =>
        AssertOwnedRuneRankPublicationAsync(RuneRankPostSendChange.Removed);

    [Fact]
    public Task HandlerOwnedRuneComposeRejectsDifferentUidAfterTargetSend() =>
        AssertOwnedRuneRankPublicationAsync(RuneRankPostSendChange.DifferentUid);

    [Fact]
    public Task HandlerOwnedRuneComposeRejectsRetiredNativeReattachmentAfterTargetSend() =>
        AssertOwnedRuneRankPublicationAsync(RuneRankPostSendChange.RemovedAndReaddedSameUid);

    [Fact]
    public Task HandlerOwnedRuneComposeRejectsDifferentAccountAfterTargetSend() =>
        AssertOwnedRuneRankPublicationAsync(RuneRankPostSendChange.DifferentAccountSameUid);

    private static LegacyRuneRankingState GetRuneRankingOwner(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_runeRankings", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<LegacyRuneRankingState>(field.GetValue(handler));
    }

    private enum RuneRankPostSendChange
    {
        SameUid,
        Removed,
        DifferentUid,
        RemovedAndReaddedSameUid,
        DifferentAccountSameUid,
    }
}
