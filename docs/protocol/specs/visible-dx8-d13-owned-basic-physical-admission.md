# Owned basic physical attack admission and publication

## Identity and scope

Revision2,2026-11-01; Root engineering requirement. Target is the existing
Chinese7899/VisibleDx8D13 profile. This is source/schema evidence, not7937 or new
live-client acceptance. The required physical horizon includes actual zero/miss,
nonlethal and normal-death receives, real repeat/cancellation and failure order.
Applicable unjoined modifiers remain explicit unfinished work, never ordinary
fallback success or proof that complete combat is implemented.

Reference: retained GameServer/MrRayzo.Game.MsgServer/MsgAttackPacket.cs,
HandlerProcess148-150, Physical623+ and repeat740+; Role.Instance/Equip.cs163-184.
The current counterparts are MsgAttackPacket, WorldPacketHandlers.PlayerCombat
and the actual Frame/Store owned admission/receive paths. No native execution is
required to read these source references.

## Packet contract

Retain2135/Physical2, existing Protobuf varints and successful Interaction-before-
HP notification order. Do not invent a2046 stance echo for the silent Action reset.
Native InteractQuery attributes and current MsgAttackPacket writer agree:

| Field | Number | Declared width | Rule |
| --- | --- | --- | --- |
| Actor/opponent UID |4/5|UInt32|Resolve actual authenticated current participants |
| X/Y |7/8|UInt16|Retain existing query semantics; server owns target/range |
| Attack type |13|UInt16 value|Physical2 in the existing envelope |
| Damage |15|Int32|Client input is not damage authority; calculate on server |
| CombatGear |16|UInt64|Preserve existing optional encoding |
| KilledMonster |17|Int32|Preserve existing field, never trust as a kill grant |
| Spell level/ID |18/19|UInt16|Preserve optional encoding and original descriptor rules |
| ResponseDamage/effect/unknown |21/25/26|UInt32|Preserve existing wire meanings/opaque value |

No new unknown-field interpretation, client allowlist or packet layout is inferred.

## Catalog/table contract

Use the actual completed equipment query's captured slots4/5/6, selected alternate
equipment, durability and exact adopted item rows. Raw Frequency is field16;
required row/index identity and numeric width must remain current. Do not replace
missing metadata with zero. Cold zero cache requires the real unpublished native
equipment constructor/admission proof. Equip.AttackSpeed uses successive300ms
floors:800-100, then right, left, ring, Agility/2, followed by applicable Cyclone
and DeadlySight branches. A fenced unjoined modifier is still unfinished work.

## Validation and transaction rules

- Actual Runtime readiness, Frame/lifetimes, current attacker/target bindings,
  range/permissions, flags, gear/Rune metadata and source-derived cadence precede
  attack commitment. A DTO, handled result, send or writable native flag is not
  a committed-hit owner. Real ownership must survive awaited preparation.
- The actual Frame's TeamStates provider is pinned with the configured clock.
  After ordered connection/native actor/vector scopes, hold the real team gate
  and witness both actual roster mappings through writer/native publication.
  Recheck friendly-fire/qualifier policy against fully captured before inputs;
  a roster snapshot is not an attack grant. Release synchronously before await.
  Historical member HP/info output does not invalidate membership-only proof.
- Original HandlerProcess resets Action/auto-attack before validation. Correct
  this unsafe order: an accepted physical hit commits actor ActionNone0 through
  the actual native/Store participant with its real stamp and target publication.
  Do not use a raw setter or a separate early session mutation. Rejected actions
  retain stance/resources/stamps and cannot create a repeat grant.
- Prepare required frames and both participants before the actual writer. A
  known writer refusal must leave no successful Interaction/HP output or dirty
  participant state. A target read lease is a real replacement-failure control.
- The successful Interaction observer sees the committed native/Store state.
  Keep Interaction then HP on the wire. Earlier tests requiring precommit state
  at that observer must be migrated; they encoded emulator mutation order.
- Only the actual committed receipt may advance the private/native attack stamp
  and allow a current eligible repeat. Distinct manual/automatic origins retain
  actual generation, target identity, configured clock and bounded worker lifetime.
  Send/observer failure after commitment never refunds or replays the hit.
- A committed-output fence must reacquire the package, exact connection and
  native scopes before retiring either holder, and revalidate the captured
  binding/native identity. Commit-side failures retain their already-held ordered
  gates; cleanup cannot clear a replacement or acquire higher gates in reverse.
  Retirement must cancel a target's independently armed native repeat too.
  Worker stop serializes slot changes with hit/arm publication under the actual
  lifecycle gate. Connection-owned holder retirement never takes that higher gate.
- Accepted stance/movement/direction/death/teleport and actual EOF/retirement must
  cancel the same owned repeat as their source rules require. Partner movement
  asymmetry and runtime-only Action remain explicit. Pair coordinates use the
  separate [journal21 contract](visible-dx8-d13-owned-pair-movement-wal21.md).

## Expected wire behavior and fixtures

Use genuine Frame login/readiness, current catalog/query, actual stance packets
and real target receives. Compute independent HP/status/packet and complete-file
oracles. Cover accepted attacks from Sit, rejected target/range/late writer,
real zero/miss, nonlethal/death, actual repeat/cancellation, transport/EOF and
replacement lifetimes. Assert no extra stance packet or saved Action rewrite.
No fake saver, injected accepting draw/owner or hand-built positive receipt.

## Evidence alignment

| Requirement | Evidence | Remaining acceptance |
| --- | --- | --- |
| Full actor state/reset | Flags.ConquerAction UInt32; HandlerProcess148-150 |Actual owned commit and failure fixtures |
| Existing packet fields |Native InteractQuery; current MsgAttackPacket |Independent goldens and integrated tests |
| Cadence/catalog selection |Equip163-184; actual query/cache metadata |Real configured-clock and required modifier paths |
| Commit before success |Sanitized Parity policy and concrete writer |Lease refusal, committed observer, transport/restart controls |

## Implementation and qualification

Snapshot13 built with zero errors. Combined focused08 passed435/435 across16
affected families, zero failures/skips, including actual lethal/derived-spell,
movement/recovery and full-width Action appearance controls. The later FSB267
retirement/worker corrections and genuine already-armed-target regression remain
unqualified; the whole batch has no current public full-gate or intake claim.
The actual Frame/Store/native producer joins and corresponding broader C# suites
must pass before integration. Durable HP-to-proficiency reconciliation is open.
Record final source paths, actual commands/results and corrected source-bug
entry when qualified. This spec grants no deployment or legacy-client execution;
original saves/routes/restore ZIP and the current execution hold remain intact.

Postcommit output/proficiency/repeat checks reacquire both actual item-storage
read scopes after preparation leases are released. Hold actors, ordered vectors,
then ordered item-storage gates; still require the existing full reusable
contents/owner proof. A read scope is never a mutation or success capability.
No await retains these scopes. The missing scopes were corrected before the
passing combined435-case run; that result does not establish global lock safety.
