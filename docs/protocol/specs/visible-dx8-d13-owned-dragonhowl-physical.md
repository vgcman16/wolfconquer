# Owned Dragonhowl physical replacement

## Identity and scope

2026-11-01 source implementation draft, VisibleDx8D13. Bounded ancestor qualification is recorded below; the reward/Team XP integration is SOURCE ONLY / UNBUILT / UNRUN.
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

At that bounded ancestor, the next frontier included broader/full Windows
checks, lethal monster rewards, Team XP/future overlapping recipients, joint
proficiency, remaining effects/events, SQL/crash/outbox and live acceptance.
The consolidated reward intake is rebased onto Root local1255822abdd2ac10379f3b2c445bfffab2fea862,
which retains the later journal24 and owned-Profs writer work. That base
qualification does not qualify the reward/Team XP integration below.

## Lethal monster reward and Team XP continuation - source caller integration

The specialized17490/17650 physical-line receive invokes monster death output,
drops, applicable boss/map rewards, then Team XP before the next monster/player
receive. It has no explicit killer attack-XP or spell-progression tail. The
ordinary DirectLineAttack experience path is not substituted for this source.

Floor preflight retains UID-free payload DATA and an exact local sequence of
planned terrain, occupancy, expiry and capacity outcomes. It neither increments
the floor UID source nor inserts a live reservation. The actual registered drop
phase rechecks the captured floor graph, consumes its monotonic UID once,
encodes the final real-UID frame, then uses the existing reservation publisher.
A selected item with no ground still consumes its UID at that actual phase;
no-ground money draws neither money amount nor UID. Grounded money retains its
single selected amount as historical DATA. No failed phase rewinds identities,
and these runtime owners do not establish durable floor recovery.

The registered killed-incarnation receipt owns each reward phase. Completed
drop and CP receipts become historical predecessors when a later real wallet
writer publishes its successor; they are not reinterpreted as still-current
output receipts. The preparation API requires the full recipient roster and
selected gain/level/query DATA before cost. The private builder discovers real
Team connections before ordered actor gates, then captures the actual complete
roster under those gates. Outside-line, zero-gain and ineligible observations
are retained when they participate in the source share calculation. Missing
required bindings refuse the whole selected reward before cost. The executor
now calls the registered reward and Team XP phases after the actual monster
death output and before the next selected target. DATA does not issue an award
or represent a future native cache. Actual awards use the existing private OwnedCombatExperience producer,
real persisted credit/step/query/restore/clear writers and the retained exclusive
Frame operation, including every reached level threshold.

Only the driver's actual final marker-clear branch can register the terminal
receipt. For a level change that receipt retains the last actual native query's
completion through the marker-only clear. Later proc work must adopt this exact
committed successor before using it; UID lookup, equal aggregate caches and a
fresh native capture are not successor authority. A recipient that is also a
later damage target uses the full future query calculation and the actual
query's resulting receive inputs. The mutable caller joins both halves through
registered award/phase tokens. Reached thresholds, automatic stats, full queries,
HP restoration and final marker clear remain required; no nonthreshold substitute
is treated as completion. This source integration is unbuilt and unrun.

The overlap is reachable in Free PK mode0: the actual friendly-fire predicate
rejects teammates only in Team PK mode2. Map700 TeamQualifier has its own
same-team refusal. On another permitted map outside a safe zone, an alive nearby
real teammate can receive the killed monster's Team XP and then be selected by
the same proc's player line. Required regression: real team membership, Free PK,
earned threshold-crossing XP, completed native query, then the independent
post-level receive/death damage and packet ordering. Team PK is the paired
permission control; an invented class/flag or direct Magic packet is not a substitute.

The actual scalar/credit writers enter the proc's ordered participant scope
before their recipient connection scope. Their concrete publication hook retains
the real successor before observers. The driver's real restored-Ready query
checkpoint similarly registers the later clear execution; the actual clear
writer records its verified after-image and final holder before its observer.
Later reads compare with those retained bytes. They cannot turn a freshly read
file or a newer arbitrary holder into continuation authority.

Future receive DATA replays the real checked equipment query and HP restoration,
including native magic/JiangHu/perfection values and the full derived-spell
image. Existing receive overloads accept that DATA explicitly; ordinary callers
retain their existing overloads. At runtime only the driver's actual final clear
and retained last native query can admit the same proc's cache/worn successor.
Equal values from an unrelated query, a current UID lookup or a copied epoch
cannot supply that bridge.

The future lethal-player boundary is represented by a registered proc token.
Before cost it validates the calculated death/query, saved Character/Spells
images and the actual journal22 envelope size without issuing a death owner or
performing IO. Envelope counting includes all base64 images and recorded
metadata before record-image clones. After the real XP/query/final-clear
successor, the token invokes the existing real death factory exactly once;
it checks that factory's full outcome against the prepared DATA and commits
through the unchanged Character/Spells journal22 and native participants.
Only the concrete ThundercloudSight receive edit is replayed onto the actual
holder, preserving the actual query's runtime timestamp. The actual private
builder now issues this token when a previously awarded recipient is lethal;
only its completed award can reach the corresponding runtime death factory.

Multiple killed monsters carry exact calculated recipient results into the next
award preparation. Runtime succession follows the preceding actual scalar receipt
or final-clear/query receipt. Scalar-only overlap captures the original completed
query token as read-only history, plus actual configured Character/Items/Spells
images before cost. A FineRain-denied death query may preserve that still-current
history; it cannot mint a future query. A missing saved Spells file stays null
through every DATA stage that makes no spell write. An empty modeled spell list
does not materialize a four-byte file or grant a journal participant.

The independent overlap intake requires saved gear201, genuine legacy19 native
metadata/admission, real Physical and Team packets, a killed monster, CP reward,
threshold-crossing Team XP/full query/final clear, then later lethal peer damage
for both17490 and17650. Its full files/native/cache/wire assertions remain
unrun and depend on qualification of this caller integration. Scalar-only awards,
multiple killed monsters and outside-line recipients are joined in source but
still require genuine integrated controls. No full-chain passing claim follows.

This source delta is unbuilt/unrun. It removes the blanket lethal-monster hold
only for the concrete joined reward branches. Map quests, ranking mailbox, study/innerpower
and other applicable rewards require their actual owners. Unknown participation
refuses the entire cast before cost. Ordered committed reward phases do not make
floor/runtime/mail/wallet/XP globally crash atomic and do not provide a durable
fresh-wire request deduplication identity.
