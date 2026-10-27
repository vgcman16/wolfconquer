# visible-dx8-d13-ordinary-spell-coherence: Store and physical spell membership

## Identity and scope

Revision 1, 2026-10-27; Root-requested source-only P1 integration design. Source is
the canonical public tree after Ninja checkpoint 9e779d5d. P0 class/level hydration
is a separate frozen proposal. Full run 40555473262 does not qualify this design.
No source implementation, execution, Git, runtime adoption or client action is
part of this artifact. Held Guild/Union paths and rank persistence are excluded.

Target is the existing `VisibleDx8D13` ordinary managed session. The immediate
goal is a genuine constructor-bound MySpells whose persisted membership agrees
with the current Store session after every cooperating publication. It is not a
detached snapshot used as runtime authority. Ninja Loading, paid operations,
native socket adoption and new request handlers remain separate.

## Packet contract

No new packet/schema/order is proposed. Managed spell frames remain opcode 2101;
removal remains the existing MsgAction 109. Ordinary admission sends AnswerOk after
durable commitment and uses the real connection. Existing saved-spell publication
belongs to Action 78; the startup slot sends only newly normalized login grants.

| Field/path | Number/offset | Wire type/width | Meaning | Presence/repetition | Bounds/default | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| Managed spell | opcode2101 | Existing typed frame | Learned/progression/soul state | Existing producers | Existing codec bounds | `LegacySpellPacket.cs` |
| Removal | MsgAction.Type109 | Existing action frame | Remove learned ID | Existing removals | Existing UID/ID fields | DerivedSpells, SkillEraser |
| Ninja user/items |2393/2395 source constants | Component codecs | Native component output | Not added here | Placement/request authority unresolved | Frozen hydration spec |

Membership preparation must not send. Caller-owned valid frames retain their
existing order. A send failure after commitment is not an uncommitted rejection.

## Catalog/table contract

`Data/PlayerSpellSnapshot.cs` models complete 12-byte little-endian saved records:
UInt16 ID, UInt16 Level, Int32 XP, byte PreviousLevel, byte SoulLevel, byte
UseSpellSoul and one opaque padding byte. The snapshot models the first 11 bytes.
`LegacyOrdinaryAdmissionSpellImages.Project` deliberately preserves old raw rows
and padding when appending the actual 1110 grant. P1 must not re-encode unchanged
saved membership merely to hydrate a runtime owner.

| Column/offset | Name | Type/width/encoding | Validation | Packet/behavior relation | Evidence |
| --- | --- | --- | --- | --- | --- |
| Header0 | Count | Int32 LE | Exact complete count/length | Saved membership | PlayerSpellSnapshotLoader |
| Row0 | ID | UInt16 LE | Positive, unique raw ID | Real native row.ID/key | Ordinary saved validation |
| Row2/4/8 | Level/XP/PreviousLevel | UInt16/Int32/byte | Native width; XP nonnegative at ordinary admission | Persisted fields | Same loader/native MsgSpell |
| Row9/10 | SoulLevel/UseSpellSoul | byte/byte | SoulLevel retains byte domain; selected soul must roundtrip0..4 | Native setters | LegacyNativeMsgSpell |
| Row11 | Opaque | byte | Preserve unchanged raw bytes | Not invented as a field | OrdinaryAdmissionSpellImages |

Native ClientSpells additionally stores cooldowns, packet fields, Soul,
HashLevelSoul, StampSpell and other transient fields. Rebuilding all rows on each
Store change would lose these values. Existing row-copy code in
`LegacyNativeSpellStorage.DirectWriters.cs` and the complete SpellValues witness
in `.Preparation.Rows.cs` establish the available physical fields. A changed
persisted row needs a private copy retaining its other actual values; unchanged
rows may retain physical identity. New rows use the genuine native row type and
setters. Preserve the actual map comparer and validate both raw identities and
comparer lookup/insertion results before the final plain fence.

No spell metadata is fabricated. Unknown saved IDs are membership, not permission
to cast. The current native selected-soul setter maps unsupported bytes to None;
ordinary native adoption must refuse unrepresentable saved selection, not silently
erase it. Missing saved files follow existing admission empty membership, without
inventing a file or runtime cast authority.

## Validation and transaction rules

### Actual Store publication map

All listed methods write SessionHolder; no central spell join currently exists.
`SessionHolder.Publish` only changes holder identity on UID change and performs
Volatile.Write. Lock-free TryGet/Snapshot are point-in-time reads. The existing
Frame callback is **after** publication and cannot be a preflight participant.

