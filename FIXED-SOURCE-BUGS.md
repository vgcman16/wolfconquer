# Source bug fixes and audit

Engineering policy: [Sanitized Parity](docs/engineering/SANITIZED-PARITY.md).
Started 2026-10-23. **Open items below are not fixed or verified claims.**
Earlier passing tests may encode defects; test totals are not a safety certificate.

## Verified fixes

### FSB-001: Reject spell admissions before spending resources

- Owners: `LegacyNativeCanUseSpell` and the typed physical admission commit.
  Old validation consumed weapon authorization/soul, spent stamina and sometimes
  mana, or exposed output rows before a later rejection.
- Corrected invariant: read-only lookup/range/resource/cooldown validation and
  complete real 2176 packet preparation precede a client/player-serialized commit.
  False means null outputs and unchanged resources, authorization, soul, cooldown
  and queued success packets. Zero mana skips Rune; only a matching sigil is read.
- Metadata identity and level must agree, except the genuine 6009-to-6010 database
  alias. Negative cooldowns reject; rune/sigil reductions cannot create negative
  costs or backdated deadlines. Missing, disposed, undersized or aliased update
  rentals reject before commitment. Notification failure cannot undo acceptance.
- Verification: both private and independent public Release passed **510/510**,
  zero failures/skips, including 87 direct admission cases, 20 allocation cases,
  retained resource wire goldens, concurrency controls and 123 dispatch controls.
  A stale level-two test fixture was corrected to populate its actual metadata
  level; existing expected damage/packet assertions were retained.
- Limits: this fixes admission, not all executor-specific eligibility/effect
  transactions. Other resource/lifecycle writers, delivery recovery and durable
  replay/idempotency remain unfinished. No client, original save or SQL was used.

### FSB-011: Bound unmanaged packet constructor allocations

- Owner: `LegacyNativePacket` constructors. The source copied arbitrary byte
  arrays into a fixed 1,024-byte allocation and checked stack depth after allocating.
- Corrected invariant: validate first, allocate at least the full byte-array size,
  expose immutable truthful Capacity, and let admission check capacity before
  unsafe serialization. Existing small-buffer bytes and cursor conventions remain.
- Verification: 20 allocation cases within the **510/510** Release selections in
  both trees cover byte copies through 16,384 bytes, capacity, null/nonpositive
  inputs, retained deep-stack rejection and disposal. The unsafe oversized
  constructor baseline was not executed.
- Limits: general unsafe readers/writers still need a full bounds/lifetime audit;
  the retained stack guard is not a measured unmanaged-allocation counter.

### FSB-002: Require a real Hairface storage owner

- Source/local owner: `LegacyNativeHairfaceStorageRuntime` constructor; the old
  implementation accepted null and `Add` later faulted after publishing an item.
- Corrected invariant: reject null immediately with `ArgumentNullException(client)`;
  retain the actual client as a readonly owner. Tests use real native client owners.
- Verification: Release focused Hairface storage/login and character-read tests:
  **173 passed, 0 failed, 0 skipped** (2026-10-23, local offline run).
- Limits: does not yet fix loader partial publication, exposed mutable rows,
  foreign-owner login reuse, equipment validation or all mutation/send failures.
  No live client, original save or SQL was used. Commit is recorded in Git history.

### FSB-015: Safe single-pass ranking and rune-slot queries

- Owners: CombatGearRank, HWRank, NinjaRank, PrestigeRanking query methods and
  Rune.Free. These are eight observed CA1827 findings across five source files.
- Corrected invariant: preserve physical UID/value order and returned Entry aliases
  with one current-owner scan under existing writer gates. Missing buckets and
  invalid rows no longer crash the affected queries. A corrupt rune cache fails
  closed; a wrapped byte Count cannot reopen an occupied position-211 bag.
- Verification: private and independent public Release each passed **754/754**,
  zero failures/skips, including 24 new controls and the retained 510 selection.
  Three directly affected old crash assertions were replaced by safe outcomes.
- Limits: no benchmark or general concurrent catalog/lifecycle proof. Public
  mutable arrays/rows, Rune.Count/Add, BestOf and other writers still need audit.

### FSB-014: Persist active title changes before publishing session or packets

- Owner: registered opcode2313 `WorldPacketHandlers.TitleStorage`. The old route
  changed memory and sent success even when saving failed, treated wing4001 as a
  normal title on removal and echoed untrusted subtype metadata.
- Corrected invariant: validate the complete owned-title list and authoritative
  representable catalog identity; prepare real acknowledgement/UseTitle/spawn
  bytes; commit the four title fields through the genuine current-session gate
  and file coordinator before publishing the updated session. Canonical numeric
  fields are identical in memory and disk, including initially absent zeros.
- A bounded shared snapshot reader proves the expected UID/title baseline and
  validates the written title/non-title Character fields. A failed replacement
  is ordinary rejection only when the complete preimage is unchanged; otherwise
  the genuine recovery owner is blocked. The PIN reader now reuses this bounded
  reader without changing its exact-value/reason-prefix contract.
- Prepared owner and peer delivery revalidates captured native identity and exact
  session references. Changed/retired recipients do not receive stale packets.
  The real @@ catalog columns preserve spaced-name title2149 score150.
- Verification: private and independent public Release each passed **1008/1008**,
  zero failures/skips, including 80 active-route guards, 55 persistence/baseline
  controls, retained PIN/title goldens and the prior754 selection.
- Limits: offline owned files only; no live client or SQL. Real sharing-failure
  and postcondition/classifier controls do not prove every OS rollback fault or
  arbitrary transport race. The frame-admission fence does not prove every
  background writer honors it. It is not a durable title journal,
  all-crash recovery, multi-file atomicity or hostile filesystem-race immunity.
  Classic opcode2152 has its separate FSB-017 correction below; native title
  lifecycle and durable recovery remain separate unfinished work.

### FSB-016: Retain real read-only process image attribution

- Owner: `ConfiguredExecutableProcessGuard`. CI39926199302 failed closed when
  MainModule returned null; that API can be transient during process startup.
  The historical TRX does not prove the exact process or startup/exit timing.
- Corrected invariant: matching names enter one retained noninheritable Windows
  handle with limited-query and synchronization rights only. Query the image
  directly; ignore image-query failure only when that same handle proves exit.
  Missing handles, unknown/live-uninspectable state and protected exact paths
  still refuse. No preliminary PID-reopening HasExited escape remains.
- Both generic and layout never-launched pipeline fixtures now use independent
  per-root signed/configured/protected executable names. Intentional real running
  exact-path, attributable namesake and legacy-shared-name controls remain.
- Verification: the complete updater suite passed **301/301** in private and
  independent public Release, zero failures/skips. Includes25 new controls and
  14 quality-parser controls, actual Win32 image reads and retained owned-command
  lifecycle tests. Fixture cleanup boundaries were strengthened, not relaxed.
- Limits: a point-in-time read-only scan is not prevention of later process
  startup, filesystem-alias resolution, process birth identity before opening
  the handle, deployment or a claim that every possible race was reproduced.

### FSB-017: Persist classic title selection before acknowledging it

- Owner: registered managed opcode2152 List/Change. Replace stale-context Set
  with the genuine current-session commit and store-issued native identity.
  Validate the complete owned-byte list, prepare replies, persist MyTitle only,
  then publish the session and lifecycle-bound acknowledgment. Failed writes
  require exact unchanged preimage evidence; unknown outcomes block recovery.
- Compatibility: retain fixed2152 bytes/order and valid clear0. Initial file
  admission compares the exact IniDocument-trimmed session projection; after
  writing, raw ownership, complete lines and encoding must still match. Without
  a load-time raw token, whitespace-only historical edits are not detectable.
- Local evidence: all68 new classic controls and retained11 codec/handler cases
  passed within private and independent public Release2164/2164 selections.
  Clean committed-tree CI is pending below. Tests use genuine temporary files and commit owners,
  not accepting saver callbacks. The first fixture failed strict parsing and was
  corrected to provide mandatory fields; the parser was not weakened.
- Limits: no durable crash journal, hostile filesystem/external-writer proof,
  complete native title granting/login/save lifecycle or new live-client proof.

### FSB-018: Admit complete weapon slots and prepare bounded responses first

- Owners: native HundredWeapons slot queries and complete Equip/Unequip/Swap/
  Show/Hide methods. Reject corrupt tails, duplicate physical rows/appearances,
  aliased bitmaps/word arrays and overlapping occupied bits before mutation.
  Check the genuine current client/player/weapon owner, not matching IDs alone.
- Prepare the real2353 response before changing bits. Capture the seal once and
  construct complete private bytes before renting/checking actual capacity;
  mutable global seal activation cannot grow a later unsafe Finalize write.
  Transport or cleanup failure after commitment cannot turn it into a refusal.
- Local evidence: complete Hundred families, all12 stationary seal bounds and
  all14 captured-seal controls passed within private/public2164/2164. Prior-frame
  references AND cloned bytes are checked; existing protobuf goldens remain.
- Limits: whole2353/2354 multi-step receivers remain uninstalled/held. Add/remove,
  ranking/loading and raw public writers are not made globally transactional.

### FSB-019: Reject corrupt or foreign equipped-rune ownership safely

- Owners: WornObjects, named effects and equipped-slot/count queries. Require
  constructor/current client binding, genuine Player/equipment ownership,
  complete authoritative/cache membership, nonzero unique UIDs and unique
  equipped physical positions. Inactive0/211/212 may legitimately repeat.
- Missing/null/wrong-ID metadata grants no named effect and cannot crash the caller;
  valid other runes still work. Keyword matching is ordinal-ignore-case, not
  host-culture dependent. Invalid queries clear out/ref results. Equipped count
  does not wrap. Old tests demanding crashes, foreign-owner effects or duplicate
  slot bonuses were replaced with refusal/no-repair controls and valid fixtures.
- Local evidence: full Rune/query/perfection/XP/related admission families and
  new ownership, metadata, culture, cache/slot controls passed within2164/2164 in both trees.
  The small AddFlag null-Rune guard only prevents that dereference/stale bonus.
- Limits: full AddFlag transaction, Rune Add/Count/Score, family-to-slot policy,
  raw-writer races, inventory transfers and persistence remain separate work.

### FSB-020: Stage bounded shop refresh, counters and saved-book loading

- Owner: native ExchangeShop component. Complete book/catalog validation and
  finite uniform ordered sampling replace the unbounded rejection loop and
  prefix removal. Refresh/Reset, checked counters and strict append loading
  publish one prepared dictionary under the cooperating method gate; escaped
  previous rows/books remain unchanged. Valid zero-count retirement is retained.
- All cooperating writers/save enforce explicit4096-row/1MiB-text engineering
  limits; these are not client protocol constants. Open requires the genuine
  current shop/player, prepares complete2317 bytes and checks real allocation
  and native send bounds before output. True means publication attempted, never
  delivered; false precedes caller-buffer writes.
- Local evidence: full shop family including143 authored refreshed/new controls
  passed in private/public2164/2164. Real comparer materialization faults preserve the
  old book; sealed wire goldens and prior-frame bytes remain covered.
- Limits: sampling distribution/order is preserved, not the old PRNG draw trace.
  No PRNG rollback, whole paid refresh/purchase receiver, wallet/item grant,
  SQL/file crash recovery or globally synchronized raw fields is claimed.

### FSB-021: Count down to the actual local daily boundary

