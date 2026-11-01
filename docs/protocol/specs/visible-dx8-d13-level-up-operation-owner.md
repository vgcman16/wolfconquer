# D13 level-up operation owner — implementation prerequisite

Status: **OPEN / dedicated owner source authored, integration unqualified, 2026-11-01.**
The operation map below preceded implementation. A dedicated owner and its
query glue are now authored in the mutable candidate, with no build/test run
by this helper. The frozen attribute/query batches remain untouched. Target profile is `VisibleDx8D13`;
the local legacy-client/updater hold remains in force.

This continues [the level-up query gap](visible-dx8-d13-level-up-equipment-query-open.md)
and uses the completed-query producer described by
[owned attribute allocation](visible-dx8-d13-owned-attribute-set.md).
Sanitized Parity applies: source ordering is evidence to investigate and retain
where valid, not authority to reproduce unsafe spending or publication.

## Observed behavior and required order

The retained reference is
`DecompiledSource/GameServer/MrRayzo.Client/GameClient.cs` in the original
`征服-龙拳天下服务端-已修复/lqtxfwd` tree. `IncreaseExperience` at lines 976–1019
adds the award, subtracts each crossed threshold, and calls `UpdateLevel` for
**each** crossed level. `UpdateLevel` at lines 2131–2241 sends the level update,
view action 92, and the applicable auto-stat updates or attribute-point update;
it then calls `Equipment.QueryEquipment(Alternante, CallItems:false)` and only
afterward assigns current HP from the resulting `Status.MaxHitpoints`.
`IncreaseExperience` sends final EXP after the level loop and its reborn-level
check. `UpdateLevel` also has real guild, mentor, reborn and team-virtue side
effects; a query owner alone does not implement their owners.

The current `LegacyCombatExperienceProjection.Create` collapses all crossed
levels and calls the standalone `RefreshVitals`. Its current frame producer
then emits a modeled HP/mana bundle. Neither operation runs the equipment
query or establishes its completed status, magic, appearance or native maxima.
The standalone model must not substitute for the real query. In particular,
the reference restores **HP** after the query; a second modeled mana restore
is not justified by this reference.

The new actual-admission control
`LegacyOwnedCombatExperienceAutoStatsTests.AutoStatLevelUpRebuildsCompletedEquipmentStatusBeforeLaterStatusQuery`
uses two real equipped item rows and parsed Taoist level-11 stats. The required
transition is level 10→11, Vitality/Spirit 20, completed max HP 5540, max mana
750 and a subsequent real status-query magic attack of 21. This is authored
regression intent, not a passing result or live-client proof.

## Actual producer and operation map

Paths below are relative to `src/WolfConquer.Core/World/`. A shared or exclusive
lease serializes operations; it does not authorize an XP award or make several
files/actions crash atomic.

