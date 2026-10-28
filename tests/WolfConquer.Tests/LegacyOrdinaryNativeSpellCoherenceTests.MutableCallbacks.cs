using System.Collections;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CallerOwnedSnapshotAccessCannotThrowOrReenterAcrossStorePreparation(bool reenter, bool indexed)
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var current = f.Current; var storage = RequireCoherent(f);
            var oldMap = storage.ClientSpells; var oldRows = oldMap.ToArray(); var files = f.Files(); int sends = f.Sent.Count;
            var list = new CallbackSpellList(current.PlayerSpells.Spells.ToArray(), indexed);
            var snapshot = new PlayerSpellSnapshot(list); // Construct while callback is unarmed.
            var proposal = current with { PlayerSpells = snapshot };
            list.Callback = reenter
                ? () => f.Store.Set(f.Connection, current with { PlayerSpells = new PlayerSpellSnapshot([]) })
                : () => throw new InvalidOperationException("owned enumeration fault");
            list.Armed = true;
            bool committed = false;
            Exception? error = Record.Exception(() => committed = f.Store.TryCompareAndSet(f.Connection, current, proposal));
            Assert.True(list.Triggered); Assert.True(error is not null || !committed);
            Assert.Same(current, f.Current); Assert.Same(oldMap, storage.ClientSpells); Assert.Equal(oldRows, oldMap.ToArray());
            f.RequireFiles(files); Assert.Equal(sends, f.Sent.Count);
        });
    }

    private sealed class CallbackSpellList(PlayerSpellSnapshotEntry[] entries, bool indexed) : IReadOnlyList<PlayerSpellSnapshotEntry>
    {
        internal bool Armed, Triggered;
        internal Action? Callback;
        public int Count { get { if (!indexed) Trigger(); return entries.Length; } }
        public PlayerSpellSnapshotEntry this[int index] { get { if (indexed) Trigger(); return entries[index]; } }
        private void Trigger()
        {
            if (Armed) { Armed = false; Triggered = true; Callback!(); }
        }
        public IEnumerator<PlayerSpellSnapshotEntry> GetEnumerator()
        {
            Trigger();
            return ((IEnumerable<PlayerSpellSnapshotEntry>)entries).GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
