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
    [InlineData(0, false)]
    [InlineData(4, false)]
    [InlineData(5, false)]
    [InlineData(0, true)]
    [InlineData(4, true)]
    [InlineData(5, true)]
    public Task PresentationObserverCannotReplaceTheCommittedFinalAndObserverFaultDoesNotUndoSuccess(
        int type, bool throwIoAfterRefusedReentry) => RunAsync(async f =>
    {
        var armed = false;
        WorldSession? observed = null;
        WorldSession? observedPrevious = null;
        WorldSession? replacement = null;
        InvalidOperationException? reentryRefusal = null;
        var observerCalls = 0;
        var framesAtObserver = -1;
        var observedGate = false;
        var ioReached = false;
        f.Sessions = new((connection, previous, current) =>
        {
            if (!armed || previous is null) return;
            armed = false;
            observerCalls++;
            observed = current;
            observedPrevious = previous;
            observedGate = WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate;
            framesAtObserver = f.Sent.Count;
            reentryRefusal = Assert.Throws<InvalidOperationException>(() => f.Sessions.Set(connection, replacement!));
            Assert.Same(current, f.Current);
            if (throwIoAfterRefusedReentry)
            {
                ioReached = true;
                throw new IOException("owned observer fault after refused nested replacement");
            }
            throw reentryRefusal;
        });
        f.Seed(type == 5 ? "1/1/1/" : "0/1/1/");
        var original = Capture(f.Original);
        replacement = f.Original with { Character = f.Original.Character with { Money = 77 },
            PlayerItems = f.Original.PlayerItems with { InventoryItems = [Item(77, 720_002)] } };
        var replacementImage = Capture(replacement);
        armed = true;
        await f.InvokeAsync((LegacyDragonSkinType)type);
        Assert.Equal(1, observerCalls);
        Assert.True(observedGate);
        Assert.IsType<InvalidOperationException>(reentryRefusal);
        Assert.Equal(throwIoAfterRefusedReentry, ioReached);
        Assert.Same(f.Original, observedPrevious);
        Assert.NotNull(observed);
        Assert.NotSame(f.Original, observed);
        Assert.Same(observed, f.Current);
        Assert.NotSame(replacement, f.Current);
        Assert.Equal(f.Original with { Character = f.Current.Character }, f.Current);
        Assert.Same(f.Original.PlayerItems, f.Current.PlayerItems);
        Assert.Same(f.Original.PlayerSpells, f.Current.PlayerSpells);
        Assert.Same(f.Original.PlayerProficiencies, f.Current.PlayerProficiencies);
        Assert.Equal(f.Original.Character.Money, f.Current.Character.Money);
        AssertModelUnchanged(original);
        AssertModelUnchanged(replacementImage);
        AssertFilesUnchanged(f);
        Assert.False(f.Recovery.IsBlocked);
        Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
        Assert.DoesNotContain("indeterminate", f.Log.ToString());
        Assert.DoesNotContain("dragonSkinPresentationPublicationUnproven", f.Log.ToString());
        Assert.DoesNotContain("rejected without commit", f.Log.ToString());
        Assert.DoesNotContain("WorldServer persisted LegacyDragonSkin", f.Log.ToString());
        AssertPresentationObserverWireAndFields(f, type, framesAtObserver);
    });

    private static void AssertPresentationObserverWireAndFields(Fixture f, int type, int framesAtObserver)
    {
        var expectedFields = new Dictionary<string, string>(f.Original.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        if (type == 0)
        {
            // The owned-unlock prefix precedes the marker commit. Its final
            // MyShow still follows because the observer could not retire it.
            Assert.Equal(1, framesAtObserver);
            expectedFields["RuntimeAnimaSkinLoaded"] = "1";
            Assert.Equal(2, f.Sent.Count);
            Assert.Equal(SkinGolden(LegacyDragonSkinType.Unlock, 1, true), f.Sent[0]);
            Assert.Equal(SkinGolden(LegacyDragonSkinType.MyShow, 0, false), f.Sent[1]);
            Assert.Contains("WorldServer sent LegacyDragonSkin MyShow", f.Log.ToString());
        }
        else
        {
            Assert.Equal(0, framesAtObserver);
            expectedFields["AnimaSkin"] = type == 4 ? "1/1/1/" : "0/1/1/";
            Assert.Equal(type == 4 ? SkinGolden(LegacyDragonSkinType.MyShow, 1, false)
                : SkinGolden(LegacyDragonSkinType.Restore, 0, true), Assert.Single(f.Sent));
            Assert.Contains(type == 4 ? "WorldServer selected LegacyDragonSkin"
                : "WorldServer restored LegacyDragonSkin", f.Log.ToString());
        }
        Assert.Equal(f.Original.Character with { Attributes = f.Current.Character.Attributes }, f.Current.Character);
        Assert.Equal(expectedFields.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            f.Current.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

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
