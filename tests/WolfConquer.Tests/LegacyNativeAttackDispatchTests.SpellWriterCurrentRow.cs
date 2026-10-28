using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackDispatchTests
{
    [Fact]
    public Task TrainingMetadataReentryRefusesBeforeAnySpellOrPacketPublication() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Attack.AtkType = 0;
        scope.Row.Experience = 1000;
        var old = scope.ClientSpell;
        var comparer = new DirectWriterProbeComparer<ushort>(() =>
            scope.Native.Storage.Add(scope.Packet, old.ID, Experience: 77));
        var metadata = new Dictionary<ushort, LegacyNativeMagicType.Magic>(comparer)
            { [0] = scope.Row, [1] = new() { ID = old.ID } };
        comparer.Arm();

        Assert.Throws<InvalidOperationException>(() =>
            LegacyNativeUpdateSpell.CheckUpdate(scope.Packet, scope.Client, scope.Attack, 10, metadata));

        Assert.Equal(0, old.Experience);
        Assert.Equal((ushort)0, old.Level);
        var current = scope.Native.Storage.ClientSpells[old.ID];
        Assert.Same(old, current);
        Assert.Equal(0, current.Experience);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task ProficiencyReentryRefusesTheWholeTrainingBeforeAnyRowOrOutput() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Attack.AtkType = 0;
        scope.NextLevel(1000);
        scope.Client.Equipment!.RightWeapon = 410009;
        var old = scope.ClientSpell;
        var comparer = new DirectWriterProbeComparer<uint>(() =>
            scope.Native.Storage.Add(scope.Packet, old.ID, Experience: 77));
        scope.Client.MyProfs!.ClientProf = new ConcurrentDictionary<uint, LegacyNativeMsgProficiency>(comparer);
        Assert.True(scope.Client.MyProfs.ClientProf.TryAdd(410, new() { ID = 410, Level = 5 }));


        var spell = new LegacyNativeSpellMutationWitness(scope.Native.Storage);
        var packet = new LegacyNativePacketMutationWitness(scope.Packet);
        var prof = scope.Client.MyProfs.ClientProf[410];
        // Arm only after every fixture lookup: the subject must trigger reentry.
        comparer.Arm();

        Assert.Throws<InvalidOperationException>(() =>
            LegacyNativeUpdateSpell.CheckUpdate(scope.Packet, scope.Client, scope.Attack, 6, scope.Rows));

        Assert.Equal(0, old.Experience);
        Assert.Equal((ushort)0, old.Level);
        var current = scope.Native.Storage.ClientSpells[old.ID];
        Assert.Same(old, current);
        Assert.Equal(0, current.Experience);
        Assert.True(comparer.Triggered);
        Assert.Equal(0u, prof.Experience);
        Assert.Same(prof, scope.Client.MyProfs.ClientProf[410]);
        spell.RequireUnchanged(); packet.RequireUnchanged(); Assert.Empty(scope.Queue);
    });

    private sealed class DirectWriterProbeComparer<T> : IEqualityComparer<T> where T : notnull
    {
        private readonly Action action;
        private bool armed;
        internal DirectWriterProbeComparer(Action action) => this.action = action;
        internal bool Triggered { get; private set; }
        internal void Arm() => armed = true;
        public bool Equals(T? left, T? right) => EqualityComparer<T>.Default.Equals(left!, right!);
        public int GetHashCode(T value)
        {
            if (armed) { armed = false; Triggered = true; action(); }
            return EqualityComparer<T>.Default.GetHashCode(value);
        }
    }
}
