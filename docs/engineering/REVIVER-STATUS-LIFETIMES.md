# Reviver status, ghost and appearance ownership

## Identity and scope

Spec revision: 2026-11-02, Root. Target: existing VisibleDx8D13 server path.
This closes actual native/session/file lifecycle gaps; it does not switch the
working client to7937 or establish new client acceptance. The client execution
hold, preserved saves/routes/ZIP and D:\cqclient baseline remain in force.

Observed current checkpoint: dd448f7f has55/55 timer/spawn and91/91
native/query/action focused passes. Full Windows gate32 is running. These
counts overlap and do not qualify the separate status/ghost proposals.

## Packet contract

Use existing D13 status-vector, mesh, HP, stamina, XP-list/circle, map-status
and player-spawn serializers. No opcode, protobuf field or encoding is added.
Preserve confirmed goldens and successful current-client ordering. Record any
source/client ordering conflict; decompiled sequencing alone is not client proof.
The real ghost producer publishes status before its appearance transition;
completion of the actual ordered output must bind the later appearance owner.
Prepared bytes, equal UID/scalars and a successful callback are not that owner.

## Catalog and table contract

No new table layout. Resolve the actual admitted player, current monster
incarnation/range, current completed equipment maximum query, real spell book,
actual physical flag registrations and their corresponding managed policies.
Unknown fields/policies stay unknown. A managed-only source registration does
not authorize fabricating a native row. Each native family must use its real
lifetime/projector, with duplicate and malformed owned keys refused safely.

## Validation and transaction rules

1. Retain source-confirmed permanent registrations with the same real object
   and timer. Remove only genuine registrations whose actual policy is true.
   Reconcile native and managed ownership per family; whole-vector equality or
   clearing all bits is insufficient. Opaque unrelated map keys/comparer survive.
2. Due ghost transition requires the genuine dead, completely logged-in actor
   and due clock. Source AddFlag requests2592000seconds and RemoveOnDead=true.
   Issue a one-use plan from the real traversal; a seeded bit grants no native
   registration, timer birth or completed-output lineage.
3. Join status, native appearance, current holder and required file writer in
   their real ordered scopes. Validate all projections and serializations before
   mutation. Never invoke the unbound native transform setter on a bound actor.
4. Actual status output must complete before the later ghost appearance step.
   Revalidate the exact owner and relevant state at the real transport gate.
   Interrupted committed output fences the matching current owner, preserves
   the original error and cannot be replayed as an uncommitted retry.
5. Reviver reset consumes the genuine ghost/transform producer lineage and real
   completed maximum query. Commit its native flag/appearance and HP/mana/stamina
   changes with the real saved-state outcome. An uncertain file outcome requires
   recovery fencing. Local locks do not prove SQL or power-loss atomicity.
6. Keep world/publication leases short and release before awaited socket IO.
   A legitimate intervening Store successor requires an actual compatible owner;
   never substitute a UID-only check, detached snapshot or accepting validator.

## Expected wire behavior and fixtures

Use actual saved fixtures, public death/cast/query routes and enabled workers.
Required controls: normal near/far Reviver; genuine spent stamina; actual
DragonSwing and native fan permanent survivors; due ghost status then appearance;
ghost-to-Reviver reset; malformed/duplicate policy; replaced native/holder owner;
changed raw future map; interrupted/blocked output and unrelated combat progress.
Assert native registrations/timers, holder, saved files and real received bytes.
Keep independent protocol goldens and truthful primary/cleanup failures.

## Evidence alignment

Original reference root is the preserved private
`征服-龙拳天下服务端-已修复/lqtxfwd/DecompiledSource/GameServer`:

- `MrRayzo.Role/StatusFlagsBigVector32.cs:148`: GetClear removes only true policies.
- `MrRayzo.Role/Player.cs:3725`: ClearFlags also handles XP-list and equipment query.
- `MrRayzo.Role/Player.cs:4401`: Revive includes transform reset, XP behavior,
  stamina100, map status, visible spawn and optional QuickMount behavior.
- `MrRayzo/ThreadPool.cs:2866`: due logged-in dead-player Ghost registration and
  body-dependent98/99 transformation. These are source observations, not proof
  of successful rendering in the selected client.

Current implementation evidence: actual TIMER27 spawn-owner refusal; reviewed
prepared-action repair; TIMER29 and NATIVE31 terminal TRX. New lifetime12 and
separate ghost/transform overlays remain unbuilt/unrun until Root intake.

## Implementation and qualification

Current owners are `WorldSessionStore.ReviverCommit/ReviverWire`, the actual
bound HP writer, physical status-vector lifetime owners and runtime ghost
producer. Keep new ownership participants in focused partial modules.

Do not claim complete Reviver behavior while source-referenced XP reset/output,
passive12660 handling, actual native XP counter ownership, special flag6/194/
flashing removals, arbitrary transformations, QuickMount or legitimate physical
successor/output continuity remain unjoined or untested. Missing native XP
ownership must not be hidden by checking only the emitted circle packet.

Run actual producer/lifetime controls, affected shared native/query neighbors and
the appropriate complete gate before publication. Live acceptance and eventual
one-endpoint-at-a-time client migration remain separate requirements.

## Newly confirmed producer gap

Root reviewed current dd448f7f `LegacyPlayerDeathState.CopyAttributes` at624:
it reconstructs OrdinalIgnoreCase; TryEnterGhost at519 and the later transform
at534 both call it. `LegacyStatusFlagDeathRegistration.CaptureAddFlag` also
reconstructs with that comparer. The native ghost owner alone cannot preserve
legitimate opaque case-distinct keys through these producers. Add a genuine
opaque-key/comparer ghost-to-Reviver control and fix each real participating
producer. A shared helper change requires reviewing its other callers and
retaining their uniquely owned alias/rejection semantics. This is OPEN.
