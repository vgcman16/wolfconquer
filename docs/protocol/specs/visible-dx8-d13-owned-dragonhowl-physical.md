# Owned Dragonhowl physical replacement

## Identity and scope

2026-11-01 source implementation draft, VisibleDx8D13. COMPILED; FOCUSED CHECKS PASSED.
This repairs the owned Physical request path that enters the basic-hit handler
before the existing Dragonhowl selector. The real physical command must select
and execute17490/17650; a direct client Magic packet is not equivalent evidence.
The current intermediate source implements player, monster and SOB primary
producers. Lethal monster rewards and the remaining effect/event joins below
are still incomplete.

References are original `MsgAttackPacket` Physical branches around624–882,
`MyCombatGear.GetSpell` and `CheckCombatGear` around1311–1412, and the existing
Triple17490/Ironbone17650 handlers and independent regression goldens. These are
server behavior references, not new live-client proof. Sanitized Parity governs
ownership, rejection, persistence and postcommit failure handling.

## Packet contract

The existing D13 Physical decoder, magic animation2239, status/HP updates2176 and refine
effects remain unchanged. The internal selected spell uses the actual primary
target coordinates, including17650; incoming Physical aim coordinates do not
authorize a target or replace this source anchor.17490 uses the existing line
geometry and emits its animation only for a nonempty target list.17650 retains
its validated terrain line, player damage75% scale and two animation passes:
visible peers first, then caster and visible peers. The initial gear range9 and
player repeat range3 remain distinct. No new opcode, width or client field is
introduced, and no wire request identifier is invented.

## Catalog and owner contract

The real Frame must own the configured native CombatGear runtime. Only the
supported legacy19 schema is used here; conflicting22 semantics stay held.
The actual ordinary admission must have published the constructor-owned native
gear from the saved UID image. The proc captures that origin, exact native gear
contents, selected learned spell rows and the exact configured gear file. A
managed gear snapshot or ambient global table is insufficient.

The selected request is privately registered against the actual Frame-captured
Physical command, native cadence slot/generation, stamina readiness, binding,
catalog/profile/recovery and active exclusive operation lease. Selection uses
the actual source RNG once: learned17490 within3 and its rate, then17650 at half
rate, then the source fallback. A selected missing/unsupported spell refuses;
it cannot silently become a basic hit or reroll the same issued request.

The primary target is retained through its real binding or registered monster/
SOB incarnation. An ordinary monster repeat can become a proc only by reading
and capturing from that exact retained repeat target under the same monster
gate. A replacement with the same UID or a different target category refuses.
Player repeats retain the exact original binding. SOB repeats observe the real
same-registration damage/reset successor; no lazy birth registration is used.
The non-player proc's BasicPhysicalAttempt-shaped slot reservation is only an
inert exclusion identity: it is never registered as a basic attempt. Private
Dragonhowl registration alone owns its command, cadence, commit and completion.

## Validation and transaction rules

Preflight captures the actual selected target set, source geometry, used metadata,
permission/roster inputs, resource cost calculation and complete frame bytes.
Every applicable participant must be real. A known preflight refusal leaves
costs, action, physical stamp, native state and successful output unchanged.
Private DATA calculations cannot register a live owner or publish a phase.

Store ownership order is the existing lifecycle gate, package scope, ordered
connection gates, native connection gate, ordered client/player gates and status
gates, followed by the existing file/journal participants as required. All sends
and observers are outside those gates. Exact canonical successors come from the
actual typed publication; callback results or fresh holder lookup cannot adopt a
different session into this cast.

The approved persistence model is ordered committed phases. Actor costs precede
mutable status phases; each target HP/death writer precedes its own output. The
Front Break output may therefore observe the original target HP, preserving the
successful source sequence. Player death uses the real journal22 Character by
account plus Spells by player UID, native query/status/cancellation and team
participants through a closed basic-hit-or-proc purpose union. No basic damage
decision or receiving target is fabricated for that writer.