| Actual entrypoint/source | Existing eligibility and publication | Durable boundary | Required coherent join |
| --- | --- | --- | --- |
| `WorldSessionStore.cs` Set | Mailbox fence; actual connection gate; takes current holder; Publish then callback | Frame callback saves sidecars after memory; I/O gaps are logged | Prepare current/proposed spells before either store; plain native-map + final-session publication under ordered real gates; do not use callback to create owner |
| Same, TryCompareAndSet | Exact expected record and mailbox fence under same gate; Publish then callback | Same post-publication auto-save | Same prepared participant; false on stale expected; no native change on failed CAS |
| `LegacyNativeClientPublication.TryUpdate` | Holds current holder identity gate, reads current, executes synchronous update then calls Set reentrantly | Inherits Set callback | Real EquipmentQuery path; preserve necessary internal composition while refusing comparer/public mutation reentry |
| `WorldSessionStore.PersistedTransactions.cs` TryCommitSession | Expected identity/mailbox fence; existing persist callback; then Publish | persist returns true before memory; no ordinary callback | Prepare spells before persist; final plain proof after callback; publish prepared membership only after actual success |
| Same, TryCommitPair | Ordered two connection gates; both expected records; one persist callback; first/second Publish | Persist-before-both; no ordinary callback | Prepare both participants before persist; no allocation/comparer between first and second publication; retain shared connection order |
| `WorldSessionStore.PreparedPersistence.cs` TryCommitPreparedSession | Genuine bound mailbox producer; expected record; callback returns final session; Publish; finish producer publication | Callback already owns durable intent/commit before it returns | Current mailbox plan does not change spells. Require returned spell membership to match the captured baseline; do not introduce a late allocating spell preparer after durable work |
| `WorldSessionStore.OrdinaryAdmission.cs` TryCommitOrdinaryAdmission | Empty real holder lease, account/recovery/mailbox checks, actual actor/PIN gates; durable plan; native prefix; Publish | Actual joint journal before volatile publication; committed PIN failure retires/fences recovery | Prepare detached real SpellStorage and immutable membership as admission participant before writer; publish slot/binding with prepared native prefix and holder; no accepting constructor callback |
| `WorldSessionStore.MailboxPackages.cs` TrySetMailboxRecoveryOwner | Empty holder plus genuine producer's read-only admission; Publish | Exact recovery load; no ordinary callback | Keep mailbox-only authority separate. No ordinary binding by adjacency; reject replacement of an existing ordinary holder |
| `WorldSessionStore.CompleteLogin.cs` TryCompleteLegacyLogin | Actual current owner; copies current Character attributes; native CompleteLogin; Publish/callback | Callback for actual deltas; PlayerSpells is retained current | Carry the same prepared spell binding/map without rebuilding rows; prove no membership change before native CompleteLogin store |
| `WorldSessionStore.cs` TryRemove/Clear | Actual connection gate; retire holder identity and remove | No spell save here | Retire ordinary binding with the holder. A late publisher cannot authorize itself with the same UID/new holder |

The inspected Trade.Exchange and Mail.Claims.PackageCommit plans retain
PlayerSpells. The transaction APIs nevertheless accept complete proposed sessions,
so their publication contract can change spells even when current callers do not.
They must still pass through the central participant: an unrelated stale full-record Set can overwrite newer spells without
containing a `PlayerSpells =` assignment. Do not solve that by guessing or merging
business intent. For a bound ordinary owner, changed membership requires an exact
expected current record. Current-only transformations such as EquipmentQuery can
continue under the real holder gate; stale copies refuse before durable/state work.

### Actual spell-changing producers and durability

This is the complete set of explicit managed PlayerSpells record assignments in
tracked Core, plus actual initial construction. Each row identifies its consuming
publication boundary, not merely a reference to the field.

