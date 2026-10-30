using System.Collections;
using System.Collections.ObjectModel;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeItemSpellCallerFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedItemRepairTests
{
    [Fact]
    public Task ReadonlyWarehouseInputEnumerationCallbackCannotHideNativeDriftBeforeTheActualStoreWriter() =>
        WithWarehouseRepair(false, async f =>
        {
            await f.Login(); var before = f.Current; var paid = PaidProposal(before);
            var values = new Dictionary<string, string>(paid.Character.Attributes, StringComparer.OrdinalIgnoreCase);
            bool callback = false;
            var probe = new WarehouseInputDictionary(values)
            {
                OnEnumeration = () =>
                {
                    Assert.True(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                    Assert.True(Monitor.IsEntered(f.Native.SpellAdmissionSyncRoot));
                    Assert.True(Monitor.IsEntered(f.Native.Player.SpellAdmissionSyncRoot));
                    Assert.Same(before, f.Current);
                    f.Native.Player.WHMoney = 556; callback = true;
                },
            };
            var proposal = paid with { Character = paid.Character with
                { Attributes = new ReadOnlyDictionary<string, string>(probe) } };
            var files = f.Files(); int first = f.Sent.Count, writes = 0;
            bool UnexpectedWriter() { writes++; throw new InvalidOperationException("Warehouse input callback reached persistence."); }
            try
            {
                Assert.Throws<InvalidOperationException>(() =>
                    f.Store.TryCommitSession(f.Connection, before, proposal, UnexpectedWriter));

                Assert.True(callback); Assert.Equal(1, probe.Enumerations);
                Assert.Equal(0, writes); Assert.Same(before, f.Current);
                Assert.Equal(556L, f.Native.Player.WHMoney); Assert.Equal(1000L, f.Native.Player.Money);
                Assert.Equal((ushort)5, RepairedTarget(f.Current).Durability);
                Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
                Assert.False(LegacyTradeJournal.HasPending(f.Users));
            }
            finally { f.Native.Player.WHMoney = 555; }
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task WarehouseAliasMapValueDriftAfterTheRealRepairWriterRetiresEvenWithoutDictionaryVersionChange(bool proposedMap) =>
        WithWarehouseRepair(false, async f =>
        {
            await f.Login(); var before = f.Current; var proposal = PaidProposal(before);
            var expectedValues = Assert.IsType<Dictionary<string, string>>(before.Character.Attributes);
            var proposedValues = Assert.IsType<Dictionary<string, string>>(proposal.Character.Attributes);
            Assert.True(LegacyItemRepairPersistence.TryPrepare(before, proposal, f.Items, Fixture.ItemUid,
                LegacyItemRepairPayment.Money, out var write, out var reason), reason);
            Assert.NotNull(write); LegacyItemRepairOutcome? outcome = null; int first = f.Sent.Count;
            bool ActualWriterThenAliasFault()
            {
                outcome = write!.TryCommit(out reason);
                if (outcome == LegacyItemRepairOutcome.Committed)
                    (proposedMap ? proposedValues : expectedValues)["WarehouseMoney"] = "556";
                return outcome == LegacyItemRepairOutcome.Committed;
            }

            Assert.Throws<LegacyCommittedSpellPublicationException>(() =>
                f.Store.TryCommitSession(f.Connection, before, proposal, ActualWriterThenAliasFault));

            Assert.Equal(LegacyItemRepairOutcome.Committed, outcome);
            Assert.False(f.Store.TryGet(f.Connection, out _)); Assert.True(f.Recovery.IsBlocked);
            Assert.True(f.Native.OrdinarySpellBinding!.IsRetired);
            Assert.Equal("556", (proposedMap ? proposedValues : expectedValues)["WarehouseMoney"]);
            Assert.Equal(555L, f.Native.Player.WHMoney); Assert.Equal(1000L, f.Native.Player.Money);
            Assert.Equal(first, f.Sent.Count); Assert.False(LegacyTradeJournal.HasPending(f.Users));
            AssertPersistedWarehouseRepair(f);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualRepairAcceptsBclReadonlyWarehouseMapsAndDoesNotEnumerateTheirWrapperAfterTheWriter(bool forwardingDictionary) =>
        WithWarehouseRepair(false, async f =>
        {
            await f.Login(); var before = f.Current; var paid = PaidProposal(before);
            var values = new Dictionary<string, string>(paid.Character.Attributes, StringComparer.OrdinalIgnoreCase);
            var probe = new WarehouseInputDictionary(values);
            IDictionary<string, string> backing = forwardingDictionary ? probe : values;
            var proposal = paid with { Character = paid.Character with
                { Attributes = new ReadOnlyDictionary<string, string>(backing) } };
            Assert.True(LegacyItemRepairPersistence.TryPrepare(before, proposal, f.Items, Fixture.ItemUid,
                LegacyItemRepairPayment.Money, out var write, out var reason), reason);
            Assert.NotNull(write); LegacyItemRepairOutcome? outcome = null;
            int first = f.Sent.Count, enumerationsAtWriter = -1;
            bool ActualWriter()
            {
                enumerationsAtWriter = probe.Enumerations;
                outcome = write!.TryCommit(out reason);
                if (outcome == LegacyItemRepairOutcome.Committed) probe.RefuseEnumeration = true;
                return outcome == LegacyItemRepairOutcome.Committed;
            }
            try
            {
                Assert.True(f.Store.TryCommitSession(f.Connection, before, proposal, ActualWriter, out var final), reason);

                Assert.Equal(LegacyItemRepairOutcome.Committed, outcome);
                Assert.Same(final, f.Current); Assert.False(f.Recovery.IsBlocked);
                Assert.Equal(555L, f.Native.Player.WHMoney); Assert.Equal(943L, f.Native.Player.Money);
                Assert.Equal("555", f.Current.Character.Attributes["WHMoney"]);
                Assert.Equal("555", f.Current.Character.Attributes["WarehouseMoney"]);
                Assert.Equal(enumerationsAtWriter, probe.Enumerations);
                if (forwardingDictionary) Assert.True(enumerationsAtWriter > 0);
                Assert.Equal(first, f.Sent.Count); // This actual Store capability itself emits no repair reply.
                Assert.False(LegacyTradeJournal.HasPending(f.Users));
                AssertPersistedWarehouseRepair(f);
            }
            finally { probe.RefuseEnumeration = false; }
        });

    private static void AssertPersistedWarehouseRepair(Fixture f)
    {
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(
            File.ReadAllBytes(f.CharacterPath), out _));
        Assert.Equal(943L, saved.Money); Assert.Equal("555", saved.Attributes["WHMoney"]);
        Assert.Equal("555", saved.Attributes["WarehouseMoney"]);
        Assert.Equal((ushort)10, Assert.Single(new PlayerItemSnapshotLoader(f.Items)
            .LoadByCharacterUid(Fixture.Uid).InventoryItems).Durability);
    }

    // This is only a caller-input fault probe. Its enumerator is the real BCL
    // Dictionary struct required by the closed native participant; no owner,
    // saver, validator, UID reservation or native queue is substituted.
    private sealed class WarehouseInputDictionary(Dictionary<string, string> values) : IDictionary<string, string>
    {
        internal Action? OnEnumeration { get; set; }
        internal bool RefuseEnumeration { get; set; }
        internal int Enumerations { get; private set; }
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
        {
            Enumerations++;
            if (RefuseEnumeration) throw new InvalidOperationException("Native final proof invoked a caller-input wrapper.");
            var callback = OnEnumeration; OnEnumeration = null; callback?.Invoke();
            return values.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
