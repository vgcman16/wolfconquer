using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackDispatchTests
{
    private static TrainingProficiencyComparer SeedTrainingMetadata(Scope scope, Action? duringRightLookup = null)
    {
        scope.Attack.AtkType = 2; // Both the spell-associated and physical passes are reachable.
        scope.NextLevel(1000);
        scope.Rows[1].Level = 1;
        scope.Client.Equipment!.RightWeapon = 410009;
        scope.Client.Equipment.LeftWeapon = 420009;
        var comparer = new TrainingProficiencyComparer(duringRightLookup);
        scope.Client.MyProfs!.ClientProf = new ConcurrentDictionary<uint, LegacyNativeMsgProficiency>(comparer)
        {
            [410] = new() { ID = 410, Level = 5, Experience = 1000, NeededExperience = 1600000, PreviouseLevel = 3 },
            [420] = new() { ID = 420, Level = 5, Experience = 2000, NeededExperience = 1600000, PreviouseLevel = 4 },
        };
        scope.Native.Storage.Add(scope.Packet, 1000, 7); // Genuine pre-existing output, retained on refusal.
        return comparer;
    }

    private sealed class TrainingProficiencyComparer(Action? callback) : IEqualityComparer<uint>
    {
        internal bool Tracking, Armed, Triggered;
        internal List<uint> Lookups { get; } = [];
        public bool Equals(uint first, uint second) => first == second;
        public int GetHashCode(uint value)
        {
            if (Tracking) Lookups.Add(value);
            if (Armed && value == 410)
            {
                Armed = false;
                Triggered = true;
                callback?.Invoke();
            }
            return value.GetHashCode();
        }
    }

    private sealed class TrainingMetadataImage
    {
        private readonly Scope scope;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeEquipItemStorage equipment;
        private readonly LegacyNativeProficiencyStorage profs;
        private readonly LegacyNativeSpellStorage? currentSpells;
        private readonly ConcurrentDictionary<uint, LegacyNativeMsgProficiency> map;
        private readonly (uint Key, LegacyNativeMsgProficiency Row, uint[] Values)[] rows;
        private readonly LegacyNativeSpellMutationWitness spells;
        private readonly LegacyNativePacketMutationWitness packet;
        private readonly byte[][] priorFrames, priorBytes;
        internal int PriorFrameCount => priorFrames.Length;

        internal TrainingMetadataImage(Scope scope)
        {
            this.scope = scope;
            player = scope.Client.Player;
            equipment = scope.Client.Equipment!;
            profs = scope.Client.MyProfs!;
            currentSpells = scope.Client.MySpells;
            map = profs.ClientProf;
            rows = map.ToArray().Select(pair => (pair.Key, pair.Value, Values(pair.Value))).ToArray();
            spells = new(scope.Native.Storage);
            packet = new(scope.Packet);
            priorFrames = scope.Queue.ToArray();
            priorBytes = priorFrames.Select(frame => frame.ToArray()).ToArray();
        }

        internal void RequireSpellStateUnchanged()
        {
            Assert.Same(player, scope.Client.Player);
            Assert.Same(equipment, scope.Client.Equipment);
            Assert.Same(profs, scope.Client.MyProfs);
            Assert.Same(currentSpells, scope.Client.MySpells);
            spells.RequireUnchanged();
        }

        internal void RequireProficiencies(uint rightGain = 0, uint leftGain = 0)
        {
            Assert.Same(map, profs.ClientProf);
            var current = map.ToArray(); // Do not add comparer lookups while verifying its call history.
            Assert.Equal(rows.Select(row => row.Key), current.Select(pair => pair.Key));
            for (int index = 0; index < rows.Length; index++)
            {
                Assert.Same(rows[index].Row, current[index].Value);
                var expected = rows[index].Values.ToArray();
                expected[2] = checked(expected[2] + (rows[index].Key == 410 ? rightGain : leftGain));
                Assert.Equal(expected, Values(current[index].Value));
            }
        }

        internal void RequirePriorFrames(int appended)
        {
            Assert.Equal(priorFrames.Length + appended, scope.Queue.Count);
            for (int index = 0; index < priorFrames.Length; index++)
            {
                Assert.Same(priorFrames[index], scope.Queue.ElementAt(index));
                Assert.Equal(priorBytes[index], scope.Queue.ElementAt(index));
            }
        }

        internal void RequireUnchanged()
        {
            RequireSpellStateUnchanged();
            RequireProficiencies();
            RequirePriorFrames(0);
            packet.RequireUnchanged();
        }

        private static uint[] Values(LegacyNativeMsgProficiency row) =>
            [row.ID, row.Level, row.Experience, row.NeededExperience, row.PreviouseLevel];
    }
}
