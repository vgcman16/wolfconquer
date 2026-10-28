using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyPerformerDanceLearningTests
{
    [Fact]
    public async Task LateSubclassDictionaryLookupCannotHideAChangedActualSourceSpellRow()
    {
        await WithDurableDanceAsync(async fixture =>
        {
            var connection = fixture.Connection();
            var store = fixture.Store();
            var source = new[] { CreateExistingSpell(), new PlayerSpellSnapshotEntry(901, 6, 2345, 2, 3, 2) };
            var changed = new PlayerSpellSnapshotEntry(901, 7, 3456, 3, 2, 1);
            var comparer = new LatePerformerAttributeComparer(() => source[1] = changed);
            var initial = fixture.Session();
            var attributes = new Dictionary<string, string>(initial.Character.Attributes, comparer);
            var original = initial with
            {
                Character = initial.Character with { Attributes = attributes },
                PlayerSpells = new PlayerSpellSnapshot(source),
            };
            store.Set(connection, original);
            fixture.Seed(CharacterUid, original.PlayerSpells);
            var before = new DurableSessionImage(original);
            comparer.Arm();
            var handled = false;
            var error = await Record.ExceptionAsync(async () =>
            { handled = await fixture.InvokeAsync(connection, store, original); });

            fixture.RequireFilesUnchanged();
            before.RequireResources(original);
            Assert.Same(source, original.PlayerSpells.Spells);
            Assert.Equal(new[] { CreateExistingSpell(), changed }, source);
            Assert.True(store.TryGet(connection, out var current));
            Assert.Same(original, current);
            Assert.Empty(fixture.Sent);
            Assert.True(comparer.Triggered);
            Assert.Equal(5, comparer.Reads);
            Assert.True(handled);
            Assert.Null(error);
            Assert.Contains("reason=sessionChanged", fixture.Log.ToString());
        });
    }

    // A real Dictionary comparer performs the caller mutation on the final
    // SubProfInfo lookup inside the real Store/file commit path.
    private sealed class LatePerformerAttributeComparer(Action change) : IEqualityComparer<string>
    {
        private bool armed;
        internal bool Triggered { get; private set; }
        internal int Reads { get; private set; }
        internal void Arm() => armed = true;
        public bool Equals(string? left, string? right) => StringComparer.OrdinalIgnoreCase.Equals(left, right);
        public int GetHashCode(string value)
        {
            if (armed && StringComparer.OrdinalIgnoreCase.Equals(value, "SubProfInfo"))
            {
                Reads++;
                if (Reads == 5) { armed = false; Triggered = true; change(); }
            }
            return StringComparer.OrdinalIgnoreCase.GetHashCode(value);
        }
    }
}
