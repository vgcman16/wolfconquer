using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class LegacyEquipmentQueryExecution
    {
        private async ValueTask<bool> RunNativeDerivedSpellsAsync()
        {
            if (!Refresh()) return false;
            // Native CombatGear.Status precedes LoadGearMythsoulBag, VIP HP and ServerInfo.
            // Catalog rows describe the loaded ranking snapshot, not live rank-update/history ownership.
            if (Context.WorldData is { } data && Session.Character.Class / 1000 is 2 or 4 or 7 or 13 or 14)
            {
                var overall = NativePirateRank(data.CombatGearRanks, Session.Character.Uid, 42);
                var weekly = NativePirateRank(data.CombatGearRanks, Session.Character.Uid, 40);
                if (Session.Character.Class is >= 7_000 and <= 7_051 &&
                    (overall is >= 1 and <= 10 || weekly is >= 1 and <= 10))
                {
                    // Source overwrites myRank with WEEK before assigning MagicLevel; overall-only means0.
                    if (!await SetNativeDerivedSpellAsync(21_930,
                            weekly is >= 1 and <= 10 ? (ushort)(11 - weekly) : (ushort)0).ConfigureAwait(false))
                        return false;
                }
                else if (!await RemoveNativeDerivedSpellAsync(21_930).ConfigureAwait(false)) return false;
            }

            if (!Refresh() || !AddManaMaximum(LegacyEquipmentNativeMana.CombatGear(Session, Context.WorldData)))
                return false; // Native Status570–589 AFTER rank-spell await, BEFORE LoadGearMythsoulBag1553.
            byte venom = 0;
            foreach (var item in CapturedEquipment)
            {
                var level = (byte)(item.MythSoul % 10);
                if (item.MythSoul != 0 && item.MythSoul % 1000 - level == 10)
                    venom = unchecked((byte)(venom + level));
            }
            return venom != 0
                ? await SetNativeDerivedSpellAsync(20_000, venom).ConfigureAwait(false)
                : await RemoveNativeDerivedSpellAsync(20_000).ConfigureAwait(false);
        }

        private async ValueTask<bool> SetNativeDerivedSpellAsync(ushort id, ushort level)
        {
            var entry = new PlayerSpellSnapshotEntry(id, level, Experience: 0,
                PreviousLevel: 0, SoulLevel: 0, UseSpellSoul: 0);
            if (!Commit(session => session with
                    { PlayerSpells = LegacySkillBookUse.AddOrUpdateSpell(session.PlayerSpells, entry) })) return false;
            return await SendAsync(LegacySpellPacket.FromSnapshot(entry).ToFrameBytes(),
                LegacyEquipmentQueryPublishedPhases.None).ConfigureAwait(false);
        }

        private async ValueTask<bool> RemoveNativeDerivedSpellAsync(ushort id)
        {
            if (!Refresh()) return false;
            if (!Session.PlayerSpells.ContainsSpell(id)) return true;
            if (!Commit(session => session with { PlayerSpells = new PlayerSpellSnapshot(
                    session.PlayerSpells.Spells.Where(entry => entry.Id != id).ToArray()) })) return false;
            return await SendAsync(new MsgActionPacket
                {
                    ObjId = CheckedUInt32(Request.Scope.OwnerUid, nameof(Request.Scope.OwnerUid)),
                    DwParam = id, Type = (MsgActionType)109,
                }.ToFrameBytes(), LegacyEquipmentQueryPublishedPhases.None).ConfigureAwait(false);
        }

        private static uint NativePirateRank(IReadOnlyList<CombatGearRankEntry> rows, long uid, byte type)
        {
            // Reproduce loaded UpdateItem top10 after every row, including replacement and stable ties.
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
            var index = Array.IndexOf(ranking.Keys.ToArray(), unchecked((uint)uid));
            return index < 0 ? 0U : unchecked((uint)(index + 1));
        }
    }
}