| Producer/method | Change and actual order | Durable/outer boundary needing treatment |
| --- | --- | --- |
| `WorldPacketHandlers.EquipmentQuery.DerivedSpells.cs` SetNativeDerivedSpellAsync / RemoveNativeDerivedSpellAsync |21930 rank-derived and20000 venom membership; Commit -> TryUpdate -> Set; then managed spell/removal frame | Frame auto-save only; genuine startup mutation makes one-time login mirroring insufficient |
| `WorldPacketHandlers.LegacySpell.cs` HandleLegacySpellAsync | Learned soul eligibility; replace selected row; Set; explicit spell Save; response | Both callback and explicit Save can write; no pre-memory durable guarantee |
| `WorldPacketHandlers.PlayerMagic.SpellExperience.cs` TryApplyLegacyPlayerMagicSpellExperienceAsync | Real damage/metadata calculation; Set new XP/level; explicit Save; frame | Many attack callers already committed damage/resources earlier; membership coherence does not make whole cast atomic |
| `WorldPacketHandlers.ItemUsage.SkillBooks.cs` HandleLegacySkillBookUseAsync | Real book/class/requirements; prepare learned row and consumed item; Set both; save spells then items; consume/spell/dialog frames | Cross-file item+spell cost requires actual joint transaction before calling a new fallible join; not repaired by mirrored maps |
| `WorldPacketHandlers.ItemUsage.RuneEquipment.cs` HandleLegacyUnequipRuneAsync | Rebirth/level/capacity/vendor/item gates; prepare item move and removed rune spell; Set; item persistence; removal and equipment refresh | Item+spell membership changes together; callback currently saves spells; no single durable transaction shown |
| `NpcEntrypoints.PromotionMasters.SkillLearning.cs` ApplyLegacyPromotionMasterSpellsAsync | Earlier class/level/learned checks; Set; explicit Save; caller spell/dialog publication | Use exact expected membership participant; preserve actual positive eligibility |
| `NpcEntrypoints.PromotionMasters.Trojan.cs` ApplyLegacyPromotionSpellsAsync | Same Store-first/save-next spell update, including source grant batches | Character promotion helper is separate; do not claim whole promotion transaction |
| `NpcEntrypoints.SkillEraser.cs` TryHandleLegacySkillEraserNpcAsync | Actual selected learned ID; Set removal; optional Save; action109/dialog | Safe missing-state refusal must precede membership/packet outputs |
| `NpcEntrypoints.SubclassMasters.cs` HandleLegacyPerformerDanceAsync | Real performer/option prerequisites; requires Store/spell root; **Save first**, then Set; spell/effect/system frames | A new fallible join after Save would dirty durable state. Prepare/lease before actual file write; callback repeats save after Set |
| `NpcEntrypoints.AccountSecurity.FullAccount.Class.cs` ApplyLegacyAccountSecurityFullAccountClassAsync | Real roots/gates/projection; custom Character+spell persistence first; Set; outputs | `.Class.Persistence.cs` replaces two files with in-process rollback attempts, not the ordinary journal. Join must be prepared before this persistence; whole class change remains separate |
| Ordinary `LegacyOrdinaryAdmissionPlan` | Complete saved rows + actual missing lineage1110 normalization; Candidate constructor; joint journal; native prefix; holder | Best first owner-adoption participant: use normalized committed image and empty holder lease; retain opaque saved padding |
| `WorldServerFrameHandler.SessionStartup.cs` StartWorldSessionAsync | Alternate internal handoff/new-role path runs starter/class sanitation/grants with saves, constructs full session, Set | Separate from ordinary existing-character plan. Must not receive ordinary authority automatically; initially remain unbound or receive its own proven preparation |
| `WorldServerFrameHandler.MailboxRecoveryStartup.cs` | Exact read-only recovery candidate loads saved spells and enters recovery holder | Mailbox-only; no ordinary native spell adoption |

The two startup callers are the internal handoff path and completed new-role path
in `WorldServerFrameHandler.cs`. The older starter/class-gate helpers are reached
through that StartWorldSessionAsync chain, not through ordinary normalization.

### Proposed smallest complete ordinary join

**Recommended activation boundary:** session-owned persisted membership plus a real
physical mirror. It is a cooperating-writer guarantee, not a universal concurrent
snapshot. A new native slot cannot become independently writable authority.

1. Store alone issues an ordinary binding at committed empty-holder admission.
   Bind exact Store, connection, holder NativeClientIdentity, account/UID, physical
   client/Player, actual constructor-bound SpellStorage and current spell image.
   Do not use callbacks, copied UID tokens or mailbox authority as issuance.
2. Before durable work, prepare a frozen private canonical PlayerSpellSnapshot,
   all required native row/map copies, and exact current-row/comparer witnesses.
   Keep native transient fields for existing entries. Required raw counts/IDs,
   representation and owner faults refuse before files, actor stores or frames.
   The final session record must contain that stable snapshot rather than the
   caller's mutable array/list. Return the actual final record to internal callers
   that need CAS/identity; do not keep using a discarded proposed record.
3. All real publication paths in the table consume that same participant. Ordered
   locks are package/domain scopes -> connection gate(s) -> client -> Player ->
   actual SpellGate. Prepare comparer-capable work before the final plain fence;
   no new callouts/allocations after durable success or between paired stores.
