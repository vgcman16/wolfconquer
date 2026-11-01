using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class OwnedPhysicalDeathPreparation
    {
        private sealed class CanonicalDeathTarget
        {
            internal WorldSession Target { get; }
            private readonly LegacyNativeAttributeMapProof attributes, paid;
            private readonly LegacyPerformerSpellSource spells;
            private readonly LegacyItemSaveGraph items;
            internal CanonicalDeathTarget(WorldSession target)
            {
                Target = target; attributes = new(target.Character.Attributes); paid = new(target.Character.PaidNobility);
                spells = new(target.PlayerSpells.Spells);
                if (!LegacyItemSaveGraph.TryCapture(target.PlayerItems, out var graph) || graph is null)
                    throw InvalidOwnedPhysicalDeath();
                items = graph;
            }
            internal bool IsCurrentPlain() => attributes.IsCurrentPlain() && paid.IsCurrentPlain() &&
                spells.IsCurrent() && items.IsCurrent(Target.PlayerItems);
        }

        private bool CanonicalTargetMatches(WorldSession target)
        {
            if (target is null || target.AccountId != Proposed.AccountId || target.Username != Proposed.Username ||
                !ReferenceEquals(target.NativeGameClient, TargetBinding.Client) ||
                !BoundHitpointsInputs.ScalarsEqual(target.Character, Proposed.Character) ||
                target.Character.SourceFile != Proposed.Character.SourceFile ||
                !ReferenceEquals(target.Character.PlayerQuests, Proposed.Character.PlayerQuests) ||
                !FieldsEqual(new LegacyNativeAttributeMapProof(target.Character.Attributes).Fields, proposedAttributes.Fields) ||
                !FieldsEqual(new LegacyNativeAttributeMapProof(target.Character.PaidNobility).Fields, proposedPaid.Fields) ||
                !PlayerItemSnapshotLoader.ToLegacyBytes(target.PlayerItems).AsSpan()
                    .SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(Proposed.PlayerItems)) ||
                !PlayerSpellSnapshotLoader.ToLegacyBytes(target.PlayerSpells).AsSpan()
                    .SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(Proposed.PlayerSpells)) ||
                !PlayerProficiencySnapshotLoader.ToLegacyBytes(target.PlayerProficiencies).AsSpan()
                    .SequenceEqual(PlayerProficiencySnapshotLoader.ToLegacyBytes(Proposed.PlayerProficiencies))) return false;
            return UnchangedSessionFieldsMatch(target);
        }

        private bool UnchangedSessionFieldsMatch(WorldSession target) =>
            target.AlternateEquipmentActive == Proposed.AlternateEquipmentActive &&
            target.InteractionPartnerUid == Proposed.InteractionPartnerUid && target.ActiveInteractionDance == Proposed.ActiveInteractionDance &&
            ReferenceEquals(target.CombatGear, Proposed.CombatGear) && target.UsersRoot == Proposed.UsersRoot &&
            target.ActiveNpcId == Proposed.ActiveNpcId && target.PendingNobilityDonationConquerPoints == Proposed.PendingNobilityDonationConquerPoints &&
            target.LastLegacyPotionUseUtc == Proposed.LastLegacyPotionUseUtc && target.LegacyTradeActive == Proposed.LegacyTradeActive &&
            ReferenceEquals(target.MagicCooldowns, Proposed.MagicCooldowns) && target.LegacyRandomSpell == Proposed.LegacyRandomSpell &&
            target.PendingBankConquerPoints == Proposed.PendingBankConquerPoints &&
            target.PendingBankConquerPointsIsDeposit == Proposed.PendingBankConquerPointsIsDeposit &&
            ReferenceEquals(target.LegacyExchangeShopBuyCounts, Proposed.LegacyExchangeShopBuyCounts) &&
            ReferenceEquals(target.LegacyExchangeShopGoodsExCounts, Proposed.LegacyExchangeShopGoodsExCounts) &&
            target.LegacyCatalogSelectedItemId == Proposed.LegacyCatalogSelectedItemId && target.LegacyVoteClaimDueAtUtc == Proposed.LegacyVoteClaimDueAtUtc &&
            target.LegacyAnimaSecurityPendingPin == Proposed.LegacyAnimaSecurityPendingPin &&
            target.LegacyAnimaSecurityOldPinVerified == Proposed.LegacyAnimaSecurityOldPinVerified &&
            ReferenceEquals(target.LegacyVendorListings, Proposed.LegacyVendorListings) &&
            ReferenceEquals(target.PhysicalStatusOverlay, Proposed.PhysicalStatusOverlay) &&
            ReferenceEquals(target.FineRainEquipmentAdmission, Proposed.FineRainEquipmentAdmission) &&
            ReferenceEquals(target.EquipmentHitpointsStatus, Proposed.EquipmentHitpointsStatus) &&
            ReferenceEquals(target.WarehouseBankPresence, Proposed.WarehouseBankPresence) &&
            ReferenceEquals(target.CanAttackFlashingRegistration, Proposed.CanAttackFlashingRegistration);

        private static bool FieldsEqual(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right) =>
            left.Count == right.Count && left.All(row => right.TryGetValue(row.Key, out var value) && row.Value == value);
    }
}
