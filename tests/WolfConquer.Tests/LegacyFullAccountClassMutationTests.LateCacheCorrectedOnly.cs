using System.Collections;
using System.Collections.ObjectModel;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyFullAccountClassMutationTests
{
    // Corrected-pipeline qualification only. These existing APIs compile on the
    // old source, but an unfired old-pipeline hook is not proof of this later seam.
    [Fact]
    public async Task LatePaidDictionaryAcquisitionCannotCommitFramesFromTheEarlierRealMagicCache()
    {
        await WithDurableClassAsync(async fixture =>
        {
            Assert.Equal(139, fixture.Original.Character.Level);
            var initial = default(LegacyNativeMagicQueryFields);
            var winner = default(LegacyNativeMagicQueryFields);
            object identity = null!;
            var armed = false;
            var gatedAcquisitions = 0;
            var triggered = false;
            var pinGateHeld = false;
            var sameSession = false;
            var noPending = false;
            var cacheUpdated = false;
            var cacheAtTrigger = default(LegacyNativeMagicQueryFields);
            var backing = new Dictionary<string, string>(fixture.Original.Character.PaidNobility, StringComparer.OrdinalIgnoreCase);
            var wrapper = new ReadOnlyDictionary<string, string>(new MagicCachePaidDictionary(backing, () =>
            {
                if (!armed || !WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate) return;
                gatedAcquisitions++;
                if (gatedAcquisitions != 2) return;
                armed = false; triggered = true;
                pinGateHeld = Monitor.IsEntered(LegacySecondaryPasswordRuntime.SyncRoot);
                sameSession = fixture.Store.TryGet(fixture.Connection, out var current) && ReferenceEquals(current, fixture.Original);
                noPending = !LegacyTradeJournal.HasPending(fixture.Users);
                cacheAtTrigger = LegacyNativeMagicQueryClient.Read(fixture.Store, fixture.Connection,
                    fixture.Original.Character.Uid, identity);
                cacheUpdated = LegacyNativeMagicQueryClient.Update(fixture.Store, fixture.Connection,
                    fixture.Original.Character.Uid, _ => winner, identity);
            }));
            fixture.UsePaidNobility(wrapper);
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(fixture.Store, fixture.Connection,
                fixture.Original.Character.Uid, out identity));
            Assert.True(LegacyNativeMagicQueryClient.Reset(fixture.Store, fixture.Connection, fixture.Original.Character.Uid, identity));
            Assert.True(LegacyNativeMagicQueryClient.TryRead(fixture.Store, fixture.Connection,
                fixture.Original.Character.Uid, out initial, identity));
            Assert.Equal((uint)fixture.Original.Character.Spirit, initial.MagicAttack);
            winner = initial with { MagicAttack = initial.MagicAttack + 1 };
            var before = new DurableClassImage(fixture.Original);
            armed = true;
            var handled = false;
            var error = await Record.ExceptionAsync(async () =>
            { handled = await fixture.InvokeAsync(fixture.Original); });

            // PaidNobility is acquired only by Inputs.Matches: under the real
            // Store gate its first witness precedes metadata/frame comparison,
            // and its second witness follows that comparison, inside the PIN gate.
            Assert.True(triggered); Assert.Equal(2, gatedAcquisitions);
            Assert.True(pinGateHeld); Assert.True(sameSession); Assert.True(noPending);
            Assert.Equal(initial, cacheAtTrigger); Assert.True(cacheUpdated);
            Assert.Equal(winner, LegacyNativeMagicQueryClient.Read(fixture.Store, fixture.Connection,
                fixture.Original.Character.Uid, identity));
            fixture.RequireFilesUnchanged();
            before.RequireUnchanged();
            Assert.True(fixture.Store.TryGet(fixture.Connection, out var stored));
            Assert.Same(fixture.Original, stored);
            Assert.Empty(fixture.Sent); Assert.False(fixture.Disconnected);
            Assert.True(handled); Assert.Null(error);
        }, level: 139);
    }

    private sealed class MagicCachePaidDictionary(Dictionary<string, string> values, Action acquire) : IDictionary<string, string>
    {
        public string this[string key] { get => values[key]; set => values[key] = value; }
        public ICollection<string> Keys => values.Keys;
        public ICollection<string> Values => values.Values;
        public int Count => values.Count;
        public bool IsReadOnly => false;
        public void Add(string key, string value) => values.Add(key, value);
        public bool ContainsKey(string key) => values.ContainsKey(key);
        public bool Remove(string key) => values.Remove(key);
        public bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
        public void Add(KeyValuePair<string, string> item) => ((ICollection<KeyValuePair<string, string>>)values).Add(item);
        public void Clear() => values.Clear();
        public bool Contains(KeyValuePair<string, string> item) => ((ICollection<KeyValuePair<string, string>>)values).Contains(item);
        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<string, string>>)values).CopyTo(array, arrayIndex);
        public bool Remove(KeyValuePair<string, string> item) => ((ICollection<KeyValuePair<string, string>>)values).Remove(item);
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        { acquire(); return values.GetEnumerator(); } // The actual BCL Dictionary enumerator, never an accepting substitute.
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
