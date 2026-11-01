using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class OwnedBasicPhysicalReceiveData
    {
        private LegacyNativeRuneItemStorage.BasicPhysicalWornSnapshot? actorWorn, targetWorn;
        private LegacyNativeRuneItemStorage.BasicPhysicalRuneRow[] actorRunes = [], targetRunes = [];
        private OwnedPhysicalPerfectionInputs? actorPerfection, targetPerfection;
        private WorldSessionStore.OwnedHawksStatusProof? hawksStatus;
        private WorldSessionStore.OwnedHawksPowerProof? hawksPower;
        private readonly DateTimeOffset modifierGateUtc;
        private uint baseDamage, effectiveDamage;

        private bool TryRequireApplicableOwnedPhysicalBranches(uint damage, out string reason)
        {
            reason = "ordinaryBasicPhysicalWornOwnerChanged";
            if (!LegacyNativeRuneItemStorage.BasicPhysicalWornSnapshot.TryCapture(ActorBinding.Items.Rune,
                    ActorBinding, ExpectedActor, out actorWorn) || actorWorn is null ||
                !LegacyNativeRuneItemStorage.BasicPhysicalWornSnapshot.TryCapture(TargetBinding.Items.Rune,
                    TargetBinding, ExpectedTarget, out targetWorn) || targetWorn is null) return false;
            actorRunes = actorWorn.Rows; targetRunes = targetWorn.Rows; baseDamage = damage;
            actorPerfection = new(ActorBinding); targetPerfection = new(TargetBinding);
            if (HasLegacyStatusFlag(ExpectedTarget.Character, LegacyHawksAmbitionFlag) &&
                (!WorldSessionStore.TryCaptureOwnedHawksStatus(TargetBinding, ExpectedTarget, out hawksStatus) || hawksStatus is null ||
                 !WorldSessionStore.TryCaptureOwnedHawksPower(TargetBinding, ExpectedTarget, out hawksPower) || hawksPower is null))
            { reason = "ordinaryBasicPhysicalHawksCasterOwnerMissing"; return false; }
            var reduction = hawksPower is null ? 0 : MulDivToUInt32(damage, hawksPower.Power, 100);
            effectiveDamage = reduction >= damage ? 0 : damage - reduction;
            var missing = UnjoinedOwnedPhysicalBranch();
            if (missing is not null) { reason = "ordinaryBasicPhysicalUnjoined:" + missing; return false; }
            reason = string.Empty; return ApplicableInputsAreCurrentPlain();
        }

        private bool ApplicableInputsAreCurrentPlain() => actorWorn is not null && targetWorn is not null &&
            actorWorn.IsCurrentPlain() && targetWorn.IsCurrentPlain() && actorPerfection is not null &&
            targetPerfection is not null && actorPerfection.IsCurrentPlain() && targetPerfection.IsCurrentPlain() &&
            (hawksStatus is null || hawksStatus.IsCurrentPlain()) && (hawksPower is null || hawksPower.IsCurrentPlain()) &&
            UnjoinedOwnedPhysicalBranch() is null;

        private string? UnjoinedOwnedPhysicalBranch()
        {
            var actor = ExpectedActor.Character; var target = ExpectedTarget.Character;
            if (ReadAttributeBool(target.Attributes, "Invisible")) return "invisibleTarget";
            if (ReadAttributeBool(actor.Attributes, RuntimeLegacyInUseIntensifyAttribute) ||
                HasLegacyStatusFlag(actor, LegacyFocusedFlag)) return "focusedPrefix";
            var pk = ReadAttributeUInt64(actor.Attributes, RuntimePkModeAttribute, "PKMode") & byte.MaxValue;
            if (!HasLegacyStatusFlag(actor, LegacyFlashingNameStatusFlag) && !HasLegacyPlayerMagicInterserverAttribute(ExpectedActor) &&
                pk != LegacyPkModeCapture && pk != LegacyPkModePeace && CanApplyLegacyPlayerMagicCanAttackFlashingName(target))
                return "flashingNamePrefix";
            if (HasLegacyStatusFlag(target, LegacyShurikenVortexFlag) || HasLegacyStatusFlag(target, LegacyManiacDanceFlag))
                return "defendedPhysicalDamage";
            if (HasLegacyStatusFlag(actor, LegacyHookMoonCasterFlag) || HasLegacyStatusFlag(actor, LegacyCleanSweepFlag) ||
                HasLegacyStatusFlag(actor, LegacyAxeShadowFlag) || HasLegacyStatusFlag(actor, LegacyAttackUpFlag)) return "earlyDamageModifier";
            if (HasLegacyStatusFlag(target, LegacyDuelFlag) || HasLegacyStatusFlag(actor, LegacyAbsolutionFlag) ||
                HasLegacyStatusFlag(target, LegacyAbsolutionFlag)) return "consumedStatusModifier";
            if (HasLegacyStatusFlag(target, LegacyNeptuneCurseFlag) ||
                HasLegacyStatusFlag(actor, LegacyNeptuneCurseFlag) && baseDamage < target.MinHitPoints) return "neptuneReceive";
            if (effectiveDamage >= target.MinHitPoints && (LegacyTransformState.TryGetActiveMaxHitpoints(actor, out _) ||
                LegacyTransformState.TryGetActiveMaxHitpoints(target, out _)))
                return "transformedReceive";
            if (HasLegacyStatusFlag(target, LegacyCombatGearWildPhoenixFlag) &&
                ReadAttributeUInt32(target.Attributes, RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute) > 0) return "wildPhoenixCounter";
            if (Context.WorldData is { } data)
            {
                if (Learned(ExpectedTarget, data, LegacyHeavensWonderSpellId, out _)) return "heavensWonderCounter";
                if (HasLegacyStatusFlag(target, LegacyRevengeTailFlag) &&
                    ReadAttributeUInt32(target.Attributes, RuntimeLegacyRevengeTailChangeAttribute, "RevengeTailChange") > 0 &&
                    Learned(ExpectedTarget, data, LegacyRevengeTailSpellId, out var revenge) && baseDamage < revenge.Damage)
                    return "revengeTailCounter";
                if (IsLegacyNinjaClass(target.Class) && RuneLevel(actorRunes, "Sky Veil") != 9 &&
                    Learned(ExpectedTarget, data, LegacyInfiniteMistSpellId, out _) &&
                    MagicRate(ExpectedTarget, data, LegacyInfiniteMistSpellId) > 0) return "infiniteMist";
                if (HasLegacyStatusFlag(target, LegacyMudWallFlag) &&
                    TryGetLegacyMudSigilImpeccablePower(ExpectedTarget, data, out var mud) && mud / 100 > 0) return "mudWall";
                if (IsLegacyTrojanClass(actor.Class) && !HasLegacyStatusFlag(actor, LegacyBloodTideFlag) &&
                    IsLegacyBloodTideCooldownReady(actor, modifierGateUtc) && Learned(ExpectedActor, data, LegacyBloodTideSpellId, out _))
                    return "bloodTide";
                if (OwnedPhysicalGearBranch(data) is { } gear) return gear;
                if (IsLegacyNinjaClass(actor.Class) && !HasLegacyStatusFlag(actor, LegacyNinjaSageSpeedModeFlag) &&
                    TryGetLegacyNinjaEffectiveLevels(ExpectedActor, out var levels) && levels > 0 &&
                    TryGetLegacyGouyuImmortalSageMode(data, levels, LegacyGouyuImmortalChanceValueIndex,
                        out var chance, out _) && chance > 0) return "ninjaSage";
            }
            if (HasLegacyStatusFlag(target, LegacyLightningShieldFlag) &&
                ExpectedTarget.PlayerSpells.Spells.Any(row => row.Id == 15_750)) return "lightningShieldActivation";
            if (HasLegacyStatusFlag(target, LegacyLightningShieldActivatedFlag) &&
                ReadAttributeUInt32(target.Attributes, RuntimeLegacyLightningShieldLeftAttribute, "LightningShieldLeft") > 0)
                return "lightningShieldDrain";
            if (OwnedPhysicalRuneBranch() is { } rune) return rune;
            if (OwnedPhysicalSpeffectBranch() is { } speffect) return speffect;
            if (OwnedPhysicalPerfectionBranch() is { } perfection) return perfection;
            if (effectiveDamage > 0 && effectiveDamage < target.MinHitPoints &&
                (HasLegacyPlayerReceiveSupportTortoiseGem(target) || HasLegacyPlayerReceiveBless(target))) return "responseGemEffect";
            if (IsLegacyPlayerReceiveFootballAttackerActive(actor)) return "footballReceive";
            if (IsLegacyPlayerReceiveTournamentAttackerActive(actor, out var tournament) &&
                (IsLegacyPlayerReceiveTournamentType(tournament, LegacySkillTournamentTypeName) ||
                 IsLegacyPlayerReceiveTournamentType(tournament, LegacyFreezeWarTypeName) ||
                 IsLegacyPlayerReceiveTournamentType(tournament, LegacyDragonWarTypeName))) return "tournamentReceive";
            return null;
        }

        private byte RuneLevel(LegacyNativeRuneItemStorage.BasicPhysicalRuneRow[] worn, string name)
        {
            foreach (var row in worn)
                if (Context.WorldData is { } data && row.ItemId <= int.MaxValue &&
                    data.ItemsById.TryGetValue((int)row.ItemId, out var indexed) && indexed.Count == 1 &&
                    indexed[0].Fields.Count > 1 && indexed[0].Fields[1].Contains(name, StringComparison.OrdinalIgnoreCase))
                    return checked((byte)(row.ItemId % 100));
            return 0;
        }

        private static byte RuneLevel(LegacyNativeRuneItemStorage.BasicPhysicalRuneRow[] worn, uint first, uint last)
        {
            foreach (var row in worn) if (row.ItemId > first && row.ItemId <= last) return checked((byte)(row.ItemId - first));
            return 0;
        }

        private static bool Learned(WorldSession source, WorldDataCatalog data, ushort id, out LegacyMagicRuntimeSpell spell) =>
            LegacyMagicRuntime.TryResolveLearnedSpell(source.PlayerSpells, data, id, out _, out spell);

        private static uint MagicRate(WorldSession source, WorldDataCatalog data, ushort id)
        {
            var entry = source.PlayerSpells.Spells.FirstOrDefault(row => row.Id == id);
            return entry is not null && TryReadLegacyMagicUInt32Field(data, id, entry.Level, LegacyMagicRateField, out var rate) ? rate : 0;
        }
    }
}
