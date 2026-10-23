using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDragonSkinTransactionTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public Task ActualFramePathReleasesSharedAdmissionBeforeTakingDragonSkinExclusiveLease(int type) => RunAsync(async f =>
    {
        f.Seed(type == 2 ? "0/0/" : "1/1/1/", [Item(9, 720_001), Item(10, Mapped(1))]);
        f.AttachFrameOwner();
        var before = Capture(f.Current);
        using var budget = new CancellationTokenSource(Bound);
        var held = await f.Barrier.EnterSharedAsync(budget.Token);
        f.KeepLease(held);
        var actual = f.FrameAsync((LegacyDragonSkinType)type);
        Assert.False(actual.IsCompleted);
        Assert.Same(before.Session, f.Current); AssertFilesUnchanged(f); Assert.Empty(f.Sent);
        held.Dispose();
        await actual;
        AssertCommitted(f, before, type == 2 ? "0/1/1/" : "0/0/");
        Assert.Equal(2, f.Sent.Count);
        Assert.Equal(SkinGolden((LegacyDragonSkinType)type, 1, true), f.Sent[1]);
        Assert.Equal(type == 2
            ? new MsgItemUsagePacket(10, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes()
            : ItemGolden(11, Mapped(1), 1, 1), f.Sent[0]);
        Assert.DoesNotContain("PARITY-BUG", f.Log.ToString());
    });

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public Task ActualTransportCloseAfterCommitThenReloadedConnectionCannotDuplicateReward(int type) => RunAsync(async f =>
    {
        f.Seed(type == 2 ? "0/0/" : "1/1/1/", [Item(9, 720_001), Item(10, Mapped(1))]);
        f.AttachFrameOwner();
        var before = Capture(f.Current);
        await f.FrameAsync((LegacyDragonSkinType)type);
        AssertCommitted(f, before, type == 2 ? "0/1/1/" : "0/0/");
        var character = File.ReadAllBytes(f.CharacterPath);
        var items = File.ReadAllBytes(f.ItemPath);
        var closed = f.Connection;
        await f.CloseTransportAsync();
        Assert.False(f.Sessions.TryGet(closed, out _));
        f.ReconnectFromFiles();
        var reloaded = Capture(f.Current);
        await f.FrameAsync((LegacyDragonSkinType)type);
        Assert.Same(reloaded.Session, f.Current); AssertModelUnchanged(reloaded);
        Assert.Equal(character, File.ReadAllBytes(f.CharacterPath));
        Assert.Equal(items, File.ReadAllBytes(f.ItemPath));
        if (type == 2) Assert.Empty(f.Sent);
        else Assert.Equal(SkinGolden(LegacyDragonSkinType.Retrieve, 1, false), Assert.Single(f.Sent));
        Assert.False(LegacyTradeJournal.HasPending(f.Users));
        Assert.DoesNotContain("PARITY-BUG", f.Log.ToString());
    });
}
