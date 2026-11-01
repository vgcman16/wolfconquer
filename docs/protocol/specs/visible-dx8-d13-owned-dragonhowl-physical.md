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

## Native retirement integration

The actual flashing publication installs a private native-0 lease tied to the
original binding, managed registration, native row and Frame clock. Generic
publication preserves that exact lease or needs typed expiry/death authority;
it cannot clear only the managed half. The real global worker carries its
retained operation lease into native+holder removal before2176. Other native
rows and raw fields remain. Committed output failures fence the original owner.
The existing managed-only10950 path does not invent a native lease.

Physical death composes native-0 removal with the genuine 194 after graph and
retires the original lease. Whole-proc death preflight observes prepared194
state without consuming it. Basic/proc writers consume once and recheck the
full proof after a possible comparer call. No validation or participant is
replaced by an accepting substitute.

## Current bounded result - 2026-11-01

Release build33 passed (0 errors, 103 reported warnings). Focused34 passed49/49,
0 failed/skipped, 1m41s; actual TRX nodes were counted. This includes the prior
34 cases, 13 generic publication/expiry loss controls and two real changed194
death cases. Earlier passing scopes overlap and are not additive.

Executed behavior includes native flashing2176 before FrontBreak/targetHP/2239,
real worker expiry at29/30 seconds, native-0 removal on a real lethal Basic hit,
1050-damage monster/SOB primary and repeat, lineage/reset refusal, mutable input
and output-loss fencing, and original17490/17650 damage/status/wire/file goldens.
The non-player reservation cannot grant Basic authority.

A valid class16051 victim earns FineRain, actually unequips its left fan during
the six-second defer window, then dies after that window. Both Basic and proc
publish the real changed194 token exactly once. Proc preflight only observes
the token; its first applicable flashing output sees it still pending. After the
same death writer commits, the original token is consumed and the independent
three-vector, zero-HP/max10764 and full saved-image goldens hold.

Before-writer proofs stay strict. After journal22 replaces saved-Spells state,
proc publication now checks the same real committed writer/anchor/afterimages,
not its expired before owner. The same purpose/native/query/wind/cancellation/
notification participants remain required. This fixes the concrete postcommit
refusal exposed by the new regression. Failed runs28/30/32 remain retained.

The changed-fan fixture preserves and checks physical WAL row order, changing
only the actual left-row Position. Parsed logical and native item graphs are
checked separately. Its proc inputs use DynamicID0 before admission because
instanced maps correctly suppress the flashing prefix used for observation.
No successful packet, damage, resource or ownership golden was relaxed.

Still required: broader/full Windows checks, complete lethal monster rewards,
Team XP and future overlapping recipients, joint proficiency, remaining effects/
events, SQL/crash/outbox and live-client acceptance after the execution hold.
