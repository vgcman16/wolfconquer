# VisibleDx8D13: genuine ordinary native hitpoints

Root qualification update: the authored source-only notes below describe the
initial cut. Native admission/storage and independent health/map controls passed
170/170; the genuine v9 sparse-image regression went red-to-green, and the later
class/HP/Rune selection passed 77/77 after its complete-image producer repair.
Selections overlap. The final combined native/ordinary/equipment/health/trade
selection passed587/587, zero skips. FSB-181 records the qualification boundary. Full CI,
deployment, native Team/death/revive and client acceptance remain pending.

## Identity and scope
- Draft2,2026-10-28; next isolated Sanitized Parity increment assigned by Root. Baseline public80fa53c9 plus reviewed FSB178 context; neither this change nor FSB178 is qualified by the tools full gate.
- Confirmed source gap: `LegacyNativePlayerRuntime.HitPointsRead.cs` declares private signed-int `_minhitpoints`; its getter/Alive/NotLife consume it, but no production assignment hydrates it. Managed `Character.MinHitPoints` and its2176 output are separate, so a genuinely admitted positive-HP player can remain physically dead to native readers.
- Implement only hydration/synchronization through actual ordinary admission and existing bound Store publication ownership. No public setter, fake alive projection, parallel HP owner, arbitrary accepting saver, native Team facade or extra notification sequence. Unbound physical storage/codec controls remain zero by construction unless their own isolated test explicitly seeds private state; they do not prove ordinary admission.

## Packet and numeric contract
- Native community reference `MrRayzo.Role/Player.cs:2609..2630` stores the assigned `int` directly; native3751..3762 sends max once then appends current signed HP. Existing Core native health goldens independently encode2176 UID field1, repeated update field2, DataType0/current and1/max, with current as unchecked sign-extended ulong varint. Managed `SendLegacyCurrentStatusHitpointPairAsync` already preserves this order and value source.
- Physical range is Int32.MinValue..Int32.MaxValue; reject an unrepresentable managed long before required commitment/publication, never truncate or invent a cap. Ordinary startup independently requires nonnegative HP, therefore its admitted intersection is0..Int32.MaxValue. Runtime signed values are not silently made alive or clamped; Alive remains the actual physical `HitPoints>0` predicate.
- Team member info's existing ushort projection and independent Status.MaxHitpoints uint are unchanged. This increment does not establish client acceptance of out-of-normal-play numeric extremes or implement the held native setter's Team/send side effects.

## Catalog/table contract
- No new catalog. Saved Character/account.ini `MinHitPoints` is the existing source scalar; Items/spells/profs keep their actual domains/owners. Maximum/derived HP remains the existing managed equipment query's responsibility; do not seed native Status.Max from current HP or add a minimum HP floor.

## Proposed ownership and publication contract
- Admission: prepare from the actual prefix capability, its original physical player and genuine final session before v7 writer. Include original raw HP in prewriter and postwriter plain proofs. Only after actual v7 success/prefix hydration may the prepared private scalar be stored alongside real map/binding/holder publication. Refusal/interruption leaves physical HP unchanged.
- Later bound publications: retain exact current holder, native identity, client/player, issued binding/recovery and expected physical HP. Prepare checked proposed int and an immutable exact Character afterimage before writers. Spell-only/HP-unchanged paths retain their existing lifecycle and perform no new HP file write. Retired/foreign/unbound records acquire no new authority from copied UID or current-looking data.
- Plain Set/CAS/completion/current-update: invoke the real registered Frame owner's typed `LegacyBoundHitpointsScalarWrite` BEFORE any Store/native publication. It captures the safe AccountId Character beforeimage, validates the actual expected scalar/storage baseline, projects the entire existing scalar saver input while preserving opaque text, and commits through the existing exact-image atomic writer. Definite refusal leaves the old holder/physical HP/file and no success; uncertain outcome fences the actual captured recovery. Actual true is followed by final raw proofs and plain native/holder stores.
- The concrete Frame postlude skips a duplicate scalar save only when that SAME completed typed write belongs to its exact issuer/connection/prior/final session and its exact afterimage covers the intended full scalar projection. A later HP-unchanged mana/other scalar update has no such typed participant and persists normally. Every further HP-changing Set/CAS prepares against the newly coherent real saved/Store/native state; no forever-deferred mirror or boolean already-saved shortcut exists.
- Externally persisted TryCommitSession/pair use the actual writer's true outcome and verify the exact prepared full Character afterimage before final plain proofs and native publication. No second independent HP writer runs. Mismatch is committed uncertainty: fence real recovery/retire, never false unchanged. The package's prepared-final callback path must retain its established HP/scalar contract or fail as committed rather than manufacture a late plan.
- No observer or packet send under Store/native/file gates. Keep all existing successful managed/wire sequences; the new participant emits no packets. Retired bindings remain pinned and cannot fall back or revive; retained raw values are historical state, not independent current-owner authority.

