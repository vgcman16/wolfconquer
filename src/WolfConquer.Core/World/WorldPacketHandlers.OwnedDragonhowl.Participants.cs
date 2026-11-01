using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Negative applicability/currentness DATA. Only the registered proc issuer
    // owns a cast. No basic-hit decision or accepting participant is constructed.
    private sealed class DragonhowlReceiveInputs
    {
        private readonly WorldPacketHandlerContext Context;
        private readonly WorldSessionStore.OwnedDragonhowlOwner Owner;
        private readonly MsgAttackPacket Packet;
        private readonly LegacyOrdinarySpellBinding ActorBinding, TargetBinding;
        private readonly WorldSession ExpectedActor, ExpectedTarget;
        private readonly WorldSession OriginalActor, OriginalTarget;
        private readonly FutureEquipmentReceiveData? actorFuture, targetFuture;
        internal WolfConquer.Core.Networking.ConquerTcpConnection TargetConnection => TargetBinding.Connection;
        internal DragonhowlReceiveInputs(WorldPacketHandlerContext context, WorldSessionStore.OwnedDragonhowlOwner owner, MsgAttackPacket packet,
            LegacyOrdinarySpellBinding actor, LegacyOrdinarySpellBinding target,
            WorldSession expectedActor, WorldSession expectedTarget,
            WorldSession? calculatedActor = null, WorldSession? calculatedTarget = null,
            FutureEquipmentReceiveData? actorFuture = null, FutureEquipmentReceiveData? targetFuture = null)
        {
            Context = context; Owner = owner; Packet = packet; ActorBinding = actor; TargetBinding = target;
            OriginalActor = expectedActor; OriginalTarget = expectedTarget;
            ExpectedActor = calculatedActor ?? expectedActor; ExpectedTarget = calculatedTarget ?? expectedTarget;
            this.actorFuture = actorFuture; this.targetFuture = targetFuture;
            modifierGateUtc = (context.TimeProvider ?? throw new InvalidOperationException("ownedDragonhowlClockMissing")).GetUtcNow();
        }
        private LegacyNativeRuneItemStorage.BasicPhysicalWornSnapshot? actorWorn, targetWorn;
        private LegacyNativeRuneItemStorage.BasicPhysicalRuneRow[] actorRunes = [], targetRunes = [];
        private DragonhowlPerfectionInputs? actorPerfection, targetPerfection;
        private WorldSessionStore.OwnedHawksStatusProof? hawksStatus;
        private WorldSessionStore.OwnedHawksPowerProof? hawksPower;
        private readonly DateTimeOffset modifierGateUtc;
        private uint baseDamage, effectiveDamage;

        internal bool TryPrepare(uint damage, out string reason)
        {
            reason = "ordinaryBasicPhysicalWornOwnerChanged";
            if (!LegacyNativeRuneItemStorage.BasicPhysicalWornSnapshot.TryCapture(ActorBinding.Items.Rune,
                    ActorBinding, OriginalActor, out actorWorn) || actorWorn is null ||
                !LegacyNativeRuneItemStorage.BasicPhysicalWornSnapshot.TryCapture(TargetBinding.Items.Rune,
                    TargetBinding, OriginalTarget, out targetWorn) || targetWorn is null) return false;
            actorRunes = actorWorn.Rows; targetRunes = targetWorn.Rows; baseDamage = damage;
            actorPerfection = new(ActorBinding, Owner, actorFuture); targetPerfection = new(TargetBinding, Owner, targetFuture);
            if (HasLegacyStatusFlag(ExpectedTarget.Character, LegacyHawksAmbitionFlag) &&
                (!WorldSessionStore.TryCaptureOwnedHawksStatus(TargetBinding, ExpectedTarget, out hawksStatus) || hawksStatus is null ||
                 !WorldSessionStore.TryCaptureOwnedHawksPower(TargetBinding, ExpectedTarget, out hawksPower) || hawksPower is null))
            { reason = "ordinaryBasicPhysicalHawksCasterOwnerMissing"; return false; }
            var reduction = hawksPower is null ? 0 : MulDivToUInt32(damage, hawksPower.Power, 100);
            effectiveDamage = reduction >= damage ? 0 : damage - reduction;
            var missing = UnjoinedOwnedPhysicalBranch();
            if (missing is not null) { reason = "ordinaryBasicPhysicalUnjoined:" + missing; return false; }
            reason = string.Empty; return IsCurrentPlain();
        }

        internal bool IsCurrentPlain(bool targetCompleted = false) => actorWorn is not null && actorPerfection is not null &&
            actorWorn.IsCurrentForDragonhowl(Owner) && actorPerfection.IsCurrentPlain() && (targetCompleted ||
            targetWorn is not null && targetWorn.IsCurrentForDragonhowl(Owner) && targetPerfection is not null && targetPerfection.IsCurrentPlain() &&
            (hawksStatus is null || hawksStatus.IsCurrentPlain()) && (hawksPower is null || hawksPower.IsCurrentPlain()) &&
            UnjoinedOwnedPhysicalBranch() is null);

        internal bool ContainsWornSnapshot(LegacyOrdinarySpellBinding binding,
            LegacyNativeRuneItemStorage.BasicPhysicalWornSnapshot snapshot, WorldSession original) =>
            ReferenceEquals(binding, ActorBinding) && ReferenceEquals(snapshot, actorWorn) && ReferenceEquals(original, OriginalActor) ||
            ReferenceEquals(binding, TargetBinding) && ReferenceEquals(snapshot, targetWorn) && ReferenceEquals(original, OriginalTarget);

        internal bool ContainsPerfectionInputs(LegacyOrdinarySpellBinding binding, object inputs) =>
            ReferenceEquals(binding, ActorBinding) && ReferenceEquals(inputs, actorPerfection) ||
            ReferenceEquals(binding, TargetBinding) && ReferenceEquals(inputs, targetPerfection);

        private string? UnjoinedOwnedPhysicalBranch()
        {
            var actor = ExpectedActor.Character; var target = ExpectedTarget.Character;
            if (ReadAttributeBool(target.Attributes, "Invisible")) return "invisibleTarget";
            if (ReadAttributeBool(actor.Attributes, RuntimeLegacyInUseIntensifyAttribute) ||
                HasLegacyStatusFlag(actor, LegacyFocusedFlag)) return "focusedPrefix";
            // The registered proc builder prepares the concrete timed flashing
            // phase after target permission and before this receive calculation.
            if (HasLegacyStatusFlag(target, LegacyShurikenVortexFlag) || HasLegacyStatusFlag(target, LegacyManiacDanceFlag))
                return "defendedPhysicalDamage";
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


        private string? OwnedPhysicalRuneBranch()
        {
            var actor = ExpectedActor.Character; var target = ExpectedTarget.Character;
            var caprice = RuneLevel(actorRunes, LegacyCapriceRuneItemBase, LegacyCapriceRuneItemMax);
            if (caprice is >= 1 and <= 27 && IsLegacyCapriceCooldownReady(actor, modifierGateUtc)) return "caprice";
            if (RuneLevel(targetRunes, LegacyReverseMagicRuneItemBase, LegacyReverseMagicRuneItemMax) != 0) return "reverseMagic";
            if (RuneLevel(actorRunes, LegacySlowPaceRuneItemBase, LegacySlowPaceRuneItemMax) != 0 &&
                ReadCalculatedTargetFields()[LegacyPlayerStatusPacket.DodgeIndex] != 0)
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
                ExpectedTarget.Character.MinHitPoints < (targetFuture?.MaximumHitpoints ?? ComputeLegacyRuntimeMaxHitpoints(ExpectedTarget.Character,
                    CheckedUInt32(ExpectedTarget.Character.MinHitPoints, nameof(ExpectedTarget.Character.MinHitPoints)),
                    ExpectedTarget.PlayerItems, ExpectedTarget.CombatGear ?? CombatGearSnapshot.Empty,
                    Context.WorldData, ExpectedTarget.AlternateEquipmentActive))) return "bloodthirstSpeffect";
            var elan = GetLegacyReceiveAttackSpeffectLevel(ExpectedTarget, LegacySpeffectElanGroup,
                "ElanLevel", RuntimeLegacySpeffectLevelAttribute + ".Elan", RuntimeLegacySpeffectLevelAttribute + ".5");
            if (ExpectedTarget.Character.MinMana < ExpectedTarget.Character.MaxMana && GetLegacyReceiveAttackElanPercent(elan) > 0 &&
                Math.Floor(ExpectedTarget.Character.MaxMana * GetLegacyReceiveAttackElanPercent(elan) / 100.0) > 0) return "elanSpeffect";
            return null;
        }

        private uint[] ReadCalculatedTargetFields() => targetFuture is null
            ? CalculateLegacyRuntimeStatusFields(ExpectedTarget, Context.WorldData)
            : DragonhowlReceiveCalculationData.Create(Context.ProtocolProfile, Context.ProtocolProfile,
                targetFuture: targetFuture).ReadTargetFields(ExpectedTarget, Context.WorldData);


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


        private string? OwnedPhysicalPerfectionBranch()
        {
            var actor = ExpectedActor.Character; var target = ExpectedTarget.Character;
            if (Positive(ReadLegacyPlayerReceiveMirrorOfSinPercent(actor), actorPerfection!.MirrorOfSin) ||
                Positive(ReadLegacyPlayerReceiveCalmWindPercent(actor), actorPerfection.CalmWind) ||
                Positive(ReadLegacyPlayerReceiveKillingFlashPercent(actor), actorPerfection.KillingFlash) ||
                Positive(ReadLegacyPlayerReceiveDrainingTouchPercent(actor), actorPerfection.DrainingTouch)) return "attackerPerfectionProc";
            var prestigeAllows = ReadLegacyPlayerReceivePrestigeLevel(target) >= ReadLegacyPlayerReceivePrestigeLevel(actor) ||
                targetPerfection!.Prestige >= actorPerfection.Prestige;
            if (prestigeAllows && (Positive(ReadLegacyPlayerReceiveLightOfStaminaPercent(actor), actorPerfection.LightOfStamina) ||
                Positive(ReadLegacyPlayerReceiveDivineGuardPercent(actor), actorPerfection.DivineGuard))) return "prestigePerfectionProc";
            if (Positive(ReadLegacyPlayerReceiveBloodSpawnPercent(target), targetPerfection!.BloodSpawn)) return "targetBloodSpawnProc";
            return null;
        }

        private static bool Positive(double managed, double native) => managed > 0 || native > 0;

    }
}
