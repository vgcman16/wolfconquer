# FSB257 draft: Owned direct Rune bag transfers

## Identity and scope

- Revision 1, 2026-10-30. Requirement owner: Root, within the active full English
  server/client reconstruction goal. This is a source-backed proposal, not an
  implementation, qualification, deployment or Rune-completion claim.
- Target: current 7899 `VisibleDx8D13` dispatcher and packet codecs. D11 fixtures,
  newer resource profiles and original emulator packet numbers do not establish
  current client authority. No new live-client observation is available here.
- Candidate: `root-owned-rune-bag-next/candidate`, prepared from exact public
  `60ab` with Root's corrected Decompose255 intake and frozen creation256 intake.
  Preserve those predecessors; take only reviewed finite changes from this scope.
- Direction/stage: an already admitted actual owned character requests ordinary
  inventory to Rune bag position 211, or position 211 back to inventory, through
  `MsgItemUsageAction.Rune`. This is not EquipRune, UnequipRune, RuneStorage,
  DropRune, ComposeRune, ChanceRune or DecomposeRune.
- Intended behavior: complete direct transfers, with real ownership, complete
  item preservation, exact quantity/capacity checks, genuine UID issuance when
  required, atomic persistence, coherent native publication and owned restart.
- The original action accepts any owned inventory root that `Rune.Add` can hold.
  There is no Rune-SKU/fragment allowlist. Do not narrow admission to families
  401--403, red fragments or a few existing fixture item IDs.
- Root approved D1--D4 before implementation: complete row/closure rekey,
  compatible whole merge selected by lowest physical inventory UID, malformed/
  over-cap refusal, and a separate actual incoming equipment-query phase.
  Source implementation remains subject to finite review and Root qualification.

## Packet contract

The existing managed codec uses opcode 2137, action 61. Offsets in this table are
payload offsets, excluding the four-byte packet header. Integers are little
endian. The fixed outgoing payload is 87 bytes, hence a 91-byte frame before any
complete appended uint arguments. The parser's minimum fixed payload is 30
bytes; a bounded handler must not read or allocate beyond the actual frame.

| Field/path | Number/offset | Wire type/width | Meaning | Presence/repetition | Bounds/default | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| Header length/opcode | frame 0/2 | uint16/uint16 LE | Existing framing; opcode 2137 | Once | Actual bounded frame | `MsgItemUsagePacket`, `WorldPacketOpcodes` |
| Id | 0 | uint32 | Selected physical item UID | Required | Current owned root in the selected source container | Source `MsgItemUsuagePacket` 2156--2188 |
| DwParam | 4 | uint64 | Zero means inventory to 211; **any nonzero** means return | Required | Do not invent a return-only value of 1 | Same source branch |
| Action | 12 | uint16 | `Rune`, 61 | Required | Exact dispatched action | Current codec/native action enum |
| Timestamp | 14 | uint32 | Request metadata | Required for full outgoing codec | Preserve successful echo value | Source `ItemUsageCreate` call |
| DwParam2/3/4 | 18/22/26 | Three uint32 | Request metadata, not new transfer authority | Once each | Preserve successful echo values | Same call/current codec |
| Fixed reserved region | 30--86 | Existing codec bytes | Existing fixed layout | Fixed outgoing extent | Preserve established codec/goldens; no invented meaning | `MsgItemUsagePacket.ToFrameBytes` |
| Arguments | 87 onward | Repeated complete uint32 | Source-unused by this action, echoed through existing codec | Zero or more | Actual bounded frame; no UID/material authority | `ReadLegacyItemUsageArgs`, `CreateLegacyItemUsageFrameWithArgs` |

Unknown-field/extension policy: retain established typed fields and complete
argument ordering in the successful echo. The current helper reconstructs the
fixed codec region; this is not a claim that arbitrary opaque request bytes are
echoed verbatim. This action has no source-backed declared material count.
Do not import Compose's argument cardinality as a new RuneBag gameplay rule.
Malformed/truncated frame or extension handling must be explicit and leave all
state unchanged; partial trailing bytes are not another argument or permission.

