# D13-NINJA-LOAD-1: Prepared Ninja loading and spell/badge publication

## Identity and scope

- Revision 2, 2026-10-27. Requirement owner: Root's approved isolated Loading design
  and reviewed comparer/public-entry refinements;
  governed by Sanitized Parity and PROTOCOL-WORKFLOW.
- Target remains VisibleDx8D13. Evidence here is the existing managed/reference
  source and independent retained byte fixtures, not a new client capture.
- Data version: current physical NinjaFile tables and owned synthetic fixtures;
  no original/runtime catalog queried. Client catalog equivalence is unproven.
- Direction: server-to-owner during the ordinary managed component Loading API.
- Prepare ranks, actual spell map, actual badge and complete frames before stores.
  Alternate/GetLevel reuse skills-only/badge-only preparation. No new dispatcher,
  host adoption, client change, gameplay balance, outer cost or rank-file change.

## Packet contract

All frames retain UInt16 little-endian declared length and opcode, four-byte
header, existing captured optional seal and native cursor4 convention. Declared
length excludes seal; actual frame includes it. Native Send's existing total1032
limit applies before commit. A new sequence/chunking contract is not invented.
The full-item nonnull-row minimum `4+4+12*n` can reject >85 rows before constructing
its message array; exact scalar widths/UID/seal may reduce that transport bound.
This is not a gameplay or independently observed client cardinality rule.

| Field/path | Number/offset | Wire type/width | Meaning | Presence/repetition | Bounds/default | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| Spell add | opcode2101 | fixed21-byte payload | Actual SpellCreate | Per newly added permitted spell | Header25 before seal | LegacyNativeSpellPacket.cs; SpellFrame fixture |
| Spell experience/ID/unknown/level/type | payload0/4/6/8/10 | int32/u16/u16/u16/u16 | Existing row fields | Required fixed | New rows keep source defaults | Same writer |
| Spell UseSoul/Soul/HashLevelSoul | payload12/16/17 | u32/u8/int32 | Existing row fields | Required fixed | Source defaults for new Ninja skills | Same writer |
| Spell removal | opcode2046, action109 | typed ActionQuery | Remove actual skill | Per removed static skill | UID field1; skill field3; action field12 | ActionPacket.Serialization + RemoveFrame |
| Badge | opcode2176 | typed UpdateProto | Actual NiniaP0 byte | Only when changed | UID field1; update field2, type123/value fields1/2; zero value omitted | MsgUpdate.Serialization + LevelFrame |
| Ninja user | opcode2395 | protobuf varints | Actual full user image | One for Ninja Loading | All21 attributed fields required, including defaults | MsgNinja.User.cs/UserFrame |
| User Type/UID/Flag/Points | fields1..4 | uint32/uint32/uint64/uint32 | Loading0, actual UID/state | Required | No null message | Same actual model |
| User elements/temporary elements | fields5..14 | uint32 | Actual captured state | Required | No new balance bound | Same actual model |
| User Score/masteries/unlock | fields15..21 | uint32 | Checked total and actual state | Required | No wrapped score | Same actual model |
| Ninja items | opcode2393 | protobuf | Full captured item image | One for Ninja Loading | Type0 field1, UID field2, Items field3 | MsgNinja.Item.cs/ItemFrame |
| Each item ID/Level/Position/Points/UK | nested1..5 | uint32 varints | Actual stored values | Required in each nonnull row | Full Loading retains UK, unlike single Add producer's0 | Same model/producer fixtures |

Optional null Items/null repeated rows retain the actual codec omission contract;
Loading itself supplies its validated captured rows. Opaque state remains opaque.
No field is inferred from a protobuf tag alone or from a different client build.

## Catalog/table contract

The relevant server metadata source is DbLocation + `gouyu_type.txt`; current
loader uses UTF8 with BOM detection, `@@`/space separators and at least12 fields.
The physical dictionary key is metadata row ID, independently of ItemID/Level.
This work does not change that loader or claim a matching client table version.

