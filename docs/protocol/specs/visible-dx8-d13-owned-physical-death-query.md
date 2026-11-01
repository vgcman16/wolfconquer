# Visible DX8 D13 owned physical death and equipment query

## Identity and scope

Revision2, 2026-11-01. This is the normal player-death continuation of the
actual owned basic physical receive, targeting7899/VisibleDx8D13. The separate
official7937 data reference supplies no runtime authority for this path.
The existing normal-death phases, equipment query calculators and independent
auto-attack wire fixtures are the behavioral references. Sanitized Parity
requires a successful durable settlement before any success notification.

## Packet contract

Keep the established Interaction-before-death-notifications wire order. Retain
DeadTalk, conditional explicit flag vectors, XPList clearing, the unconditional
registration vector, equipment refresh, derived spell packets, ServerInfo and
peer appearance, status, HP pairs, perfection,194 postlude, final HP0, Dead vector
and conditional final explicit flag vectors in their existing source order.
The former intermediate live HP mutations are replaced by internally calculated
historical HP frames. Each source HP assignment still has team-info output
before its self maximum-only then maximum+HP pair. A transport callback cannot
cause the committed server HP to move backward to a historical phase.

No new opcode, layout, field default or profile selection is introduced.

Historical appearance is prepared from the private calculated Character data,
not passed off as the current live WorldSession. Validate its full UInt32 Action
with the scoped Action reader, encode a private numeric serialization copy, then
narrow only the outgoing field31 to UInt16 as the existing spawn codec does.
Named saved actions retain their admitted meaning;65786 remains65786 in native
state and emits250 on this field. The historical phase itself is not rewritten.
The actual death decision, current owner witnesses and committed receipt still
control preparation and eventual notification; the data codec grants no authority.

## Catalog/table contract

Reuse the actual loaded WorldDataCatalog and existing pure item, class,
perfection, InnerPower, SubClass, JiangHu, CombatGear, Mythsoul and Rune formulas.
Retain source selection order, broken-item rules, integer widths and rounding.
Reject an unrepresentable calculated HP, maximum, mana, magic, prestige, Vigor
or Venom phase before the writer instead of preserving unchecked overflow.
HP assignments use the existing signed-int setter range; maximum/mana/magic
remainUInt32. Percentage multiplication uses a wide intermediate and refuses a
reduction greater than its maximum. No invented saturation is applied.
Recorded21930 and20000 transitions are calculated from the actual captured rank
and selected equipment inputs. The full before/proposed spell snapshots must be
committed with Character using the distinct actual death settlement; no separate
spell save, item-book journal reuse or caller supplied spell level is accepted.

## Validation and transaction rules

The private receive-decision registry must bind the actual attempt, original
target and internally calculated lethal projection. Require both actual current
connection/native owners and the actual Frame context/clocks. Prepare all death
phases, frames, native cache/vector participants, team final state and complete
durable images before the writer. Inputs and catalog rows used in the calculation
must remain current through the final pre-publication proof.

Read GhostStamp and DeadStamp independently at their source positions. FineRain
admission makes exactly its established ordered signed-clock reads. Denial
retains the old native query/perfection state and emits the established message;
normal death continues to final HP0 and Dead.

The prepared query is inert: it performs no sends, Store publication, native
mutation or durable write. It cannot manufacture an ordinary completed-query
token. Native194 uses a separate concrete death-query purpose tied to the real
plan and joint settlement. Its registration timestamp comes from the real native
flag constructor; duplicate registration must not renew that timestamp.

After durable commitment, all native/holder participants publish in one guarded
non-callback continuation. Historical notifications use the actual Store's
committed publication pointer and current final owners before and after sends.
The old before-plan proof intentionally expires after publication and must never
be used to authorize those notifications. A failed send does not undo the hit or
arm auto-attack. No delegate collector, query replay or passed handled boolean
can substitute for the actual committed receipt.

## Expected wire behavior and fixtures

Preserve genuine zero/nonlethal/lethal controls and independent packet goldens.
Add writer-refusal controls proving no success frame, HP/action/stamp change or
resource loss; retry only after the actual refusal is removed. Cover normal
death with equipment/Rune phases, FineRain refusal, VIP/Circle HP frames, current
perfection/194 publication, derived-spell durability, actual team membership,
disconnect/rebind and transport failure. Use owned disposable files and real
native/session owners. Synthetic data-only tests are separate from live proof.

## Evidence alignment

NormalDeath.Phases and EquipmentQuery.* establish the existing sequence and
formulas. The owned receive decision establishes the actual damage result.
The actual configured writer establishes durable commitment. The concrete native
participants establish final cache/vector state. None alone proves the others,
whole-client compatibility, SQL or live acceptance.

## Implementation and qualification

Implementation is isolated under root-native-action-owner-next. Build12 passed;
the pure builder and notification/native joins remain under qualification. Earlier
focused04/05 failures and the diagnostic identifying an inappropriate live-session
spawn call are retained under root-action260-initial-compile-next. New death
appearance controls cover actual named saved input and full-width65786, with
complete durable/native/Store checks before output; their results remain pending.
Full modifier/transform/persistence/English/7937/live horizons remain open.
Root alone qualifies and intakes this coherent batch. The preceding public
c271eec1 full gate passed and its actual outputs are preserved separately.