Relevant responses retain opcode 2116 `LegacyGameItem`, Add mode 1 or Update
mode 3, and opcode 2137/action 3 `RemoveInventory`. RemoveInventory uses the old
selected UID and zeros for its other typed fields. Item display encoding remains
the actual selected profile's existing 206-byte item codec; raw-self Rune display
adjustment/Effect 1 does not create a learned spell or change saved Rune identity.

## Catalog/table contract

Reference data is the original `lqtxfwd/GameServer/DBServer/itemtype.txt`, decoded
by the existing item catalog reader. Preserve its actual configured decoding,
delimiter/layout and adoption pipeline; do not substitute a fixture-only table,
accessor default, guessed client table or another profile. The live witness must
join the actual adopted raw list and unique ID index, with current row/field
identity and a strict parse of every used field. Record only dependencies used
by the approved branch; metadata is not gameplay ownership.

| Column/offset | Name | Type/width/encoding | Validation | Packet/behavior relation | Evidence |
| --- | --- | --- | --- | --- | --- |
| Raw field 0 / index key | Item ID | Actual positive item ID | Exactly one matching raw/index row; keys agree | Selected/receiver item metadata identity | `ItemType`, adopted catalog |
| Raw field 25 | RuneSpellID | Strict uint32 where Rune display requires it | Actual unique row and current witness; no guessed mapping/classification | Existing item display override only | `MsgGameItem.ITEM_ID`, `LegacyGameItemPacket` |
| Raw field 46 | Raw maximum stack | Strict uint16 | Parse without fallback; apply the proved accessory policy below; effective zero means one physical unit, not zero quantity | Whole merge, stack count and exact inventory slots | Original inventory stack overload and `ContainItemWithStack` |

The source has one relevant post-parse rule: `ItemType.DBItem.Parse` 258--266
sets `StackSize = 1` when the item's source `ItemPosition(ID)` is 15 or 16.
The current `ItemType.LegacyStackSize` likewise models that accessory override.
Consequently raw field 46 alone is not the effective cap for every permitted
inventory root. The historical recipe must retain the strict raw field and
rebuild this known ID/position-derived policy, not accept an arbitrary effective
cap or a parser-default zero. Do not use an accessory SKU list as transfer
permission. Timed cap-1 construction in the original also applies to this case.

Persisted metadata contains only the required item identity/raw-cap dependency.
There are no fresh durability, timer, color, HP or spell-ownership defaults in
the approved full-clone projection. Fields 21/38 are historical evidence for the
discarded source construction, not required dependencies of this transaction.
Any live Rune display lookup must retain its actual current metadata witness,
but unused display/HP/spell authority must not be added to the saved recipe.

Actual 401--403 rows inspected in the predecessor audit use raw cap 0. This is
data evidence, not a universal hardcoded Rune exception or permission for saved
raw stack greater than one. Positive-cap fragments and other inventory SKUs must
use their own actual adopted row. Required class/level, Beast slot, magic
classification and Equip permissions are not source prerequisites for this
container transfer and must not be imported as unrelated action costs.

Score evidence is explicit: original `Rune.Score` 20--35 excludes both position
0 and 211, as does the current score calculator. Direct transfers do not change
the actual active Rune providers, storage score or learned-spell provenance.

## Validation and transaction rules

### Established eligibility and ownership

Require the actual admitted Runtime/Frame/connection, current native/Store item
and spell binding, configured complete saved-before images, real registry domain
and exclusive owner custody. Preserve the existing complete graph, raw UID,
current catalog and lifecycle checks. An internal ticket, matching highwater,
receipt, local lock, hash or an accepting saver is not this ownership chain.