- Owner: ExchangeShop.GetTime. Original arithmetic targeted midnight+60seconds
  using three Now samples and large doubles, while Server.Reset follows the
  local DayOfYear boundary. Use one Now sample and bounded integer ceiling of
  remaining day ticks:1..86400 seconds. Keep UInt32 protobuf field4/opcode2317.
- Local evidence:26 deterministic literal calendar/subsecond/kind/extrema cases
  and retained real-clock/wire/shop tests passed in private/public2164/2164. Old tests
  requiring the extra minute were corrected, not retained as gameplay rules.
- Limits: calendar-wall-time countdown, not monotonic/DST elapsed-time accuracy,
  scheduler completion or an atomic daily-reward/reset transaction.

### FSB-022: Validate every character in the existing name repertoire

- Owner: NameStrCheck, used by the genuine Clan creation query. The source regex
  examined only the final character, accepting invalid interiors such as A B/A@B.
  Remove only its trailing anchor: retain exact ASCII/Arabic/CJK ranges, allowed
  punctuation, case-sensitive reserved tokens, UTF16 size bound and size opt-out.
- Verification: private/public Release2596/2596,0fail/0skip includes40 new
  controls, all262144 bounded UTF16-unit/position observations, existing name/API
  controls and the full Clan family. Old invalid-interior acceptance assertions
  now require refusal. Valid names and real Clan dictionaries/counter stay intact.
- Limits: no stored-name migration/deletion, normalized uniqueness, rename-queue
  validation or admin-authority claim. Those portions of FSB022 remain open.

### FSB-025: Widen inclusive RNG range arithmetic before taking a draw

- Owner: RandomLite bounded Next overloads. Full signed width2^32 previously
  overflowed to zero and threw after advancing the RNG. Compute width and final
  offset in Int64; retain guards, constant-range no-draw behavior and the genuine
  unbounded generator. Current Chi type/stat bands keep their seeded sequences.
- Verification: private/public Release2646/2646,0fail/0skip, including44 complete
  RNG controls (31 new), six seeds, signed width boundaries through2^32 and an
  independent BigInteger mapping. The old crash-enforcing test now requires the
  literal corrected seed97 result -1874220851 and next draw512644276.
- Limits: bounds safety only. The existing31-bit domain cannot cover a full-width
  interval and modulo remains biased. Uniform/full-domain sampling, fairness,
  concurrency and cryptographic security are not fixed or certified.

### FSB-023: Prepare real flag state and packets before commitment

- Owners: Player AddFlag/AddSpellFlag and the physical status-vector family.
  Validate the complete real dictionary/bitmap/row graph, current client/View,
  bounds and timer arithmetic before changes. Prepare COW state and genuine
  bounded2176/2215 frames; duplicates cannot reset XP or consult optional Rune.
- Opaque one-use plans keep staged mutable arrays private. Final typed commitment
  checks player/client/vector/View/UID AFTER comparer-dependent validation.
  Defensive cache arrays are not authority for death-clear. Invalid membership
  is explicit unavailability, never ordinary absence granting permission.
- Verification: Private/public Release1931/1931 each,0failed/0skipped.
  Independent native status/timed goldens, all XP flag branches, real pool/seal/
  alias faults, cache/timer/index/plan replay and late-owner controls retained.
- Limits: cooperating gates, not tuple hardware atomicity or arbitrary raw-writer
  exclusion. Remove/Clear/HP/death/XP workers, full cast/economy transactions,
  nonplayer postcommit sends, durable recovery and host adoption remain open.

### FSB-026: Potion rejection must not grant bonuses or leave dirty timers

- Original Player3145..3158 enabled a potion before date/flag/message preparation
  could fail and always claimed30 minutes. Both real methods now prepare their
  positive duration, flag state and bounded genuine SYSTEM talk before commitment.
  Message text reports the supplied duration; existing valid flag rows stay intact.
- Verification: Private/public Release1931/1931 each,0failed/0skipped.
  Includes missing/null/small/disposed/aliased real rentals, malformed state,
  no-output/no-bonus rejection, largest positive int duration, genuine full codec
  comparison and final-commit ownership changes. Old dirty-prefix tests corrected.
- Limits: no item-consumption caller is installed; expiry workers, raw races,
  durable potion state and delivery acknowledgement remain unproven. A delivery
  failure after commitment does not undo accepted potion state.

### FSB-027: Checked current-owner XP-duration adjustment

- Original IncreaseXPDuration reselected mutable state and added unchecked into
  an escaped row. Capture one full genuine graph, select the existing14-priority/
  Normal policy once, check signed sum/deadline, recapture after comparer work
  and verify exact owner/UID/vector before the single duration assignment.
- Verification: Private/public Release1931/1931 each,0failed/0skipped.
  Retains valid signed reductions/row/timer identity and adds44 boundary/ownership/
  malformed/replay controls. The old successful overflow expectation is replaced.
- Limits: full receive/XP opening/counter-goal/death/worker transactions remain
  separate; raw escaped rows and concurrent writers are not made globally atomic.

### FSB-028: Physical damage cannot dirty outputs or shared spell data on admission

- Whole OnMonster now validates real current status and all transitive query
  bounds before out assignment, RNG, procs or calculation. The floor branch
  retains its genuine player/status-free Damage2 behavior.
- The effective spell minimum10 is local; never write a caller's shared Magic
  catalog row. Valid float/NaN/infinity, gear branches, damage/RNG and2319 wire
  controls are retained; two players can reuse the same unchanged catalog row.
- Verification: Private/public Release1931/1931 each,0failed/0skipped.
  Includes44 new physical/call-site controls plus corrected old mutation assertions.
- Limits: admission-time protection and catalog integrity, not a whole cast/HP/
  death/resource transaction. Later owner faults and raw/lifecycle races stay open.

### FSB-029: Refused status is not a target, partial ranking or success animation

- AI stages target selection and clears stale Target/sets Idle when required flight
  membership is unavailable, before movement/stamps/RNG. Rank appearance resolves
  full flag priority before copying any Entry field. Existing successful order stays.
- Known-absent Shield/Stigma must pass real AddFlag before success animation.
  Sage distinguishes valid duplicates from refused grants, checks current bindings
  after membership and disposes the genuine archive rental synchronously.
- Verification: Private/public Release1931/1931 each,0failed/0skipped.
  Real malformed-tail callers, prior frames/buffers and existing valid packet/
  appearance/selection goldens remain; corrupt fixtures are not silently repaired.
- Limits: full rank publication, map enumeration/AI movement,
  complete refinement procs/casts/host recovery remain open. Prepared Sage and
  Shield/Stigma successors are recorded separately below.

### FSB-030: Commit required XP goals and load complete goal records safely

- Original Player.XPCount assigned20 before required HeroRewards goal103 could
  fail; the native goal loader also published valid prefixes/default-zero rows.
  Stage a complete bounded goal book, preserve valid row/claim aliases, revalidate
  actual current owners and commit the required goal with the counter under the
  genuine client/player/goal/book gates. Other XP values do not read goal owners.
- Reject malformed counted records before merge; preserve valid order/duplicate
  rules, full uint IDs/byte claims and genuine before-Player constructor timing.
  Widen stage-count arithmetic so uint.MaxValue cannot wrap into a free completion.
- Verification: private/public Release **2392/2392** each,0failed/0skipped;
  includes the186-case lifecycle/goal selection and70 new real-owner controls.
- Limits: in-process cooperating publication, not durable reward claims, SQL,
  complete XP/death workers, arbitrary raw writers or host adoption. Active reward
  grant safety still needs the real shared item-UID authority described in FSB-009.

### FSB-031: Prepare complete Sage status/archive before committing its flag

- Original Ninja.SageMode added its flag before archive preparation could fail.
  Prepare both genuine2176 frames and distinct bounded real loans first. Typed
  commitment binds the exact current capture, opaque token, staged words, Sage273
  policy and full uint archive timer. Valid duplicates retain their prior row.
- Final checks reject retired owners, disposed loans and changed Size/cursor/
  bytes. Failed delivery after commitment is not an uncommitted rejection.
- Verification: private/public Release **2392/2392** each,0failed/0skipped,
  including104 Sage controls, independent wire/seal/full-uint goldens,12 real
  final-buffer drift cases and17 mismatched-plan cases.
- Limits: no complete cast/host/durable replay or delivery acknowledgement;
  raw asynchronous disposal/field changes after final checks remain separate.

### FSB-032: Prepare Shield/Stigma animations before status acceptance

- Original effects could grant status before animation lookup/serialization or
  caller-buffer validation failed. Prepare the complete genuine animation and
  status frames, distinct real loans and caller preimage before opaque typed
  acceptance. New Stigma90/MagicShield40 and duplicate empty-target behavior stay.
- Successful operations retain caller2239 bytes/Size/cursor4 and2176→2239 order.
  Rejection preserves complete caller/state/XP/prior queues; accepted transport
  loss never refunds the grant or creates a retryable false result.
- Verification: private/public Release **2392/2392** each,0failed/0skipped;
  109 new cases plus retained valid effect and full generic animation goldens.
- Limits: these effects, not resource admission/full casts/HP/death, durable
  persistence, arbitrary raw races or per-recipient delivery transactions.

### FSB-033: Revalidate physical state and prepared loans after comparer work

- The sanitized layer still checked some data before its last real dictionary
  comparer call. A normal-returning comparer could then change an earlier status
  row/bitmap or a retained packet's Size, allowing stale authority or native send
  overread. Pure full-graph checks now follow final lookups; membership answers
  must agree with literal captured keys. Timer permission/renewal is guarded too.
- AddFlag/AddSpellFlag and potion typed commits also validate captured allocation,
  actual capacity, exact prepared Size/cursor/bytes after comparer work. Duplicate
  potions validate their message after the final query. Existing valid packets stay.
- Verification: private/public Release **2392/2392** each,0failed/0skipped,
  including118 final-state and54 ordinary flag/potion loan-drift controls.
  The unsafe unmanaged overread baseline was not executed.
- Limits: scoped genuine component operations, not all native packet IO, hostile
  comparers mutating new staged books, raw concurrent lifetime/ABA or full host
  recovery. General reader/writer audit FSB-012 remains open.

### FSB-034: Publish complete owned rune storage without count wrap or cache escape

- Original Rune.Add inserted before allocating its cache; null/UID/cache faults
  and escaped array edits could leave contradictory owners. Count wrapped at256.
  Stage the genuine comparer-preserving dictionary/cache, validate complete row
  identity and current assigned client/Player/UID, then publish both references.
- Count is exact int (no native production/wire consumer); Objects returns fresh
  defensive arrays, including empty arrays, retaining valid item aliases/order.
  Malformed raw queries are explicitly unavailable; they cannot grant free slots.
  Equipped readers join the same cooperating gate order and whole-graph checks.
- Verification: private/public Release **2896/2896** each,0failed/0skipped;
  includes56 new storage controls, retained source-valid calculations/position
  policies,32 concurrent Adds and broader item-loading/save consumers. The first
  local run exposed an empty-array reuse regression; implementation was fixed,
  keeping the original valid constructor assertion unchanged.
- Limits: no new Equip/Unequip/economy/SQL/host path, full loader transaction,
  global item-UID ownership or arbitrary raw-writer isolation. The unsafe native
  save truncation/partial-write owner is being replaced separately, not certified.

### FSB-035: Prepare complete refinement procs before status, chance or stamina effects

- Owners: native MirrorOfSin, KillingFlash and LightOfStamina. Old methods could
  emit a success refinement before invalid status was discovered, consume chance
  before known preparation failure, or change stamina before its scalar existed.