| Column/offset | Name | Type/width/encoding | Validation | Packet/behavior relation | Evidence |
| --- | --- | --- | --- | --- | --- |
| 0/1/2 | RowID/ItemID/Level | uint32 | One exact required ItemID/Level match; independent row key | Actual selected skill/score metadata | NinjaFile.Load.Rows |
| 3..5 | Exp/UK1/UK2 | uint32 | Existing loader rules | Not consulted for Loading skills | Same parser |
| 6/7 | magic1/magic2 | uint32 | Selected values fit ushort before routing | New skills and retained skill list | Same parser/Alternate |
| 8/9 | Power/Damage | int32/uint32 | Existing loader rules | Unused here | Same parser |
| 10 | Opaque source column | Unknown | Not interpreted | No invented behavior | Parser comment |
| 11 | Score and retained UK5 alias | uint32 | Checked score arithmetic | User and rank total | Same parser/Scores |

Canonical actual item maps require positive ItemID key==row.ItemID and unique
physical rows. Reuse existing component bounds4096 item rows/16384 metadata rows.
Missing/ambiguous required metadata refuses; it cannot manufacture DBItem0.
Explicit real magic0 retains the source Add semantics, distinct from absence.

## Validation and transaction rules

- Resolve actual constructor/current Ninja, Player owner/UID, required physical
  equipment and SpellStorage owner/current slot. Non-Ninja Loading prepares only
  rank removal and does not query unused owners/metadata/packet storage.
- Preserve selection order: ordinary slots1..8; alternate9..16 with paired1..8
  fallback. Preserve first actual match and existing-spell precedence. Actual Add
  exclusions1105/4000..4070 remain; stale25 skills remove in existing list order.
- Reject magic narrowing; new spells requiring a byte level reject >255. Checked
  selected-level sums retain badge thresholds9..17 ->1..9. No gameplay cap added.
- Use the frozen checked ranking preparation, including unchanged thresholds,
  category validation, actual six owners/caps/tails and final appearance fence.
- Capture complete spell maps/row scalars; prepare copy-on-write maps and actual
  message models. Do not temporarily publish Items/MyNinja/MySpells or use queues
  as accepting providers. No resource costs apply to this loading transition.
- Lock order: client/player -> Ninja persistence -> actual SpellStorage gate ->
  rank registry -> six category gates. Audit typed writers against that order.
  No new lock is added around a reentrant send/callback with inverted order.
- Prepare bytes, retain/validate real rentals and final plain graph/storage
  checks before any rank/skill/badge store. After the last flag-comparer read,
  repeat plain owner/state/item/metadata/equipment/spell/badge checks only.
- Commit rank maps, actual spell-map reference and actual badge backing byte using
  stores only. Preserve escaped old maps/rows. Reject unchanged before commit.
- Publish adds, removals, changed badge, user, items after commit, checking current
  physical owner/UID/slots/socket/state/map/badge/storage before every enqueue.
  Stale notification is skipped. Send cannot prove delivery; publication loss is
  postcommit, not rollback or an uncommitted retry authorization.
- All observed typed spell writers need the approved real gate coordination;
  public raw writes, cross-reader atomicity, crash/database durability and outer
  reference NPC/resource/file transactions are not certified by this operation.

### Approved typed spell mutation extension

Actual affected entrypoints are SpellStorage.Add, Remove, RebornSpell, ClearSpells
and Update (delegating Add). They must prepare a real copy-on-write spell map and
the complete exact existing frames before any store. Genuine original-comparer
lookups happen before the final plain graph/owner fence; faults or reentry refuse
with original map/row identities, caller packet bytes/cursor, rank maps, badge and
queues unchanged. Add retains source exclusions/fields; Remove absent is a valid
no-op only after complete admission. Reborn retains soul/prior-level behavior with
representable previous-byte admission. Clear prepares its whole captured sequence
before one map publication, preserving valid frame order and unrelated rows.

