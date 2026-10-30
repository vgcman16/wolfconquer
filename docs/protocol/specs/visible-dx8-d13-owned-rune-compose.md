# Owned existing-UID Rune composition

Revision1,2026-10-30. SOURCE ONLY isolated successor from frozen113; Root alone
integrates/executes. Full English and full Rune horizons remain ACTIVE. Root's
FSB250 Character-publication correction is a separate owned candidate and must
be joined deliberately before qualification. Draft FSB251 covers this milestone.

## Actual source and packet contract

Original MsgItemUsuagePacket.ComposeRune2074–2128 resolves the target from the Rune
container and all declared materials from inventory or Rune storage. It derives
material points from RuneTable.GetRunePlusPoints99–114:
`cumulativeEXP(family,level) * effectiveStack + RuneEXP`, where source stack0 means1.
Each target level consumes the positive difference of cumulative progress rows.
Red maximum is1, blue27, yellow9. At maximum, target XP becomes0 and later selected
materials are not consumed. No silver/CP fee is present. UID allocation is absent.

Success sends one existing RemoveInventory per consumed material in request order,
then one target2116 UpdateItem packet, then updates the existing rank owner. There
is no Compose echo and no four-frame equipment refresh. Preserve item override,
mode, complete fields, and existing D13 payload/argument layout. Declared1..49
selects only that prefix and ignores surplus; malformed short declared prefixes
refuse. Declared0 or>=50 selects no materials as source does and may produce the
source target update through the real owned no-op transaction.

Compose does NOT call Rune.Equip, Rune.Unequip, SpellAdd or spell progression. An
existing target14160:16 default row/origin remains unchanged when item4020517
advances to4020518, despite the new item's mapping14160:17. Do not invent Rune XP,
grant overwrite or a learned-level upgrade. Actual equipped-provider eligibility
still applies to future casts.

## Sanitized validation

Replace the clean handler's publish-before-persistence and accepted-prefix commit
with complete deterministic preflight and one real transaction. Any rejected
operation leaves funds, items, spells, claims, native/Store state, ranking and
outputs unchanged. Original invalid-later partial consumption is a source bug,
not a protocol sequence to preserve.

Require the actual admitted Store/native binding, configured Items/Spells roots,
UID registry/recovery readiness, current item/catalog witnesses and real ranking
owner when ranking is requested. No managed Items-only accepting saver fallback.
Do not import Equip's Reborn/level/Beast/class admission into this distinct source
operation. Target stays in its actual Rune container and keeps its UID, position,
stack and deposited/perfection closure except exact ItemID/RuneEXP projection.
All1,219 actual401–403 rows declare raw metadata stack0 (nonstack). Original
Inventory.AddItemWitchStack stacks only metadataStackSize>0 and otherwise creates
one item per unit; ContainItemWithStack treats raw0 as one physical item. A compose
target therefore requires rawStack0/1. Refuse targetStack>1 before arithmetic so
one material cost cannot upgrade multiple units. Material4040001 remains genuinely
stackable up to10000. Apply the same proved rawStack0/1 rule to reached destructive
material Runes401–403, preventing forged nonstackable quantities from multiplying
progress. More generally, every reached material's effective quantity
`max(rawStack,1)` must fit `max(actual adopted raw field46,1)`. Capture that required
ushort field without the permissive LegacyStackSize fallback. Malformed/missing
required cap metadata refuses before arithmetic. Never infer broad material caps
from one essence sample. Keep the source stop-at-max check before later cap or
progress access; selected later UIDs must exist but their unused metadata is not
recorded or accepted as operation authority.

Selected material UIDs must be nonzero, distinct, different from target and actual
roots in inventory/Rune storage. Each reached destructive material must be an
actual catalog item in the source4010000..4100000 range, unlocked, deposit-free
and fully consumed exactly once. Do not destroy unproven child closures. Bound or
other nontransfer properties are not guessed into trade rules. Preserve source
stack0-as1 arithmetic without rewriting any surviving row.