- Complete genuine captured status/owner state and bounded native2319/2176
  refinement/status/scalar frames now precede acceptance. XPList remains20seconds
  without XPCount reset; Light still restores only values below100. Successful
  native packet order, predicate priority and one genuine eligible Killing draw
  are retained. Invalid state cannot contribute a grant, stamina change or prefix.
- The eligible Killing loss now borrows three potential-success packets rather
  than one: an explicit bounded-allocation tradeoff, not a speedup. Post-draw raw
  mutation may still refuse; no RNG rollback is claimed. Accepted transport loss
  does not turn a committed grant into a retryable rejection.
- Verification: private/public Release **3065/3065** each,0failed/0skipped, including116 new
  controls, retained proc-rate/wire/visible-peer goldens and the corrected old
  test that required a refinement prefix before a NullReferenceException.
  The selection also includes53 genuine owned-file UID registry controls.
  FullWindows39958461749 on exact256d7a18 passed: core23176/0failed (165excluded),
  updater301/301, launcher49/49; this is offline proof, not host activation.
- Limits: no general CheckEffects activation, full HP/death/cast transaction,
  raw-writer isolation, live client or packet delivery atomicity. Other refinement
  procs and host recovery remain unfinished.

### FSB-037: Prepare a complete native item image before atomic destination replacement

- Owner: complete native SaveClientItems. Original FileMode.Create truncated
  before row enumeration/conversion, ignored failed writes and could leak its
  open handle after a later owner/row failure. Nested deposits were silently lost.
- Capture the real current client/Player/UID/configured root and complete owned
  containers/rows. Prepare checked184-byte item and80-byte perfection image first;
  stage in the same directory, flush/close/verify, recheck the exact baseline and
  owners under the genuine file coordinator, then Replace or no-overwrite Move.
- Distinct duplicate UIDs and unrelated repeated physical owners reject. Only
  the source-proven exact wardrobe/equipment indexes of one physical row emit
  once. Reject unsupported nested deposits instead of discarding grandchildren.
  Preserve native flat ordering, ownerless deposited-perfection predicate and
  independent successful bytes. The obsolete unsafe SaveOpenedFile is retired.
- Ordinary rejection requires unchanged destination proof; unknown replace
  outcome fences that canonical path in this owner. The void entry cannot
  silently acknowledge a failed save. Own validation InvalidDataException is
  explicitly classified; it does not derive from IOException.
- Verification: private/public Release **3116/3116** each,0failed/0skipped,
  including51 new save/text controls, genuine Windows replace-sharing denial,
  late shared-file-gate retirement, retained native ABI/reader/roundtrip goldens.
  The initial local run exposed the validation-exception omission; fixed in
  production without weakening rejection/no-prefix assertions.
  FullWindows39959714814 on exact01431fe3 passes: core23227/0failed (165excluded),
  updater301/301, launcher49/49, all8stageexit0. This remains offline component proof.
- Limits: one-file component, process-local fence only. Unknown post-replace
  fault classification is source-reviewed, not a fabricated runtime proof.
  Full loader/host/SQL/multi-file recovery, raw writers, hostile path ABA and
  power-loss/directory durability remain open. No original save/client was used.

### FSB-038: Bound native fixed32 text and reject null records explicitly

- Owner: native item perfection record. Unterminated getters could read beyond
  their buffers; oversized setters overran them and shorter text kept stale tails.
- Getters stop within32 bytes. Setters reject null/oversize before any write,
  accept full32, retain genuine low-eight-bit/Windows ANSI behavior and clear
  unused field bytes. No UTF8 guess, truncation or struct-layout change.
  Factory/ApplyTo reject null arguments explicitly before stores.
- Verification: all34 perfection-record cases pass within both **3116/3116**
  selections, including13 new boundary/field-isolation cases. Valid80-byte
  layout/offset/raw/header/ANSI/factory assertions remain; unsafe tail/NRE
  expectations and the dependent null-recipient assertion are corrected.
- Limits: whole reader/postlude/load transaction remains separate audit work;
  bounded accessors alone do not authorize an item or make partial loads safe.

### FSB-039: Prepare selected DivineGuard before cooldown or success publication

- Owner: complete native selected DivineGuard proc. The source reset its stamp
  and sent refinement11 before fallible status/view work; an old test required
  that dirty prefix. Invalid preflight now preserves stamp/status/resources,
  consumes no chance draw and sends no success frames.
- Capture the genuine client/player/vector/map/view and prepare all bounded,
  disjoint real frames before the genuine absent-flag5% draw. Final pure owner,
  status and loan checks precede plain commitment. Preserve flag177/15seconds,
  selected-trigger now+10minute stamp even on loss/present, and exact owner
  refine/status/Azure versus peer refine/status order. Present skips RNG.
- Verification: private/public Release **3214/3214** each,0failed/0skipped,
  including98new controls and14 retained ProcRates cases. Seeded win/loss,
  malformed owners/status, actual loan corruption/disposal, stale state,
  token replay, independent wire goldens and accepted send failure are checked.
  The previously deferred testconstant parentheses joins this behavior batch.
  FullWindows39962905914 on exactaf3ca964 passes: core23325/0failed
  (165excluded), updater301/301, launcher49/49, all8stageexit0.
- Limits: no full outer CheckEffects authorization, combat/HP/Team/revive or
  host activation. Eligible loss prepares three potential-success loans; this
  is a safety tradeoff, not a speed claim. No RNG rollback, raw-writer global
  atomicity or confirmed network delivery is claimed. Offline only.

### FSB-040: Stage complete native quest files and canonical indexes

- Owner: complete RoleQuests Save/Load. The source truncated before validating,
  ignored short I/O, leaked load handles and published partial/default/split-alias
  rows. Unsafe opened-file entry points are retired after migrating every caller.
- Save captures the genuine constructor-bound book/player/UID/absolute root,
  validates complete physical rows and Accepted subset, prepares bounded native
  bytes and uses real same-directory flushed atomic replacement with baseline
  checks. Unproven write outcomes fence this path locally; void wrappers cannot
  silently acknowledge rejected saves. No new quest owner is fabricated.
- Load validates the entire counted image and tail before detached merge. Keep
  nonconflicting rows and exact identical overlaps as canonical physical aliases;
  reject duplicate/conflicting UIDs and invalid indexes without publication.
  Preserve raw status bits, native enumeration, UID0 rows and more-than-five
  accepted persistence; no new gameplay cap. Validate the merged64MiB bound and
  physical indexes independently of caller comparers before reference commitment.
- Verification: private/public Release **3465/3465** each,0failed/0skipped.
  Retained byte goldens, first-save/reload, real Windows sharing denial, released
  handles, truncation/tail/count faults, changing-hash/collapsing comparers, owner
  drift and oversized file/merged-book controls pass. The first run exposed
  DirectoryInfo's missing sentinel; production now reads actual attributes and
  reports genuine missing directories as NotFound without hiding access errors.
  FullWindows39965399561 on exact0b0da909 passes: core23360/0failed
  (165excluded), updater301/301, launcher49/49,8stageexit0.
- Limits: offline one-file component and cooperating gates, not host/login/SQL,
  all quest/objective/award transactions, multifile/crash recovery or atomic raw
  observation of two public maps. Raw writers/path ABA remain open. Unknown
  post-replace classification is source-reviewed, not invented runtime proof.

### FSB-041: Debit only the admitted bound type of rune fragments

- Owners: direct six-fragment UseItem and NPC RuneFragments combine. Native
  Inventory.Contain checks ItemId+Bound, but Remove loses the binding predicate;
  the replacement inherited that mismatch and one test required the wrong debit.
- Both debit paths now select exactly the binding admitted by their count. The
  direct path retains per-unit updates; the NPC retains per-stack batch order
  and uses the selected row's staged auto-binding. Opposite-bound stock stays
  untouched; nineteen matching fragments cannot borrow a twentieth from it.
- Verification: private Release **236 passed/0 failed/0 skipped**; independent
  sanitized public **229 passed/0 failed/5 existing external-fixture skips** of234.
  All12 new controls pass in both trees. Registered direct ingress and real NPC
  public-owner fixtures check complete item images, disk reload, refusal identity,
  unchanged original rows, exact20-fragment cost and retained reward/wire order.
  The corrected old assertion genuinely failed before the production change.