The source gates only `Reborn >= 2` and `Level >= 15`, before the direction branch.
Select exactly one actual UID in inventory for add, or exactly one Rune root at
position 211 for return. Wrong container, missing/duplicate/misbound UID or stale
input refuses. No equipped/inactive position is a return substitute.

Source updates `Player.FinRane` before even checking eligibility. Correct that
dirty rejected effect: any supported FinRane update must be issued by its actual
runtime owner only on a committed success. `FinRane` is not `FineRaneSave`, the
distinct equipment-query throttle stamp. Do not invent a flag, caller-provided
time or recovery-time clock read as authority for either field.

No money, CP, item fee or source RuneBag cooldown is present. Do not invent one.
Own-character Locked items are not prohibited by this source action; neither
arbitrary lock clearing nor an unrelated blanket Locked denial is acceptable.

### Inventory to position 211

`Rune.Add` 242 accepts the same object/UID only when the UID is absent from Rune
storage and fewer than 100 roots occupy position 211. Count roots, not stack
units; other Rune positions do not consume this 100-root capacity.

Prepare an exact inventory removal and append the complete selected root at 211.
Preserve its physical UID, every unrelated saved field, full deposited-child
closure and perfection/owner/signature data. Preserve unrelated roots and stable
source bucket order through lossless serialization. The exact global physical
UID set is unchanged. No UID reservation, color draw, metadata-default item
replacement, timer renewal, provider adoption or learned-spell mutation occurs.
Any new quantity validation must use actual caps and physical raw-zero semantics,
not rewrite a malformed row into acceptance.

### Position 211 to inventory

The reference is **not** a same-UID move: `Rune.Remove(false)` 307 first removes
the selected Rune root, then `Inventory.AddItemWitchStack(ID, 0, rawStack,
Bound == 1, Locked:0)` 746 performs a whole merge or new UID construction. The
old selected root UID retires in both branches. The actual item counter supplies
each fresh inventory UID; reusing the old UID would change this successful
source identity behavior without declaring it.

The original implementation is unsafe: it checks item metadata after removal,
ignores an Add failure, can delete a raw-zero nonstack unit, clears a receiving
stack's Locked, renews timed items and discards selected XP/opaque fields on new
construction. Its oversized split path uses lossy capacity arithmetic and may
fall back to returned-item Warehouse 65535. These are research observations,
not acceptance criteria. The current managed same-UID fallback, destructive
full-inventory special case and locked-clearing special merge are also unsafe
or source-inaccurate and cannot be used as the new transaction contract.

Required safe invariants are: normalize raw zero to one physical unit; prove all
required metadata and the full return result before removal; preserve the total
units and every nonconsumed physical UID; retain unrelated fields; derive actual
inventory slots from the complete whole-merge/new-root plan; and require final
inventory root count at most 40. A compatible whole merge may succeed at 40
roots with no birth. A full inventory requiring a new root refuses without
deleting, publishing or silently using an unjoined returned-item owner.

### Root-approved sanitized decisions

| Decision | Status and behavior | Source/safety reason and remaining authority |
| --- | --- | --- |
| D1: Rich-row return construction | **ROOT APPROVED:** retire the selected UID, obtain one genuine new UID, and rekey/copy the complete row/closure with only root UID and position changed. Merge only complete compatible payloads, changing the receiver's quantity only. | Source fresh construction resets selected fields. Keep child UIDs, perfection, opaque represented fields, Locked, XP, durability, color and timer exactly. No neutral-row flag, fresh default, RNG or lifetime renewal is needed. |
| D2: Whole first-fit selection | **ROOT APPROVED SANITIZED ORDER:** choose the lowest physical inventory UID among fully compatible rows that can receive the ENTIRE normalized quantity. | Source native-dictionary first-fit is not proved by snapshot order. This explicit deterministic policy replaces that unstable choice; multiple fitting rows no longer cause an arbitrary same-UID fallback. No partial fill occurs. |
| D3: Malformed/over-cap stored quantity | **ROOT APPROVED:** refuse malformed or over-cap inputs before mutation, rather than repair them or invoke Warehouse 65535. | Source overflow splitting is not proof that an already-invalid saved row is authorized. Raw 0/1 remains a legitimate nonstack unit. No returned-item owner has been joined for this action. |
| D4: Incoming equipment query | **ROOT APPROVED:** commit/publish the bag transfer, emit its source prefix/echo, then invoke existing `ExecuteLegacyEquipmentQueryAsync` through its actual owner, followed by rank. These are separate commit/recovery phases, not one invented combined transaction. | Source queries after the bag mutation and echo. Actual FineRain admission, status HP, transform, refinement and other query effects cannot be replaced by a generic four-frame refresh or arbitrary recorded after-HP. Query failure cannot turn the committed bag transfer into rejection, refund or permission to replay it. |

