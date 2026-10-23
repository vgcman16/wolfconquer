using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDragonSkinTransactionTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public Task CommittedSendFailureDoesNotBecomeRejectionAndReplayCannotDuplicate(int type) => RunAsync(async f =>
    {
        f.Seed(type == 2 ? "0/0/" : "1/1/1/", [Item(9, 720_001), Item(10, Mapped(1))]);
        var before = Capture(f.Current);
        f.FailInventorySend = true;
        await f.InvokeAsync((LegacyDragonSkinType)type);
        AssertCommitted(f, before, type == 2 ? "0/1/1/" : "0/0/");
        Assert.Single(f.Sent);
        Assert.DoesNotContain(f.Sent, bytes => LegacyDragonSkinPacket.TryParse(Frame(bytes), out _));
        var committed = Capture(f.Current);
        var character = File.ReadAllBytes(f.CharacterPath);
        var items = File.ReadAllBytes(f.ItemPath);
        f.Sent.Clear(); f.FailInventorySend = false;
        await f.InvokeAsync((LegacyDragonSkinType)type);
        Assert.Same(committed.Session, f.Current); AssertModelUnchanged(committed);
        Assert.Equal(character, File.ReadAllBytes(f.CharacterPath));
        Assert.Equal(items, File.ReadAllBytes(f.ItemPath));
        if (type == 2) Assert.Empty(f.Sent);
        else Assert.Equal(SkinGolden(LegacyDragonSkinType.Retrieve, 1, false), Assert.Single(f.Sent));
        Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public Task PostCommitStoreRetirementSuppressesAcknowledgementWithoutUndoingFiles(int type) => RunAsync(async f =>
    {
        f.Seed(type == 2 ? "0/0/" : "1/1/1/", [Item(9, 720_001), Item(10, Mapped(1))]);
        var original = Capture(f.Original);
        var replacement = f.Original with { Character = f.Original.Character with { Money = 77 } };
        var replacementImage = Capture(replacement);
        f.OnInventorySend = () => f.Sessions.Set(f.Connection, replacement);
        await f.InvokeAsync((LegacyDragonSkinType)type);
        Assert.Same(replacement, f.Current); AssertModelUnchanged(original); AssertModelUnchanged(replacementImage);
        Assert.Single(f.Sent);
        Assert.DoesNotContain(f.Sent, bytes => LegacyDragonSkinPacket.TryParse(Frame(bytes), out _));
        var expected = type == 2 ? "0/1/1/" : "0/0/";
        Assert.Equal(expected, f.ReloadCharacter().Attributes["AnimaSkin"]);
        var persistedItems = PlayerItemSnapshotLoader.ToLegacyBytes(f.ReloadItems());
        Assert.NotEqual(f.ItemsBefore, persistedItems);
        Assert.DoesNotContain("rejected without commit", f.Log.ToString());
        f.Sent.Clear(); f.OnInventorySend = null;
        await f.InvokeAsync((LegacyDragonSkinType)type);
        Assert.Same(replacement, f.Current); Assert.Empty(f.Sent);
        Assert.Equal(expected, f.ReloadCharacter().Attributes["AnimaSkin"]);
        Assert.Equal(persistedItems, File.ReadAllBytes(f.ItemPath));
        Assert.Contains("dragonSkinCharacterBaselineStale", f.Log.ToString());
    });

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]
    public Task PresentationObserverPublicationThenRetirementAndThrowIsIndeterminateNotUnchanged(int type) => RunAsync(async f =>
    {
        var armed = false;
        WorldSession? observed = null;
        WorldSession? replacement = null;
        f.Sessions = new((connection, previous, current) =>
        {
            if (!armed || previous is null) return;
            armed = false; observed = current;
            f.Sessions.Set(connection, replacement!);
            throw new IOException("owned presentation observer retired published session");
        });
        f.Seed(type == 5 ? "1/1/1/" : "0/1/1/");
        var original = Capture(f.Original);
        replacement = f.Original with { Character = f.Original.Character with { Money = 77 },
            PlayerItems = f.Original.PlayerItems with { InventoryItems = [Item(77, 720_002)] } };
        var replacementImage = Capture(replacement);
        armed = true;
        await f.InvokeAsync((LegacyDragonSkinType)type);
        Assert.NotNull(observed); Assert.NotSame(f.Original, observed);
        Assert.Same(replacement, f.Current); AssertModelUnchanged(original); AssertModelUnchanged(replacementImage);
        AssertFilesUnchanged(f);
        Assert.Contains("indeterminate; presentation publication unproven", f.Log.ToString());
        Assert.Contains("dragonSkinPresentationPublicationUnproven:IOException", f.Log.ToString());
        Assert.DoesNotContain("rejected without commit", f.Log.ToString());
        if (type == 0)
        {
            // MyShow legitimately publishes its owned-unlock prefix before the
            // runtime loaded-marker CAS; no final MyShow may follow retirement.
            Assert.Equal(SkinGolden(LegacyDragonSkinType.Unlock, 1, true), Assert.Single(f.Sent));
            Assert.Equal("1", observed.Character.Attributes["RuntimeAnimaSkinLoaded"]);
        }
        else Assert.Empty(f.Sent);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]
    public Task PresentationWithStaleSuppliedOwnerCannotOverwriteNewInventory(int type) => RunAsync(async f =>
    {
        f.Seed("1/1/1/");
        var original = Capture(f.Original);
        var replacement = f.Original with { PlayerItems = f.Original.PlayerItems with { InventoryItems = [Item(77, 720_002)] } };
        f.Sessions.Set(f.Connection, replacement);
        var before = Capture(replacement);
        await f.InvokeAsync((LegacyDragonSkinType)type, supplied: f.Original);
        AssertRejected(f, before, "dragonSkinCurrentOwnerUnavailable");
        AssertModelUnchanged(original);
    });
}
