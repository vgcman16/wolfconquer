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

## Active audit

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
| FSB-009 | Rewards record claim/MAC before grants; partial failure loses claim; IP/MAC indexing differs | Validate full reward; atomic/idempotent claim and grant; correct identity checks | Open, reward execution held |
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
| FSB-023 | Flag registration/reset/publication can mutate before a null bitmap/view or later failure | Validate/prepare before coherent current-owner registration; duplicates reject without side effects | Isolated implementation in progress; not integrated |
| FSB-024 | Broader prestige rebuild still has legacy partial-publication, foreign-owner, duplicate-slot and arithmetic-wrap assumptions | Validate the whole genuine graph and prepare complete rank/guild state before coherent publication | Audit required; missing named-rune metadata is handled, not the whole rebuild |
| FSB-025 | RandomLite inclusive-range arithmetic can overflow or fault after consuming a draw; full-width sampling has limited domain and modulo bias | Bounded representable results, validation before draws and an explicit distribution/domain policy | Arithmetic bounds fixed; full-domain/distribution/fairness and concurrency remain open |

## CodeFactor cleanup

Latest published5a62e0d4: **8FIXED/3NEW**, **2272 total**. All seven older API
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
Remote analysis and full CI for this combined behavioral checkpoint are pending;
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