Complete metadata/closure
compatibility must cover Bound, Locked, durability, XP, sockets, plus, activation,
expiry, attributes, opaque serialized fields, perfection and deposited children.
Do not clear Locked, renew lifetime or discard one root's payload during a merge.
Child-bearing or incompatible rows use the full lossless rekey branch when the
actual native/catalog graph and capacity permit it. Distinct owned child UIDs
cannot be collapsed by merging parents; do not detach or duplicate children.
Root explicitly prohibits a whole merge when either root has ANY deposited
children. There is no proved stack rule that preserves both child-UID sets.
Compare complete normalized per-root serialized payloads, ignoring only root
UID, root position and root stack count. Preserve every other byte/field. The
before graph still requires lossless actual raw-image and native-owner proof;
this comparison does not bless unrepresented or malformed saved bytes.

### Prepare, commit, publish and restart

The future pure planner takes the complete before snapshot, selected UID,
direction and exact used metadata. It returns exact source/receiver selection,
normalized quantities, destination stack quantities and required new-root count.
The approved bound admits at most one fresh root: over-cap input is refused, so
there is no split or returned-Warehouse branch. Fresh rekey retains raw stack 0
or 1 exactly; a whole merge uses the sum of both effective physical quantities.
It performs no I/O, RNG, reservation, owner creation or live mutation.

The approved recorded recipe must rebuild the complete result from bounded
historical inputs: exact direction/selected UID, used actual metadata, approved
merge selection, and only genuinely required issuance/random data. A detached
operation record is data, never a live capability. Request echo arguments are
not inventory or allocation authority. Do not accept arbitrary afterimages as
the recipe or use today's catalog to reinterpret a historical committed intent.

UID modes are derived from the actual plan: inbound has zero births/retirements;
whole merge retires the selected UID and has zero births; a nonmerge return
retires the selected UID and uses exactly the required genuinely issued range.
The precise registered namespace reservation/retirement and WAL schema are lead-
owned design work pending approval. Never borrow a Decompose-kind receipt,
retired ordinary allocator or mailbox permission to make this producer pass.

Validate and prepare the complete graph, serialized images, protocol frames,
capacity and allocation prerequisites before issue. Recheck actual mutable owner,
catalog, files and pending identity at the established write boundaries. Persist
complete Character/Items/Spells as required by the approved writer, then publish
the coherent native/Store result. No current RuneBag publish-before-persist
behavior survives. No success packet, rank mutation, FinRane update or physical
new UID/native item publication may escape an uncommitted rejection. An already
issued namespace range is the distinct fenced, permanently burned case below.

The bag transaction itself preserves complete Character/Spells bytes and all
spell origins; selected 211 is not an equipped provider. Inbound query effects
belong to the separately approved actual D4 owner. Outbound has **no** source
equipment query, HP clamp, HP/status pair,
refinement refresh or spell add/remove. Preserve current/saved/native HP exactly
on return, including an already active transform whose cap is below current HP.