- Full [Windows39970166492](https://github.com/vgcman16/wolfconquer/actions/runs/39970166492)
  passes4m44s on exact27e2ae9b: core23372/0failed (165externalfixtureexcluded),
  updater301/301, launcher49/49,8stageexit0,204build warnings. CodeFactor1NEW
  duplication/0FIXED,total2294; defer that cleanup to a coherent behavior batch.
- Limits: matching debit selection only, not whole reward/persistence atomicity,
  globally durable UID allocation, registered NPC ingress or live-client proof.
  The12 controls are additional to retained old cases; no balance values changed.

### FSB-042: Prepare complete quest counters and caller frames before mutation

- Owners: all three native Set/Increase objective operations. The source changed
  live arrays before unsafe serialization, wrapped unsigned counters and trusted
  split source/Accepted aliases; older tests explicitly accepted these defects.
- Require the genuine current constructor client/player/book and cooperating
  gates. Capture the complete canonical physical graph independently of custom
  comparer callbacks; reject shared nonempty mutable arrays, permit harmless
  shared empty arrays. Copy input and retained values, check every addition and
  prepare complete2136 bytes plus actual caller allocation/preimage before writes.
  Commit one canonical row array and the bounded caller image, then publish.
- Verification: private/public Release **292/292** each,0failed/0skipped.
  Includes70 new cases and19 retained/repaired objective cases, plus neighboring
  native item/quest/persistence controls. Exact native UID35024/35007 tails,
  stable ASCII/multibyte seals,1032-byte send/actual allocation boundaries, late
  overflow, null/disposed callers, input alias isolation and full-book faults pass.
  Legitimate missing/finished/inactive no-ops and nonshrinking tails remain.
- Full [Windows39971090546](https://github.com/vgcman16/wolfconquer/actions/runs/39971090546)
  passes4m50s on exact8e5ff255: core23442/0failed (165externalfixtureexcluded),
  updater301/301, launcher49/49,8stageexit0,204build warnings. CodeFactor4NEW
  (3complexity+1precedence)/1FIXED,total2297. The precedence-only parentheses fix
  is batched with the shop-capacity change; complex validation stays unchanged.
- Limits: actual native objective callers are not installed; this is component
  proof, not login/host/client activation or full quest award/save transactions.
  Raw public graph/packet writers, disposal races and delivery recovery remain
  open. A failed send after commitment is not an uncommitted refusal or refund.

### FSB-043: Refuse modeled shop purchases that cannot fit in inventory

- Owners: normal BuyItem and Race/Honor/Champion point shops. Current routes
  appended new rows and debited wallets without required capacity admission.
- A shop-only bounded check admits the complete modeled row count before costs,
  session, packets or persistence: normal uses its normalized full count;
  point shops require one row. Reject full40/already-overcap41 bags and do not
  truncate a requested normal purchase. Shared creators/returned awards are not capped.
- Verification: genuine registered-handler baseline **18 failed/15 passed** of33
  reproduced spending/changed sessions on overcapacity. After the four guards,
  private Release **334 passed/0 failed/0 skipped**; independent public
  **325 passed/0 failed/9 existing external-fixture skips** of334. All33 new
  controls pass; old shop checks and292 quest/component controls are retained.
  Verify exact wallets, untouched refusal files/rows, complete saved/reloaded
  positive images and packet order across gold/CP/boundCP/Honor/Race/Champion.
- One observed CodeFactor precedence finding is fixed mechanically in the
  objective length formula and checked by the retained292 cases in this batch.
- Full [Windows39972176836](https://github.com/vgcman16/wolfconquer/actions/runs/39972176836)
  passes4m17s on exacta92b1fb0: core23475/0failed (165externalfixtureexcluded),
  updater301/301, launcher49/49,8stageexit0,205build warnings. CodeFactor2NEW
  complexity/1FIXED (the observed precedence),total2298. The new xUnit2017
  assertion-style warning is corrected with the FSB044 behavior batch, not its own CI.
- Limits: current normal owner remains all-or-none/one-row-per-unit; original
  stacking/partial affordability, plus/activation/quest hooks and full wallet/
  file/session transactions remain open. No global UID/host/client proof.

### FSB-044: Prepare canonical quest acceptance before publication

- Owners: native `Accept` and `AcceptKingDomMission`. The reference modifies
  shared quest metadata for1469/409, publishes ordinary indexes separately, and
  constructs/sends the packet after those writes. A diagnostic failure can also
  escape a successful acceptance and invite an incorrect retry.
- Validate the genuine current client/player/book and complete canonical graph;
  use local effective intention counts without changing the catalog. Bound the
  projected persisted image before allocation. Prepare the real2120 packet and
  comparer-preserving private maps, then recheck physical state after the final
  comparer before publishing. Ordinary maps share one canonical row; Kingdom
  retains its source-only policy and existing finished/out semantics.
- Only exceptions escaping the real postcommit Send are contained. Accepted
  maps/out remain committed and return true; precommit errors remain errors.
  No rollback, retry, fabricated successful delivery or recursive logging occurs.
- Verification: private and independent public Release each **418/418 passed**,
  zero failures/skips. This includes75 new acceptance cases, two genuine Windows
  child-process delivery faults, and341 retained quest/packet/console/shop cases.
  Tests-first unguarded delivery baseline failed **2/2** specifically on the
  escaped TypeInitializationException after all committed-state assertions passed.
  Child OS handles are restored; cached real console initialization failure is
  proved afterward. No main process handle/logger or game session is changed.
- One xUnit2017 Shop fixture assertion is corrected mechanically in this batch.
- Full [Windows39975270475](https://github.com/vgcman16/wolfconquer/actions/runs/39975270475)
  passes4m11s on exact39eaaf458e4526555de2e682298b94c80ff172c2: core23552/
  0failed of23717 (165externalfixtureexcluded), updater301/301, launcher49/49,
  8stageexit0,204build warnings. Receipt20261023T150622623Z-73fd3de4 finished
  15:10:11.1538337Z. CodeFactor3NEW complexity/0FIXED,total2301; no score-only rewrite.
- Limits: native acceptance callers/host are not activated. FinishQuest, full GUI,
  awards, automatic disk/SQL saves, raw competing writers, delivery recovery and
  whole quest lifecycle remain open. No live client or original save was used.

### FSB-045: Aggregate exchange material costs before preparing a purchase

- Owner: registered Goods exchange route. Repeated material IDs checked the same
  stock independently; the shared consumer could silently pay only a prefix and
  still award the reward. Checked quantity multiplication could escape as an error.
- Check UInt32 products and repeated-ID sums, then validate complete captured
  stock before preparing any debit/reward. Consume only private rows and frames
  in original definition/physical-stack order; any unpaid remainder discards the
  entire plan. Leave the shared helper's25+ unrelated callers unchanged.
- Tests-first baseline: **10 failed/15 passed** of25. Three failures demonstrate
  genuine changed sessions on unpaid duplicate costs, two are escaped product
  overflows, and five are diagnostic-classification differences rather than
  additional economic exploit proofs.
- Verification: private Release **446 passed/0 failed/0 skipped**; independent
  public **442 passed/0 failed/4 existing external-fixture skips** of446. All25
  new finite registered-handler cases pass, including7 success/order cases with
  complete saved/reloaded native-image comparisons and18 refusal cases. Retained
  exchange, shop and native quest controls remain. No old test was removed.
- Existing count/shop/amount/reward/capacity guards, active exact-ID/mixed-binding/
  zero-stack-as-one policy, reward values, per-definition update/remove frames
  and reward/echo order remain. The handler extraction is a semantic material
  phase boundary, not a complexity-score rewrite.
- Full [Windows39976822207](https://github.com/vgcman16/wolfconquer/actions/runs/39976822207)
  passes4m05s on exact8abeec78727a476675861f4853f87e3a1cd4c444, completed
  15:21:24Z: core23577/0failed of23742 (165externalfixtureexcluded), updater301/301,
  launcher49/49,8stageexit0,204warnings. Receipt20261023T151734524Z-050a9ec1.
  CodeFactor3NEW (complexity,blank-line,precedence)/1FIXED old complexity,total2303.
  The2 mechanical corrections join the next behavior batch, not a separate CI.
- Limits: current creator UID policy and unconditional session publication are
  unchanged. Global UID activation, raw/cross-await writers, duplicate physical
  ownership, save-before-publication and durable multifile economy remain open.
  GoodsEx donation/selection and native interchangeable-material/binding parity
  are separate work; no live client, SQL or original save was used.

### FSB-046: Drain late-accepted TCP clients before shutdown completes

- Owner: local ConquerTcpServer.DisposeAsync, not a decompiled gameplay rule.
  The client-task snapshot preceded cancellation and accept-loop completion.
  A client accepted concurrently could be registered after that snapshot, so
  shutdown returned while its real frame handler/transport cleanup still ran.
- Cancel and stop listener admission, await the actual accept producer, then
  capture client tasks under the existing gate and await their real completion.
  Wire formats, handling order and awaited transport-close ownership are unchanged.
- Tests-first baseline:3 passed/1 failed of4; the genuine late-accepted handler
  was still held when Dispose had already completed. Scheduling uses the actual
  registration gate and real loopback socket, not injected task-list entries.
- Verification: private and independent public Release each25/25, zero failures
  or skips, including all4 new shutdown controls and21 retained networking/
  disconnect cases. Held handler and asynchronous transport cleanup both delay
  shutdown; peer EOF follows cleanup. Never-started/empty listeners are covered.
- Limits: normal single-owner shutdown only; concurrent/repeated disposal,
  failing cancellation/log callbacks, all detached runtime producers and full
  borrowed-Host lifetime are separate work. This is not global quiescence,
  UID activation, deployment or actual-client proof. FullWindows39986796881
  passes4m41s on exact93e7259168ad0a4d2eb0718f8c0db6af41261200, completed
  16:31:18Z:23593corepassed/0failed of23758 (165externalfixtureexcluded),
  updater301/301, launcher49/49,8stageexit0,204warnings. Receipt
  20261023T162652064Z-8218bc29 finished16:31:10.4777032Z.

### FSB-047: Spend the pack row that actually passed admission

- Owners: SacredRefinery, P7Soul, Steed and P7SacredTreasures pack consumers.
  Admission required a positive stack in the selected binding class, but debit
  selected an earlier matching ID regardless of binding and/or positive stock.
  This could convert the wrong binding class or falsely free inventory capacity.
- Debit now selects the first positive eligible row in original inventory order.
  Colored Steed retains its existing nonzero-bound class; other listed costs
  remain unbound. Reward values, CP costs, RNG, update/remove/reward frame order,
  shared consumption helpers and post-debit capacity policy are unchanged.
- Tests-first baseline:239 total,213 passed/26 failed. Ten corrected old unsafe
  expectations and16 new zero-stock/false-capacity controls fail on old code.
  The initial fixture-only compile failure executed no tests; setup/import fixes
  preserved every assertion before that real baseline. No tests were removed.
- Verification: private and independent public Release each239/239, zero failures/
  skips:205 retained cases
  (including10 repaired unsafe expectations) plus34 new controls. Full item
  metadata, saved/reloaded images, exact debit frames, first-eligible order and
  full40-slot rejection are checked.
- Limits: bounded selector repair, not whole economy atomicity, duplicate-UID
  validation, concurrent raw-writer/session safety, multi-file recovery, global
  UID activation or live-client proof. FullWindows39989920933 passes4m37s on
  exact8411bb619ec53477a6eb035aa96db29c45c0e4d9, completed16:54:13Z:
  23627corepassed/0failed of23792 (165externalfixtureexcluded), updater301/301,
  launcher49/49;8stageexit0,204warnings. Receipt20261023T164951218Z-143565ae
  finished16:54:06.1546457Z.


### FSB-048: Keep adjacent pack debits in their admitted binding class

- Owners: required-pack Black Friday catalog grants, Dazzling Diamond Box and
  the five-Spirit Phoenix exchange. Their unbound-stock gate was followed by
  ID-only debit. Phoenix also included empty rows in its per-pass targets.
- Capture positive unbound costs in original physical order. Phoenix filters
  both pass snapshots and current targets, preserving eligible round-robin order
  and exactly five cost frames. Black Friday/Diamond rewards, CP branches, RNG,
  capacity policy, UID API and debit/message/reward ordering are unchanged.
- Tests-first baseline:135 total,119 passed/16 failed. Five old unsafe debit
  expectations and11 new preserved-row cases reproduce the bug on unchanged
  production. All105 retained intentions remain, plus30 modular new controls.
- Verification: private and independent public Release each374/374,0failed/
  0skipped across all seven pack
  families (this135 plus retained FSB047239). Full item preimages, genuine saved/
  reloaded images, exact frame sequences, first-eligible/round-robin order and
  insufficient eligible stock are covered.
  Phoenix's old max-plus-one fixture reward UID changes21 to11 when the correct
  eligible20 is consumed and foreign10 survives; no payout or allocator tuning.
- Limits: bounded selectors, not duplicate-UID/raw-writer/current-session safety,
  multi-file/economy/outbox recovery or canonical UID activation. Future UID
  producer predicates are authored separately; genuine legacy fixture migration
  remains open. FullWindows39991776324 passes4m57s on exact
  918899075bce5d8ddc1d0e8127d8c1cb87c9d810, completed17:08:04Z:
  23657corepassed/0failed of23822 (165externalfixtureexcluded), updater301/301,
  launcher49/49;8stageexit0,204warnings. Receipt20261023T170347775Z-bb22ad8a
  finished17:07:57.7038917Z. No live client, SQL or original-save action.


### FSB-009 (partial): Prepare Super Guild War rewards before consuming claims

- Owner: the sole registered SuperGuildWar TryClaimReward caller. The in-memory
  claim pool/UID marker changed before item creation, checked wallet arithmetic,
  attribute copies and complete save/frame preparation. A later fault could lose
  the claim or expose partial rewards; retrying the same UID could not recover it.
- Prepare the private complete candidate, persistence values/item image and exact
  owner/dialog/broadcast frames before the unchanged real claim decision. Expected
  preparation faults leave claim, reward state, files and output unchanged. Grant
  reuses prepared values/bytes; no speculative claim/refund or accepting callback.
- Genuine registered2084 baseline **8 failed/4 passed** of12: every failure is a
  repaired SAME UID unable to claim after an earlier late fault, with no event
  reset/reseed. Four leader2-slot/deputy4-slot boundary controls already passed.
- Verification: private Release **468 passed/0 failed/0 skipped**; independent
  public **464 passed/0 failed/4 existing external-exchange skips** of468. All12
  new controls pass, plus the old5 reward/5 backend and446 retained cases. Full
  physical files, wallets/flags, claim-pool exhaustion and exact owner/broadcast
  frames are checked. Two exchange mechanical style corrections join this batch.
- Existing active payouts remain leader300000CP + tuned500000silver + one2100065
  item/flag151, deputy flag152 only. Source7770 documents the older model with
  50000000source silver; neither that amount nor the newer decompile's mail branch
  replaces the current approved tuning. No wire format or live routing changed.
- FullWindows39978653171 ran all12 guild cases successfully but failed one old
  RiseOfTaoism timer/log race (23588corepassed/1failed/165excluded). The fixture
  now waits for actual async completion; private/public focused15/15each pass.
  FullWindows39980179395 now passes4m15s on exact16763a82:23589core/0failed,
  165excluded, updater301/301, launcher49/49,8stageexit0,204warnings.
  This is NOT full FSB009 closure:
  Lottery cadence, unconditional session publication, claim+memory+two-file crash
  atomicity, durable outbox/reconciliation, raw writers and global UID activation
  remain open. No live client, original saves or SQL were used.

### FSB-049: Prepare Plume collection progress and its item together

- Owner: map1002/family2 quest1007 Turtledove Plume. The real mission requires
  collecting item711236, not independent kill credit. Old code published/saved
  objective progress before validating or preparing the item; missing metadata
  advanced2 to3 without granting a plume.
- Prepare checked private quest/item snapshots and complete objective/item frames
  first, then publish one combined in-memory candidate through the actual current
  session CAS. Missing definitions, invalid durability, objective/frame overflow
  and item creation overflow refuse before either half is published.
- Valid one-item reward and objective-before-item wire order remain. The existing
  quest save runs after CAS. Its ordinary I/O failure remains a committed coherent
  memory reward with an explicit persistence gap, not rollback or file atomicity.
- Tests-only baseline13:7passed/6failed. One unsafe old expectation corrected;
  three old valid/no-op controls retained, nine new controls added. Private Release
  complete boss/reward class now439/439, zero failed/skipped, including all13.
  Clean public Windows39998007520 passes4m39s on exact7c9e9a11a47fda490f43124b71587198ee9725d0:
  23687core/0failed of23852 (165externalfixtureexcluded), updater301/301,
  launcher49/49;8stageexit0,204warnings. Completed2026-10-23T17:52:55Z.
- Limits: existing local max+1 UID API, postcommit observer/cancellation/delivery
  failures, raw writers, cross-file recovery and whole-combat atomicity remain
  open. The isolated canonical UID equivalent separately passed17/17 within
  791/791 focused integration checks; that family is not activated in main.

### FSB-050: Stop false monster reward receipts and undefined luck charges

- Owners: Earth Force3646 full-bag response, Cloud Riding Scroll1337 reward and
  Deityland WarDevil reward notice. The recovered WarDevil branch never grants
  its announced treasure; it now refuses itemRewardDefinitionUnavailable before
  luck debit, reward RNG, treasure broadcast or effect. No payout was invented.
- Earth full inventory reports the existing capacity wording in the same
  MessageBox layout. Valid fragments/conversion stay unchanged. Cloud prepares
  the genuine grant and both complete frames before exact-current CAS, then
  retains MessageBox-before-item order and rechecks the committed owner before
  each send. No-owned-scroll remains a no-add branch without a false receipt.
- Corrected tests-only baseline25:4passed/21failed. Four unsafe old facts corrected,
  21 new cases added; complete private boss/reward class439/439, zero failed/skipped.
  Existing generic kill-CP no-award controls isolate the new scenes; assertions
  were not weakened. The unregistered connection stops at the real earlier
  ArrowBlades session refresh: its control requires no frames/no reward branch,
  not a fabricated later diagnostic. The passing full Windows checkpoint above
  includes all25 message controls as well as the retained boss/reward family.
- Limits: WarDevil grant enablement still needs an authoritative definition and
  real entitlement transaction. Current item creation, durable rewards/outbox,
  whole-kill atomicity, unrelated later writers and check/send races remain open.
  Observer retirement after Cloud CAS suppresses stale output, not claimed rollback.

### FSB-051: Prepare Earth Force conversion before fragment debit output

- Owner: map3998/family40906/quest3646, native MonsterRole2131..2160.
  Previously the fragment update and five-fragment removal frames could precede
  final Earth Force preparation; missing/malformed metadata or checked UID/
  durability failure could leave a prefix without the earned reward.
- Prepare the complete fragment/conversion inventory and all frames privately.
  Validate each required physical metadata row and bounded durability, require
  the exact current session, then CAS only the final inventory. Publish the same
  fragment/update/removal/Earth frames in order, checking the committed owner.
  After-commit notification failure is not a rejection or rollback.
- Actual tests-only32:10passed/22failed. One unreachable unregistered diagnostic
  was corrected to the real earlier ArrowBlades refusal, retaining all state/file
  checks and requiring EMPTY all frames. All32 then pass. Full private focused
  reward/rank/Status batch592/592, zero failed/skipped; all471 boss/reward cases
  pass. Valid0/3/4/5/6 and split-row conversions retain quantities, binding,
  independent full2116 bytes and consume order; FSB050 capacity wording remains.
- Limits: operation-local memory/output boundary, not whole-kill, durable game-file
  transactions, entitlement/outbox, delivery or global UID closure. Current
  max+1 remains until the separate canonical registry cutover; its equivalent
  overlay/real-ledger fixtures are isolated and not yet executed or activated.
  Clean full Windows40002265954 passes4m44s on exact549e9829c1a9095fd986fe5cce38c5be6106d5ee:
  23753core/0failed of23918 (165existing externalfixtureexcluded), updater301/301,
  launcher49/49;8stageexit0,212warnings/0buildErrors. Completed18:24:11Z.

### FSB-052: Unavailable rankings cannot authorize bonuses or spell removal

- Owners: complete combat-gear rank effects and the real MyCombatGear.Status
  caller. The prior query returned ordinary rank0 for absent/null owners/lists
  and could accept a winning prefix before a malformed tail. In particular an
  unavailable Pirate family could authorize removal of existing spell21930.
- Capture and validate every class-required physical family, including complete
  tails, under its real rank gates before producing detached UID ordinals or any
  Status rank mutation. Observed unavailable/drifting state throws explicit
  InvalidDataException; valid empty/nonmembership remains rank0. No repair,
  dictionary callback, new rank order, numeric formula or wire format is used.
- Independent real Status baseline8:2valid controls passed/6unsafe cases failed;
  after correction all45 Status cases and46 rank-effect cases pass within592/592.
  Seven old private exception-oriented cases now assert safe unavailability/no
  publication;20 other effect controls stay. Private/public export shapes differ:
  public retains its RankBonuses owner and all6 existing bonus tests, adds34
  controls, and does not import duplicate private mapping/query methods.
- Limits: prepared rank reads, not whole-Status/global rank transactions. Raw
  mutable owners/ABA, later non-rank failures, spell persistence, concurrent
  lifecycle and Host activation remain open. The clean full public CI above
  includes all34 new public rank/Status controls and6 retained bonus controls.

### FSB-053: Preflight selected status values before applying effects

- MyCombatGear.Status previously read selected named coefficients after rank,
  spell and earlier flag effects; a missing second bless row was explicitly
  tested to retain the dirty prefix. Now capture the same 22 predicates and
  their required scalar slots before any Status effects or packet rental.
  Missing/null/short active values refuse with InvalidDataException; inactive
  optional metadata stays unconsulted. Valid formulas, conversions, per-addition
  clamps, composite indices and output order are unchanged.
- Tests-only Status79:47passed/32failed. After repair, surrounding rank/query/
  Status176/176 and combined reward/status721/721 pass, zero failed/skipped.
  One unsafe old bless fact is corrected;34 additional controls compare complete
  stat/perfection/spell/queue/caller-packet/flag preimages.
- Limits: selected-input preparation, not a whole Status transaction, immutable
  raw owners, global lifecycle authorization, spell persistence or delivery.

### FSB-054: Prepare five nature-force grants before session publication

- Map3998 families40900/40902/40905/40907/40908 previously Set inventory before
  complete item-frame preparation and could overwrite stale owners. Prepare
  strict required metadata, items and full frames privately, CAS the exact
  current session once, and check the committed owner before item output.
  Loaded Effect256, malformed durability and UID overflow refuse unchanged.
  A committed observer retirement/send failure is not misreported as rollback.
- Restore the native eight-Rune-Fragment ceiling; retain quest/rate/capacity
  ordering, one unbound grant, stack UIDs, quantities and complete2116 goldens.
  Tests-only74:39passed/35failed; all74 pass within combined721/721, including
  all545 boss/reward cases. New predicate assertions were batched on intake,
  without exclusions, suppressions or protocol refactoring.
- Limits: in-memory grant boundary, not durable files/rewards, whole-kill atomicity,
  auto-route implementation, outbox/delivery or global UID safety. The separately
  composed canonical UID successor remains isolated and unactivated.

### FSB-055: Retain primary launcher-test failures during dispatcher cleanup

- Current CodeFactor CA2219 identified the launcher test helper throwing a join
  timeout from finally, hiding assertion/skip/operation failures. Capture the
  primary error, independently attempt shutdown/join, retain distinct observed
  cleanup errors, and rethrow one original error with its stack or aggregate
  primary-first. Already-safe updater aggregation is unchanged.
- Real STA tests-only8:5passed/3failed; repaired full launcher57/57 passes, zero
  failed/skipped. Held shutdown threads are always released and actually joined
  in fixture cleanup. No game/client execution or accepting fake dispatcher.
- Limits: bounded test-harness diagnostics, not gameplay or production shutdown
  completion; work still live beyond the helper deadlines is not certified.

Full FSB053/054/055 checkpoint: clean Windows40008366439 passes4m14s on exact
8843a6384f167b9e16a19b30e8d6b93c6a7efc74, completed2026-10-23T19:09:18Z.
Core23861passed/0failed of24026 (165existing externalfixtureexcluded), updater
301/301, launcher57/57; all8stages exit0,204warnings/0buildErrors. Parser20+13
controls are synthetic only. This does not close the explicit gameplay limits.

### FSB-056: Admit only positive unbound DragonSkin unlock costs

- `src/WolfConquer.Core/World/WorldPacketHandlers.AnimaSkin.cs` selected a mapped
  Unlock item without checking binding or quantity. Retrieve creates an unbound
  item, permitting bound-to-unbound conversion; an empty stack could also supply
  a skin. Select the first mapped row with `Bound == 0 && StackSize > 0`, retaining
  inventory order, the one-unit cost, other item fields and existing frame order.
  Native `DragonSkin.Process` uses `Inventory.Contain(mappedId, 1, 0)`; this is
  behavioral reference, not new client authority. Opcode 2415 is unchanged.
- Real registered-frame tests-first baseline: all 16 new cases failed before the
  repair. Repaired focused suite: private 70/70 and public 70/70 passed. Refusals
  cover bound-only/zero-only rows and nonzero binding 255, with truthful actual-UID
  `OK=false` Unlock/Retrieve replies, no debit/item frames, no skin and unchanged
  session, full item/character state, file bytes and write times. Mixed-order
  controls cover all three mappings and valid stacks of one/three after earlier
  bound/zero rows, preserving later eligible rows, metadata and exact success
  frames; saved item/skin results are checked.
- Historical FSB056 admission-only boundary (before FSB057): the real
  `WorldServerFrameHandler` session callback does
  autosave changed inventory. The explicit character skin save is separate;
  successful individual saves are not a coherent item-plus-skin transaction.
  Crash/replay recovery, concurrent ownership, persistence/send-failure atomicity
  and delivery remain open for the separate transaction work. This is admission
  proof, not full durability, a new full-CI checkpoint or live-client/deployment
  proof.

### FSB-057: Commit DragonSkin items and skin through the shared journal

- The managed Unlock/Retrieve path previously saved items and character skin
  separately. `WorldPacketHandlers.AnimaSkin.*` and `LegacyDragonSkinPersistence.*`
  now use the shared `LegacyTradeJournal` v2 single-owner transaction: Character
  is keyed by account (fixture 100), Items by character (1293600). Strict complete
  item-graph/skin-book and persisted baselines, current-owner persisted CAS,
  exclusive admission and recovery readiness for the exact roots precede commit.
  FSB056 positive unbound costs and existing successful frames remain. Retrieve
  retains zero durability for absent optional metadata; present malformed
  metadata refuses. Presentation changes also use current-owner CAS.
- Registered tests-only baseline: 89 total, 8 passed/81 failed. First admitted
  run: 186 total, 161 passed/25 failed; all 89 registered and 70 retained cases
  passed. The 25 of 27 component failures were raw `IReadOnlyList` collection-
  expression fixture wrappers correctly rejected by strict validation. Only
  `Fixture.Seed` changed to reload its just-written file with the real
  `PlayerItemSnapshotLoader`, plus a raw-byte preservation assertion; production
  validation was not weakened. Then 186/186 passed; broader actual Trade/barrier/
  session selection 368/368 passed, zero failed/skipped. The same 26 paths (15
  production, 11 new test) transferred to public, which passed 368/368 with zero
  failed/skipped in two seconds: 116 new + 70 retained + 182 adjacent cases.
- Controls cover interrupted 0/1/2-replacement prefixes rolling forward, sharing
  failure rollback/retry, stale owners, postcommit retirement/replay/transport
  close and presentation-CAS observers. Recovery is process-restart reconciliation,
  not power-loss, multiprocess, raw external-writer or ABA proof. Durable outbox/
  delivery and global UID safety remain open: the current creator is still max+1;
  the canonical alternative is not installed. No SQL/live/original-save action,
  routing change or Host activation.
- Full Windows [40013043779](https://github.com/vgcman16/wolfconquer/actions/runs/40013043779)
  passed on exact public `f2f122f2a9f337ec0bcb80332f1d93579c2868ca` in 4m11s:
  core 23993 passed/0 failed of24158 (165 existing external-fixture exclusions),
  updater301/301, launcher57/57, all8 stages exit0, build204 warnings/0errors.
  Receipt20261023T194003090Z-230b1736 finished19:43:51.8908154Z; job completed
  2026-10-23T19:43:59Z. Parsers20+13 are synthetic, no network/service probes.
  This is offline source acceptance, not deployment or complete gameplay parity.

### FSB-058: Debit only admitted positive unbound NPC exchange costs

- Owners: Arthur VIP quest rewards, EventUtility flower/Stardust/Buddha
  exchanges and Labyrinth Simon diamond rewards. Native NPC handlers authorize
  ordinary costs with `Inventory.Contain(id, count, 0)`, but native `Remove`
  ignores binding. The old managed debit copied that flaw after an unbound gate.
  Simon additionally counted empty rows as one diamond; Buddha selected an empty
  necklace. These are server defects, not packet compatibility requirements.
- Debit now selects only matching `Bound == 0 && StackSize > 0` rows, retaining
  lowest eligible UID order. Simon authorization and debit count actual positive
  stack units. All four Arthur costs/rewards, four-red/four-white flower costs,
  30-percent branches, one-Stardust choices, one-necklace cost and exact17-diamond
  rewards remain unchanged. Diagnostics no longer claim any-binding removal.
- Combined tests-first batch with FSB059:108 cases,82 passed/26 failed. Six old
  facts that required wrong binding/lock behavior were corrected;39 new cases
  were added, including seventeen empty diamond rows and16 positive+one empty.
  The wider private Release NPC/reward selection passed338/338,zero failed/skipped.
  Independent public Release also passed338/338,zero failed/skipped.
- Controls retain full saved item comparisons, valid economics, metadata and
  successful packet order. Arthur covers all four chains and binding255;
  Event/Simon cover noncanonical bindings2/7 and empty rows before real costs.
  These are admitted-cost repairs, not whole current-owner NPC transactions,
  crash recovery, global UID safety, SQL or live-client/deployment acceptance.

### FSB-059: Preserve existing item locks when granting RedBodhi stack units

- Owner: FullAccount option16 RedBodhiBean grant. Updating a compatible existing
  stack called `WithLocked(0)`, silently unlocking player stock. Remove only that
  existing-row reset; genuinely new rows retain the original unlocked defaults.
  Ten1000-unit calls, capacity policy, metadata, binding and frame order remain.
- Two unsafe old facts now require the existing lock to survive. Three new
  Locked0/1/2 cases check every saved-file byte (only StackSize changes), retained
  UnlockTimer/perfection/owner metadata, unchanged source/character files and
  all ten independent206-byte opcode2116 goldens. The Locked1/2 controls and both
  unsafe old facts failed before repair; Locked0 already passed.
- Included in private/public338/338 above. This is metadata preservation,
  not a whole grant journal,
  recovery/delivery guarantee, global allocator activation or live proof.
- Full Windows [40017540883](https://github.com/vgcman16/wolfconquer/actions/runs/40017540883)
  passed on exact public06210e3ad195366b3e052fabb6d67e82cf24f762 in5m26s:
  core24032 passed/0failed of24197,165 existing external-fixture exclusions;
  updater301/301,launcher57/57,all8 stages exit0,build204 warnings/0errors.
  Receipt20261023T201551325Z-371218ea finished20:20:17.8510965Z;
  GitHub job completed2026-10-23T20:20:24Z. Root read exact runnerHEAD,
  receipt/stdout and all three terminal TRXs. Parser20+13 controls remain
  synthetic only; no HTTP/service,SQL,live-client or deployment proof.

### FSB-060: Refuse full-bag Vote item claims before spending points

- Owner: `RealmTransferServices.MiscServices` Vote item rewards, options43–46.
  The old path could deduct VotePoints without granting an item when the bag was
  full. Capacity now rejects before persistence, session mutation or success.
- Preserve all four costs/rewards, metadata validation and successful frames.
  Full-bag controls require unchanged points, items, saved files and outputs.
- Verification: included in the shared admission batch below. This does not
  establish whole Vote claim recovery or a durable economy transaction.

### FSB-061: Debit only eligible Lottery exchange costs before allocating rewards

- Owner: `JailGuildServices.Rewards`, Collector Wong and Market exchanges.
  Authorization could accept unbound stock while debit selected an earlier bound
  or empty row. Select only matching `Bound == 0 && StackSize > 0` rows, retaining
  original eligible order and existing costs/payouts.
- Market checks capacity against the exact staged ticket removal before reward
  allocation; a full bag cannot reach UID overflow/allocation first. Refusal
  preserves costs, economy, files and success output. Valid slot-freeing and
  noncanonical-binding controls remain.
- Verification: shared batch below; no global allocator/Host activation or
  complete exchange transaction/replay claim.

### FSB-062: Check Deityland capacity against the actual planned key debit

- Owner: `Deityland.TryUnlockLegacyDeitylandChestAsync`. The old ANY-short-key
  prediction let a full40 bag with ordered stacks `[2,1]` append item41 after
  charging the first stack2 without freeing a slot.
- Prepare the exact existing debit/frames privately, then check the resulting
  inventory for item payouts. Refusal discards both; CP-only rewards need no slot.
  Preserve binding/enumeration policy, costs, rewards/rates and debit-first frames.
- Verification: shared batch below. Pre-persistence session publication,
  shared zero-stack admission and NPC respawn/claim lifecycle remain open.

### FSB-063: Require the Epic chest's own Epic key

- Owners: Epic chest dialog/unlock constants and registered caller. Native code
  and the copied test consumed Rare3303973 while the Epic dialog requested
  Epic3303974. Unlock now uses the existing Epic dialog key constant.
- Registered Epic-only success, Rare-only unchanged refusal and mixed-key
  controls preserve Rare stock and consume Epic stock for either genuine random
  item payout or200,000CP. The old unsafe mixed-key fact is corrected; wire
  layouts, rates and successful packet ordering are unchanged.
- Catalog provenance: `.setup_work/seed-json/items.json` rows2373051–55 and
  2373123–27 name RareChestKey/EpicChestKey. Its `manifest.json` identifies a
  2026-07-19 export from original `lqtxfwd/GameServer/DBServer`: SERVER catalog
  evidence, not independent client-table/binary proof.
- Shared tests-first evidence for FSB060–063:50 cases=37pass/13fail and26 cases=
  15pass/11fail; combined76=52pass/24fail. Added43 net new cases and corrected
  four unsafe old test methods. Private widened Release selection passed
  **326/326**, zero failed/skipped (`claim-chest-green.trx`, terminal exit0).
  Independent public Release also passed326/326,zero failed/skipped. Full
  Windows CI is pending; focused checks are not a full-CI claim.
- D13 offline registered/component fixtures and owned files only. Private-method
  post-roll controls are not registered Epic/Frame/client proof. No live client,
  SQL, crash safety, coherent multi-file atomicity or full-parity acceptance.
  Fourteen equivalent xUnit2031 substitutions in eight test files are local
  hygiene, not a verified CodeFactor portal finding reduction.

## Active audit

Current work does not close whole managed reward transactions. SuperGuildWar's
checked preparation now precedes its claim decision as verified above, but the
claim/session/two-file durable transaction remains open. Lottery cadence can
still precede cancellation/persistence. These remain FSB009 work, not safety
proved by the isolated UID-adoption proposals.

| ID | Defect / affected owner | Required invariant | Status |
| --- | --- | --- | --- |
| FSB-001 | CanUseSpell spends stamina/mana and mutates authorization/soul before late rejection; exposes rows on false | Validate and prepare first; rejection is side-effect free; serialize competing admissions | Admission fixed; full executor transaction still open |
| FSB-002 | Hairface storage retains null owner and fails later | Reject invalid owner at construction; valid ownership remains stable | Constructor fixed; adjacent storage audit remains |
| FSB-003 | Guild donation narrows uint to signed int, allowing a wallet credit | Positive bounded amount; wallet/fund/donation commit together | Open, not integrated |
| FSB-004 | League receiver uses a recycled packet after its wrapper was disposed | Keep exclusive packet ownership through final use | Open, joint graph held |
| FSB-005 | Trade/booth/inventory disconnect, duplicate UID and retry windows | Validated atomic/idempotent transfer with durable reconciliation | Full audit required |
| FSB-006 | SQL multi-row save/load and native eager lifecycle are incomplete | Real transaction boundaries, constraints and recovery proof | Open |
| FSB-007 | Guild/league/title/catalog loaders publish partial state; file saves rewrite in place | Stage valid state; coherent publication and recoverable file replacement | Implementation/audit in progress |
| FSB-008 | Name-based GM/PM privileges and inconsistent title4001 equip/remove classification | Server-owned authority and validated consistent title metadata | Implementation/audit in progress |
| FSB-009 | Rewards record claim/MAC before grants; partial failure loses claim; IP/MAC indexing differs | Validate full reward; atomic/idempotent claim and grant; correct identity checks | Scoped SuperGuild preparation verified468 private/464public+4external skips; Lottery and durable whole claims remain open |
| FSB-010 | Guild/league lookups, appointment bounds and caller-selected guild IDs lack validation | Validate request, role, capacity and own-guild authority before effects | Implementation/audit in progress |
| FSB-011 | Packet byte-array constructor can overrun fixed allocation | Allocate complete input and expose truthful capacity before unsafe use | Constructor fixed; wider bounds audit open |
| FSB-012 | General native packet readers/writers trust mutable Size or MAX_SIZE instead of actual allocation | Check allocation, cursor, frame and lifetime before every unsafe access | Open |
| FSB-013 | Existing spell executors have late eligibility gates, resource wraps and shared metadata mutations; old tests preserve these | Full-cast preflight, bounded arithmetic and no shared metadata mutation on cast | Retroactive executor audit open |
| FSB-014 | Active managed title receiver changes memory before save success, mishandles wing4001 and untrusted subtype/catalog fields | Validated save-before-session commit, authoritative metadata and lifecycle-bound publication | Scoped opcode2313 fixed; durable recovery/native lifecycle open; classic route tracked separately |
| FSB-015 | Repeated ranking scans crash on invalid rows; Rune.Free can reopen occupied211 after byte-count wrap | Single current-owner queries and fail-closed capacity/occupancy checks | Affected queries fixed; broader owner audit open |
| FSB-016 | Updater relies on transient MainModule image enumeration; synthetic pipeline names collide with a real owned namesake test | Retained read-only image/exit attribution; unknown live state refuses; isolated signed fixture identities | Reader/isolation fixed301/301 both trees; point-in-time scan limits remain |
| FSB-017 | Classic opcode2152 Change uses unconditional Set from a potentially stale context | Current authenticated owner and persisted commit before publication; no replacement-session overwrite | Scoped managed route fixed; native lifecycle/durable recovery open |
| FSB-018 | Weapon slot aliases/corrupt tails and seal growth can authorize partial state or exceed response allocation | Complete physical admission and captured bounded packet before mutation | Scoped slot methods fixed; full2353/2354 receivers held |
| FSB-019 | Rune effects accept foreign owners, incomplete/duplicate slots and fault on missing metadata | Genuine current complete ownership and safe deterministic query | Equipped queries fixed; writers/full lifecycle open |
| FSB-020 | Shop refresh can loop forever or lose stock; counters wrap and Load publishes prefixes | Bounded staged book operations and truthful prepared publication | Native component fixed; economy receivers/durability held |
| FSB-021 | Shop countdown targets midnight plus a minute using three clocks | One bounded local-calendar countdown sample | Display calculation fixed; scheduler/reset transaction open |
| FSB-022 | Name policy regex checks only the final character; rename and uniqueness policies differ | Complete validated name policy and consistent authoritative uniqueness | Whole-string repertoire check fixed; rename/uniqueness and stored-name migration remain open |
| FSB-023 | Flag registration/reset/publication can mutate before a null bitmap/view or later failure | Validate/prepare before coherent current-owner registration; duplicates reject without side effects | Scoped component locally verified1931; full lifecycle/host transactions remain open |
| FSB-024 | Broader prestige rebuild still has legacy partial-publication, foreign-owner, duplicate-slot and arithmetic-wrap assumptions | Validate the whole genuine graph and prepare complete rank/guild state before coherent publication | Audit required; missing named-rune metadata is handled, not the whole rebuild |
| FSB-025 | RandomLite inclusive-range arithmetic can overflow or fault after consuming a draw; full-width sampling has limited domain and modulo bias | Bounded representable results, validation before draws and an explicit distribution/domain policy | Arithmetic bounds fixed; full-domain/distribution/fairness and concurrency remain open |
| FSB-026 | Potion bonus/timer precede fallible flag/message work; duration text always30 | Prepare all scoped effects before commitment; truthful duration | Scoped component locally verified1931; expiry/item/durable lifecycle remains open |
| FSB-027 | XP duration mutates escaped row unchecked and from stale owner | Checked selected duration, complete graph and final current ownership | Scoped component locally verified1931; whole XP/receive lifecycle remains open |
| FSB-028 | Physical calculation dirties outputs/RNG before bad status; mutates shared spell factor | Admission before effects; immutable catalog input | Scoped calculation locally verified1931; full cast/HP transaction remains open |
| FSB-029 | Invalid status can leave stale AI/partial ranks or false-success effects | Explicit unavailability before dependent writes/publication | Scoped callers locally verified1931; broader caller/host recovery remains open |
| FSB-030 | XP20 precedes required goal admission; malformed goal loads publish prefixes; stage count wraps | Complete validated owned goal/counter publication | Scoped component verified2392; durable claims/whole lifecycle open |
| FSB-031 | Sage flag precedes archive preparation; typed plans/frames can mismatch | Both genuine frames and exact current plan before flag commit | Scoped Sage verified2392; cast/host/delivery recovery open |
| FSB-032 | Shield/Stigma flag precedes animation/caller preparation | Complete bounded frames and caller before typed acceptance | Scoped effects verified2392; full cast/HP/durability open |
| FSB-033 | Final comparer can change earlier state or prepared native loan metadata | Pure complete state and exact live frame checks after last comparer | Scoped status/flag/potion paths verified2392; general packet/raw-writer audit open |
| FSB-034 | Rune insertion/cache publication can split; Count wraps; public arrays corrupt ownership | Complete owned staged publication, exact count and explicit unavailable queries | Scoped storage verified2896; full equip/load/save/economy/host remains open |
| FSB-035 | Refinement publishes before late status failure or spends chance/stamina before preparation | Complete real state/frames before scoped proc acceptance | Scoped procs verified3065 both trees and full Windows39958461749; full cast/HP remains open |
| FSB-036 | Per-session max+1 and deterministic starter item IDs can collide across accounts, deposits and restarts | One durable forward-only domain allocator adopted by every real producer before effects | Allocator component53/53 owned-file controls pass; host activation, trusted historical migration and all-producer adoption HOLD |
| FSB-037 | Native save truncates before validation, persists prefixes or loses nested deposits | Complete owned image and truthful atomic file outcome before acknowledgement | Scoped native save verified3116 both trees and full Windows39959714814; host/loader/SQL recovery open |
| FSB-038 | Fixed32 text reads/writes escape buffers or retain stale tails | Bounded field access, validate before write and explicit null admission | Scoped record verified34cases within3116 both trees; full load/postlude audit open |
| FSB-039 | Selected DivineGuard resets cooldown and publishes refinement before fallible status work | Complete real preparation and final ownership before stamp/status/draw effects | Scoped proc verified3214 both trees; outer authorization/combat/HP/host remains open |
| FSB-040 | Quest saves truncate; loads accept partial/default rows and split canonical aliases | Complete real image and coherent validated merge before file/map publication | Scoped persistence verified3465 both trees; host/SQL/raw lifecycle recovery open |
| FSB-041 | Rune admission filters Bound but debit consumes either binding | Debit exactly admitted ItemId+Bound and preserve opposite stock | Scoped direct/NPC debit verified236 private;229 public plus5existing external skips; whole reward/UID transactions open |
| FSB-042 | Quest objective mutation precedes unsafe packet work; counter wrap and split aliases | Complete current canonical graph, checked counters and real bounded frame before writes | Scoped whole operations verified292 both trees; native callers/full awards/delivery recovery open |
| FSB-043 | Normal and point shops append/debit without full modeled capacity | Complete required inventory rows fit before costs/publication | Scoped guards verified334 private;325public plus9existing external skips; native stack/partial/whole transaction work open |
| FSB-044 | Quest acceptance mutates shared metadata, can split indexes or throw after committed send failure | Prepare complete canonical maps/frame first; truthful accepted result after commit | Scoped acceptance verified418 both trees; native callers/awards/durable lifecycle remain open |
| FSB-045 | Duplicate exchange requirements reuse stock; partial consumption still awards; products can overflow | Complete summed material admission and private ordered full debit before reward | Scoped material repair verified446 private;442public plus4existing external skips; whole economy/UID lifecycle open |

## CodeFactor cleanup

Earth/rank fullCI40002265954 passed with212 warnings. Root batches its8 new
test-only warnings across4 files:6 equivalent xUnit predicate assertions and2
explicit positive-result null checks. No production or protocol change, no new
suppression/exclusion, no separate fullCI per warning. Public Release focused578/578
passes,zero failed/skipped,2s; build204warnings/0errors after the batch. This is a
test-only follow-up to the exact fullCI code snapshot above, not another fullCI.
Latest remote CodeFactor count was not read; compiler warnings are not that count.

Exchange public8abeec78 is full-Windows verified above. Its two mechanical new
findings are corrected in the SuperGuild behavior batch; the new semantic
handler's complexity is deferred. Current published report3NEW/1FIXED,total2303
does not certify those uncommitted style corrections or full gameplay completion.

Acceptance public39eaaf45 is full-Windows verified above. Its3 new findings are
complexity in Acceptance, AcceptanceGraph and the isolated child scenario;0fixed,
global2301. They are deliberately deferred while economic defects are repaired.
The one observed xUnit2017 build warning was removed in the same behavior batch;
the build now reports204 warnings, not a zero-warning or zero-CodeFactor claim.

Latest shop checkpoint publica92b1fb0 passes full
[Windows39972176836](https://github.com/vgcman16/wolfconquer/actions/runs/39972176836)
in4m17s, completed2026-10-23T14:50:09Z. Core23475passed/0failed of23640
(165externalfixturecasesexcluded), updater301/301, launcher49/49;8stageexit0,
205build warnings. Receipt20261023T144607865Z-5325d992 finished14:50:02.2305785Z.
CodeFactor2NEW complexity (Honor shop admission and test wallet/wire assertions),
1FIXED objective arithmetic precedence,total2298. These methods are not rewritten
just for the score. The new xUnit2017 test-assertion style warning is queued with
the next behavior batch; no extra full-CI loop is started for that warning.

Previous objective checkpoint public8e5ff255 passes full
[Windows39971090546](https://github.com/vgcman16/wolfconquer/actions/runs/39971090546)
in4m50s, completed2026-10-23T14:41:57Z. Core23442passed/0failed of23607
(165externalfixturecasesexcluded), updater301/301, launcher49/49;8stageexit0,
204build warnings. Receipt20261023T143736718Z-b71804ed finished14:41:48.4630069Z.
CodeFactor4NEW (3complexity+1arithmeticprecedence),1FIXED,total2297. The one
mechanical parentheses correction joins the shop behavior checkpoint; validation
methods are not split for a score and no separate full-CI warning loop is run.

Previous rune-bound checkpoint public27e2ae9b passes full
[Windows39970166492](https://github.com/vgcman16/wolfconquer/actions/runs/39970166492)
in4m44s, completed2026-10-23T14:35:25Z. Core23372passed/0failed of23537
(165externalfixturecasesexcluded), updater301/301, launcher49/49;8stageexit0,
204build warnings. Receipt20261023T143059748Z-cab8dee5 finished14:35:18.3387670Z.
CodeFactor1NEW DuplicateCode in the existing batch inventory helper,0FIXED,
total2294. The separate bound-aware NPC batch retained packet order; shared-loop
cleanup is deferred to a coherent behavioral batch, not a score-only CI loop.

Previous quest checkpoint public0b0da909 passes full
[Windows39965399561](https://github.com/vgcman16/wolfconquer/actions/runs/39965399561)
in4m06s, completed2026-10-23T14:01:13Z. Core23360passed/0failed of23525
(165externalfixturecasesexcluded), updater301/301, launcher49/49;8stageexit0,
204build warnings. Receipt20261023T135722313Z-95cce8c2 finished14:01:06.3980602Z.
Both local3465 selections pass. CodeFactor4NEW complexity/0FIXED,total2293.
These validation/commit methods are not reshaped solely for the score. The debt
is still open; static findings and test totals are not gameplay completion.

Previous DivineGuard checkpoint publicaf3ca964 passes full
[Windows39962905914](https://github.com/vgcman16/wolfconquer/actions/runs/39962905914)
in4m43s, completed2026-10-23T13:44:48Z. Core23325passed/0failed of23490
(165externalfixturecasesexcluded), updater301/301, launcher49/49;8stageexit0,
206build warnings. Receipt20261023T134022374Z-6aac4289 finished13:44:41.9366811Z.
Both local3214 selections pass. CodeFactor3NEW (2complexity+1duplication),
1FIXED (the deferred testconstant),total2289. No cosmetic control-flow rewrite
is started; none of this proves completed gameplay or host activation.

Previous verified behavioral checkpoint: public01431fe3,
[Windows39959714814](https://github.com/vgcman16/wolfconquer/actions/runs/39959714814)
passed4m45s, completed2026-10-23T13:20:50Z. Core23227passed/0failed of23392
(165externalfixturecasesexcluded), updater301/301, launcher49/49, all8stageexit0.
Receipt20261023T131620699Z-d343aea0 finished13:19:58.8004245Z;206build warnings
remain. This is offline automated proof, not deployment/live-client/full parity.

CodeFactor on that exact commit reports4NEW (3complexity+1testconstant),11FIXED,
total2287. All10precedence targets from256d7a18 are confirmed cleared, plus1older
native-save existence finding. That one testconstant is now included with the
next real behavior batch, not a separate edit/test/commit/fullCI ceremony. Complexity debt
is retained, not hidden or reshaped solely for a grade. No zero-issues or finished
gameplay claim is made.

Previous256d7a18 reports19NEW=10readability+9complexity,0FIXED,total2294. Its
Windows39958461749 passed4m23s; all10readability targets now cleared by01431fe3.

Previous public7879381d reports2NEW complexity/21FIXED,total2275. All
nineteen targeted precedence/readability records across10C#files are confirmed
cleared, plus2older findings. The two new complex methods and older nine complex
findings are deferred per the defect-first guardrail. No packet switch/control
flow is rewritten merely for a score; no zero-issues/gameplay-completion claim.

Rune plus the single19-record parentheses batch now passes private/public
Release2896/2896 each,0failed/0skipped. Parentheses only make existing operator
precedence explicit; no formula, ordering or packet-switch body changed. One
fullCI passed for this coherent Rune behavior checkpoint, not each warning.
Actual remote analysis confirms the claimed targets; complexity debt stays explicit.

Latest verified public code4727d8b3:

[Windows39949964185](https://github.com/vgcman16/wolfconquer/actions/runs/39949964185)
passed4m02s on exact4727d8b38e48535505a602ad32d288bdcde83e2a, completed
2026-10-23T12:03:34Z. Actual TRX: core22524 passed/0failed of22689 (165 external-
fixture cases excluded), updater301/301, launcher49/49; all8 stages exit0.
Receipt20261023T115947032Z-230b177a finished12:03:27.4697567Z. Build204 warnings
remain. CodeFactor:6NEW (all method complexity),7FIXED,total2267. The six complex
safety methods are recorded as maintainability debt, not reshaped for the score.
This proves the committed offline batch, not live-client/deployment/full parity.

Previous verified public code7a79ebc5: **0NEW/4FIXED**, **2268 total findings**,
net59 below2327. The three targeted follow-ups and one older RNG arithmetic
record are inactive. Full Windows39945614990 passed4m06s on exact
7a79ebc5c0098e6a78f88d0b6ebde752fca50313, completed2026-10-23T11:27:05Z:
core22196 passed/0failed of22361 (165 excluded), updater301/301, launcher49/49,
all8 stages0. Receipt20261023T112314959Z-b46ced1a finished11:26:58.3548561Z.
Build203 warnings remain; this is offline CI, not live-client or deployment proof.
Cosmetic follow-up loop stops here; next work is real flag/AI/caller safety.

Earlier published5a62e0d4: **8FIXED/3NEW**, **2272 total**. All seven older API
targets cleared; the eighth fixed record is an older duplicate that was reissued
under a new key. Two new findings are nameof suggestions in test expectation
constructors; literal ParamName assertions remain independent. The third is the
existing HW/Ninja Rank mutation duplication, not new gameplay functionality.

Current combined defect-first successor passes private/public2646/2646,0fail/
0skip. It includes the real FSB025 range repair, two test-expression fixes and one
shared typed HW/Ninja capped-ranking algorithm with19 new controls. Genuine
fields/constructor/gate/ref dictionary/ref cap remain; UID is captured once and
upsert uses one lookup. Stable unsigned ties/caps/row identity and late cap reads
remain tested. Mutation-before-fallible-sort/raw-race safety is still open.
No packet switch, protocol layout or handler ordering was refactored for score.
Remote analysis and full CI now verify this combined behavioral checkpoint above;
no per-warning full-CI loop is intended. Defect-first batching is now explicit in
the engineering policy.

Full Windows39943323494 passed on exact5a62e0d47751583f866a52c4fb910afec532f532
in4m09s: core22146 passed/0failed of22311 (165 excluded), updater301/301,
launcher49/49, all8 stages0. Receipt20261023T110433716Z-671600c3 finished
11:08:20.2704671Z. This verifies the preceding name/API batch, not the current
uncommitted successor, deployment or live-client play.

Earlier published checkpoint1990e8bc reports **0NEW/15FIXED**, **2277 total**
findings, net50 below the2327 baseline. All15 complexity/arithmetic records
introduced by2b are confirmed inactive. Full Windows39941895742 passed on exact
1990e8bccbc33bfede505b567ef3aaf5698a5db5: core22083 passed/0failed of22248
(165 fixture-dependent cases excluded), updater301/301, launcher49/49, all8
check stages exit0. Receipt20261023T105105363Z-d2463376 finished10:55:38.2705674Z;
the job completed10:55:45Z in4m55s. Build warnings remain; this is not deployment.

The next older-quality batch passes private/public Release2596/2596,0fail/0skip.
It includes the FSB022 correction above, five CA1853 ranking Remove lookups and
two CA1507 parameter-name literals. Four ranking owners retain successful-removal
sorting/caps and missing-key reference identity; Nobility still refreshes real
remaining rows even when the key is absent. Fourteen genuine owner/API controls
check single lookup, ordering and row identity. Nine exception/seeded-state cases
retain exact parameter messages and no draw on invalid ranges. The known
RandomLite full-range bug is separate FSB025 debt, not new safety acceptance.
Its actual full CI and remote analysis are now recorded above.

Earlier CodeFactor analyzed public2b993ece: **18FIXED/15NEW**, total **2292 findings**.
All seven targeted CA1827 and six CA1847 records cleared, plus five older records.
The15 new records are12 complexity and3 arithmetic-readability findings. They
were addressed by1990e8bc through semantic preparation/admission/parsing phases,
not hidden by suppressions/exclusions; all15 are confirmed cleared above.

Private and independent public Release each passed **2255/2255**,0fail/0skip,
including22 new classic/shop phase controls and the expanded full XP-flag/
prestige families. Validation order, reasons, physical ownership, copy-on-write
publication, prepared packet bounds and wire goldens remain checked. The prior
2164/2164 selection and31 name-search controls are retained. Full name policy
FSB022 remains open; tests do not depend on uncommitted Guild/Union APIs.

Full Windows39939873007 on exact2b993ece failed: core22056 passed/5failed,
165 excluded; updater301/301 passed; launcher/later checks were not run.
Receipt20261023T103337163Z-06e30071 finished2026-10-23T10:37:01Z. The five
failures were old tests requiring missing-rune exceptions. They now assert
coherent XP replacement/addition with zero bonus, complete status/timer/packet
state, and complete prestige/rank/guild updates with no named rune bonus.
No malformed vector was made a valid fixture; the broader flag transaction and
prestige rebuild remain separate audit work. The current2255-case selections
pass all five corrected cases. Full CI1990e8bc now passes as recorded above.

The unchanged native CRT controls require a measured PowerShell7 binding. An
earlier missing/array-valued environment caused8 probe failures in both trees;
the exact measured executable passed all eight without changing/skipping tests.

Previous verified checkpoint: full Windows39933407353 passed in4m06s on exact
`eae56f67e7ba11216b605c108a32abd54d40c73f`: core21682 passed/0failed
(165 excluded), updater301/301, launcher49/49 and all eight check steps exit0.
This is offline/synthetic CI, not deployment or complete gameplay parity.

Earlier remote snapshot: **2295 findings**, net32 fewer than the2327 baseline.
Commit8f7ea7 reports0NEW/7FIXED; follow-up eae56f67 reports0NEW/3FIXED. The ten
fixed records include all seven newly introduced warnings and three older ones.
The entire TitleStorage.cs file report now has zero active findings. Its handler
and flag-update phases remain behaviorally covered, not suppressed or excluded.
Private/public Release each passed1119/1119 again, zero failures/skips, after
the one-file follow-up, now also full-CI verified above. The15 CA1865 fixes
were already confirmed absent; their group contains only the held Guild record.

The seven-warning successor is now locally verified: private and independent
public Release each passed **1119/1119** selected core tests and **301/301**
complete updater tests, zero failures/skips. Named validation/commit/parsing
phases preserve short-circuit order, reasons, canonical metadata and real
save-before-publication safeguards; explicit arithmetic parentheses retain bytes.
The standard PID guard still rejects nonpositive IDs before native calls.
Link-control cleanup now attempts every exact-owned cleanup, preserves the
primary assertion/skip and every cleanup error in order, and retains the original
stack for a sole failure. Seven new failure/skip/stack/order controls pass.
No blanket suppression, protocol change or weakened test was used. Both8f7ea7
and its later one-file follow-up passed full Windows CI; the remote report also
confirms the targeted findings cleared. This is not a completed backlog audit.

Authorized alongside behavioral repairs. The public report was retrieved on
2026-10-23: grade A, 2,327 findings across 4,864 files. Category counts: complexity
822; duplication 196; performance 106; maintainability 1,203. This is a snapshot,
not proof that every reported issue has been inspected.

Later same-day report: grade A, **2,325 findings across 4,867 files**. The two
Hairface CA1827 records are no longer listed: existence uses `Any`, and catalog
resolution uses one `FirstOrDefault` instead of counting then repeating a lookup.

Three CA2244 findings are now addressed in test fixtures: two differently named
constants intentionally share one roll key and both assigned the same value.
Removed only the redundant second writes; a new alias Fact plus shared assertions
strengthen all 71 affected existing cases. All 72 are in the **510/510** selections.
No gameplay or packet field was changed. The refreshed CA2244 report now returns
zero active findings; overall snapshot is **2,322 findings across 4,875 files**.

The eight CA1827 findings across five ranking/rune owners are repaired and
verified in both 754-case selections. The refreshed report now shows **2314**
findings: the eight selected records are gone, in addition to the earlier five.
Only the named findings were deeply inspected; this is not a full debt audit.
Another15 CA1865 character-overload fixes are verified in private/public Release:
**1112/1112** selected core tests and **301/301** complete updater tests, all green.
The78 new controls retain path/comment/status/UTC/URL decisions and actual parser
outputs; no measured speedup is claimed. One held Guild-persistence finding is
excluded from this batch. The newer remote snapshot confirms these15 absent.
Report: https://www.codefactor.io/repository/github/vgcman16/wolfconquer/issues

No blanket warning suppression or exclusion is authorized. An A grade is not
gameplay completion; retain behavior and protocol tests through quality refactors.
