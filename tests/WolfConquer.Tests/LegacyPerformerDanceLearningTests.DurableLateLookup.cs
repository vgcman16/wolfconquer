using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPerformerDanceLearningTests
{
    [Fact]
    public async Task UnsupportedSubclassComparerRefusesAtStoreProofBeforeItsLateSourceMutation()
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
            var accepted = initial with { PlayerSpells = original.PlayerSpells };
            store.Set(connection, accepted);
            fixture.Seed(CharacterUid, original.PlayerSpells);
            var before = new DurableSessionImage(accepted);
            comparer.Arm();
            var error = Assert.Throws<InvalidOperationException>(() => store.Set(connection, original));

            fixture.RequireFilesUnchanged();
            before.RequireUnchanged();
            Assert.Same(source, original.PlayerSpells.Spells);
            Assert.Equal(new[] { CreateExistingSpell(), new PlayerSpellSnapshotEntry(901, 6, 2345, 2, 3, 2) }, source);
            Assert.True(store.TryGet(connection, out var current));
            Assert.Same(accepted, current);
            Assert.Empty(fixture.Sent);
            Assert.False(comparer.Triggered);
            // The callback comparer is refused by the continuation input proof
            // before the NPC phase; it cannot exercise the former late lookup.
            Assert.Equal(0, comparer.Reads);
            Assert.Equal("experienceContinuationPendingRequiresOwner", error.Message);
            Assert.False(fixture.Recovery.IsBlocked);
            Assert.False(LegacyTradeJournal.HasPending(fixture.UsersRoot));
            Assert.Null(connection.NativeGameClient); Assert.Null(original.NativeGameClient);
            Assert.DoesNotContain("WorldServer handled LegacyNpc Performer dance learning", fixture.Log.ToString());
            Assert.DoesNotContain("reason=sessionChanged", fixture.Log.ToString());
        });
    }

    // Declared negative caller code. Its formerly fifth lookup must never run
    // now that the actual Store requires a supported BCL attribute comparer.
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