The incoming bag commit is complete before the actual query phase begins. Query
interruption, admission refusal or transport failure cannot undo the bag images,
reissue its UID action or claim it never committed. Bag restart finishes the
captured bag intent; it does not invent a historical query result, read a fresh
query clock, replay notifications or run a new query as part of WAL recovery.
The existing query owner's own persistence/recovery boundaries remain explicit.
Invoke the actual joined query owner, not an accepting callback or a prepared
four-frame substitute. Rank is the source post-query action and must use actual
current committed state under that owner's lifecycle rules.

After actual issuance, any uncertain precommit failure must fence the true live
producer/registry before another reservation; an allocated range stays burned.
After commitment, notification failure is not rollback or another grant. The
completed-producer history work from FSB256 must remain intact rather than be
bypassed by archive/debt flags. Retained unsupported work remains fenced.

Owned restart requires exact supported record/recipe/schema, actual captured
targets and known stages, same exclusive custody, protected captured-before and
reserved-birth UID checks, namespace/current receipt joins when applicable, and
an ordinary full raw rescan before readiness. No logical overlay, generic
allow-pending option, unrelated root/stage exemption, allocation, reroll or
success notification is introduced by recovery. A second restart must perform
no replay, further reservation, UID change or field normalization. Generic
unowned opens must retain their pending-work refusal.

## Expected wire behavior and fixtures

The following successful order is retained with Root's explicit sanitized D1--D3
identity/preservation/selection policy:

1. Inventory Rune-family root to 211: 2116 Add for the same UID and full fields,
   exact typed 2137/action-61 request echo including arguments, actual equipment
   query phase, then Rune ranking refresh.
2. Inventory non-Rune root to 211: prepend the old UID's 91-byte
   2137/RemoveInventory; then the same Add/echo/query/rank sequence.
3. Return with whole merge: old bag UID RemoveInventory, 2116 Update of the
   actual receiving UID/quantity, exact action-61 echo, then rank. No query.
4. Return with a new root: old bag UID RemoveInventory, 2116 Add for the one actual
   newly registered inventory root, exact echo, rank.
   No query. Packet IDs must come from the actual receipt, never fixture literals.

Source `QueryEquipment` 2376 performs FineRain's six-second admission. A denied
query sends `You Shoud wait 6 seconds.` after the already committed transfer/echo;
it does not undo the transfer. An admitted query invokes AppendItems, stat/status
HP and transform work, refinement and WindWalker flag effects. Current
`ExecuteLegacyEquipmentQueryAsync` owns a bounded joined query pipeline; current
RuneBag's unconditional four-frame helper is not proof of this whole sequence.
D4 must preserve actual proved query phases and identify remaining unjoined
effects honestly, without inventing a successful placeholder response.

Planned genuine fixtures must bootstrap saved owned inputs, admit through real
Runtime/Frame/native/Store and issue the actual action. They must cover same UID
ingress with full closure/Locked/opaque preservation, non-Rune ingress, no-birth
whole merge including full inventory, fresh registered return, raw 0/1,
capacity/missing metadata/quantity refusal and exact current/native/disk/ledger
convergence before replies. Use actual adopted catalog rows and BCL-backed
metadata collections, not an accepting saver or fabricated item authority.

Recover actual producer interruptions at replacement prefixes and a real stage
flush, before fresh login assertions, then restart again. Capture genuine WAL
and private random data only from the real writer when such data is required;
no positive handmade journal, operation-kind row, reservation or recipe. Negative
corruption may alter genuine captured evidence and must preserve all refusal
bytes, lease ownership and unrelated UID/history inputs.

Audit old RuneBag tests rather than preserving deletion, locked clearing,
same-UID re-creation or publish-before-save assertions. Retain independent full
request/91-byte Remove/206-byte item codec goldens and valid metadata/tail/order,
UID, ranking and peer invariants. An old D11 managed fixture is not genuine D13
native ownership; describe any coverage migration explicitly.

## Evidence alignment