Candidate spell maps retain the actual original comparer. Original membership
lookups and private candidate comparer callouts all precede final plain fences.
Compare those results with the complete raw expected-ID/reference graph: failed
private insertion/removal cannot become a ghost notice, duplicate or valid absence.
Inconsistent stateful comparers refuse before publication, retaining the real
original graph; no default-comparer substitute is silently installed.
The composed AddItem item-map override likewise preserves original.Items.Comparer
and checks planned raw positive ItemID/reference outcomes before publication. The
frozen base is unchanged; the new override is an explicit production overlap.
Public AddItem/UpdateRank/Loading/Alternate/GetLevel reject already-held own
NinjaState as well as current SpellGate, before actor locks and again under them.
Internal preparers replace all former legitimate public nested composition.
An internal read of the genuine current Ninja and Monitor.IsEntered(NinjaState)
lets typed spell/direct-writer entry refuse an in-progress Ninja owner without
making optional SpellStorage mandatory. Internal prepared spell helpers remain
the permitted operation path. This prevents item/appearance comparer callbacks
from publishing a nested mutation before a later outer refusal.
Public typed mutation reentry while the real SpellGate is held refuses before
stores. Legitimate composition uses private preparation rather than recursively
calling a public mutator under that gate. Serialization/send callouts occur after
mutation gates release; a postcommit delivery problem does not roll back state.
Loading/Alternate/GetLevel reject public entry whenever SpellGate is already held,
even when NinjaState is also held; internal preparers are the permitted locked
composition. Check the current-slot gate before acquiring client/player locks,
and recheck under them, so inverted callers cannot block before reaching refusal.
These are actual coordination guards, not accepting hooks.
Root approved this extension because the old Add indexer followed by TryAdd can
throw from a later comparer after already storing a row. Gate-only wrapping would
retain that defect. Direct file/soul/XP writers get bounded coordination only;
their separate outer transaction guarantees are not thereby certified.

## Expected wire behavior and fixtures

Retain the existing FullLoading sequence: add16310/add16320/remove16330/badge1/
user/full items. One independent add frame is
`14005909080210111A0A08641001180220032800` (2393 Add fixture, not Loading capture).
Existing UserFrame, ItemFrame, SpellFrame, RemoveFrame and LevelFrame calculate
independent bytes; retain their literal/schema expectations and successful order.

Refusals cover missing/foreign owners, malformed spell/rank tails, required
metadata, overflow/narrowing, late real-comparer changes and inadequate storage.
They preserve every rank map, spell row/map, badge, state and success queue. Tiny
native-storage controls are corrected-only and never execute on unsafe old code.
Fixtures use genuine component owners and owned packet/storage data, no host or
socket pump. Root alone builds/runs focused, retained and full qualification.

## Evidence alignment

| Requirement | Spec clause | Decoded catalog reference | Bounded wire fixture | Status/conflict |
| --- | --- | --- | --- | --- |
| Frame fields/order | Packet contract | No client catalog queried | Existing independent Ninja loading goldens | Source + synthetic, no new D13 capture |
| Metadata references | Catalog contract | Actual current parser, owned rows | Genuine Meta fixtures | Source-derived; runtime catalog not queried |
| Slot selection/add exclusions | Validation | Actual item/metadata fields | Existing ordinary/alternate controls | Preserve valid algorithm, refuse unsafe casts |
| Reject without prefix | Transaction rules | Actual owners/maps | New unchanged-state controls | Implementation/Root execution pending |
| Live receipt/delivery | Notification | Unavailable | Unavailable | Explicitly unresolved/fenced |

## Implementation and qualification

Compose only in helper-ninja-loading-safety-next. List every overlap with the
frozen25 explicitly; those base artifacts remain unchanged. Required modules are
real SpellStorage preparation/coordination, plain Player badge commit, managed
typed payload/frame helpers and shared Loading/Alternate/GetLevel preparation.
No dispatcher/profile activation changes. Replace every touched unsafe-prefix
acceptance; do not suppress or bless it. No execution occurred while authoring
this spec. Root will record actual outcomes/source-bug ledger/checklist entries.
See this lane's DESIGN.md and the frozen rank/AddItem README for outer limits.
