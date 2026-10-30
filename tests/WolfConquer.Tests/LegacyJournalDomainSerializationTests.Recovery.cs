using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyJournalDomainSerializationTests
{
    [Fact]
    public void CapturedRecoveryCannotReplayOrDeleteAReplacementRealTransaction()
    {
        using var data = new LegacyOwnedAdmissionRuntimeFixture(bootstrap: false);
        var first = SeedOwner(data, 1, 40); var second = SeedOwner(data, 2, 100); var later = SeedOwner(data, 3);
        Assert.False(LegacyTradeJournal.TryCommitCharacterResources(data.Users, PairChanges(first, second), out _,
            LegacyCharacterResourcesJournalInterruption.AfterPending));
        var capture = CaptureRecovery(data);
        Assert.True(LegacyTradeJournal.TryRecover(data.Users, data.Items, out var completed, out var reason), reason);
        Assert.Equal(1, completed);
        var laterChanges = OwnerChanges(later, 2);
        using var interruption = LegacyJournalInterruption.StopAfter(0);
        Assert.False(LegacyTradeJournal.TryCommitSingleOwner(data.Users, data.Items, laterChanges, interruption, out _));
        var before = data.Files();
        object?[] arguments = [capture, 0, null, null]; // No owned-startup recovery capability in this control.
        Assert.False(Assert.IsType<bool>(InvokeRecoveryPhase("RecoverCaptured", arguments)));
        Assert.Equal(0, arguments[1]); Assert.Equal(LegacyTradeJournal.RecoveryRequired + ":pendingRecordChanged", arguments[2]);
        data.AssertFiles(before);
        Assert.True(LegacyTradeJournal.TryRecover(data.Users, data.Items, out completed, out reason), reason);
        Assert.Equal(1, completed); Assert.Equal(laterChanges[0].After, File.ReadAllBytes(later.Character));
    }

    [Fact]
    public void CapturedAlreadyCompletedRecoveryIsAStableZeroCountWithoutDoubleApplication()
    {
        using var data = new LegacyOwnedAdmissionRuntimeFixture(bootstrap: false);
        var first = SeedOwner(data, 1, 40); var second = SeedOwner(data, 2, 100);
        Assert.False(LegacyTradeJournal.TryCommitCharacterResources(data.Users, PairChanges(first, second), out _,
            LegacyCharacterResourcesJournalInterruption.AfterPending));
        var capture = CaptureRecovery(data);
        Assert.True(LegacyTradeJournal.TryRecover(data.Users, data.Items, out var count, out var reason), reason);
        Assert.Equal(1, count); var before = data.Files();
        object?[] arguments = [capture, 0, null, null];
        Assert.True(Assert.IsType<bool>(InvokeRecoveryPhase("RecoverCaptured", arguments)));
        Assert.Equal(0, arguments[1]); data.AssertFiles(before);
    }

    [Fact]
    public void MissingCapturedRecordWithBeforeImagesDoesNotBecomeSuccessfulCompletion()
    {
        using var data = new LegacyOwnedAdmissionRuntimeFixture(bootstrap: false);
        var first = SeedOwner(data, 1, 40); var second = SeedOwner(data, 2, 100);
        Assert.False(LegacyTradeJournal.TryCommitCharacterResources(data.Users, PairChanges(first, second), out _,
            LegacyCharacterResourcesJournalInterruption.AfterPending));
        var capture = CaptureRecovery(data);
        File.Delete(Path.Combine(data.Users, ".managed-trade-journal", "pending.json")); // Actual owned evidence-loss control.
        var before = data.Files();
        object?[] arguments = [capture, 0, null, null];
        Assert.False(Assert.IsType<bool>(InvokeRecoveryPhase("RecoverCaptured", arguments)));
        Assert.Equal(0, arguments[1]);
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired + ":capturedRecordMissingOrIncomplete:", Assert.IsType<string>(arguments[2]));
        data.AssertFiles(before);
    }

    private static object CaptureRecovery(LegacyOwnedAdmissionRuntimeFixture data)
    {
        object?[] arguments = [data.Users, data.Items, null, null, null, null, null, null];
        Assert.True(Assert.IsType<bool>(InvokeRecoveryPhase("TryCaptureRecovery", arguments)));
        Assert.NotNull(arguments[6]); return arguments[6]!;
    }

    private static object? InvokeRecoveryPhase(string name, object?[] arguments)
    {
        // Actual managed source phases over quiescent owned files, not a fake
        // pending record, acceptance callback or permission to replay a live World.
        var method = typeof(LegacyTradeJournal).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
        try { return method.Invoke(null, arguments); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
    }
}
