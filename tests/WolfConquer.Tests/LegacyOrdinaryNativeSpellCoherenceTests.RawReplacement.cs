using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public async Task RawReplacementOfAdmittedMapCannotBeBlessedByAnExpectedSessionWriter()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var before = f.Current; var storage = RequireCoherent(f);
            var original = storage.ClientSpells;
            var replacement = new ConcurrentDictionary<ushort, LegacyNativeMsgSpell>(original.ToArray(), new ProbeComparer());
            storage.ClientSpells = replacement;
            var files = f.Files(); int sent = f.Sent.Count;
            var changed = before with { PlayerSpells = new PlayerSpellSnapshot(before.PlayerSpells.Spells
                .Select(s => s.Id == 6001 ? s with { Experience = 2222 } : s).ToArray()) };
            Assert.Throws<InvalidOperationException>(() => f.Store.TryCompareAndSet(f.Connection, before, changed));
            Assert.Same(before, f.Current); Assert.Same(replacement, storage.ClientSpells);
            f.RequireFiles(files); Assert.Equal(sent, f.Sent.Count);
        });
    }
}