For a permission-approved ordinary player target that requires flashing name,
the proc now prepares the actual actor flag0 prefix before damage calculation.
The prepared managed vector preserves its raw comparer and unrelated fields;
the death-removal key is true and the managed timer is the same captured local
Frame-clock value as the native row. The real native vector owns its complete
before/after registration graph, with duration30 and invoker0. Only the exact
registered Store phase can issue its single-use publication. Costs/stamp commit
first, then native+holder flashing state before its owner/visible2176 output,
then Front Break and target HP phases, then animation2239. No native callback
or compatibility AddFlag substitutes for that publication. Preflight failure
publishes neither flag nor cost; later output failure uses the actual committed
proc fence. Each phase checks cancellation before committing. Native raw reset,
retirement, expiry during this cast, or changed registration graph invalidates
the retained owner. Scheduled expiry/death execution still needs its own real
runtime qualification; a row's duration/removal fields alone do not prove it.

After a committed phase, failure fences the real participating owners and
preserves primary/cleanup errors. Successful completion is privately issued by
the actual execution and is required to arm a real repeat. Legitimate dead or
out-of-repeat-range outcomes are separate from an unexpected completion/native
proof failure. Fresh physical requests have no global exactly-once guarantee.

This is not all-target crash atomicity. Journal22 does not make actor costs,
multiple targets, ephemeral monsters/SOBs and rewards one recoverable transaction.
There is no general output outbox or all-system rollback claim.

## Intermediate implementation and qualification

The current source snapshot includes actual player/non-player request admission,
ordered costs/mutable status/nonlethal HP, real normal player death settlement,
incarnation-bound monster damage and version-bound SOB damage, plus their closed
output receipts. CTF, SuperGuildWar, class-pole and four-partition Nobility
scoring/reset paths have actual typed score owners and exact actor successors.
Preflight does not clear expired rounds or advance their process. The source
does not borrow the unbound compatibility route.
Pure unchanged-seed files are excluded from the producer manifest.

The following selected branches still refuse before debit and remain active work:

- Lethal monster drops, map events, random/boss rewards, mail, study and team XP
  continuation. The separate real floor/CP preparations remain unactivated
  until the complete applicable reward chain is joined. Nonlethal output is
  not a death reward receipt, and a runtime floor reservation is not a journal.
- SOB event families beyond the implemented paths, combined event roles,
  zero-damage event no-op handling, instanced CTF capture and required Guilds
  file-name fallback. Unsupported selected events refuse the entire cast.
- Applicable consumed statuses and effects whose exact native/resource writer
  has not yet joined, including Hawks/shield/counter/special rune branches.

Required qualification starts with the ten existing physical-request regressions
using real configured legacy19 metadata, saved gear201 and ordinary admission;
their damage/status/wire expectations remain meaningful. It must also cover
missing/stale metadata or gear, exact file/owner refusal, player death, target
incarnation/reset drift and output loss. Compiler feedback and focused red tests are retained; the
full gate and live-client acceptance remain unqualified for this draft.
The reservation control must prove that a real non-player proc's inert slot
identity cannot enter the basic-hit/death completion path; it must not mint a
token or invoke a supplied accepting owner to simulate that case.

Source-bug ledger handoff remains OPEN: owned Physical proc interception;
checked mutable-percent overflow; invalid SOB death-behavior rejection before
HP mutation. No verified fixed claim follows from this source snapshot.

## Current bounded result - 2026-11-01

Build20 passed0errors/159warnings. Focused21 passed18/18,0failed/0skipped:
original12gear/reentry cases plus2real admission-refusal and4native lifecycle
metadata/order/refusal/output-loss cases. The17490/17650damage/status/wire
expectations remain independent. Genuine FineRain data backs100000HP after
its100HP buff. Native flashing2176 precedes FrontBreak, targetHP and2239;
connection loss after that actual commit fences both real bindings before
laterHP/animation/repeat. Actual timed-worker/death removal is still unqualified.

Preflight now uses the actual beforewriter proof instead of requiring an
already-committed HPwriter. Worn-input currentness can follow only the actual
private proc's committed successor, retaining original native graph and exact
binding/snapshot provenance. Ordinary/basic snapshot behavior stays strict.
Per-phase refusal diagnostics are data only and grant no execution authority.
Earlier gear/focused/admission reds and logs remain retained, not relabeled.

Loaded metadata is an immutable published image; its actual complete native
publisher transition, rather than a source-file edit alone, invalidates the old
Frame owner in the passing refusal control. No fresh owner or writer is faked.

Next qualification includes real worker expiry/death cleanup, applicable194
native death preparation, monster/SOB primary/repeat/reset and output loss.
Lethal monster rewards, remaining selected effects/events, full suite, SQL,
all-target crash safety/outbox and live client acceptance remain open.
