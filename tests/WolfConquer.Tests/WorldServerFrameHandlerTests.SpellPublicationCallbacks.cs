using System.Collections;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Action78RechecksActualHolderAfterSameValueLearnedCollectionCallbacks(bool recreateHolder)
    {
        using var fixture = new SavedSpellPublicationFixture(_usersRoot, alreadyCyclone: true);
        await fixture.Login();
        var original = fixture.Current;
        var rows = new SpellPublicationCallbackRows(original.PlayerSpells.Spells);
        fixture.Store.Set(fixture.Connection, original with { PlayerSpells = new PlayerSpellSnapshot(rows) });
        var admitted = fixture.Current;
        var files = fixture.Files();
        var first = fixture.Sent.Count;
        var callbacks = 0;
        fixture.ObserveSend = frame =>
        {
            if (!IsSpellFrame(frame) || fixture.Sent.Count != first) return;
            // Leave the immediate post-send check intact; exercise the next
            // actual pre-send membership check, where a stale pass could leak
            // the next prepared spell before another check catches retirement.
            rows.EnumerationsToSkip = 1;
            rows.BeforeEnumeration = () =>
            {
                callbacks++;
                if (!recreateHolder) return;
                Assert.True(fixture.Store.TryRemove(fixture.Connection, out var removed));
                Assert.Same(admitted, removed);
                fixture.Store.Set(fixture.Connection, admitted);
            };
        };

        var request = ConfirmSpellRequest();
        await fixture.Confirm(request);

        Assert.Equal(1, callbacks);
        Assert.Same(admitted, fixture.Current); // Same value/reference is not holder identity.
        Assert.Equal(first + (recreateHolder ? 1 : 3), fixture.Sent.Count);
        Assert.Equal(LearnedWire, fixture.Sent[first]);
        if (!recreateHolder)
        {
            Assert.Equal(LearnedCycloneWire, fixture.Sent[first + 1]);
            Assert.Equal(request, fixture.Sent[first + 2]);
        }
        fixture.AssertFiles(files);
    }

    [Fact]
    public async Task SpellPublicationBindingCannotAdoptAHolderRecreatedDuringLearnedValidation()
    {
        using var fixture = new SavedSpellPublicationFixture(_usersRoot, alreadyCyclone: true);
        await fixture.Login();
        var original = fixture.Current;
        var rows = new SpellPublicationCallbackRows(original.PlayerSpells.Spells);
        fixture.Store.Set(fixture.Connection, original with { PlayerSpells = new PlayerSpellSnapshot(rows) });
        var admitted = fixture.Current;
        Assert.True(LegacySpellPublicationPlan.TryPrepare(admitted.PlayerSpells, null, out var plan));
        Assert.NotNull(plan);
        var files = fixture.Files();
        var first = fixture.Sent.Count;
        var callbacks = 0;
        rows.BeforeEnumeration = () =>
        {
            callbacks++;
            Assert.True(fixture.Store.TryRemove(fixture.Connection, out var removed));
            Assert.Same(admitted, removed);
            fixture.Store.Set(fixture.Connection, admitted);
        };
        var field = typeof(WorldServerFrameHandler).GetField("_tradeStates", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var trades = Assert.IsType<LegacyTradeStateStore>(field.GetValue(fixture.Handler));

        Assert.False(LegacySpellPublicationOwner.TryBind(fixture.Store, trades, fixture.Connection, admitted, plan, out var owner));

        Assert.Null(owner);
        Assert.Equal(1, callbacks);
        Assert.Same(admitted, fixture.Current);
        Assert.Equal(first, fixture.Sent.Count);
        fixture.AssertFiles(files);
    }

    // A real public snapshot collection, not a replacement owner or saver. The
    // callback preserves every learned row and retires only genuine Store state.
    private sealed class SpellPublicationCallbackRows(IReadOnlyList<PlayerSpellSnapshotEntry> source)
        : IReadOnlyList<PlayerSpellSnapshotEntry>
    {
        internal Action? BeforeEnumeration;
        internal int EnumerationsToSkip;
        public int Count => source.Count;
        public PlayerSpellSnapshotEntry this[int index] => source[index];
        public IEnumerator<PlayerSpellSnapshotEntry> GetEnumerator()
        {
            if (BeforeEnumeration is not null && EnumerationsToSkip-- <= 0)
                Interlocked.Exchange(ref BeforeEnumeration, null)?.Invoke();
            return source.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