| Actual ingress / producer | Current outer lease and source | Required join |
| --- | --- | --- |
| Network physical and magic attack, including combat-gear and SOB branches | `WorldServerFrameHandler.Dispatch.cs` takes Shared for `MsgAttack`; dispatch passes the real lease in `FrameOperationLease`. Combat branches converge on `MonsterCombat.cs`'s private `ApplyLegacyFinalExperienceGainAsync`. | Take Exclusive at this **outer dispatch**, before resolving targets, paying spell costs or committing damage. Retain all actual physical/magic permission and transaction owners. Never acquire Exclusive inside the XP helper. |
| Pet attack | `WorldPacketHandlers.MsgAction.PetAttack.cs` calls the Strike experience path; ordinary `MsgAction` currently takes Shared. | Parse the actual PetAttack action for outer Exclusive classification; retain ordinary malformed-frame refusal and pet-owner checks. |
| Due owned physical repeat | `WorldServerFrameHandler.BasicPhysicalWorker.cs` takes Shared before due-request capture and calls `HandleFrameCoreAsync` with the actual operation and private repeat request. | Acquire Exclusive before capture/consumption of the repeat ticket, pass the same lease to Core, preserve generation-specific retirement. Do not call public `HandleFrameAsync` recursively. |
| Team XP recipients | `TryApplyLegacyMonsterTeamExperienceAsync` captures the real roster and invokes the same private XP producer for each recipient; there is no separate recipient world lease. | Retain the triggering operation's Exclusive and each recipient's own binding/holder/file proof. Hold the real team membership gate only during the synchronous recipient commit, never across query/output awaits. |
| Periodic ScarOfEarth, ShurikenVortex and ManiacDance | `WorldServerFrameHandler.RuntimeTicks.cs` wraps the global worker batch in Shared. The corresponding Frame partials call `CreateMonsterRuntimeContext`; that context currently supplies neither `TradeOperations` nor `FrameOperationLease`. | Establish explicit Exclusive worker phases **outside** the old Shared phase, before the actual due attack consumes state or deals damage. Pass the real operation through the Frame-created context. An inner acquisition while the global Shared is retained deadlocks. |
| Floor-spell XP | `MonsterRuntime.cs:SendDueFloorSpellEffectsAsync` runs under that same global Shared and dispatches TideTrap, LayTrap, Thundercloud, RageOfWar and combat-gear floor effects. Confirmed XP calls occur in `PlayerMagic.Traps.TideTrapRuntime.cs`, `PlayerMagic.RageOfWar.cs`, and `CombatGear.FloorAndPassives.cs`; this does not assert every listed floor effect awards XP. | Put this real floor-effect phase under its own outer Exclusive and propagate the issuing Frame operation. Resolve the actual effect owner/recipient rather than treating the first context session as the XP actor. |
| DragonFurnace monster-death reward XP | `MonsterDeath.InventoryRewards.DragonFurnace.cs` invokes `ApplyLegacyExperienceGainAsync` after its reward work, within the originating combat operation. | Reuse the combat operation and actual XP producer; do not claim earlier reward publication is rolled back when later XP/query preparation refuses. |
| ExpBall item 723700 | `ItemUsage.PackConsumables.cs` calculates levels, consumes the item/increments `ExpBallUsed`, and publishes via `PublishLegacyItemUsageSession`; ordinary item Use is outer Shared. | Needs its **item + Character + daily-counter** commit owner, then the common level-query continuation. Combat purpose cannot authorize this spend. Do not merely classify Exclusive and declare the existing generic publication transactional. |
| NPC ExpBall rewards | `NpcEntrypoints.AgatePrizeCenter.cs`, `PromotionMasters.QuestFinishRewards.cs`, and `WarEvents.Flame.cs` use `TryCalculateLegacyExpBallUse` and their reward/promotion publication. NPC dialog is normally outer Shared; BankCps handlers are an existing separately owned exception. | Each actual reward/quest/cost owner must admit the award and prepare the query before its joint commit. Route the recognized outer reward operation before Shared; do not lend a combat purpose or bypass required quest/item owners. |
| Blessing and mentor claims | `WorldPacketHandlers.BlessingInfo.cs` and `.Claim.cs` calculate ExpBall-style level/EXP changes. Their ordinary frames take Shared. | Preserve the actual claim counter/mentor entitlement owner and join its commit; no grant from a bare amount or packet mode. These are not covered by the combat XP producer. |
| Direct level-setting/admin services | `AdminUtilityNpcs.LevelOrProficiency.Mutations.cs`, `SocialCraftingServices.LevelAbdo.cs`, and the distinct FullAccount class operation | These are not XP awards. Existing native refusals remain until their own real transaction/query joins exist. FullAccount is a reference for an actual prepared query, not a transferable capability. |

Character XP is distinct from the player worker's XP-circle `XPCount`,
proficiency XP, InnerPower levels, and other subsystem experience. Those
counters must not acquire a level-up purpose by name matching.

Existing `HandlerOwnsOperation` routing in `WorldServerFrameHandler.ItemMutations.cs`
must remain first: trade, mailbox, item transfers, warehouse refresh, vendor,
BankCps and other real inner owners already acquire their own operation.
Wrapping those handlers in a new outer Exclusive would also deadlock.

## Smallest concrete operation and query join

