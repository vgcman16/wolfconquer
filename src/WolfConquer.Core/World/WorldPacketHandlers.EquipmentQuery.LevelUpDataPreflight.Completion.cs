namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class DragonhowlLevelExperienceData
    {
        internal FutureEquipmentReceiveData? FinalReceiveData => queries.Count == 0 ? previous?.FinalReceiveData : queries[^1].Future;
        private byte[]? FutureItemImage => itemsImage ?? scalarDeathInputs?.Items ?? previous?.FutureItemImage;

        internal FuturePlayerDeathData.Images ProjectFutureDeathImages(FuturePlayerDeathData death,
            string usersRoot, string itemRoot, string spellRoot)
        {
            CaptureScalarDeathInputs(usersRoot, itemRoot, spellRoot);
            if (projectedCharacterImage is null ||
                FutureItemImage is not { } itemImage || !IsCurrentPlain()) throw Invalid();
            return death.ProjectImages(binding.AccountId, projectedCharacterImage, projectedSpellImage,
                itemImage, usersRoot, itemRoot, spellRoot);
        }

        // The model is only an expected result. The supplied token must still
        // be the actual query's retained completion for the exact live owner.
        internal bool MatchesCompletedQuery(LegacyOrdinarySpellBinding owner, WorldSession current,
            LegacyNativeMagicQueryClient.CompletedMaximums completion)
        {
            if (!ReferenceEquals(owner, binding) || FinalReceiveData is not { } future ||
                !completion.MatchesReceiveData(owner, current, future.Magic, future.JiangHu,
                    future.Perfection, future.Prestige, future.MaximumHitpoints, future.MaximumMana) ||
                !FutureSessionMatches(current, ProjectedAfter)) return false;
            return progression.IsCurrent() && queries.All(query => query.IsCurrent());
        }

        internal static bool FutureSessionMatches(WorldSession current, WorldSession expected)
        {
            if ((current with { Character = expected.Character, PlayerSpells = expected.PlayerSpells,
                    PhysicalStatusOverlay = expected.PhysicalStatusOverlay,
                    EquipmentHitpointsStatus = expected.EquipmentHitpointsStatus,
                    FineRainEquipmentAdmission = expected.FineRainEquipmentAdmission }) != expected ||
                (current.Character with { Attributes = expected.Character.Attributes,
                    PaidNobility = expected.Character.PaidNobility }) != expected.Character ||
                current.PhysicalStatusOverlay != expected.PhysicalStatusOverlay ||
                current.EquipmentHitpointsStatus != expected.EquipmentHitpointsStatus ||
                !WolfConquer.Core.Data.PlayerSpellSnapshotLoader.ToLegacyBytes(current.PlayerSpells).AsSpan()
                    .SequenceEqual(WolfConquer.Core.Data.PlayerSpellSnapshotLoader.ToLegacyBytes(expected.PlayerSpells))) return false;
            // FineRain records the time of the real query, not the earlier DATA
            // calculation. Only a real completion can reach this comparison.
            var actualAdmission = LegacyFineRainEquipmentAdmission.ForOwner(current);
            var expectedAdmission = LegacyFineRainEquipmentAdmission.ForOwner(expected);
            if (actualAdmission.OwnerUid != expectedAdmission.OwnerUid ||
                !HasLegacyStatusFlag(expected.Character, 212) && actualAdmission != expectedAdmission) return false;
            return FutureFieldsMatch(current.Character.Attributes, expected.Character.Attributes) &&
                FutureFieldsMatch(current.Character.PaidNobility, expected.Character.PaidNobility);
        }

        private static bool FutureFieldsMatch(IReadOnlyDictionary<string, string> current,
            IReadOnlyDictionary<string, string> expected)
        {
            var actual = LegacyNativeAttributeMapProof.CaptureRaw(current);
            var captured = LegacyNativeAttributeMapProof.CaptureRaw(expected);
            if (!ReferenceEquals(actual.CapturedComparer, captured.CapturedComparer) ||
                actual.Fields.Count != captured.Fields.Count) return false;
            // Do not fold unrelated opaque keys. Their original comparer and
            // exact ordinal spelling/value survive the actual scalar writer.
            foreach (var row in captured.Fields)
                if (!actual.Fields.Any(value => string.Equals(value.Key, row.Key, StringComparison.Ordinal) &&
                    string.Equals(value.Value, row.Value, StringComparison.Ordinal))) return false;
            return actual.IsCurrentPlain() && captured.IsCurrentPlain();
        }
    }
}
