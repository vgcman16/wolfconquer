# Owned native item graph join

## Identity and scope
Revision1, 2026-10-29. Baseline sourceef3ab6e1/generated5362feb1.
Milestone: ordinary admission and subsequent real Store item publications share
one genuine native Inventory/Equipment/Warehouse/Wardrobe/Rune/Relic/Mythsoul
representation of the canonical saved PlayerItemSnapshot. Implement both first
admission and central ongoing/single/pair publication; admission-only observation
is not completion. Existing D13/old7899 authority remains. Do not claim7937
catalog mapping, native combat activation, fullconstructor parity, SQL or liveproof.

## Packet contract
No new opcodes, schemas or packets. Successful existing startup/inventory/mail/
equip/trade wire ordering is retained. Prepare required native changes BEFORE
actual durable writers; rejection has no resources/files/holder/native/output
prefix. A failure after actual persistence is committed recovery-required, never
an uncommitted refusal/refund. Keep real recovery owners and typed diagnostics.

## Catalog/table contract
Actual WorldDataCatalog.Items and ItemType constants establish server-side
RequiredClass2 and RuneSpellId25 semantics already used by the managed host.
They do not establish arbitrary new67 client column meanings. Use strict real
row/key/required-field validation, not default0 for missing/malformed Rune data.
No fabricated global ItemsBase or partial metadata owner is installed. A scoped
owned constructor path must consume actual catalog evidence and preserve genuine
native item construction/conversion effects. Do not reinterpret all global
native metadata APIs as initialized from a two-field lookup.

The current saved snapshot is the adoption source; preserve its UIDs/locations,
parents/deposits and perfection fields. This is NOT another legacy load-time
filter/normalizer/job-fallback. In particular do not silently drop205901 or clamp
already accepted snapshot fields. Actual constructor-derived SpellID/effects and
native extra lifetime behavior must be specified/tested, not guessed.

## Validation and transaction rules
- Capture the actual issuing Store/holder identity, connection/nativeclient/
  player and saved snapshot; UID alone never authorizes a binding. Reuse genuine
  ordinary admission/prefix/spell owner mechanisms, no accepting callback/queue.
- First-admission preparation may create genuine detached container objects while
  real slots are absent and current playerUID is preprefix0. They are not current
  gameplay owners until the actual durable admission/prefix and assignment-only
  publication. Refuse partial/foreign existing slots; never overwrite them.
- For subsequent item changes keep the genuine container owners, prepare complete
  state/maps/cache/revisions detached, validate canonical global UID/allowed item
  aliases and real metadata, then publish native and holder with the existing
  actual Store commitment. Cover real Set/TryReplace, persisted single/pair and
  admission paths through the central publication plan; no mailbox/equip stale
  graph requiring relogin. Retire/detach on actual holder retirement/replacement.
- Preserve real guild/job/location behavior chosen by canonical snapshot owners.
  No invented inventory/bank caps, missing owners, UID issuer or root cloning
  policy. Explicitly define valid wardrobe/equipment same-item alias.
- Complete every allocation/comparer/converter/metadata callback before final
  pure witness checks. Nothing throwing/callback-bearing follows first native/
  holder publication. Detect row/map/snapshot/owner changes before commitment;
  after committed ownership loss, block actual recovery and retire actual owner.
- Same real player/client/storage gates and ordered Store pair gates; do not
  introduce an independent cross-player order. Reentrant raw/game mutations must
  not authorize writes through a stale joined owner. Public raw field writers/
  observers and SQL/power-loss guarantees remain explicit limits.

## Expected wire behavior and fixtures
Use real owned Frame/Store/persistence fixtures, actual native containers/items,
real item catalog rows and actual current holder/issuer, not placeholder owners.
Required controls: login populates genuine graph; mailbox claim/equip/drop or
other actual inventory changes update it without relogin; pair publication stays
coherent; malformed/missing Rune metadata, duplicateUID, foreign/partial owner,
late preparation/actual writer failure and postcommit owner loss preserve truth;
callback changes are detected; retirement disallows old binding. Existing valid
startup packets/layout goldens/coverage remain. No client/executable/SQL/service
execution, Git or duplicate CI/build by helpers. Root qualifies after the active
fullWindows40990231326 terminal and preserves its matching runtime on success.