1. Add a focused Frame routing partial for XP-capable **combat ingress**. The
   initial code edit is `WorldServerFrameHandler.Dispatch.cs` plus the new
   classifier, and the lease choice in `BasicPhysicalWorker.cs`. Reuse the
   existing barrier's `OwnsActiveExclusiveLease` test; do not introduce a bool
   grant or a second semaphore. The whole attack operation holds this lease,
   even when that particular attack ultimately awards no XP.

2. Split the global worker's lexical Shared scope at the existing positions of
   ScarOfEarth, ShurikenVortex, ManiacDance and the floor-effect phase. Preserve
   their order relative to all intervening expiry work. Each named phase takes
   Exclusive before capturing due work; intervening phases retain Shared.
   Use named real worker methods, not a caller-supplied action callback. Extend
   only the affected Frame methods/context construction to carry the exact
   operation and `_worldOperations`. Check recovery again after each queued
   acquisition. Do not replace the entire batch with Exclusive without auditing
   all its nested operation owners.

3. Retain the private `IssueCombatExperience`/CWT-backed `OwnedCombatExperience`
   as award provenance. Extend its captured source with the real Frame's active
   Exclusive operation proof and exact source/recipient bindings. Store-side
   validation must match the actual Frame's barrier, Store, catalog, roots,
   recovery and current native graph; a context that merely contains some
   active barrier is insufficient. An unregistered token, borrowed peer lease,
   disposed lease, synthetic context or replacement holder refuses.

4. Add a separate closed `PreparedLevelUpRebuild` producer, issued by
   `TryCommitCombatExperience` only after that purpose and the recipient's
   current owner pass. Proposed API shape:

   ```csharp
   PreparedLevelUpRebuild.TryPrepare(context, purpose, binding,
       expected, proposedLevelStep, out plan);
   plan.LockedSpellPath;       // actual prepared file owner, if required
   plan.IsCurrentPlain();
   plan.BindCommitted(actualPreparedFinalSession);
   await plan.RebuildAndRestoreAsync(actualReceipt, cancellationToken);
   ```

   These are proposed internal APIs, not existing code. The constructor stays
   private; no supplied calculator, persistence callback or success flag can
   create authority. A successful result must carry the actual completed query
   holder/continuation, because the query legitimately replaces the initial
   committed holder. It must not simply return the pre-query record or fetch a
   fresh arbitrary holder and call it the result.

5. Extract/reuse the **DATA** preflight work already used by
   `EquipmentQuery.AttributePreflight.cs`, `AttributeHitpointsPreflight.cs`,
   `AttributeMagicPreflight.cs`, and the selected derived-file preparation.
   Capture the genuine current item/native graph, selected metadata, source
   attributes/comparers, relevant roots and real derived spell file before
   committing the level step. Preserve the checked actual phase order. The XP
   operation must not call `PreparedAttributeRebuild.TryPrepare` or pretend to
   be a 2203 allocation. Existing missing/stale aggregate caches are inputs to
   rebuild, not proof of a completed query and not automatic rejection.

6. Extend `LegacyEquipmentQueryPublicationScope` and its currentness checks
   with this separate private level-up purpose. A closed purpose dispatch is
   acceptable; an arbitrary accepting callback is not. Add a specific rebuild
   reason. Invoke the existing `ExecuteLegacyEquipmentQueryAsync` directly in
   the same Frame operation with `CallItems:false`. Retain private query epoch,
   item/native custody, derived-file checks, completion publication and output
   order. Every publication must prove the actual binding is not retired and
   still belongs to its Frame/connection/player/Store; equal replacement values
   do not establish identity.

7. In the actual XP writer, use the established synchronous order: Frame
   lifecycle → package → recipient connection/native actors → short roster
   gate → one sorted Character/optional Spells file-lock set. Do not acquire
   PlayersSpells while already retaining only Users in reverse path order.
   Preflight and all known refusals precede the XP/stat durable mutation.
   Commit the real Character/native/holder step, release synchronous gates,
   send the applicable level/action92/stat-or-points frames, execute the full
   query, then commit current HP to the **actual completed** max through an
   actual bound HP writer. Send final EXP only after the required level work.
   The operation lease remains held across these awaits; monitors do not.

