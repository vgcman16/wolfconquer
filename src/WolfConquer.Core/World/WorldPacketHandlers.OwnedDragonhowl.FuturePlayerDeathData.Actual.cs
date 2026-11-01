using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class FuturePlayerDeathData
    {
        // Comparison only. The argument must already have been issued by the
        // genuine death factory against the exact completed-XP successor.
        internal bool MatchesActual(OwnedPhysicalDeathPreparation actual)
        {
            if (actual.Dragonhowl is null || !actual.TryReadNativeQueryProposal(actual.TargetBinding,
                    actual.Proposed, out var native) || native.Admitted != QueryAdmitted ||
                native.Fields != Query.Magic || native.JiangHu != Query.JiangHu ||
                native.PerfectionPrestige != Query.Prestige || native.Perfection != Query.Perfection ||
                !actual.TryCapturePhysicalDeathHistory(actual.TargetBinding, out var history) ||
                history.QueryAdmitted != QueryAdmitted || history.Alternate != before.AlternateEquipmentActive ||
                !history.SourceItems.AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems)) ||
                !history.SourceSpells.AsSpan().SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(before.PlayerSpells)) ||
                history.RankingRows.Count != ranks.Length || history.DerivedSpells.Count != changes.Length ||
                history.GrantedRows.Count != grants.Length) return false;
            for (int index = 0; index < ranks.Length; index++)
                if (history.RankingRows[index].CharacterId != ranks[index].CharacterId ||
                    !history.RankingRows[index].Fields.SequenceEqual(ranks[index].Fields)) return false;
            for (int index = 0; index < changes.Length; index++)
                if (history.DerivedSpells[index] != changes[index]) return false;
            for (int index = 0; index < grants.Length; index++)
            {
                var granted = history.GrantedRows[index]; var expected = grants[index];
                if (granted.Id != expected.Id || granted.TypeId != expected.TypeId || granted.Name != expected.Name ||
                    !granted.Fields.SequenceEqual(expected.Fields)) return false;
            }
            var current = actual.Proposed;
            if ((current with { Character = Session.Character, PlayerItems = Session.PlayerItems,
                    PlayerSpells = Session.PlayerSpells, PhysicalStatusOverlay = Session.PhysicalStatusOverlay,
                    EquipmentHitpointsStatus = Session.EquipmentHitpointsStatus,
                    FineRainEquipmentAdmission = Session.FineRainEquipmentAdmission }) != Session ||
                (current.Character with { Attributes = Session.Character.Attributes,
                    PaidNobility = Session.Character.PaidNobility }) != Session.Character ||
                !BoundHitpointsInputs.PaidEqual(current.Character, Session.Character) ||
                current.PhysicalStatusOverlay != Session.PhysicalStatusOverlay ||
                current.EquipmentHitpointsStatus != Session.EquipmentHitpointsStatus ||
                LegacyFineRainEquipmentAdmission.ForOwner(current).OwnerUid !=
                    LegacyFineRainEquipmentAdmission.ForOwner(Session).OwnerUid ||
                !PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems).AsSpan()
                    .SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(Session.PlayerItems)) ||
                !PlayerSpellSnapshotLoader.ToLegacyBytes(current.PlayerSpells).AsSpan()
                    .SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(Session.PlayerSpells))) return false;
            return DeathFieldsMatch(current.Character.Attributes, Session.Character.Attributes);
        }

        private static bool DeathFieldsMatch(IReadOnlyDictionary<string, string> current,
            IReadOnlyDictionary<string, string> expected)
        {
            var actual = LegacyNativeAttributeMapProof.CaptureRaw(current);
            var captured = LegacyNativeAttributeMapProof.CaptureRaw(expected);
            if (!ReferenceEquals(actual.CapturedComparer, captured.CapturedComparer) || actual.Fields.Count != captured.Fields.Count)
                return false;
            foreach (var row in captured.Fields)
            {
                var matches = actual.Fields.Where(value => string.Equals(value.Key, row.Key, StringComparison.Ordinal)).ToArray();
                if (matches.Length != 1) return false;
                if (row.Key is LegacyPlayerDeathState.GhostStampAttribute or LegacyPlayerDeathState.DeadStampAttribute)
                {
                    // These two actual configured-clock reads occur inside the
                    // real death factory. Preflight validates their bounded
                    // representation; it does not substitute a frozen clock.
                    if (!DateTimeOffset.TryParseExact(matches[0].Value, "O", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out _)) return false;
                }
                else if (matches[0].Value != row.Value) return false;
            }
            return actual.IsCurrentPlain() && captured.IsCurrentPlain();
        }
    }
}
