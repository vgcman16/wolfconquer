using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class OwnedBasicPhysicalReceiveData
    {
        private string? OwnedPhysicalGearBranch(WorldDataCatalog data)
        {
            var actor = ExpectedActor.Character;
            var gear = ExpectedActor.CombatGear ?? CombatGearSnapshot.Empty;
            foreach (var id in EnumerateLegacyCombatGearUpdateTargetSpells(ExpectedTarget))
                if (TryResolveLegacyCombatGearUpdateTargetSpell(ExpectedTarget, data, id, out var entry, out _) &&
                    PotentialOwnedPhysicalTargetGearRate(data, id, entry.Level)) return "defenderGearPassive";
            if (HasLegacyCombatGearEquippedForClass(gear, actor.Class, LegacyCombatGearStoneCrackerItemId))
            {
                var arrows = ReadAttributeUInt32(actor.Attributes, RuntimeLegacyCombatGearArrowBladesAttribute);
                var stars = ReadAttributeUInt32(actor.Attributes, RuntimeLegacyCombatGearStarFlowAttribute);
                if (arrows <= 2 || arrows >= 3 && !HasLegacyStatusFlag(actor, LegacyCombatGearWaterTop4WeeklyFlag) ||
                    stars <= 2 || stars == 4) return "stoneCrackerCounters";
                if (TryResolveLegacyPlayerMagicRuntimeSpell(ExpectedActor, data, LegacyCombatGearArchShadowSpellId, out _, out var arch) &&
                    LegacyGetDistance(actor.X, actor.Y, ExpectedTarget.Character.X, ExpectedTarget.Character.Y) < arch.Range &&
                    CanSeePlayer(actor, ExpectedTarget.Character) &&
                    CanLegacyPlayerMagicDamageTarget(Context, ExpectedActor, ExpectedTarget, arch, out _)) return "stoneCrackerArchShadow";
            }
            if (HasLegacyCombatGearEquippedForClass(gear, actor.Class, LegacyCombatGearDragonhowlItemId) &&
                !HasLegacyStatusFlag(actor, LegacyCombatGearDragonPierceCritFlag) &&
                Resolved(data, LegacyCombatGearDragonhowlReceiveSpellId)) return "dragonhowlReceive";
            if (HasLegacyCombatGearEquippedForClass(gear, actor.Class, LegacyCombatGearBloodlustItemId) &&
                (Resolved(data, LegacyCombatGearBloodlustImmersionSpellId) || Resolved(data, LegacyCombatGearBloodlustInsoucianceSpellId)))
                return "bloodlustReceive";
            if (HasLegacyCombatGearEquippedForClass(gear, actor.Class, LegacyCombatGearTaoist1ItemId) &&
                Resolved(data, LegacyCombatGearDeadwoodCurseReceiveSpellId)) return "deadwoodReceive";
            if (HasLegacyCombatGearEquippedForClass(gear, actor.Class, LegacyCombatGearTaoist3ItemId) &&
                TryResolveLegacyPlayerMagicRuntimeSpell(ExpectedActor, data, LegacyCombatGearCrackMantraReceiveSpellId, out _, out var crack) &&
                CanSeePlayer(actor, ExpectedTarget.Character) &&
                CanLegacyPlayerMagicDamageTarget(Context, ExpectedActor, ExpectedTarget, crack, out _)) return "crackMantraReceive";
            if (HasLegacyPirateCombatGearEquipped(ExpectedActor))
            {
                if (Resolved(data, LegacyCombatGearPirateArmedReceiveSpellId) ||
                    !HasLegacyStatusFlag(actor, LegacyCombatGearPirateSenseFlag) && Resolved(data, LegacyCombatGearPirateSenseReceiveSpellId) ||
                    CanSeePlayer(actor, ExpectedTarget.Character) && Resolved(data, LegacyCombatGearPirateDarkReceiveSpellId))
                    return "pirateReceive";
                foreach (var id in EnumerateLegacyCombatGearPirateBlackJadeUseRuneCandidates(ExpectedActor, data))
                {
                    if (!IsLegacyCombatGearPirateBlackJadeUseRuneSpell(id) ||
                        !TryResolveLegacyPlayerMagicRuntimeSpell(ExpectedActor, data, id, out var entry, out _)) continue;
                    int field = id == LegacyCombatGearPirateFusingUseRuneSpellId ? LegacyMagicDamageField : LegacyMagicRateField;
                    if (TryReadLegacyMagicUInt32Field(data, id, entry.Level, field, out var rate) && rate > 0)
                        return "pirateBlackJadeUseRune";
                }
            }
            // The earlier active Gear replacement is Archer-only in this
            // source. Physical packets do not acquire that unrelated frontier.
            if (IsLegacyKineticSparkPassivePlayerProcAttackType(Packet.Query.AttackType) &&
                HasLegacyStatusFlag(actor, LegacyKineticSparkFlag) && Learned(ExpectedActor, data, LegacyKineticSparkSpellId, out var kinetic) &&
                kinetic.Sort == LegacyMagicSort.KineticSpark) return "kineticReplacement";
            return null;
        }

        private bool Resolved(WorldDataCatalog data, ushort id) =>
            TryResolveLegacyPlayerMagicRuntimeSpell(ExpectedActor, data, id, out _, out _);

        private bool PotentialOwnedPhysicalTargetGearRate(WorldDataCatalog data, ushort id, ushort level) => id switch
        {
            LegacyCombatGearArmorOfImmunitySpellId or LegacyCombatGearHunterSpellId or LegacyCombatGearSolidBulwarkSpellId => true,
            LegacyCombatGearMagneticLightSpellId => HasLegacyStatusFlag(ExpectedTarget.Character, LegacyVenomFlag | LegacyMagicShieldFlag) ||
                TryReadLegacyMagicUInt32Field(data, id, level, 12, out var rate) && rate > 0,
            LegacyCombatGearAblazeBladeSpellId or LegacyCombatGearFlowKnackSpellId or LegacyCombatGearWaterAegisSpellId or
                LegacyCombatGearFantasyKnackSpellId or LegacyCombatGearDivineEmptinessSpellId or LegacyCombatGearFlameShieldSpellId =>
                TryReadLegacyMagicUInt32Field(data, id, level, 12, out var rate) && rate > 0,
            _ => false,
        };
    }
}
