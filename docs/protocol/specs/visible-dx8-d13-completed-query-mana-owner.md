# HELD: incremental completed-query HP and mana draft

2026-11-01 correction: this draft is superseded for 2203 by the
[full owned allocation rebuild](visible-dx8-d13-owned-attribute-set.md).
Original `MsgAtributeSet` echoes then runs the complete equipment query, which
also updates magic attack and other derived caches. The incremental architecture
below was not qualified or activated; its source is retained only in
`.artifacts/helper-attribute-cache-hold`. Do not use it as an allocation contract.

## Identity and scope

Source draft, Chinese 7899 / VisibleDx8D13. This is the completed-query HP/mana
prerequisite for [owned attribute allocation](visible-dx8-d13-owned-attribute-set.md).
It does not turn a status overlay, public epoch constructor or supplied total
into permission to allocate attributes. Client execution remains held.

## Packet and catalog contract

No new packet, field, catalog layout or wire ordering. The existing query and
2203 output codecs remain the contract. Existing item, purification, combat-gear
and JiangHu mana additions are historical outcomes of that actual query. A later
attribute operation must not repeat a time-sensitive purification lookup.

## Producer and lifetime

Only the private admitted equipment-query execution may issue completion. It
retires the previous completed owner before its first admitted query work,
captures the class/Spirit contribution at the actual addition, and completes
only after the complete query including its postlude returns successfully.
An interrupted query, replaced owner or nested newer query cannot restore an
older completion. A denied query that never starts computation supplies no new
completion. The retained native query epoch must still be genuinely registered.

Completion binds the actual Store/connection/native identity, binding/player,
class/four attributes, exact managed mana overlay and HP cache, native Status
object and both maximum values, historical mana aggregate and class/Spirit
contribution. It records the actual ordered HP additions, attribute contribution,
Sacrifice percentage reduction and any absolute transform replacement. Ordinary publications
that retain those exact inputs may preserve the cache; an equal-valued overlay
replacement does not establish its provenance.

## Attribute change transaction

Under the real lifecycle, package, connection and native actor gates, prepare
mana as `aggregate - recordedBase + validatedNewBase`. Replay the closed HP
recipe, replacing only its single attribute step. Percentage reduction uses a
wide product, integer division and checked subtraction; an absolute transform
step remains absolute. Require UInt16-representable attribute contributions and
UInt32-representable aggregates, with no underflow or wrap.
Allocate the replacement overlay/cache before the Character writer. Revalidate
the exact before owner immediately before and after the durable writer; publish
the matching native cache and retained completion by assignment alongside the
native stats and holder. Output uses the resulting current receipt outside gates.
Missing/stale completion refuses before debit. Uncertain post-writer publication
uses the stat writer's existing fence and never reports rollback.

## Validation and boundaries

Required controls use real admitted queries: completion followed by allocation,
missing completion, a replaced overlay, partial query, query reentry and overflow.
Pure arithmetic or supplied cache values are DATA only. This source draft is
UNBUILT/UNRUN until Root qualifies it; it is not live-client or crash-recovery proof.

The recipe is DATA only; constructing one cannot mint a completed owner. The
actual private execution must prove its recorded recipe reproduces its current
completed HP maximum. A raw final-HP subtraction is never used. This changes
cache maxima, not current HP/mana or unrelated skill/status caches; their writers
and output boundaries remain independently owned.
