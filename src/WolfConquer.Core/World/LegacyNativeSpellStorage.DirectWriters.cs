using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSpellStorage
{
    internal static void RequireDirectEntry(LegacyNativeGameClientRuntime client)
    {
        if (LegacyNativeNinja.HasActiveOwnedPreparation(client) ||
            client.MySpells is { } storage && Monitor.IsEntered(storage.MutationGate) ||
            client.MyProfs is { } profs && Monitor.IsEntered(profs.MutationGate) ||
            client.OrdinarySpellBinding is { } pin && (Monitor.IsEntered(pin.Storage.MutationGate) ||
                Monitor.IsEntered(pin.Proficiencies.MutationGate)))
            throw UnavailableNinjaSpells();
    }

    // Caller holds client/player then this actual gate. No earlier map/row is
    // accepted merely because it used to occupy the mutable public slot.
    internal DirectSpellSnapshot CaptureDirectSnapshot(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player)
    {
        RequireNinjaSpellOwner(client, player, player.UID);
        var map = ClientSpells;
        return new(this, client, player, player.UID, map);
    }

    private static LegacyNativeMsgSpell CopyDirectSpell(SpellValues value) => new()
    {
        ColdTime = value.ColdTime, IsSpellWithColdTime = value.WithColdTime, Size = value.Size,
        PacketType = value.PacketType, PacketStamp = value.PacketStamp, Experience = value.Experience,
        ID = value.Id, UnKnow = value.Unknown, Level = value.Level, Type = value.Type,
        UseSoul = value.UseSoul, Soul = value.Soul, StampSpell = value.Stamp, PreviousLevel = value.PreviousLevel,
        SoulLevel = value.SoulLevel, HashLevelSoul = value.HashLevelSoul,
    };

    internal sealed class DirectSpellSnapshot
    {
        private readonly LegacyNativeSpellStorage storage;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly uint uid;
        private readonly ConcurrentDictionary<ushort, LegacyNativeMsgSpell> map;
        private readonly SpellRow[] rows;
        private readonly Dictionary<ushort, LegacyNativeMsgSpell> expected;

        internal DirectSpellSnapshot(LegacyNativeSpellStorage storage, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player, uint uid, ConcurrentDictionary<ushort, LegacyNativeMsgSpell> map)
        {
            this.storage = storage; this.client = client; this.player = player; this.uid = uid; this.map = map;
            rows = CaptureNinjaSpellRows(map); expected = rows.ToDictionary(row => row.Key, row => row.Row);
        }

        internal void RequireCurrent()
        {
            storage.RequireNinjaSpellOwner(client, player, uid);
            if (!ReferenceEquals(storage.ClientSpells, map)) throw UnavailableNinjaSpells();
            RequireNinjaSpellRows(map, rows);
        }

        internal DirectSpellChange? Find(ushort id)
        {
            var found = CheckSpellLookup(map, expected, id); // Original comparer, before the last plain fence.
            RequireCurrent();
            if (!found) return null;
            var index = Array.FindIndex(rows, row => row.Key == id);
            return new(this, index);
        }

        internal LegacyNativeMsgSpell[] CopyValues() => rows.Select(row => CopyDirectSpell(row.Values)).ToArray();

        internal DirectSpellMap PrepareLoadedMap(LegacyNativeMsgSpell[] loaded)
        {
            foreach (var row in loaded)
            {
                if (row is null) throw UnavailableNinjaSpells();
                CheckSpellLookup(map, expected, row.ID);
            }
            var candidate = new ConcurrentDictionary<ushort, LegacyNativeMsgSpell>(map.Comparer);
            var merged = new Dictionary<ushort, LegacyNativeMsgSpell>(expected);
            foreach (var row in rows)
            {
                if (!candidate.TryAdd(row.Key, row.Row)) throw UnavailableNinjaSpells();
            }
            foreach (var row in loaded)
            {
                if (row is null) throw UnavailableNinjaSpells();
                if (merged.ContainsKey(row.ID)) continue; // Existing rows and first permitted file row win.
                if (!candidate.TryAdd(row.ID, row)) throw UnavailableNinjaSpells();
                merged.Add(row.ID, row);
            }
            RequireExpectedSpellMap(candidate, merged);
            _ = CaptureNinjaSpellRows(candidate);
            return new(this, candidate);
        }

        internal sealed class DirectSpellMap
        {
            private readonly DirectSpellSnapshot source;
            private readonly ConcurrentDictionary<ushort, LegacyNativeMsgSpell> candidate;
            internal DirectSpellMap(DirectSpellSnapshot source, ConcurrentDictionary<ushort, LegacyNativeMsgSpell> candidate)
            { this.source = source; this.candidate = candidate; }
            internal void Publish()
            {
                source.RequireCurrent();
                source.storage.ClientSpells = candidate;
            }
        }

        internal sealed class DirectSpellChange
        {
            private readonly DirectSpellSnapshot source;
            private readonly int index;
            private readonly SpellRow[] after;
            private IEnumerator<KeyValuePair<ushort, LegacyNativeMsgSpell>>? trainingBefore, trainingFinal;
            internal LegacyNativeMsgSpell Value { get; }
            internal DirectSpellChange(DirectSpellSnapshot source, int index)
            {
                this.source = source; this.index = index;
                after = (SpellRow[])source.rows.Clone();
                Value = CopyDirectSpell(source.rows[index].Values);
            }

            internal void PrepareSoul(LegacyNativeMsgSpell.UseSpellSoulTyp soul) =>
                after[index] = after[index] with { Values = after[index].Values with { UseSoul = soul } };
            internal void PrepareProgress(int experience, ushort level) =>
                after[index] = after[index] with { Values = after[index].Values with { Experience = experience, Level = level } };
            internal void RequireCurrent() => source.RequireCurrent();
            internal void ArmUnboundProgressProof()
            {
                RequireCurrent();
                if (source.client.OrdinarySpellBinding is not null || trainingBefore is not null || trainingFinal is not null)
                    throw UnavailableNinjaSpells();
                trainingBefore = source.map.GetEnumerator();
                trainingFinal = source.map.GetEnumerator();
            }
            internal bool ConsumeUnboundProgressProof()
            {
                try
                {
                    source.storage.RequireNinjaSpellOwner(source.client, source.player, source.uid);
                    return source.client.OrdinarySpellBinding is null &&
                        ReferenceEquals(source.storage.ClientSpells, source.map) &&
                        ConsumeFinalWitness(ref trainingBefore, source.rows) &&
                        ConsumeFinalWitness(ref trainingFinal, source.rows);
                }
                catch (InvalidOperationException) { return false; }
            }
            // Only the compound unbound training owner consumes the two raw
            // proofs for ALL participants before entering these plain stores.
            internal void PublishPreparedUnboundProgress()
            {
                source.rows[index].Row.Experience = after[index].Values.Experience;
                source.rows[index].Row.Level = after[index].Values.Level;
            }
            internal void CommitSoul()
            {
                RequireCurrent();
                source.rows[index].Row.UseSoul = after[index].Values.UseSoul;
            }
            internal void CommitProgress()
            {
                RequireCurrent();
                source.rows[index].Row.Experience = after[index].Values.Experience;
                source.rows[index].Row.Level = after[index].Values.Level;
            }
            internal bool MatchesAfter()
            {
                try
                {
                    source.storage.RequireNinjaSpellOwner(source.client, source.player, source.uid);
                    if (!ReferenceEquals(source.storage.ClientSpells, source.map)) return false;
                    RequireNinjaSpellRows(source.map, after);
                    return true;
                }
                catch (InvalidOperationException) { return false; }
            }
        }
    }
}