Resolve all selected roots before mutation. Missing/ambiguous required catalog or
progress rows, negative/out-of-UInt32 numbers, nonpositive target level gaps,
checked overflow, impossible family/level or malformed count refuse atomically.
The full selected set is validated before any accepted prefix becomes durable;
do not let an invalid later selection debit an earlier material. Actual material
4040001 has maxstack10000, cumulative family404 level1 progress10. Actual blue
target17→18 gap is109000−94000=15000, so a real stack1500 supplies that exact gap.

## Spell provenance and Character projection

Use actual current raw-self semantics. Keep the existing target grant/default
origin unchanged when its mapped ID remains supplied. Consuming an equipped Rune
must remove an exact producer-owned default row/origin only when it is the last
actual provider of that ID. Preserve shared, unmarked/permanent rows; the actual
classified caster gate prevents use without a current active provider. Changed
owned rows stay safely refused pending their real mutation owner. Reuse the closed
legacy passive-origin retirement contract, never classify unmarked history.

Character HP, native HP, wallets, escrow and unrelated fields remain unchanged,
including while an actual transform is active. Original Compose has no equipment
query or HP publication, so no hidden clamp, HP frames or unused transform/alternate
authority is added to this operation. Retain strict canonical/native Character
validation and exact byte-preserving saved projection for proven origin effects
only. Later stat refresh and lifetime effects remain a separate explicit milestone.

## Durable operation and owned restart

Introduce a distinct version16 Character+Items+Spells operation record rather
than broadening old9/12/13/14/15 semantics. Record selected ordered UIDs, actual
required before/final Rune mapping inputs and exact reached cumulative progress
rows through real adopted catalog and Store witnesses. The strict operation has
SelectedUid, Materials, Metadata, Progress and separate MaterialMetadata rows
(ItemId, MaximumStackSize). Record only sorted distinct CONSUMED material IDs and
their actual raw caps; replay requires exact membership, rejecting unused cap
evidence. Rune mapping metadata is not widened to essence families. The projection derives
arithmetic and consumed roots itself;
it does not accept a caller-supplied final item, point total, removed UID set or
spell outcome. Bound metadata/counts/order, reject duplicate/unknown properties
and prove exact required dependency membership.

Actual existing file locks and configured domain serialize all three targets;
same exclusive UID custody and same lease transfer remain. Captured before/after
images must be complete, lossless and exact outputs of the operation. Raw UID
union after equals before minus exactly the consumed deposit-free roots, bounded
by actual pinned highwater. No new UID or history inference. Extend physical
captured-BEFORE ownership protection to16 with no logical overlay/ignored path.
Unknown intent/stages, changed pending identity/current target, foreign roots,
retained work and malformed evidence remain fenced. Recovery rescan precedes
readiness and second restart must not replay.

## Evidence plan and qualification

Lead owns deterministic projection, closed evidence/schema/writer/startup plumbing
and integration review. Test helper owns new actual Runtime/Frame/native fixture
partials plus an additive actual RuneLevelExperience catalog fixture input. The
other child is assigned Root's real Character-publication gap before any second
Compose production scope. No accepting owner, positive handmade WAL or fake claim.

Genuine stored/equipped/active/passive targets; full declared-prefix protocol and
metadata, untouched target grant/default origin, material last-provider retirement
and shared/permanent controls; complete byte/native/UID/rank proof before replies.
Actual StopAfter0..3 and real stages, recovery/no replay, malformed metadata,
duplicate/self/missing/locked/deposit/overflow/nonpositive curves/invalid-later
atomic refusals. Existing unsafe partial-prefix test must become refusal while
retaining valid independent goldens. Profile-limited old unowned fixtures cannot
claim actual owned D13 acceptance.

All authored source remains UNRUN until Root executes. No build/test/runtime/API,
Git, host/client/updater/AV/original data actions. ChanceRune, Decompose allocations,
other compose/resource/passive effect and full Rune lifecycle remain separate
ACTIVE work; this milestone does not declare the horizon complete.