4. For Set/CAS's existing post-save behavior, call the outcome **memory committed,
   durability unproven** until a real spell-file transaction is adopted. A failed
   auto-save cannot roll back a committed map or allow duplicate grants. Durable
   callers must prepare before their writer; do not move preflight after it.
   If an actual durable writer succeeds but the final current-owner proof fails,
   retain/fence committed recovery work; never report unchanged rejection or
   discard the durable outcome as though no grant happened. The ordinary journal
   already has that classification. A generic bool persist result by itself does
   not prove crash recovery for every other producer.
5. Do not silently permit native reverse writes. Existing Add/Remove/Reborn/Clear/
   Update, Ninja prepared skill publication, native soul/XP commits and spell-file
   loading can alter ClientSpells without Store. Their current actor->SpellGate
   order cannot acquire Store afterward. For the first ordinary **read/mirror**
   milestone, refuse these unjoined typed writes for this genuine binding before
   actor locks; detached/unbound component fixtures retain their existing APIs.
   Alternatively implement their reverse Store transaction in the same batch.
   This is an explicit activation choice for Root, not an accepting fallback.
6. Managed publishers pin current Store holder + physical mirror + exact prepared
   membership before/after each await, reusing LegacySpellPublicationOwner's actual
   lifetime pattern. Retire on Remove/UID/owner/slot change. No native Socket or
   queue substitution is used. Preserve Action78/startup order.

Reverse-write sources requiring either refusal or integration are
`LegacyNativeSpellStorage.Mutation{,.Plan}.cs`, `.Preparation.Change.cs` consumed by
`LegacyNativeNinja.Loading.Operation.cs`, `.DirectWriters.cs` consumed by
`LegacyNativeMsgSpell.Handler.cs`, `LegacyNativeUpdateSpell.DirectCommit.cs` and
`Data/LegacyNativeServerDatabasePersistence.SpellLoad.cs`. Native file Save is a
durable writer too; it cannot bypass an ordinary binding's actual disk authority.
Public mutable ClientSpells/row fields and raw writers remain an explicit limit;
subsequent consuming preflight must detect divergence, not silently heal it.

## Expected wire behavior and fixtures

Root qualification must use real Store/connection/client/Player, constructor-bound
SpellStorage, actual saved rows and actual packet capture. No accepting persistence
or fake owner callbacks establish the join.

- Ordinary saved + normalized1110 membership agrees in canonical session and native
  rows after real journal commit, preserving raw padding and physical owner identity.
- Actual startup EquipmentQuery adds/replaces/removes21930/20000, then both graphs
  and unchanged transient fields agree; valid frames/order retain existing goldens.
- Every Store entrypoint's stale/malformed/foreign/throwing comparer preparation
  fails unchanged before its actual durable writer; paired preparation failure
  changes neither side. Prepared-mailbox returned membership change is refused
  before intent via a required producer invariant, not late rollback fiction.
- A stale unrelated record cannot restore an older spell list; equal modeled
  membership is carried without resetting cooldowns or row identity.
- Selected soul0..4 roundtrips; unsupported saved selection refuses ordinary
  adoption without changing files/Store/owners. Test null/duplicate/ID0/negativeXP
  and mutated caller lists through actual Store consumers.
- Retire/remove/recreate same UID invalidates publication. Test wrong physical
  Player/MySpells, pre-held inverse gates and genuine comparer reentry.
- Explicit native-bound write refusal or a real reverse transaction is tested
  through existing Add/Remove/Loading/soul/XP/file entrypoints; no dirty-prefix
  assertions. Tiny native buffers remain corrected-only under existing policy.
- Durable-before-Set dance/full-account paths and resource-consuming skill-book/
  rune paths need their own real prepared transaction controls before activation.

## Evidence alignment

| Requirement | Spec clause | Decoded catalog reference | Bounded wire fixture | Status/conflict |
| --- | --- | --- | --- | --- |
| Saved physical membership | Table + admission join | Actual12-byte snapshot schema | Existing spell codec | Source-supported; no owner implementation yet |
| Startup-derived coherence | Producer table | Actual equipment/rank rows | Existing managed frames | Concrete immediate one-time-hydration gap |
| Central authority | Publication table | Not applicable | Not applicable | All holder publication methods traced |
| Durable guarantees | Each publication/producer row | Real file/journal sources | Not applicable | Best-effort callback and custom earlier saves differ; do not conflate |
| Native reverse mutation | Join step5 | Native metadata remains required | Component tests only | Real unresolved authority/lock-order boundary |

## Implementation and qualification

No production postimages are authorized by this spec alone. Proposed first source
assignment is **one consumed batch**, not an orphan converter:

- New `World/LegacyNativeSpellStorage.SessionSnapshot.cs`: complete persisted-field
  projection/preparation on genuine rows; no slot/packet/disk writes.
