using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed class PreparedSessionSpellPublication : IDisposable
    {
        private readonly WorldSessionStore store;
        private readonly ConquerTcpConnection connection;
        private readonly SessionHolder holder;
        private readonly WorldSession? expected;
        private readonly LegacyOrdinarySpellBinding? binding;
        private readonly object finalIdentity;
        private readonly LegacyNativePlayerRuntime.OwnedHitpointsPublication? hitpoints;
        private LegacyNativePlayerRuntime.OwnedActionPublication? action;
        private LegacyNativePlayerRuntime.OwnedActorScalarsPublication? actorScalars;
        private readonly LegacyNativeEquipItemStorage.OwnedAlternatePublication? alternateEquipment;
        private readonly LegacyNativePlayerRuntime.OwnedRuneBagPublication? runeBag;
        private readonly OwnedWindWalkerProjection? windWalker;
        private readonly OwnedDragonhowlFlashingProjection? flashing;
        private readonly LegacyPreparedSpellFileWrite.RuneCharacterPublication? runePublication;
        private readonly LegacyPreparedPairMovementWrite.CharacterPublication? pairMovementPublication;
        private readonly LegacyPreparedMovementWrite.CharacterPublication? movementPublication;
        private readonly WorldPacketHandlers.OwnedPhysicalDeathPreparation? physicalDeath;
        private readonly OwnedDragonhowlDeath? dragonhowlDeath;
        private readonly OwnedHawksAmbitionPublication? hawks;
        private readonly WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation? basicPhysical;
        private readonly LegacyPreparedBasicPhysicalWrite.CharacterPublication? physicalDeathPublication;
        private readonly LegacyPreparedTeleportDestinationWrite.CharacterPublication? teleportPublication;
        private readonly LegacyPreparedDerivedSpellQueryWrite.Publication? derivedSpellQueryPublication;
        private readonly WorldPacketHandlers.OwnedFullAccountClassSpellRetirement? classRetirement;
        private LegacyNativePlayerRuntime.OwnedMoneyPublication? money;
        private LegacyNativePlayerRuntime.OwnedCpsPublication? cps;
        private LegacyNativePlayerRuntime.OwnedWarehouseMoneyPublication? warehouseMoney;
        internal LegacyBoundHitpointsScalarWrite? HitpointsWrite { get; }
        internal bool HasCommittedHitpoints => HitpointsWrite?.IsCommitted == true;
        private readonly SpellActorScope? actors;
        private readonly LegacyNativeSpellStorage.PreparedSessionSpellMap? map;
        private readonly LegacyNativeProficiencyStorage.PreparedSessionProficiencyMap? proficiencies;
        private LegacyOwnedNativeItemImage.Preparation? itemImage;
        private LegacyNativeOwnedItemGraph.PreparedPublication? items;
        private readonly bool deferItems;
        private bool deferredItemsPrepared;
        private OwnedVigorPublication? vigorPublication;
        private LegacyRootItemEffectFlagClock.PreparedSuccession? rootFlagSuccession;
        private RootClockPublication? rootFlagPermit;
        private LegacyRootItemEffectFlagClock.PreparedInstall? rootFlagInstall;
        internal WorldSession FinalSession { get; private set; }
        internal LegacyOrdinarySpellBinding? Binding => binding;
        internal object PublishedIdentity => finalIdentity;
        internal LegacyNativeSpellStorage.PreparedSessionSpellMap PreparedSpellGraph => map ?? throw InvalidSpellPublication();
        internal LegacyNativeProficiencyStorage.PreparedSessionProficiencyMap PreparedProficiencyGraph => proficiencies ?? throw InvalidSpellPublication();

        internal PreparedSessionSpellPublication(WorldSessionStore store, ConquerTcpConnection connection,
            SessionHolder holder, WorldSession? expected, WorldSession proposed,
            WorldPacketHandlers.LegacyCapriceCharacterFields? caprice = null, bool deferItems = false,
            LegacyPreparedSpellFileWrite.RuneCharacterPublication? runePublication = null,
            LegacyPreparedPairMovementWrite.CharacterPublication? pairMovementPublication = null,
            LegacyPreparedMovementWrite.CharacterPublication? movementPublication = null,
            WorldPacketHandlers.OwnedPhysicalDeathPreparation? physicalDeath = null,
            WorldPacketHandlers.OwnedHawksAmbitionCast? ownedHawksCast = null,
            WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation? basicPhysical = null,
            LegacyPreparedBasicPhysicalWrite.CharacterPublication? physicalDeathPublication = null,
            LegacyPreparedTeleportDestinationWrite.CharacterPublication? teleportPublication = null,
            LegacyPreparedDerivedSpellQueryWrite.Publication? derivedSpellQueryPublication = null,
            WorldPacketHandlers.OwnedFullAccountClassSpellRetirement? classRetirement = null,
            OwnedAttributeChange? attributeChange = null,
            WorldPacketHandlers.OwnedContinuationQueryMutation? continuationQuery = null,
            OwnedDragonhowlDeath? dragonhowlDeath = null,
            LegacyNativeStatusFlagsBigVector32.OwnedDragonhowlFlashingLease.ExpiryPublication? dragonhowlFlashingExpiry = null)
        {
            bool pendingContinuation = !LegacyExperienceContinuationFence.AllowsOrdinaryPublication(expected, proposed);
            if (pendingContinuation && (continuationQuery is not null ? derivedSpellQueryPublication is not null :
                    derivedSpellQueryPublication?.PermitsPendingContinuation(store, connection, expected, proposed) != true) ||
                !pendingContinuation && continuationQuery is not null)
                throw new InvalidOperationException("experienceContinuationPendingRequiresOwner");
            this.store = store; this.connection = connection; this.holder = holder; this.expected = expected;
            this.deferItems = deferItems;
            this.runePublication = runePublication;
            this.pairMovementPublication = pairMovementPublication;
            this.movementPublication = movementPublication;
            this.physicalDeath = physicalDeath;
            this.dragonhowlDeath = dragonhowlDeath;
            this.basicPhysical = basicPhysical; this.physicalDeathPublication = physicalDeathPublication;
            this.teleportPublication = teleportPublication;
            this.derivedSpellQueryPublication = derivedSpellQueryPublication;
            this.classRetirement = classRetirement;
            if (dragonhowlFlashingExpiry is not null && (expected is null || caprice is not null || deferItems ||
                runePublication is not null || pairMovementPublication is not null || movementPublication is not null ||
                physicalDeath is not null || ownedHawksCast is not null || basicPhysical is not null ||
                physicalDeathPublication is not null || teleportPublication is not null || derivedSpellQueryPublication is not null ||
                classRetirement is not null || attributeChange is not null || continuationQuery is not null || dragonhowlDeath is not null))
                throw InvalidSpellPublication();
            if (attributeChange is not null && (expected is null || classRetirement is not null || caprice is not null ||
                deferItems || runePublication is not null || pairMovementPublication is not null || movementPublication is not null ||
                physicalDeath is not null || ownedHawksCast is not null || basicPhysical is not null ||
                physicalDeathPublication is not null || teleportPublication is not null || derivedSpellQueryPublication is not null))
                throw InvalidSpellPublication();
            if (classRetirement is not null)
            {
                if (expected is null || caprice is not null || deferItems || runePublication is not null ||
                    pairMovementPublication is not null || movementPublication is not null || physicalDeath is not null ||
                    ownedHawksCast is not null || basicPhysical is not null || physicalDeathPublication is not null ||
                    teleportPublication is not null || derivedSpellQueryPublication is not null) throw InvalidSpellPublication();
                classRetirement.RequireCurrent(store, connection, expected, proposed);
            }
            if (expected is not null && physicalDeathPublication is null && derivedSpellQueryPublication is null && classRetirement is null)
                LegacyDerivedSpellOrigins.RequireUnchanged(expected.Character, expected.PlayerSpells, proposed.Character, proposed.PlayerSpells);
            if (derivedSpellQueryPublication is not null)
            {
                if (expected is null || caprice is not null || deferItems || runePublication is not null ||
                    pairMovementPublication is not null || movementPublication is not null || physicalDeath is not null ||
                    ownedHawksCast is not null || basicPhysical is not null || physicalDeathPublication is not null || teleportPublication is not null)
                    throw InvalidSpellPublication();
                derivedSpellQueryPublication.RequireCurrent(store, connection, expected, proposed);
            }
            if (teleportPublication is not null)
            {
                if (expected is null || caprice is not null || deferItems || runePublication is not null ||
                    pairMovementPublication is not null || movementPublication is not null || physicalDeath is not null ||
                    ownedHawksCast is not null || basicPhysical is not null || physicalDeathPublication is not null) throw InvalidSpellPublication();
                teleportPublication.RequireCurrent(store, connection, expected, proposed);
            }
            if (basicPhysical is not null && (expected is null || caprice is not null || deferItems ||
                runePublication is not null || pairMovementPublication is not null || movementPublication is not null || ownedHawksCast is not null ||
                !ReferenceEquals(basicPhysical.ExpectedTarget, expected) || !ReferenceEquals(basicPhysical.Death, physicalDeath)))
                throw InvalidSpellPublication();
            if (dragonhowlDeath is not null && (basicPhysical is not null || expected is null ||
                physicalDeathPublication is null || !ReferenceEquals(dragonhowlDeath.Death, physicalDeath) ||
                !ReferenceEquals(dragonhowlDeath.ExpectedTarget, expected) || !dragonhowlDeath.IsCurrentPlain()))
                throw InvalidSpellPublication();
            if (physicalDeathPublication is not null && (dragonhowlDeath is null
                    ? basicPhysical?.Death is null || !ReferenceEquals(basicPhysical.Death, physicalDeath)
                    : !ReferenceEquals(dragonhowlDeath.Death, physicalDeath)))
                throw InvalidSpellPublication();
            if (physicalDeath is not null && (basicPhysical is null && dragonhowlDeath is null || physicalDeathPublication is null))
                throw InvalidSpellPublication();
            if (ownedHawksCast is not null && (caprice is not null || deferItems || runePublication is not null ||
                pairMovementPublication is not null || movementPublication is not null || physicalDeath is not null))
                throw InvalidSpellPublication();
            if (physicalDeath is not null && (expected is null || caprice is not null || deferItems ||
                runePublication is not null || pairMovementPublication is not null || movementPublication is not null))
                throw InvalidSpellPublication();
            if (movementPublication is not null)
            {
                if (expected is null || caprice is not null || deferItems || runePublication is not null || pairMovementPublication is not null)
                    throw InvalidSpellPublication();
                movementPublication.RequireCurrent(store, connection, expected, proposed);
            }
            if (pairMovementPublication is not null)
            {
                if (expected is null || caprice is not null || deferItems || runePublication is not null)
                    throw InvalidSpellPublication();
                pairMovementPublication.RequireCurrent(store, connection, expected, proposed);
            }
            if (runePublication is not null)
            {
                if (expected is null || caprice is not null || deferItems) throw InvalidSpellPublication();
                runePublication.RequireCurrent(store, connection, expected, proposed);
            }
            binding = holder.SpellBinding;
            finalIdentity = expected is not null && expected.AccountId == proposed.AccountId &&
                expected.Character.Uid == proposed.Character.Uid ? holder.NativeClientIdentity : new object();
            if (binding is null)
            {
                if (dragonhowlFlashingExpiry is not null) throw InvalidSpellPublication();
                if (continuationQuery is not null) throw InvalidSpellPublication();
                if (derivedSpellQueryPublication is not null) throw InvalidSpellPublication();
                if (ownedHawksCast is not null) throw InvalidSpellPublication();
                if (basicPhysical is not null || physicalDeathPublication is not null) throw InvalidSpellPublication();
                // A retired/foreign attachment is never an unbound fallback.
                if (connection.NativeGameClient?.OrdinarySpellBinding is not null ||
                    proposed.NativeGameClient?.OrdinarySpellBinding is not null ||
                    connection.NativeGameClient?.Player?.HasOwnedVitalsAttachment == true ||
                    proposed.NativeGameClient?.Player?.HasOwnedVitalsAttachment == true ||
                    connection.NativeGameClient?.Player?.HasOwnedActionAttachment == true ||
                    proposed.NativeGameClient?.Player?.HasOwnedActionAttachment == true ||
                    connection.NativeGameClient?.Player?.HasOwnedActorScalarsAttachment == true ||
                    proposed.NativeGameClient?.Player?.HasOwnedActorScalarsAttachment == true ||
                    connection.NativeGameClient?.Player?.HasOwnedMoneyAttachment == true ||
                    proposed.NativeGameClient?.Player?.HasOwnedMoneyAttachment == true ||
                    connection.NativeGameClient?.Player?.HasOwnedCpsAttachment == true ||
                    proposed.NativeGameClient?.Player?.HasOwnedCpsAttachment == true ||
                    connection.NativeGameClient?.Player?.HasOwnedWarehouseMoneyAttachment == true ||
                    proposed.NativeGameClient?.Player?.HasOwnedWarehouseMoneyAttachment == true) throw InvalidSpellPublication();
                FinalSession = proposed;
                return;
            }
            if (expected is null || proposed.AccountId != binding.AccountId || proposed.Character.Uid != binding.Uid ||
                !string.Equals(proposed.UsersRoot, binding.SessionUsersRoot, StringComparison.Ordinal) ||
                !ReferenceEquals(proposed.NativeGameClient, binding.Client) ||
                !store.BindingIsCurrentPlain(binding, holder, expected)) throw InvalidSpellPublication();
            actors = new(binding);
            try
            {
                if (continuationQuery is not null && (expected is null || !ReferenceEquals(continuationQuery.Expected, expected) ||
                    !ReferenceEquals(continuationQuery.Proposed, proposed) || !continuationQuery.IsCurrentPlain(binding) ||
                    caprice is not null || deferItems || runePublication is not null || pairMovementPublication is not null ||
                    movementPublication is not null || physicalDeath is not null || ownedHawksCast is not null || basicPhysical is not null ||
                    physicalDeathPublication is not null || teleportPublication is not null || derivedSpellQueryPublication is not null ||
                    classRetirement is not null || attributeChange is not null)) throw InvalidSpellPublication();
                if (basicPhysical is not null && (!ReferenceEquals(basicPhysical.TargetBinding, binding) ||
                    !basicPhysical.IsCurrentPlain() || !basicPhysical.CanonicalOutcomesMatch(basicPhysical.Actor, proposed)))
                    throw InvalidSpellPublication();
                physicalDeathPublication?.RequireCurrent(store, connection, expected, proposed);
                flashing = OwnedDragonhowlFlashingProjection.Prepare(binding, expected, proposed, physicalDeath, dragonhowlFlashingExpiry);
                if (physicalDeath is not null)
                {
                    // The actual death plan already projects its privately
                    // derived194 outcome against this exact before vector.
                    // Reapplying the generic old194 snapshot would overwrite
                    // that result. This typed plan is the only alternate path.
                    if (!ReferenceEquals(physicalDeath.TargetBinding, binding) ||
                        !ReferenceEquals(physicalDeath.ExpectedTarget, expected) ||
                        !Monitor.IsEntered(binding.Player.BitVector.SyncRoot) ||
                        !physicalDeath.TryReadNativeWindWalkerProposal(binding, proposed, out _, out _, out _))
                        throw InvalidSpellPublication();
                }
                else windWalker = OwnedWindWalkerProjection.PrepareBound(binding, proposed);
                if (windWalker is not null)
                {
                    if (runePublication is not null && !ReferenceEquals(proposed.Character, windWalker.Proposed.Character))
                        throw InvalidSpellPublication();
                    proposed = windWalker.Proposed;
                }
                binding.Items.RequireCurrentBeforePreparation();
                alternateEquipment = LegacyNativeEquipItemStorage.OwnedAlternatePublication.PrepareBound(binding, expected, proposed);
                runeBag = runePublication?.PrepareBagNativePublication(binding, expected, proposed);
                map = LegacyNativeSpellStorage.PrepareSessionSnapshot(binding.Client, binding.Player,
                    expected.PlayerSpells, proposed.PlayerSpells);
                proficiencies = LegacyNativeProficiencyStorage.PrepareSessionSnapshot(binding.Client, binding.Player,
                    expected.PlayerProficiencies, proposed.PlayerProficiencies);
                var final = ReferenceEquals(proposed.PlayerSpells, map.FinalSnapshot) &&
                    ReferenceEquals(proposed.PlayerProficiencies, proficiencies.FinalSnapshot) ? proposed :
                    proposed with { PlayerSpells = map.FinalSnapshot, PlayerProficiencies = proficiencies.FinalSnapshot };
                if (!deferItems)
                {
                    PrepareItems(final);
                    if (!ReferenceEquals(final.PlayerItems, itemImage!.FinalSnapshot))
                        final = final with { PlayerItems = itemImage.FinalSnapshot };
                }
                hitpoints = LegacyNativePlayerRuntime.OwnedHitpointsPublication.PrepareBound(binding, expected, final);
                if (teleportPublication is not null || basicPhysical is not null || dragonhowlDeath is not null || movementPublication is not null || pairMovementPublication is not null || expected.Character.MinHitPoints != final.Character.MinHitPoints ||
                    expected.Character.MinMana != final.Character.MinMana || caprice is not null)
                    HitpointsWrite = (store.framePersistenceOwner ?? throw InvalidSpellPublication())
                        .PrepareNativeHitpointsWrite(store, connection, binding, expected, final, caprice, runePublication, pairMovementPublication, movementPublication,
                            basicPhysical, physicalDeathPublication, teleportPublication, classRetirement, continuationQuery, dragonhowlDeath);
                FinalSession = HitpointsWrite?.Proposed ?? final;
                if (continuationQuery is not null && !SameContinuationQuerySession(continuationQuery.Proposed, FinalSession))
                    throw InvalidSpellPublication();
                hawks = OwnedHawksAmbitionPublication.PrepareBound(binding, expected, FinalSession, ownedHawksCast);
                action = LegacyNativePlayerRuntime.OwnedActionPublication.PrepareBound(binding, expected, FinalSession);
                actorScalars = LegacyNativePlayerRuntime.OwnedActorScalarsPublication.PrepareBound(binding, expected, FinalSession, pairMovementPublication, movementPublication, teleportPublication, classRetirement, attributeChange);
                money = LegacyNativePlayerRuntime.OwnedMoneyPublication.PrepareBound(binding, expected, FinalSession);
                cps = LegacyNativePlayerRuntime.OwnedCpsPublication.PrepareBound(binding, expected, FinalSession);
                warehouseMoney = LegacyNativePlayerRuntime.OwnedWarehouseMoneyPublication.PrepareBound(binding, expected, FinalSession);
                RequireCurrentBeforeWriter();
            }
            catch { items?.Dispose(); windWalker?.Dispose(); flashing?.Dispose(); actors.Dispose(); throw; }
        }

        private void PrepareItems(WorldSession proposed)
        {
            if (binding is null || items is not null) throw InvalidSpellPublication();
            binding.Items.RequireCurrentBeforePreparation();
            var context = (store.framePersistenceOwner ?? throw InvalidSpellPublication()).RequireOwnedNativeItemContext();
            if (!ReferenceEquals(context.Catalog, binding.ItemCatalog) || context.Profile != binding.ItemProfile)
                throw InvalidSpellPublication();
            itemImage = LegacyOwnedNativeItemImage.Prepare(context.Catalog, proposed.PlayerItems,
                context.Profile, binding.Items.Image);
            if (!ReferenceEquals(itemImage.SourceSnapshot, proposed.PlayerItems) ||
                !ReferenceEquals(itemImage.BaseImage, binding.Items.Image)) throw InvalidSpellPublication();
            items = LegacyNativeOwnedItemGraph.PrepareRefresh(binding.Items, itemImage);
            if (!ReferenceEquals(items.Graph, binding.Items) ||
                !ReferenceEquals(items.FinalSnapshot, itemImage.FinalSnapshot)) throw InvalidSpellPublication();
        }

        // Only the actual Store-issued mailbox capability calls this before its
        // real joint writer. Spell/vital ownership cannot be replaced by it.
        internal WorldSession PrepareDeferredItems(WorldSession proposed)
        {
            if (!deferItems || deferredItemsPrepared || expected is null ||
                !ReferenceEquals(holder.Read(), expected) ||
                proposed.AccountId != expected.AccountId || proposed.Character.Uid != expected.Character.Uid ||
                proposed.Character.MinHitPoints != expected.Character.MinHitPoints || proposed.Character.MinMana != expected.Character.MinMana ||
                !ReferenceEquals(proposed.PlayerSpells, expected.PlayerSpells) ||
                !ReferenceEquals(proposed.PlayerProficiencies, expected.PlayerProficiencies) ||
                !ReferenceEquals(proposed.NativeGameClient, expected.NativeGameClient) ||
                !string.Equals(proposed.UsersRoot, expected.UsersRoot, StringComparison.Ordinal)) throw InvalidSpellPublication();
            deferredItemsPrepared = true;
            if (binding is not null)
            {
                if (LegacyActionState.RequireCurrent(proposed.Character) != LegacyActionState.RequireCurrent(expected.Character))
                    throw InvalidSpellPublication();
                if (LegacyNativeActorScalarState.RequireCurrent(proposed.Character) !=
                    LegacyNativeActorScalarState.RequireCurrent(expected.Character)) throw InvalidSpellPublication();
                PrepareItems(proposed);
                proposed = proposed with { PlayerItems = itemImage!.FinalSnapshot,
                    PlayerSpells = map!.FinalSnapshot, PlayerProficiencies = proficiencies!.FinalSnapshot };
                action = LegacyNativePlayerRuntime.OwnedActionPublication.PrepareBound(binding, expected, proposed);
                actorScalars = LegacyNativePlayerRuntime.OwnedActorScalarsPublication.PrepareBound(binding, expected, proposed);
                money = LegacyNativePlayerRuntime.OwnedMoneyPublication.PrepareBound(binding, expected, proposed);
                cps = LegacyNativePlayerRuntime.OwnedCpsPublication.PrepareBound(binding, expected, proposed);
                warehouseMoney = LegacyNativePlayerRuntime.OwnedWarehouseMoneyPublication.PrepareBound(binding, expected, proposed);
            }
            FinalSession = proposed;
            RequireCurrentBeforeWriter();
            return FinalSession;
        }

        // The actual deferred-mail capability calls this after recipe/catalog
        // preparation and before its producer can reserve any item identities.
        internal void RequireCurrenciesBeforeReservation(WorldSession proposed)
        {
            if (!deferItems || deferredItemsPrepared || expected is null ||
                !ReferenceEquals(holder.Read(), expected) || proposed.AccountId != expected.AccountId ||
                proposed.Character.Uid != expected.Character.Uid ||
                !ReferenceEquals(proposed.NativeGameClient, expected.NativeGameClient) ||
                !string.Equals(proposed.UsersRoot, expected.UsersRoot, StringComparison.Ordinal))
                throw InvalidSpellPublication();
            RequireCurrentBeforeWriter();
            if (binding is null) return; // Wholly managed path has no fabricated native limits/owner.
            LegacyNativePlayerRuntime.OwnedCpsPublication.RequireRepresentable(proposed.Character);
            var proposedWarehouseMoney = new LegacyWarehouseMoneyState.NativeValueProof(proposed.Character);
            // No callback-bearing read follows this complete native tuple proof.
            if (!store.BindingIsCurrentPlain(binding, holder, expected) ||
                !money!.IsCurrentPlain() || !cps!.IsCurrentPlain() || !warehouseMoney!.IsCurrentPlain() ||
                !proposedWarehouseMoney.IsCurrentPlain()) throw InvalidSpellPublication();
        }

        internal byte[] PrepareReviverSpawn(uint maximumHitpoints)
        {
            if (binding is null || action is null || expected is null) throw InvalidSpellPublication();
            RequireCurrentBeforeWriter();
            var attributes = LegacyNativeAttributeMapProof.CaptureRaw(FinalSession.Character.Attributes);
            var frame = LegacyPlayerSpawnPacket.FromPreparedOwnedAction(action, binding, expected,
                FinalSession, maximumHitpoints).ToFrameBytes();
            RequireCurrentBeforeWriter();
            if (!attributes.IsCurrentPlain()) throw InvalidSpellPublication();
            return frame;
        }

        internal void RequireCurrentBeforeWriter()
        {
            if (!ReferenceEquals(holder.Read(), expected) || binding is not null &&
                !store.BindingIsCurrentPlain(binding, holder, expected!)) throw InvalidSpellPublication();
            map?.RequireCurrentBeforeWriter();
            proficiencies?.RequireCurrentBeforeWriter();
            items?.RequireCurrentBeforeWriter();
            if (hitpoints is not null && !hitpoints.IsCurrentPlain() || action is not null && !action.IsCurrentPlain() ||
                actorScalars is not null && !actorScalars.IsCurrentPlain() || money is not null && !money.IsCurrentPlain() ||
                cps is not null && !cps.IsCurrentPlain() || warehouseMoney is not null && !warehouseMoney.IsCurrentPlain() ||
                alternateEquipment is not null && !alternateEquipment.IsCurrentPlain(FinalSession.AlternateEquipmentActive) ||
                runeBag is not null && !runeBag.IsCurrentPlain())
                throw InvalidSpellPublication();
            if (windWalker is not null && !windWalker.IsCurrentPlain()) throw InvalidSpellPublication();
            if (flashing is not null && !flashing.IsCurrentPlain(FinalSession)) throw InvalidSpellPublication();
            if (physicalDeath is not null && !physicalDeath.IsCurrentPlain(binding!)) throw InvalidSpellPublication();
            if (hawks is not null && !hawks.IsCurrentPlain()) throw InvalidSpellPublication();
            if (basicPhysical is not null && !basicPhysical.IsCurrentPlain()) throw InvalidSpellPublication();
            if (derivedSpellQueryPublication is not null && !derivedSpellQueryPublication.IsCurrentPlain()) throw InvalidSpellPublication();
            if (classRetirement is not null && !classRetirement.IsCurrentPlain()) throw InvalidSpellPublication();
            if (binding is not null && !store.BindingIsCurrentPlain(binding, holder, expected!)) throw InvalidSpellPublication();
        }

        internal void AttachRootClockInstall(LegacyRootItemEffectFlagClock.PreparedInstall prepared)
        {
            if (rootFlagInstall is not null || !prepared.IsCurrentPlain()) throw InvalidSpellPublication();
            rootFlagInstall = prepared;
        }

        internal void ArmFinalProof()
        {
            if (deferItems && !deferredItemsPrepared) throw InvalidSpellPublication();
            if (binding is not null && vigorPublication is null)
                vigorPublication = PrepareVigorPublication(store, connection, holder, binding,
                    expected ?? throw InvalidSpellPublication(), FinalSession, finalIdentity);
            rootFlagSuccession ??= LegacyRootItemEffectFlagClock.PreparedSuccession.Prepare(
                store, connection, expected, FinalSession, finalIdentity);
            if (rootFlagSuccession is not null && rootFlagPermit is null)
                rootFlagPermit = RootClockPermit(store, connection, holder, expected, FinalSession, finalIdentity, rootFlagSuccession);
            map?.ArmFinalProof();
            proficiencies?.ArmFinalProof();
            items?.ArmFinalProof();
            HitpointsWrite?.Inputs.Arm();
        }
        internal bool ConsumeBeforeWriterPlain() => ReferenceEquals(holder.Read(), expected) &&
            (rootFlagSuccession is null || rootFlagPermit is not null && RootClockPermitBefore(rootFlagPermit)) &&
            (rootFlagInstall is null || rootFlagInstall.IsCurrentPlain()) &&
            (binding is null || vigorPublication is not null && VigorPublicationReady(vigorPublication)) &&
            (classRetirement is null || classRetirement.IsCurrentPlain()) &&
            (binding is null || store.BindingIsCurrentPlain(binding, holder, expected!) && map!.ConsumeBeforeWriterPlain() &&
                proficiencies!.ConsumeBeforeWriterPlain() && items!.ConsumeBeforeWriterPlain() && hitpoints!.IsCurrentPlain() && action!.IsCurrentPlain() && actorScalars!.IsCurrentPlain() &&
                money!.IsCurrentPlain() && cps!.IsCurrentPlain() && warehouseMoney!.IsCurrentPlain() && alternateEquipment!.IsCurrentPlain(FinalSession.AlternateEquipmentActive) && (runeBag is null || runeBag.IsCurrentPlain()) &&
                (windWalker is null || windWalker.IsCurrentPlain()) &&
                (flashing is null || flashing.IsCurrentPlain(FinalSession)) &&
                (physicalDeath is null || physicalDeath.IsCurrentPlain(binding)) &&
                (hawks is null || hawks.IsCurrentPlain()) &&
                (basicPhysical is null || basicPhysical.IsCurrentPlain()) &&
                (derivedSpellQueryPublication is null || derivedSpellQueryPublication.IsCurrentPlain()) &&
                (HitpointsWrite is null || HitpointsWrite.Inputs.ConsumeBeforePlain()));

        // The raw map enumerator was allocated before the writer. Consume it
        // once, without a comparer or a fresh iterator at this final boundary.
        internal bool IsCurrentPlain() => ReferenceEquals(holder.Read(), expected) &&
            (rootFlagSuccession is null || rootFlagPermit is not null && RootClockPermitBefore(rootFlagPermit)) &&
            (rootFlagInstall is null || rootFlagInstall.IsCurrentPlain()) &&
            (binding is null || vigorPublication is not null && VigorPublicationReady(vigorPublication)) &&
            (classRetirement is null || classRetirement.IsCurrentPlain()) &&
            (binding is null || store.BindingIsCurrentPlain(binding, holder, expected!) && map!.ConsumeCurrentPlain() &&
                proficiencies!.ConsumeCurrentPlain() && items!.ConsumeCurrentPlain() && hitpoints!.IsCurrentPlain() && action!.IsCurrentPlain() && actorScalars!.IsCurrentPlain() &&
                money!.IsCurrentPlain() && cps!.IsCurrentPlain() && warehouseMoney!.IsCurrentPlain() && alternateEquipment!.IsCurrentPlain(FinalSession.AlternateEquipmentActive) && (runeBag is null || runeBag.IsCurrentPlain()) &&
                (windWalker is null || windWalker.IsCurrentPlain()) &&
                (flashing is null || flashing.IsCurrentPlain(FinalSession)) &&
                (physicalDeath is null || physicalDeath.IsCurrentPlain(binding)) &&
                (hawks is null || hawks.IsCurrentPlain()) &&
                (basicPhysical is null || basicPhysical.IsCurrentPlain()) &&
                (derivedSpellQueryPublication is null || derivedSpellQueryPublication.IsCurrentPlain()) &&
                (HitpointsWrite is null || HitpointsWrite.IsCommitted && HitpointsWrite.Inputs.ConsumeAfterPlain()));

        // Call only after the final plain proof and actual successful writer.
        internal void PublishNative()
        {
            if (binding is null) return;
            map!.PublishPreparedMap();
            proficiencies!.PublishPreparedMap();
            items!.PublishPrepared();
            alternateEquipment!.PublishPrepared();
            runeBag?.PublishPrepared();
            binding.PublishSnapshots(FinalSession.PlayerSpells, FinalSession.PlayerProficiencies);
            binding.PublishItemSnapshot(FinalSession.PlayerItems);
            hitpoints!.PublishPrepared();
            action!.PublishPrepared();
            actorScalars!.PublishPrepared();
            money!.PublishPrepared();
            cps!.PublishPrepared();
            warehouseMoney!.PublishPrepared();
            hawks?.PublishPrepared();
        }

        internal bool AcceptExternallyPersistedHitpoints() => HitpointsWrite is not null
            ? HitpointsWrite.RequireExternallyCommitted() : classRetirement?.RequireCommittedPair() ?? derivedSpellQueryPublication?.RequireCommittedPair() ??
                movementPublication?.RequireCommittedCharacter() ?? pairMovementPublication?.RequireCommittedCharacter() ?? runePublication?.RequireCommittedCharacter() ?? true;

        internal void PublishHolder()
        {
            var permit = vigorPublication;
            var retention = permit is null ? null : VigorRetention(permit);
            holder.PublishPrepared(FinalSession, finalIdentity);
            if (rootFlagSuccession is not null)
                rootFlagSuccession.Publish(rootFlagPermit ?? throw InvalidSpellPublication());
            if (binding is not null)
                binding.PublishVigorRetention(retention, permit ?? throw InvalidSpellPublication());
        }

        public void Dispose()
        {
            rootFlagSuccession?.Dispose();
            items?.Dispose();
            windWalker?.Dispose();
            flashing?.Dispose();
            actors?.Dispose();
        }
    }

    private bool BindingIsCurrentPlain(LegacyOrdinarySpellBinding binding, SessionHolder holder, WorldSession expected) =>
        ReferenceEquals(binding.Store, this) && _sessions.TryGetValue(binding.Connection, out var actual) &&
        ReferenceEquals(actual, holder) && ReferenceEquals(holder.SpellBinding, binding) &&
        ReferenceEquals(holder.NativeClientIdentity, binding.NativeIdentity) && ReferenceEquals(holder.Read(), expected) &&
        expected.AccountId == binding.AccountId && expected.Character.Uid == binding.Uid &&
        string.Equals(expected.UsersRoot, binding.SessionUsersRoot, StringComparison.Ordinal) &&
        ReferenceEquals(expected.NativeGameClient, binding.Client) && ReferenceEquals(expected.PlayerSpells, binding.Snapshot) &&
        ReferenceEquals(expected.PlayerProficiencies, binding.ProficiencySnapshot) &&
        ReferenceEquals(expected.PlayerItems, binding.ItemSnapshot) &&
        binding.PhysicalOwnerIsCurrentPlain() && binding.Player.AttributeStateMatches(expected);

    internal bool IsBoundSpellOwnerCurrent(LegacyOrdinarySpellBinding binding) =>
        _sessions.TryGetValue(binding.Connection, out var holder) && holder.Read() is { } current &&
        BindingIsCurrentPlain(binding, holder, current);

    private static InvalidOperationException InvalidSpellPublication() =>
        new("ordinarySpellPublicationRequiresCurrentCompleteOwner");

    private static void RefuseBoundSpellReentry(ConquerTcpConnection connection)
    {
        // A comparer/serializer on any holder may not acquire another Store
        // writer gate, including an otherwise unbound target connection.
        if (IsCurrentThreadInsideAnyConnectionGate || LegacyRuneRankingState.IsCurrentThreadInsideLease)
            throw InvalidSpellPublication();
        if (connection.NativeGameClient is { } native &&
            (Monitor.IsEntered(native.SpellAdmissionSyncRoot) ||
             native.Player is { } player && Monitor.IsEntered(player.SpellAdmissionSyncRoot) ||
             native.MySpells is { } storage && Monitor.IsEntered(storage.MutationGate) ||
             native.MyProfs is { } profs && Monitor.IsEntered(profs.MutationGate) ||
             native.OrdinarySpellBinding is { } binding &&
                (Monitor.IsEntered(binding.Storage.MutationGate) || Monitor.IsEntered(binding.Proficiencies.MutationGate)) ||
             LegacyNativeNinja.HasActiveOwnedPreparation(native))) throw InvalidSpellPublication();
    }

}
