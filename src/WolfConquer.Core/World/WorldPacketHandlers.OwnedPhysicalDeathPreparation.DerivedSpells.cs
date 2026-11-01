using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed record OwnedPhysicalDeathDerivedSpellChange(ushort Id, PlayerSpellSnapshotEntry? After);

    internal sealed partial class OwnedPhysicalDeathPreparation
    {
        private IReadOnlyList<OwnedPhysicalDeathDerivedSpellChange> derivedSpells =
            Array.AsReadOnly(Array.Empty<OwnedPhysicalDeathDerivedSpellChange>());

        internal bool TryReadDerivedSpellChanges(LegacyOrdinarySpellBinding binding,
            out IReadOnlyList<OwnedPhysicalDeathDerivedSpellChange> changes)
        {
            changes = derivedSpells;
            return IsCurrentPlain(binding);
        }

        private sealed partial class OwnedPhysicalDeathBuilder
        {
            private readonly List<OwnedPhysicalDeathDerivedSpellChange> derivedSpells = [];

            private void CalculateDerivedSpells()
            {
                if (Data is { } data && current.Character.Class / 1000 is 2 or 4 or 7 or 13 or 14)
                {
                    var overall = CalculatePirateRank(data.CombatGearRanks, current.Character.Uid, 42);
                    var weekly = CalculatePirateRank(data.CombatGearRanks, current.Character.Uid, 40);
                    if (current.Character.Class is >= 7_000 and <= 7_051 &&
                        (overall is >= 1 and <= 10 || weekly is >= 1 and <= 10))
                    {
                        var rank = weekly is >= 1 and <= 10 ? weekly : overall;
                        SetDerivedSpell(21_930, checked((ushort)(11 - rank)));
                    }
                    else RemoveDerivedSpell(21_930);
                }
                else if (LegacyDerivedSpellOrigins.Read(current.Character).ContainsKey(21_930))
                    RemoveDerivedSpell(21_930); // A real temporary rank grant cannot outlive its eligible class.
                metadata.Extend(current);
                AddMana(LegacyEquipmentNativeMana.CombatGear(current, Data));
                AddMagic(LegacyPlayerStatusCalculator.NativeMagicGear(current, Data));
                byte venom = 0;
                foreach (var item in equipment)
                {
                    var level = (byte)(item.MythSoul % 10);
                    if (item.MythSoul != 0 && item.MythSoul % 1000 - level == 10)
                        venom = checked((byte)(venom + level));
                }
                if (venom != 0) SetDerivedSpell(20_000, venom);
                else RemoveDerivedSpell(20_000);
                metadata.Extend(current);
                AddMagic(LegacyPlayerStatusCalculator.NativeMagicMythsoul(current, Data));
            }

            private void SetDerivedSpell(ushort id, ushort level)
            {
                var entry = new PlayerSpellSnapshotEntry(id, level, 0, 0, 0, 0);
                var reconciled = LegacyDerivedSpellReconciliation.Project(current.Character, current.PlayerSpells,
                    new[] { new LegacyDerivedSpellDesiredRow(id, entry) });
                if (reconciled.Changes.Count == 0) return; // Existing unmarked membership belongs to another producer.
                if (Data is not { } data || !LegacyMagicRuntime.TryResolveSpell(data, id, level, out _))
                    throw InvalidOwnedPhysicalDeath();
                metadata.RequireDerivedSpell(id, level);
                current = current with { Character = LegacyDerivedSpellOrigins.WithOrigins(current.Character,
                    LegacyDerivedSpellOrigins.Read(reconciled.Character)), PlayerSpells = reconciled.Spells };
                derivedSpells.Add(new(id, entry));
                AppendSelf(LegacySpellPacket.FromSnapshot(entry).ToFrameBytes());
            }

            private void RemoveDerivedSpell(ushort id)
            {
                var reconciled = LegacyDerivedSpellReconciliation.Project(current.Character, current.PlayerSpells,
                    new[] { new LegacyDerivedSpellDesiredRow(id, null) });
                if (reconciled.Changes.Count == 0) return;
                current = current with { Character = LegacyDerivedSpellOrigins.WithOrigins(current.Character,
                    LegacyDerivedSpellOrigins.Read(reconciled.Character)), PlayerSpells = reconciled.Spells };
                derivedSpells.Add(new(id, null));
                AppendSelf(new MsgActionPacket { ObjId = Uid, DwParam = id, Type = (MsgActionType)109 }.ToFrameBytes());
            }

            private static uint CalculatePirateRank(IReadOnlyList<CombatGearRankEntry> rows, long uid, byte type)
            {
                // Loaded UpdateItem retains top10 after each row, preserving
                // stable ties and replacement behavior rather than final-sort.
                var ranking = new Dictionary<uint, uint>();
                foreach (var row in rows)
                {
                    if (row.CharacterId < 0 || row.Fields.Count < 4 ||
                        !byte.TryParse(row.Fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kind) ||
                        kind != type || !uint.TryParse(row.Fields[3], NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out var points)) continue;
                    ranking[unchecked((uint)row.CharacterId)] = points;
                    ranking = ranking.OrderByDescending(pair => pair.Value).Take(10)
                        .ToDictionary(pair => pair.Key, pair => pair.Value);
                }
                int index = Array.IndexOf(ranking.Keys.ToArray(), unchecked((uint)uid));
                return index < 0 ? 0U : unchecked((uint)(index + 1));
            }
        }
    }
}
