using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal enum DragonhowlAudience { Owner, OwnerVisible, TargetVisible }
    private sealed record DragonhowlGeometry(LegacyCombatGearTriple17490Line Triple, LegacyCombatGearIronbone17650Line? Iron)
    {
        internal bool Contains(long x, long y) => Iron is not null ? Iron.Contains(x, y) : IsLegacyCombatGearTriple17490InLine(Triple, x, y);
    }

    private static DragonhowlGeometry? PrepareDragonhowlGeometry(WorldPacketHandlerContext context, WorldSession actor,
        MsgAttackPacket packet, LegacyMagicRuntimeSpell spell)
    {
        var x = CheckedUInt16(actor.Character.X, nameof(actor.Character.X));
        var y = CheckedUInt16(actor.Character.Y, nameof(actor.Character.Y));
        var anchored = GetLegacyCombatGearDirectLineAnchoredQuery(context, actor, packet.Query);
        var triple = new LegacyCombatGearTriple17490Line(x, y, anchored.X, anchored.Y);
        LegacyCombatGearIronbone17650Line? iron = null;
        // CheckCombatGear constructs its internal spell query from the actual
        // selected target coordinates for both spells, not the Physical wire's
        // untrusted aim fields.
        if (spell.Id == 17_650 && (!TryCreateLegacyCombatGearIronbone17650Line(x, y, anchored.X, anchored.Y,
            spell.Range, CreateLegacyMoveLineLocationValidator(context, actor), out iron) ||
            iron.Coordinates.Any(point => point.X is < 0 or > ushort.MaxValue || point.Y is < 0 or > ushort.MaxValue))) return null;
        return new(triple, iron);
    }

    internal static bool TrySelectDragonhowlPlayerParticipants(WorldPacketHandlerContext context, MsgAttackPacket packet,
        WorldActiveSession? primary, LegacyPhysicalCombatGearSelection selection, IReadOnlyList<WorldActiveSession> observed,
        out WorldActiveSession[] participants)
    {
        participants = [];
        if (context.Session is not { } actor || selection.Spell is not { Id: 17_490 or 17_650 } spell ||
            PrepareDragonhowlGeometry(context, actor, packet, spell) is not { } geometry) return false;
        // Discovery only, before acquiring ordered actor gates. The actual
        // killed-owner preparation separately captures and proves the complete
        // native Team roster; this list cannot authorize an award. A missing or
        // unbound member therefore refuses a selected lethal reward, rather
        // than being replaced with an invented recipient owner.
        HashSet<ConquerTcpConnection> rewardConnections = context.TeamStates is { } teams &&
            EnumerateVisibleMonsterTargets(context, actor.Character).Any(row =>
                geometry.Contains(row.Monster.X, row.Monster.Y) &&
                CanLegacyPlayerAttackMonster(actor.Character, row.Monster, row.MonsterFamily, out _))
            ? teams.GetConnections(context.Connection).ToHashSet() : [];
        participants = observed.Where(row => ReferenceEquals(row.Connection, context.Connection) ||
            ReferenceEquals(row.Connection, primary?.Connection) ||
            rewardConnections.Contains(row.Connection) &&
                row.Session.NativeGameClient?.OrdinarySpellBinding is { IsRetired: false } binding &&
                ReferenceEquals(binding.Store, context.Sessions) && ReferenceEquals(binding.Connection, row.Connection) ||
            IsOwnedDragonhowlObservedPlayer(actor, row.Session) && row.Session.Character.MinHitPoints > 0 &&
            geometry.Contains(row.Session.Character.X, row.Session.Character.Y)).ToArray();
        return participants.Any(row => ReferenceEquals(row.Connection, context.Connection) && ReferenceEquals(row.Session, actor)) &&
            (primary is null || participants.Any(row => ReferenceEquals(row.Connection, primary.Connection) && ReferenceEquals(row.Session, primary.Session)));
    }
    internal sealed class DragonhowlPhase
    {
        internal readonly ConquerTcpConnection Connection;
        internal readonly WorldSession Before, After;
        internal readonly DragonhowlAudience Audience;
        internal readonly IReadOnlyList<byte[]> Frames;
        internal readonly WorldSessionStore.OwnedDragonhowlDeath? Death;
        internal readonly WorldSessionStore.OwnedDragonhowlFutureDeath? FutureDeath;
        internal readonly WorldSessionStore.OwnedDragonhowlAwardPhase? FutureAward;
        internal readonly WorldSessionStore.OwnedDragonhowlNonPlayer? NonPlayer;
        internal readonly DragonhowlMonsterOutput? MonsterOutput;
        internal readonly DragonhowlSobOutput? SobOutput;
        internal readonly PreparedDragonhowlSobEvent? SobEvent;
        internal readonly DragonhowlFlashingName? Flashing;
        internal readonly PreparedDragonhowlMonsterRewards? MonsterRewards;
        internal readonly WorldSessionStore.OwnedDragonhowlTeamExperience? TeamExperience;
        private readonly LegacyNativeAttributeMapProof beforeAttributes, beforePaid, afterAttributes, afterPaid;
        private readonly DragonhowlCooldownInputs beforeCooldowns, afterCooldowns;
        internal DragonhowlPhase(ConquerTcpConnection connection, WorldSession before, WorldSession after,
            DragonhowlAudience audience, byte[][] frames, WorldSessionStore.OwnedDragonhowlDeath? death = null,
            WorldSessionStore.OwnedDragonhowlNonPlayer? nonPlayer = null, DragonhowlMonsterOutput? monsterOutput = null,
            DragonhowlSobOutput? sobOutput = null, PreparedDragonhowlSobEvent? sobEvent = null,
            DragonhowlFlashingName? flashing = null,
            WorldSessionStore.OwnedDragonhowlFutureDeath? futureDeath = null,
            PreparedDragonhowlMonsterRewards? monsterRewards = null,
            WorldSessionStore.OwnedDragonhowlTeamExperience? teamExperience = null,
            WorldSessionStore.OwnedDragonhowlAwardPhase? futureAward = null)
        {
            Connection = connection; Before = before; After = after; Audience = audience;
            Death = death; FutureDeath = futureDeath;
            FutureAward = futureAward;
            if (futureAward is not null && (death is not null || futureDeath is not null || nonPlayer is not null || flashing is not null))
                throw new InvalidDataException("ownedDragonhowlConflictingAwardPhase");
            NonPlayer = nonPlayer;
            MonsterOutput = monsterOutput; SobOutput = sobOutput; SobEvent = sobEvent;
            Flashing = flashing;
            MonsterRewards = monsterRewards; TeamExperience = teamExperience;
            if ((monsterRewards is null) != (teamExperience is null) || monsterRewards is not null &&
                (nonPlayer is null || monsterOutput?.RequiresDeathContinuation != true || death is not null || futureDeath is not null))
                throw new InvalidDataException("ownedDragonhowlConflictingMonsterRewardPhase");
            if (futureDeath is not null && (death is not null || nonPlayer is not null))
                throw new InvalidDataException("ownedDragonhowlConflictingFutureDeath");
            if (flashing is not null && (death is not null || futureDeath is not null || nonPlayer is not null))
                throw new InvalidDataException("ownedDragonhowlConflictingFlashingPhase");
            if (death is not null && nonPlayer is not null) throw new InvalidDataException("ownedDragonhowlConflictingTargets");
            if (new object?[] { monsterOutput, sobOutput, sobEvent }.Count(value => value is not null) > 1 ||
                (monsterOutput is not null || sobOutput is not null || sobEvent is not null) && nonPlayer is null)
                throw new InvalidDataException("ownedDragonhowlConflictingTargetOutput");
            Frames = Array.AsReadOnly(frames.Select(row => row.ToArray()).ToArray());
            beforeAttributes = LegacyNativeAttributeMapProof.CaptureRaw(before.Character.Attributes);
            beforePaid = LegacyNativeAttributeMapProof.CaptureRaw(before.Character.PaidNobility);
            afterAttributes = LegacyNativeAttributeMapProof.CaptureRaw(after.Character.Attributes);
            afterPaid = LegacyNativeAttributeMapProof.CaptureRaw(after.Character.PaidNobility);
            beforeCooldowns = new(before.MagicCooldowns); afterCooldowns = new(after.MagicCooldowns);
        }
        internal bool IsCurrentPlain() => beforeAttributes.IsCurrentPlain() && beforePaid.IsCurrentPlain() &&
            afterAttributes.IsCurrentPlain() && afterPaid.IsCurrentPlain() &&
            beforeCooldowns.IsCurrentPlain(Before.MagicCooldowns) && afterCooldowns.IsCurrentPlain(After.MagicCooldowns) &&
            (NonPlayer is null || NonPlayer.InputsAreCurrentPlain()) && (SobEvent is null || SobEvent.IsCurrentPlain()) &&
            (FutureDeath is null || FutureDeath.InputsAreCurrentPlain()) &&
            (FutureAward is null || FutureAward.InputsAreCurrent()) &&
            (MonsterRewards is null || MonsterRewards.RecordedInputsAreCurrent()) &&
            (Flashing is null || Flashing.IsCurrentPlain());
    }

    private sealed class DragonhowlCooldownInputs
    {
        private readonly Dictionary<ushort, DateTimeOffset>? map;
        private readonly KeyValuePair<ushort, DateTimeOffset>[] values;
        internal DragonhowlCooldownInputs(IReadOnlyDictionary<ushort, DateTimeOffset>? source)
        {
            if (source is not null)
            {
                if (source.GetType() != typeof(Dictionary<ushort, DateTimeOffset>) ||
                    source is not Dictionary<ushort, DateTimeOffset> plain ||
                    !ReferenceEquals(plain.Comparer, EqualityComparer<ushort>.Default))
                    throw new InvalidDataException("ownedDragonhowlCooldownInputUnsupported");
                map = plain;
            }
            values = map?.ToArray() ?? [];
        }
        internal bool IsCurrentPlain(IReadOnlyDictionary<ushort, DateTimeOffset>? current) =>
            map is null ? current is null : ReferenceEquals(current, map) && map.Count == values.Length &&
                values.All(pair => map.TryGetValue(pair.Key, out var actual) && actual == pair.Value);
    }

    internal sealed partial class OwnedDragonhowlPreparation
    {
        internal readonly WorldSessionStore Store;
        internal readonly WorldSessionStore.OwnedDragonhowlOwner Owner;
        internal readonly WorldPacketHandlerContext Context;
        internal readonly MsgAttackPacket Packet;
        internal readonly WorldActiveSession? Primary;
        internal readonly LegacyPhysicalCombatGearSelection Selection;
        internal readonly IReadOnlyList<DragonhowlPhase> Phases;
        private readonly LegacyOwnedPhysicalMetadataWitness[] metadata;
        private readonly DragonhowlReceiveInputs[] receives;
        private readonly LegacyRuneCastEligibility? eligibility;
        private readonly Dictionary<ConquerTcpConnection, WorldSession> finals;
        private readonly LegacySpellAnimationPacket animation;

        private OwnedDragonhowlPreparation(WorldSessionStore store, WorldSessionStore.OwnedDragonhowlOwner owner,
            WorldPacketHandlerContext context, MsgAttackPacket packet, WorldActiveSession? primary,
            LegacyPhysicalCombatGearSelection selection, DragonhowlPhase[] phases,
            LegacyOwnedPhysicalMetadataWitness[] metadata, DragonhowlReceiveInputs[] receives,
            LegacyRuneCastEligibility? eligibility, Dictionary<ConquerTcpConnection, WorldSession> finals,
            LegacySpellAnimationPacket animation)
        {
            Store = store; Owner = owner; Context = context; Packet = packet; Primary = primary;
            Selection = selection; Phases = Array.AsReadOnly(phases); this.metadata = metadata; this.receives = receives;
            this.eligibility = eligibility; this.finals = finals; this.animation = animation;
        }

        internal bool TryGetFinalProjection(ConquerTcpConnection connection, out WorldSession final) =>
            finals.TryGetValue(connection, out final!);
        internal bool UsesRewardParticipant(ConquerTcpConnection connection) => Phases.Any(phase =>
            phase.TeamExperience is { } team && Store.IsDragonhowlRewardParticipant(Owner, team, connection));
        internal bool ContainsWornSnapshot(LegacyOrdinarySpellBinding binding,
            LegacyNativeRuneItemStorage.BasicPhysicalWornSnapshot snapshot, WorldSession original) =>
            receives.Any(input => input.ContainsWornSnapshot(binding, snapshot, original));
        internal bool ContainsPerfectionInputs(LegacyOrdinarySpellBinding binding, object inputs) =>
            receives.Any(input => input.ContainsPerfectionInputs(binding, inputs));
        internal bool InputsAreCurrentPlain() => TryReadCurrentInputs(out _);

        internal static bool TryPrepare(WorldSessionStore store, WorldSessionStore.OwnedDragonhowlOwner owner,
            WorldPacketHandlerContext context, MsgAttackPacket packet, WorldActiveSession? primary,
            LegacyPhysicalCombatGearSelection selection, WorldActiveSession[] participants,
            out OwnedDragonhowlPreparation? prepared, out string reason)
        {
            prepared = null; reason = "ownedDragonhowlPreparationUnavailable";
            if (!store.IsOwnedDragonhowlBeforeCurrent(owner) || context.Session is not { } actor ||
                context.WorldData is not { } catalog || selection.GearItemId != LegacyCombatGearDragonhowlItemId ||
                selection.PiratePassive || selection.Spell is not { Id: 17_490 or 17_650 } spell ||
                selection.PlayerSpell is not { } learned ||
                !LegacyRuneCastEligibility.TryCapture(context, actor, spell, out var eligibility) ||
                !TryValidateLegacyXpAdmissionPrerequisites(actor, spell, out reason) ||
                !TryPrepareLegacyGenericMagicPlan(context, actor, spell, true,
                    (context.TimeProvider ?? throw new InvalidOperationException("ownedDragonhowlClockMissing")).GetUtcNow(),
                    out var costs, out reason)) return false;
            if (ReadAttributeBool(actor.Character.Attributes, RuntimeLegacyInUseIntensifyAttribute) ||
                HasLegacyStatusFlag(actor.Character, LegacyFocusedFlag))
            { reason = "ownedDragonhowlUnjoined:focusedPrefix"; return false; }
            if (PrepareDragonhowlGeometry(context, actor, packet, spell) is not { } geometry)
            { reason = "ownedDragonhowlInvalidIronboneGeometry"; return false; }
            var iron = geometry.Iron;
            bool InLine(long x, long y) => geometry.Contains(x, y);

            // Every selected category is prepared before costs. An applicable
            // unjoined reward/event remains a refusal of the whole cast.

            var metadata = new List<LegacyOwnedPhysicalMetadataWitness>();
            var receives = new List<DragonhowlReceiveInputs>();
            var phases = new List<DragonhowlPhase>();
            bool retainedRewards = false;
            try
            {
                var finals = new Dictionary<ConquerTcpConnection, WorldSession>();
                var calculated = participants.ToDictionary(row => row.Connection, row => row.Session);
                var recipientData = new Dictionary<ConquerTcpConnection, DragonhowlLevelExperienceData>();
                var animationTargets = new List<LegacySpellAnimationTarget>();
                var source = actor;
                var actorBinding = actor.NativeGameClient!.OrdinarySpellBinding!;
                var ownMetadata = LegacyOwnedPhysicalMetadataWitness.CaptureDragonhowl(actorBinding, catalog, actor);
                ownMetadata.RequireRuntimeSpell(spell); metadata.Add(ownMetadata);
                var costActor = costs.Candidate with { Character = WithCharacterAttribute(costs.Candidate.Character, "Action", 0U) };
                Append(context.Connection, ref source, costActor, DragonhowlAudience.Owner, costs.Frames);
                if (!PrepareDragonhowlMonsterPhases(store, owner, context, spell, geometry, ownMetadata,
                    ref source, phases, finals, animationTargets, calculated, recipientData, out reason)) return false;
                foreach (var row in participants)
                {
                    if (ReferenceEquals(row.Connection, context.Connection) ||
                        !InLine(row.Session.Character.X, row.Session.Character.Y) || row.Session.Character.MinHitPoints <= 0 ||
                        HasLegacyStatusFlag(row.Session.Character, LegacyDeadFlag) || HasLegacyStatusFlag(row.Session.Character, LegacyGhostFlag)) continue;
                    var target = calculated[row.Connection];
                    if (!CanLegacyPlayerMagicDamageTarget(context, source, target, spell, out _, out var failure))
                    {
                        if (failure != LegacyPlayerMagicGateFailureEffect.None)
                        { reason = "ownedDragonhowlUnjoined:permissionEffect"; return false; }
                        continue;
                    }
                    var targetBinding = target.NativeGameClient!.OrdinarySpellBinding!;
                    var flashing = DragonhowlFlashingName.Prepare(context, actorBinding, source, target);
                    if (flashing is not null)
                        Append(context.Connection, ref source, flashing.After, DragonhowlAudience.OwnerVisible,
                            [flashing.Frame], flashing: flashing);
                    var witness = LegacyOwnedPhysicalMetadataWitness.CaptureDragonhowl(targetBinding, catalog, target);
                    metadata.Add(witness);
                    recipientData.TryGetValue(context.Connection, out var actorAward);
                    recipientData.TryGetValue(row.Connection, out var targetAward);
                    var calculation = DragonhowlReceiveCalculationData.Create(context.ProtocolProfile, context.ProtocolProfile,
                        actorAward?.FinalReceiveData, targetAward?.FinalReceiveData);
                    var input = new DragonhowlReceiveInputs(context, owner, packet, actorBinding, targetBinding, actor, row.Session,
                        source, target, actorAward?.FinalReceiveData, targetAward?.FinalReceiveData);
                    var damage = CalculateLegacyCombatGearTriple17490PlayerDamage(source, target, spell, catalog, calculation);
                    if (!input.TryPrepare(damage.Damage, out reason)) return false;
                    receives.Add(input);
                    if (HasLegacyStatusFlag(target.Character, LegacyHawksAmbitionFlag) ||
                        HasLegacyStatusFlag(target.Character, LegacyIronShieldFlag) ||
                        HasLegacyStatusFlag(target.Character, LegacyDragonSwingFlag) || HasLegacyStatusFlag(source.Character, LegacySlayerFlag))
                    { reason = "ownedDragonhowlUnjoined:consumedPhysicalModifier"; return false; }
                    var mutable = PrepareDragonhowlMutableStatus(context, source, target, catalog, calculation);
                    var flags = mutable.PublishesFrontBreak ? new[] { LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                        CheckedUInt32(source.Character.Uid, nameof(source.Character.Uid)), LegacyPlayerDeathState.CreateStatusFlagVector(
                            mutable.ActorAfter.Character.Attributes, LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)).ToFrameBytes() } : [];
                    Append(context.Connection, ref source, mutable.ActorAfter, DragonhowlAudience.OwnerVisible, flags);
                    Append(row.Connection, ref target, mutable.TargetAfter, DragonhowlAudience.TargetVisible, []);
                    var effect = mutable.EnteredCriticalBlock ? ResolveLegacyPlayerPlayerPhysicalEffectsCore(source, target, spell, catalog, damage, calculation) :
                        new LegacyPlayerPlayerPhysicalEffectsResolution(damage);
                    if (effect.RefineEffect != LegacyRefineEffect.None)
                    {
                        var refine = new LegacyMsgRefineEffectPacket(CheckedUInt32(source.Character.Uid, nameof(source.Character.Uid)),
                            Param: 0, Effect: effect.RefineEffect).ToFrameBytes();
                        Append(row.Connection, ref target, target, DragonhowlAudience.TargetVisible, [refine]);
                    }
                    var breakthrough = ApplyLegacyPlayerPlayerPhysicalSpellBreakthroughBranch(source, target, spell, catalog,
                        effect.DamageResult, calculation, applyPenetrationCompensation: false);
                    damage = FinishLegacyCombatGearTriple17490PlayerDamage(source, target, spell, catalog,
                        breakthrough.DamageResult, breakthrough.State, calculation);
                    damage = ApplyLegacyCombatGearTriple17490BattlePowerAdvantage(source, target, catalog, damage);
                    damage = ApplyLegacyPlayerPlayerPhysicalDefensiveStance(target, damage);
                    damage = ApplyLegacyPlayerPlayerPhysicalSpellInfinity(source, spell, damage);
                    damage = ApplyLegacyPlayerPlayerPhysicalSpellNoBreakthroughBonus(breakthrough.State, damage);
                    uint received = damage.Damage;
                    if (TryGetLegacyEligibleEquippedRuneLevel(source, catalog, "NoMercy", out _) && received < LegacyPhysicalNoMercyMinimumDamage)
                        received = LegacyPhysicalNoMercyMinimumDamage;
                    if (TryGetLegacyEligibleEquippedRuneLevel(source, catalog, "Assassin", out _) && ReadAttributeBool(source.Character.Attributes, RuntimeAutoHuntAttribute))
                        received = AddLegacySaturating(received, LegacyPhysicalAssassinAutoHuntDamage);
                    received = ApplyLegacyPlayerReceiveMudWall(target, catalog, received);
                    if (spell.Id == 17_650) received = MulDivToUInt32(received, 75, 100);
                    // This is the receive prefix after the spell's own 75% scale,
                    // not an extra spell-calculation multiplier. Consumed Hawks
                    // and shield branches require their separate concrete owners.
                    received = ApplyDragonhowlReceiveScalarModifiers(source.Character, received);
                    var previous = CheckedUInt32(target.Character.MinHitPoints, nameof(target.Character.MinHitPoints));
                    var sourceCharacter = WithCharacterAttribute(source.Character, RuntimeLegacyThundercloudSightAttribute,
                        CheckedUInt32(target.Character.Uid, nameof(target.Character.Uid)));
                    sourceCharacter = WithCharacterAttribute(sourceCharacter, RuntimeLegacyAttackHitAttribute, "true");
                    Append(context.Connection, ref source, source with { Character = sourceCharacter }, DragonhowlAudience.Owner, []);
                    var targetCharacter = WithCharacterAttribute(target.Character, RuntimeLegacyThundercloudSightAttribute,
                        CheckedUInt32(source.Character.Uid, nameof(source.Character.Uid)));
                    if (received >= previous)
                    {
                        if (targetAward is not null)
                        {
                            var futureDeath = store.PrepareDragonhowlFutureDeath(owner, targetBinding, source,
                                target, target with { Character = targetCharacter }, received);
                            Append(row.Connection, ref target, futureDeath.Projected, DragonhowlAudience.Owner, [], futureDeath: futureDeath);
                            animationTargets.Add(new(CheckedUInt32(target.Character.Uid, nameof(target.Character.Uid)), received, true, damage.Effect));
                            continue;
                        }
                        var death = store.PrepareDragonhowlDeath(owner, targetBinding,
                            target with { Character = targetCharacter }, received);
                        Append(row.Connection, ref target, death.Death.Proposed, DragonhowlAudience.Owner, [], death);
                        animationTargets.Add(new(CheckedUInt32(target.Character.Uid, nameof(target.Character.Uid)), received, true, damage.Effect));
                        continue;
                    }
                    var after = target with { Character = targetCharacter with { MinHitPoints = previous - received } };
                    uint maximum = GetLegacyEquipmentRuntimeMaxHitpoints(after, catalog, previous);
                    var hp = LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
                        CheckedUInt32(target.Character.Uid, nameof(target.Character.Uid)), maximum, previous - received)
                        .Select(update => update.ToFrameBytes()).ToArray();
                    Append(row.Connection, ref target, after, DragonhowlAudience.Owner, hp);
                    animationTargets.Add(new(CheckedUInt32(target.Character.Uid, nameof(target.Character.Uid)), received, true, damage.Effect));
                }
                if (!PrepareDragonhowlSobPhases(store, owner, context, spell, geometry, ownMetadata,
                    ref source, phases, finals, animationTargets, out reason)) return false;
                var animation = new LegacySpellAnimationPacket(CheckedUInt32(actor.Character.Uid, nameof(actor.Character.Uid)),
                    0, iron?.EndpointX ?? geometry.Triple.AnchorX, iron?.EndpointY ?? geometry.Triple.AnchorY, spell.Id,
                    (byte)Math.Min(learned.Level, byte.MaxValue), 0, learned.UseSpellSoul, 0, animationTargets);
                _ = animation.ToFrames();
                var result = new OwnedDragonhowlPreparation(store, owner, context, packet, primary, selection,
                    phases.ToArray(), metadata.ToArray(), receives.ToArray(), eligibility, finals, animation);
                if (!result.InputsAreCurrentPlain() || !store.IsOwnedDragonhowlBeforeCurrent(owner)) return false;
                prepared = result; retainedRewards = true; reason = string.Empty; return true;

                void Append(ConquerTcpConnection connection, ref WorldSession before, WorldSession after,
                    DragonhowlAudience audience, byte[][] frames, WorldSessionStore.OwnedDragonhowlDeath? death = null,
                    DragonhowlFlashingName? flashing = null, WorldSessionStore.OwnedDragonhowlFutureDeath? futureDeath = null)
                {
                    after = after with { Character = new BoundHitpointsInputs(before.Character, after.Character).After };
                    WorldSessionStore.OwnedDragonhowlAwardPhase? futureAward = null;
                    if (recipientData.ContainsKey(connection) && death is null && futureDeath is null)
                        futureAward = store.PrepareDragonhowlAwardPhase(owner,
                            before.NativeGameClient!.OrdinarySpellBinding!, before, after);
                    phases.Add(new(connection, before, after, audience, frames, death, flashing: flashing,
                        futureDeath: futureDeath, futureAward: futureAward));
                    finals[connection] = before = after;
                    calculated[connection] = after;
                }
            }
            finally
            {
                if (!retainedRewards)
                    foreach (var phase in phases) phase.MonsterRewards?.Dispose();
            }
        }
    }

    private static uint ApplyDragonhowlReceiveScalarModifiers(CharacterSnapshot actor, uint damage)
    {
        if (HasLegacyStatusFlag(actor, LegacyHookMoonCasterFlag))
            damage = MulDivToUInt32(damage, ReadAttributeUInt32(actor.Attributes, "HookMoonPower"), 100);
        if (HasLegacyStatusFlag(actor, LegacyCleanSweepFlag))
            damage = AddLegacySaturating(damage, MulDivToUInt32(damage,
                ReadAttributeUInt32(actor.Attributes, "CleanSweepPower", "LegacyCleanSweepPower"), 100));
        if (HasLegacyStatusFlag(actor, LegacyAxeShadowFlag))
            damage = AddLegacySaturating(damage, MulDivToUInt32(damage,
                ReadAttributeUInt32(actor.Attributes, "AxeShadowPower", "LegacyAxeShadowPower"), 100));
        if (HasLegacyStatusFlag(actor, LegacyAttackUpFlag)) damage = MulDivToUInt32(damage, 2, 1);
        return damage;
    }

    internal static bool IsOwnedDragonhowlObservedPlayer(WorldSession actor, WorldSession target) =>
        CanSeePlayer(actor.Character, target.Character) && ReadCharacterDynamicId(actor.Character) == ReadCharacterDynamicId(target.Character);

    internal static bool TryPrepareOwnedDragonhowlActual(WorldSessionStore store, WorldSessionStore.OwnedDragonhowlOwner owner,
        WorldPacketHandlerContext context, MsgAttackPacket packet, WorldActiveSession? primary,
        LegacyPhysicalCombatGearSelection selection, WorldActiveSession[] participants,
        out OwnedDragonhowlPreparation? prepared, out string reason) =>
        OwnedDragonhowlPreparation.TryPrepare(store, owner, context, packet, primary, selection, participants, out prepared, out reason);
}
