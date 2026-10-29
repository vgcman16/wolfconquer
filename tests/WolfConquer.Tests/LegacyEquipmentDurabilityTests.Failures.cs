using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyEquipmentDurabilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualTransportFailureAfterDurableMoveNeverRollsBackOrRegrantsOnReplay(bool unequip) => Run(async f =>
    {
        var before = f.Current;
        var expected = f.ExpectedPositions((EquipmentFixture.GearUid, unequip ? (ushort)0 : (ushort)2));
        var character = File.ReadAllBytes(f.CharacterPath);
        var registry = File.ReadAllBytes(f.Data.State);
        var attempts = 0;
        IOException? deliberatelyThrown = null;
        Exception? proofFailure = null;
        f.BeforeSend = _ =>
        {
            attempts++;
            try { AssertCommittedBeforeOutput(f, expected, character); }
            catch (Exception failure) { proofFailure = failure; throw; }
            // Assignment happens only after the real durable-before-output proof.
            throw deliberatelyThrown = new IOException("Owned deliberate equipment transport failure.");
        };
        var error = await Record.ExceptionAsync(() => f.Dispatch(unequip));
        f.BeforeSend = null;
        if (proofFailure is not null) ExceptionDispatchInfo.Capture(proofFailure).Throw();
        // The real Frame dispatcher catches peer/handler exceptions; production
        // does not promise to propagate this transport error to its caller.
        Assert.Null(error);
        Assert.NotNull(deliberatelyThrown);
        Assert.False(string.IsNullOrEmpty(deliberatelyThrown.StackTrace));
        Assert.Equal(1, attempts);
        Assert.Empty(f.Sent);
        Assert.Equal(expected, File.ReadAllBytes(f.ItemsPath));
        Assert.Equal(character, File.ReadAllBytes(f.CharacterPath));
        Assert.Equal(registry, File.ReadAllBytes(f.Data.State));
        Assert.NotSame(before, f.Current);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        var retained = f.Data.Files();
        var committed = f.Current;
        await f.Dispatch(unequip);
        Assert.Same(committed, f.Current);
        Assert.Empty(f.Sent);
        f.Data.AssertFiles(retained);
        Assert.Equal(1, f.Current.PlayerItems.InventoryItems.Concat(f.Current.PlayerItems.EquipmentItems)
            .Count(row => row.Uid == EquipmentFixture.GearUid));
    }, equipped: unequip);

    [WindowsTradeJournalFailureFact]
    public Task ActualWindowsItemsReplaceRefusalNeverPublishesSuccessAndFencesAnyPendingJournal() => Run(async f =>
    {
        var before = f.Current;
        var character = File.ReadAllBytes(f.CharacterPath);
        var items = File.ReadAllBytes(f.ItemsPath);
        var registry = File.ReadAllBytes(f.Data.State);
        var files = f.Data.Files();
        var native = CaptureNative(f);
        using (var held = new FileStream(f.ItemsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await f.Dispatch();
        Assert.Empty(f.Sent);
        Assert.Equal(character, File.ReadAllBytes(f.CharacterPath));
        Assert.Equal(items, File.ReadAllBytes(f.ItemsPath));
        Assert.Equal(registry, File.ReadAllBytes(f.Data.State));
        AssertNativeUnchanged(f, native);
        if (LegacyTradeJournal.HasPending(f.Data.Users))
        {
            Assert.True(f.Recovery.IsBlocked);
            Assert.Contains("equipmentMoveRecoveryRequired", f.Log.ToString(), StringComparison.Ordinal);
            if (f.CurrentOrNull is { } current) Assert.Same(before.PlayerItems, current.PlayerItems);
            var count = f.Sent.Count;
            await f.Dispatch();
            Assert.Equal(count, f.Sent.Count);
            Assert.Equal(items, File.ReadAllBytes(f.ItemsPath));
        }
        else
        {
            Assert.Same(before, f.Current);
            f.Data.AssertFiles(files);
        }
    });
}