## Evidence alignment
| Requirement | Existing source | Status |
| --- | --- | --- |
| Raw-only startup gap | NativeEquipmentLoad/SessionStartup/OrdinaryAdmissionPlan | confirmed current architecture gap |
| Real issuer/commit/publication join | OrdinaryAdmissionLease + PreparedSessionSpellPublication | actual integration points |
| Genuine184/80 fields/constructors | item record/image/materialization and FSB226 | reusable with scoped catalog path; no global fake |
| Current row semantics | ItemType.RequiredClassFieldIndex/RuneSpellIdFieldIndex | server model; not new7937 field authority |
| Coherent full native lifecycle | owned Frame mutation tests | required, not yet implemented |

## Implementation and qualification
Three isolated helper scopes: A scoped catalog/image/genuine conversion; B actual
container birth/refresh publication; C real Store/admission binding/integration.
Each sends a finite API/file plan before edits and coordinates dependencies.
Root reviews interfaces/callers, integrates, runs focused/appropriate wider and
actual-commit CI, and alone stages/commits/pushes. No helper changes actual
checkouts or59held Guild/Union paths. All authored code remains unqualified until
actual tests. Do not fabricate a safe-looking partial implementation to go green.

## Aligned implementation decisions (2026-10-30)

- Image Preparation retains proposal proof separately from the published Image.
  The Image owns a deep canonical FinalSnapshot and genuine metadata/native
  provenance. Equal canonical input reuses the image; unchanged root/subtree and
  child nodes retain genuine identity where proven, including derived expiry.
- Refresh uses the existing seven real container objects, complete detached maps,
  cache and bag state. Physical identity checks are not membership authority;
  full membership proof runs under the actual actors/storage gates before image
  callbacks and again before the writer. No new standalone authority callbacks.
- Normal mailbox/equip/drop/sell delivery consumes the exact canonical session
  returned by Store. Generic item publications return their exact final session
  to the existing continuation; no UID or value-equality alias grants authority.
  Generic Set/CAS retain existing per-file best-effort postpublication item/
  scalar saves: some IO failures are logged internally, while only escaping
  failures get the typed publication/recovery fence. This remains an open
  durability defect, not a prewriter transaction or a crash-safe atomic claim.
- Mailbox package preparation is a closed Store-issued capability. Strict actual
  item-ID catalog preflight precedes UID reservation; full materialized image
  preparation precedes package work commit. Genuine intent/UID receipts are not
  refunded or reissued on later uncertainty. Source snapshot proof and canonical
  delivery identity remain distinct without any postcommit graph recapture.
- Strict scoped construction never installs a synthetic global catalog. A normal
  ITEM_ID lookup failure preserves the previous ID. Valid DateTime binary kind
  bits are preserved; lossy relic attributes are rejected before commitment.
- The preceding actual full Windows gate40990231326 is complete/success on
  5362feb1. Its matching runtime is preserved. Root alone executes this new
  candidate's tests; helpers author finite source scopes without execution.

## Regression-driven integration refinements

Actual Frame catalog provenance can be null when the genuine constructors need
no Rune metadata (including empty/non-Rune roots/deposits). No empty or global
catalog substitute is installed. Any actual Rune requirement still refuses a
missing/malformed real catalog before UID use, writer or native publication.

Read-only runtime inspection retains exact real Store/player/spell/PIN issuance
while observing item slots/values as diagnostic subjects. Missing or different
item state can be reported without granting gameplay authority; gameplay Store
publication still requires the full joined graph. Diagnostics remain non-atomic
observations and never claim synchronized runtime tuple or acceptance PASS.

Joined native proficiency training must read actual unique active weapon IDs
from canonical admitted equipment through the real Store/actor/storage lease,
not replace an admitted Equipment owner or populate a synthetic cache. Full
native QueryEquipment/stat/combat activation remains a distinct unproven boundary.
Operator mail delivery must keep its original source graph proof separate from
its exact canonical committed session. No UID/value equality fallback authorizes
a continuation. Malformed inventory positions cannot replace the valid graph.