8. Use the existing `ActionPublication` failure pattern for postcommit send,
   query refusal, stale epoch, direct Store replacement, cancellation or HP
   publication uncertainty. The continuation must fence its real owner once
   and preserve the committed result; it must not classify these as unchanged
   rejections or repeat the XP grant. Receipt checks after legitimate query
   publications need the purpose's exact succession, not the initial receipt's
   old record and not an unchecked refresh. No global `Store.Set` lock is added.

## Progression and recovery boundaries that must stay explicit

The outer operation join is independently implementable, but it does not
resolve the existing multi-level collapse. A single final Character commit
does not authorize intermediate native/query states for earlier levels.
Conversely, per-level commits require a durable continuation/resumption rule
for remaining already-awarded XP after a committed query/output failure.
Before activating multi-level progression, choose and implement that real
boundary and retain the valid per-level wire sequence. Do not add a temporary
one-level-only production limit to make the single-transition test pass.

The first meaningful regression is the existing actual one-transition
auto-stat case; it is not completion of multi-level, item/NPC, mentor, reborn or
team-virtue behavior. The source's guild/mentor/team hooks need their actual
participants before positive behavior can be claimed. Changing a recipient
record or issuing a query-purpose token does not create those participants.

Even with an Exclusive lease, damage, earlier rewards, other team recipients,
the Character step, derived query files and final HP restoration are an
**ordered committed progression**, not one crash-atomic transaction. A process
crash may occur between them. Existing restart/login query reconstruction is
relevant evidence but is not a durable XP continuation or output outbox. No
fresh network request exactly-once claim is possible without a durable request
identity. Preserved Character bytes and pending recovery must be tested, not
inferred from a lock or a later successful login.

## Concrete implementation ownership and qualification

The authored `PreparedLevelUpRebuild` uses the agreed preparation/file-lock
API and `RebuildAndRestoreAsync`. It calls the actual query and existing bound
HP setter, then returns `CompletedLevelUpPublication.Session/TryCurrent`.
The caller owns one catch using the real receipt's new
`FenceCommittedLevelUpFailure`; the plan does not fence a second time.
The actual final query publication issues `CompletedMaximums` through a
private association with its epoch, binding, native status object and cache
identities. Allocation/association occurs while the proof is inert, before
the final plain native writes; no arbitrary token or equal cache can validate.
The query/HP continuation checks that proof before final EXP output.

Root owns the outer Frame/repeat/periodic lease changes, and the independent
XP producer helper owns Store/caller integration. Their combined activation
and verification are not established by this owner handoff. The current
producer draft explicitly holds multi-threshold awards pending their durable
continuation owner; that is an open limitation, not the intended final feature
or a substitute for the per-threshold source behavior above.

Proposed production edits are: Frame dispatch classifier and
`BasicPhysicalWorker`; `RuntimeTicks` and the four affected periodic context
paths; `CombatExperiencePurpose`, `MonsterCombat`, `LegacyCombatExperienceProjection`
and `WorldSessionStore.CombatExperience`; new focused level-query preparation,
query-currentness and HP-continuation partials; the existing query scope/guard
dispatch and rebuild-reason enum. Reuse existing packet codecs for level and
action92; preserve exact self/view recipient rules. Item/NPC/claim transactions
are separate explicit implementation scopes, not silently included by changing
a common helper.

Required actual-owner controls include the admitted auto-stat test; parsed
stat/metadata refusal before recipient XP changes; genuine network and server
repeat leases; periodic due-owner routing; disposed/foreign/shared lease
refusal; replacement holder and retired binding refusal; correct full query
status/magic/native maxima and post-query HP restore; team gate not retained
across output; output/query failure after durable commit plus genuine restart;
and multiple thresholds with no duplicate grant. Expected wire bytes/sequences
must come from the selected codec/source evidence, not captured output as its
own oracle. DATA projection tests alone do not establish any of these owners.

This document changes no frozen code and reports no build, test, client,
deployment or complete parity result.