- New `World/LegacyOrdinarySpellOwner.cs` and
  `World/WorldSessionStore.SpellPublication.cs`: Store-issued binding, exact prepared
  holder participant and ordered current-owner proof. No caller-supplied acceptance.
- New `World/LegacyOrdinaryAdmissionPlan.Spells.cs` plus actual Prepare/Plan
  integration: detached constructor/normalized rows prepared before journal and
  plain owner publication afterward.
- Existing Store core + its five publication partials in the table: consume the
  prepared participant everywhere. Recovery/CompleteLogin preserve their exact
  distinct lifecycles rather than issuing ordinary authority.
- Existing native typed entry guards, only after Root chooses the step5 boundary.
  Do not touch rank persistence or held Guild/Union sources.

Concrete participant API for that batch, proposed before source edits:

```text
WorldSessionStore (private nested types/methods)
  PreparedSessionSpellPublication PrepareSessionSpellPublication(
    connection, actual SessionHolder, expected current session, proposed session)
  PreparedSessionSpellPublication.FinalSession : WorldSession
  PreparedSessionSpellPublication.RequireCurrent() : void
  PreparedSessionSpellPublication.PublishNative() : void // plain stores only

LegacyNativeSpellStorage.SessionSnapshot.cs
  PrepareDetachedSessionSnapshot(actual client, frozen snapshot) -> actual owner/image
  PrepareCurrentSessionSnapshot(actual client/player, frozen snapshot) -> COW image
  prepared image.RequireCurrent() / PublishPrepared() // no sends/files/callbacks
```

The Store methods resolve actual holders themselves; a caller cannot supply or
construct one. `RequireCurrent` performs the final plain identity/scalar/row check
after all comparer-capable preparation. `PublishNative` has no independent commit
authority: only the actual Store publication path calls it after its specified
writer boundary. Fresh admission uses its real empty-holder lease and prepares
the detached image before the journal; later current-image updates require the
actual published binding. The canonical FinalSession is prepared before writer
entry and is the record actually returned/published by internal transaction APIs.

Before adopting this in the two save-before-Set producers, add a concrete
expected-session transaction entry under the same real Store gate so this
participant is prepared **before** the existing file writer. Do not pass a new
accepting callback as proof or leave those producers writing first. The existing
mailbox prepared producer must establish its no-spell-change invariant before
durable intent; a post-return mismatch is committed recovery work, not rollback.

Tests should be split into genuine ordinary-admission construction, all Store
publication methods with actual participants, startup EquipmentQuery, lifetime/
comparer failures, and chosen native reverse-write behavior. A standalone detached
converter with no consumed integration is not proposed as completed work.

Proposed isolated test postimages, under `tests/WolfConquer.Tests/`:

- `WorldServerFrameHandlerTests.OrdinarySpellCoherence.Admission.cs`: use the
  existing private OrdinaryLoginFixture partial; saved membership + actual1110,
  strict malformed/soul conversion, real journal interruption and no native slot
  publication before durable success.
- `WorldServerFrameHandlerTests.OrdinarySpellCoherence.EquipmentQuery.cs`: same
  genuine fixture/Store/managed handler;21930/20000 changes, persisted fields,
  transient native values and existing independent valid spell/removal bytes.
- `WorldSessionStoreSpellCoherenceTests.Publication.cs`: actual Store single/CAS/
  pair/prepared/complete/retire paths; shared fixture uses real participants rather
  than accepting savers. Verify both sides before diagnostic assertions.
- `WorldSessionStoreSpellCoherenceTests.Ownership.cs`: current holder retirement,
  stale full-record overwrites, mutable lists, actual comparer/reentry and no
  post-durable preparation/callbacks.
- `WorldServerFrameHandlerTests.OrdinarySpellCoherence.NativeBoundary.cs`: chosen
  real native typed-entry contract under the issued ordinary binding, plus valid
  unbound existing-component controls. No old undersized native-buffer runs.

The two pre-save producers need focused existing-handler regressions in their
own test partials when included: actual failed native/map preflight must leave
their owned Character/spell files unchanged. Skill-book/rune controls retain
resource witnesses and must not certify their unmodified outer persistence.

Before source changes, Root must settle: read/mirror-only native guard versus real
reverse transactions, and which managed persistence changes are included in the
same activation batch. Canonical session return values must be consumed by their
actual callers rather than treating void Set as returning the proposed object.
These are finite integration decisions, not missing providers.
No result here proves SQL, complete spells/casts, client acceptance, deployment,
crash safety or whole cross-reader atomicity.
