using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class OwnedBasicPhysicalReceiveData
    {
        private string? OwnedPhysicalRuneBranch()
        {
            var actor = ExpectedActor.Character; var target = ExpectedTarget.Character;
            var caprice = RuneLevel(actorRunes, LegacyCapriceRuneItemBase, LegacyCapriceRuneItemMax);
            if (caprice is >= 1 and <= 27 && IsLegacyCapriceCooldownReady(actor, modifierGateUtc)) return "caprice";
            if (RuneLevel(targetRunes, LegacyReverseMagicRuneItemBase, LegacyReverseMagicRuneItemMax) != 0) return "reverseMagic";
            if (RuneLevel(actorRunes, LegacySlowPaceRuneItemBase, LegacySlowPaceRuneItemMax) != 0 &&
                CalculateLegacyRuntimeStatusFields(ExpectedTarget, Context.WorldData)[LegacyPlayerStatusPacket.DodgeIndex] != 0)
                return "slowPace";
            if (IsLegacyPlayerOnXpSkill(actor) && RuneLevel(actorRunes, LegacyXpBoosterRuneItemBase, LegacyXpBoosterRuneItemMax) != 0 &&
                ReadAttributeUInt32(actor.Attributes, RuntimeLegacyXpBoosterAttribute) > 0) return "xpBooster";
            var killer = RuneLevel(actorRunes, LegacyXpKillerRuneItemBase, LegacyXpKillerRuneItemMax);
            if (IsLegacyPlayerOnXpSkill(target) && killer != 0 &&
                (killer != 6 || ReadAttributeUInt32(target.Attributes, "XPCount") < 30)) return "xpKiller";
            if (!HasLegacyStatusFlag(actor, LegacyTidalWaveFlag) && !HasLegacyStatusFlag(actor, LegacyHealingSnowFlag) &&
                RuneLevel(actorRunes, LegacyTidalWaveRuneItemBase, LegacyTidalWaveRuneItemMax) != 0 &&
                IsLegacyPlayerReceiveXpRuneCooldownReady(actor, RuntimeLegacyTidalWaveStampUtcAttribute, modifierGateUtc)) return "tidalWave";
            if (!HasLegacyStatusFlag(target, LegacyQuenchFlag) &&
                RuneLevel(actorRunes, LegacyQuenchRuneItemBase, LegacyQuenchRuneItemMax) != 0 &&
                IsLegacyPlayerReceiveXpRuneCooldownReady(actor, RuntimeLegacyQuenchStampUtcAttribute, modifierGateUtc)) return "quench";
            return null;
        }

        private string? OwnedPhysicalSpeffectBranch()
        {
            var venom = GetLegacyReceiveAttackSpeffectLevel(ExpectedActor, LegacySpeffectVenomGroup,
                "VenomLevel", RuntimeLegacySpeffectLevelAttribute + ".Venom", RuntimeLegacySpeffectLevelAttribute + ".2");
            var venomSource = venom == uint.MaxValue ? uint.MaxValue : venom + 1;
            if (venom != 0 && GetLegacyReceiveAttackVenomValues(venomSource).RatePercent > 0 && Context.WorldData is { } data &&
                TryResolveLegacyPlayerMagicRuntimeSpell(ExpectedActor, data, LegacyAttackStatusVenomSpellId, out _, out _))
                return "venomSpeffect";
            var solid = GetLegacyReceiveAttackSpeffectLevel(ExpectedTarget, LegacySpeffectSolidGroup,
                "SolidLevel", RuntimeLegacySpeffectLevelAttribute + ".Solid", RuntimeLegacySpeffectLevelAttribute + ".3");
            if (solid != 0 && GetLegacyReceiveAttackSolidValues(solid) is { Value: > 0, Duration: > 0 }) return "solidSpeffect";
            var blood = GetLegacyReceiveAttackSpeffectLevel(ExpectedTarget, LegacySpeffectBloodthirstGroup,
                "BloodthirstLevel", RuntimeLegacySpeffectLevelAttribute + ".Bloodthirst", RuntimeLegacySpeffectLevelAttribute + ".4");
            if (GetLegacyReceiveAttackBloodthirstRate(blood) > 0 &&
                ExpectedTarget.Character.MinHitPoints < ComputeLegacyRuntimeMaxHitpoints(ExpectedTarget.Character,
                    CheckedUInt32(ExpectedTarget.Character.MinHitPoints, nameof(ExpectedTarget.Character.MinHitPoints)),
                    ExpectedTarget.PlayerItems, ExpectedTarget.CombatGear ?? CombatGearSnapshot.Empty,
                    Context.WorldData, ExpectedTarget.AlternateEquipmentActive)) return "bloodthirstSpeffect";
            var elan = GetLegacyReceiveAttackSpeffectLevel(ExpectedTarget, LegacySpeffectElanGroup,
                "ElanLevel", RuntimeLegacySpeffectLevelAttribute + ".Elan", RuntimeLegacySpeffectLevelAttribute + ".5");
            if (ExpectedTarget.Character.MinMana < ExpectedTarget.Character.MaxMana && GetLegacyReceiveAttackElanPercent(elan) > 0 &&
                Math.Floor(ExpectedTarget.Character.MaxMana * GetLegacyReceiveAttackElanPercent(elan) / 100.0) > 0) return "elanSpeffect";
            return null;
        }
    }
}