All original paths below are within
`征服-龙拳天下服务端-已修复/lqtxfwd/DecompiledSource/GameServer`.
Original files and saves remain read-only.

| Requirement | Spec clause | Decoded catalog reference | Bounded wire fixture | Status/conflict |
| --- | --- | --- | --- | --- |
| Direction/eligibility/echo | Packet and established eligibility | Not applicable | Current `MsgItemUsagePacket`; existing RuneBag tests | Source-observed `MrRayzo.Game.MsgServer/MsgItemUsuagePacket.cs` 2156--2188; no new client observation |
| Same UID add and 100 roots | Inbound | Existing selected item row | Existing RuneBag add fixture must migrate owner | Source-observed `MrRayzo.Role.Instance/Rune.cs` 242--251 |
| Old UID retirement on return | Return/UID modes | Selected actual cap row | Current same-UID fallback conflicts | Source-observed `Rune.cs` 307 onward and `Inventory.cs` 746 onward |
| Whole first-fit and fresh construction | D1/D2 | `DBServer/itemtype.txt`, field 46; `ItemType.Parse` accessory cap override | New independent genuine receipt-based fixtures required | Root approved full-clone rekey and lowest-compatible-UID merge; no fields 21/38 defaults |
| Raw zero is one physical unit | Return/quantity | Actual cap 0/positive rows | Pure and genuine controls required | `Inventory.ContainItemWithStack`; original return raw-zero loop loses units |
| No bag score/provider change | Catalog/Character identity | Existing Rune score/storage rows unchanged | Preserve full Spells/origin bytes | Source-observed `Rune.Score` 20--35 excludes 0/211 |
| Incoming query versus return no-query | D4/expected order | Actual joined equipment-query dependencies | Query admission/HP fixture required for approved phase | Root approved separate phase; `Equip.QueryEquipment` 2376 / `OnDequeue` 2267 onward; current RuneBag helper incomplete |
| Atomic file/UID/native lifecycle | Prepare/commit/restart | Actual adopted registry/domain/history | New actual WAL interruptions required | Current RuneBag publish-before-persist is unsafe; strict20/real owner source-authored, unbuilt/unrun |

## Implementation and qualification

### Root-approved closed durable shape

Version20 uses the existing CharacterItemsSpellsDomain and exactly one RuneBag object with IntoBag (bool), SelectedUid (uint), Metadata (ItemId and RawMaximumStackSize), Request and Allocation. Request/Allocation are explicitly null only when the actual pure recipe derives NewUidCount0 (ingress or a compatible whole merge). A rekey derives NewUidCount1 and requires the actual canonical registry request/receipt with count1. No arbitrary no-birth boolean, accepting receipt or inferred high-water ownership is permitted. Character bytes remain exact. Every present complete Spells image remains byte-identical, including row order and reserved byte11. A genuinely absent saved Spells baseline with empty owned membership follows the actual prepared writer: AFTER is the canonical four-byte empty file, so physical presence changes in that one case. No file is deleted after admission to manufacture absence, and no nonempty membership or unknown provider is repaired. Only the derived Items projection otherwise changes. Ingress invokes the existing real owned equipment query after committed publication and echo; that query has its own HP/status/cooldown/persistence phase, never part of an invented globally atomic bag20 claim. Return invokes none.

UID state version6 retains the existing closed version5 root shape. Only the actual RuneBag return Runtime issuer may append NamespaceReservation { Version:1, Kind:"RuneBagReturn" }, writer "rune-bag-return-v1", count1. Version6 also understands the existing exact RuneDecompose marker; versions1..5 keep their established validation. Existing Decompose append uses max(5,currentVersion), and supported startup checks include5/6, so no downgrade or silent skipped work occurs. Generic reservation cannot mint either typed marker. Unknown kinds/versions, cross-kind transitions, unsupported identical pending states and unrelated work remain fenced.