## Validation loop and expected controls
- Genuine owned ordinary admission observes saved/managed/native HP agreement at first actual output and after real equipment query; physical Alive/NotLife agree with that real scalar. Invalid signed range and actual precommit journal interruption do not hydrate a player.
- Actual bound HP change must show actual saved file before physical advance/first success; definite real writer/file refusal must advance neither managed nor physical HP or emit success. Ambiguous/postcommit publication failure preserves truthful historical outcome and actual recovery fence. Cover two HP changes and a later HP-unchanged scalar change in one logical Frame lifecycle.
- Exact stale holder, retired/reissued owner, raw physical drift and unbound storage controls; spell-only native changes preserve HP. Keep independent2176 byte goldens and existing zero-default unbound tests. Tests must not reflectively set HP to make admitted fixtures alive.
- Companion source-only test scope: helper-native-owned-hitpoints-tests-next. Root reviews actual API shape before composition and alone builds/runs focused and appropriate full qualification. No test/build/live action is claimed by this draft.

## Evidence gaps and limits
- Root approved the concrete prepublication writer shape with exact postlude coverage and sequential-write invariants; code/source-only qualification remains pending. Source contract is sufficient for physical int storage and current managed2176; raw client acceptance, full native HP setter/Team send graph, damage/death/revive side effects, SQL, power-loss and cross-process writer exclusion are not proven by hydration.
- Actual old Frame scalar postlude logs persistence failures. New bound HP changes require the real typed prepublication writer; unrelated scalar/sidecar behavior is not silently relabeled durable. Do not mistake this spec for a completed fix.

## Prospective modules and API
- New `LegacyNativePlayerRuntime.OwnedHitpoints.cs`: nested prepared physical scalar; private field remains private, no public setter or getter override. Existing read-only prerequisite comments become accurate about the separate owned writer.
- New `LegacyBoundHitpointsScalarWrite.cs` / `.Images.cs`: actual registered Frame owner/Store/binding, exact immutable before/after bytes and intended scalar values; `TryCommit`, `RequireExternallyCommitted`, and `CoversCommittedPublication` consume genuine lifecycle evidence, never a caller acceptance flag.
- New `WorldSessionStore.HitpointsPublication.cs`: shared orchestration/committed-fault classification; existing `PreparedSessionSpellPublication` and genuine ordinary admission include native HP proof/publication.
- Narrow existing callsites: Store main Set/CAS, SpellPublication, SpellAdmission, SpellProducers, PublicationObservers, PersistedTransactions and PreparedPersistence; specialized CompleteLogin is unchanged because its projection cannot change HP. The Frame persistence signature accepts the concrete typed completed write solely to skip the exact duplicate scalar phase. No broad Store/native refactor, new authority registry or changes to native spell-only publication.


## Source-only implementation boundary
- The authored source now uses the approved prepublication typed writer, full exact prepared external afterimage proof, and the existing strict dictionary captures. Complete old/new/canonical dictionary witnesses are allocated before callbacks and consumed at prewriter/postwriter/postlude boundaries; invalidated raw enumerators fail the proof. The postlude skip consumes its own full canonical projection witness and exact completed-plan identity, not a saved-HP boolean.
- Current health consumables change HP plus Items, call Store.Set, then attempt item persistence separately (`WorldPacketHandlers.ItemUsage.cs` health branch). This hydration increment does **not** close potion HP/item consumption atomicity. Compound inventory/resources and other sidecars need their own genuine joint transaction/caller migration; keep that gameplay safety gap OPEN. No new item saver or global transaction is invented here.
- Retired bindings cannot hydrate/mutate again or fall back to unbound publication. Retained positive physical HP is historical; this change deliberately does not falsify Alive or implement complete native map-view retirement. Bound current-owner checks remain required by consumers.