No-birth ingress/merge performs no UID row, generation, high-water or table-capacity mutation, but still requires the real current Runtime/Registry/domain/known completed-history and complete file/native owner. Birth rekey retains the exact row until real three-image commitment and private writer proof. A post-issue abort fences live retry; only a genuine fresh configured startup with full game-journal/stage/retained-work/raw clearance may retire its exact bookkeeping while retaining burned high-water. Pending20 recovery keeps the row and proves the exact recipe/images/receipt under the same exclusive custody. Captured BEFORE UIDs and the exact new birth range are protected from foreign physical ownership even at prefix0/all-AFTER; unrelated references retain ordinary pinned high-water rules. No caller-supplied ignored roots or generic pending allowance exists.

FinRane (distinct from QueryEquipment FineRaneSave) is a transient native success publication joined to the actual prepared Store/native owner; it is never set on rejected preflight or synthesized as a persisted replay clock. Full item closure/perfection/opaque data survives rekey; a selected or receiver deposited-child closure is not whole-merged because that could destroy physical child identities. Full capacity then refuses or uses the valid new-root branch, without narrowing supported item categories.

Existing affected implementation paths are
`WorldPacketHandlers.ItemUsage.RuneBag.cs`, `.RuneBag.ExistingStackMerge.cs`,
`.RuneBag.FullInventoryAddFailure.cs`, existing ItemUsage dispatch/codecs, and
the actual equipment-query/UID/persistence owners. They are evidence inputs,
not newly approved fallbacks. Focused planner/recipe files are
`WorldPacketHandlers.ItemUsage.RuneBag.Plan.cs` and `LegacyRuneBagRecipe.cs`.
`LegacyRuneBagPlanTests.cs` and `.Projection.cs` provide pure data controls, not
actual producer/UID-owner qualification. Lead owns final authority,
schema and API decisions; the other helper's actual handler/fixture scope remains
disjoint. A lossless data-only `PlayerItemSnapshotItem.WithUid` may reuse the
existing complete Copy builder; it grants no registry issuance authority.

No builds, tests, native calls, API probes, host actions, Git or legacy client
execution were performed. Root alone reviews/integrates/qualifies the finite
source batch. D1--D4 are approved; the exact future WAL/namespace authority and
actual query/handler integration are authored as source-only work and remain unbuilt/unrun until Root qualification. The raw scan runs outside Runtime/Registry gates because it takes ordered snapshot locks; the separate typed maximum observation under the current Registry gate only denies out-of-range input. A fresh-root return scans its actual reserved birth for foreign physical ownership before issuing its game WAL. Unexpected issuer failures and every issued-but-uncommitted exit fence the Runtime and Registry, even if no receipt escaped. The same actual Trades scope covers the producer and final deny guard; only the exact private committed-writer pointer exempts a successful action. Fresh full-clearance startup may retire burned orphan bookkeeping; live abort reconciliation remains unfinished.
Current configured client acceptance, full SQL, live
deployment and full Rune/server/client completion remain unproved and active.

Use [Sanitized Parity](../../engineering/SANITIZED-PARITY.md) and the
[protocol workflow](../../engineering/PROTOCOL-WORKFLOW.md). The lead must record
the final deliberate source corrections and precise executed verification in
`FIXED-SOURCE-BUGS.md` under FSB257 only after qualification, and update the
current checklist/handoff. This draft is not a fixed claim. Original
`CONQUER-HANDOFF.md`'s executable/AV/session/route/save/retained-mail hold applies.

## Root qualification update - 2026-10-31

This supersedes the earlier source-only status. All94new bag/planner/creator-
lifecycle outcomes have actual passing evidence; original fixture failures are
retained. The combined128new RuneBag/WindWalker set passes across overlapping
runs. Builds have zero errors and no changed-path warnings. Exact raw images,
registered UID custody, native/Store publication and restart assertions remain.
Reviewed source is intaken; its new exact-public full gate is pending. No SQL,
power-loss, protected native authentication or live/newer-client proof is added.
