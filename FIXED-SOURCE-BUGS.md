# Source bug fixes and audit

Engineering policy: [Sanitized Parity](docs/engineering/SANITIZED-PARITY.md).
Started 2026-10-23. **Open items below are not fixed or verified claims.**
Earlier passing tests may encode defects; test totals are not a safety certificate.

## Latest accepted integration gate — player-booth full Windows CI GREEN

[Windows40051408447](https://github.com/vgcman16/wolfconquer/actions/runs/40051408447)
passed exact public `b8cc5cfa12e1fa60c8a61b4478d12e5631309341`; job120744692469
completed2026-10-24T01:52:36Z. Receipt20261024T014836141Z-8091590e finished
01:52:26.9077483Z; all8 sequential stages exited0. Root read actual runnerHEAD,
receipt, terminal TRX counters/outcome groups and build/test/parser logs, then
verified completed GitHub success: core **24569passed/0failed**,165 existing
NotExecuted rows (24734 total), updater305/305, launcher57/57, synthetic parsers20+13
(no HTTP/service probes). Build191warnings/0errors,56.23s. All41new booth controls
pass alongside the retained24528core baseline. This is offline source integration,
not all-transfer/global UID safety, live-client acceptance, complete English,
deployment, full gameplay parity or a cleared CodeFactor backlog.

## Previous accepted gate — Trade closure full Windows CI GREEN

[Windows40048716539](https://github.com/vgcman16/wolfconquer/actions/runs/40048716539)
passed exact public `3f7a5a9f2d915e282b11cac0ff5e50f0fdef1634`; job120736279115
completed2026-10-24T01:18:09Z. Receipt20261024T011404291Z-3bd038af finished
01:17:59.7428817Z; all8 sequential stages exited0. Root read actual runnerHEAD,
receipt, terminal TRX counters/outcome groups, build/test/parser logs and completed
GitHub success before accepting: core **24528passed/0failed**,165 existing
NotExecuted rows (24693 total), updater305/305, launcher57/57, synthetic parsers20+13
(no HTTP/service probes). Build191warnings/0errors,54.51s. All58new Trade controls
are retained alongside the earlier24470core passes. This is offline source
integration proof, not all-transfer safety, live-client, complete English,
deployment, full gameplay parity or a cleared CodeFactor backlog.

## Previous accepted gate — magic admission full Windows CI GREEN

[Windows40046944516](https://github.com/vgcman16/wolfconquer/actions/runs/40046944516)
passed exact public `781cdf227e249744b068f06bab1adec1e6dad7ed`; job120730744913
completed2026-10-24T00:55:10Z. Receipt20261024T005058389Z-3e862afb finished
00:55:01.5416317Z; all8 sequential stages exited0.
Root read actual runner HEAD, receipt, TRX counters/outcome groups, terminal
build/test/parser logs and completed GitHub success before cleanup:
core **24470passed/0failed**,165 existing NotExecuted rows (24635 total);
updater305/305; launcher57/57; synthetic parser20+13 (no HTTP/service probes).
Build191warnings/0errors. This accepts shared/root/basic eligibility and the
generic/Recruit/Pounce behavioral batch offline, not live-client, deployment,
complete English, full gameplay parity or a cleared CodeFactor backlog.

## Previous accepted gate — quest/updater full Windows CI GREEN

[Windows40041857100](https://github.com/vgcman16/wolfconquer/actions/runs/40041857100)
passed exact public `999ce3262dcf5d6ac9c365af3670b542a7339952`; job120714544998
completed2026-10-23T23:55:01Z. Receipt20261023T235053615Z-3475fa2f finished
23:54:52.1499601Z; all8 sequential stages exited0.
Actual runner HEAD, receipt, TRXs and terminal logs were read before cleanup:
core **24340passed/0failed**,165 existing NotExecuted rows (24505 total);
updater305/305; launcher57/57; synthetic parser20+13 (no HTTP/service probes).
Build190warnings/0errors. This verifies the quest-loader/Frame and updater-pin
batch in offline CI, not live-client, deployment, complete English or full parity.

## Previous integration gate — runtime full Windows CI GREEN

[Windows40036685838](https://github.com/vgcman16/wolfconquer/actions/runs/40036685838)
passed exact public `c90122abc1961cef8c8f4a90f6dec623a3ddbaf8`; job120697981313
completed2026-10-23T23:01:24Z in4m21s. Receipt20261023T225718764Z-c6c587e8
finished23:01:13.5433267Z; all8 sequential stages exited0.
Actual terminal TRXs: core **24183passed/0failed**,165 existing NotExecuted
rows (24348 total); updater301/301; launcher57/57. Build190warnings/0errors.
Synthetic updater-status/parser controls20+13 passed, with no HTTP/service
probes. Root read actual runnerHEAD, receipt, build/parser stdout, TRXs and
GitHub terminal state before report cleanup. This is public source verification,
not live-client, deployment, complete English or full-parity acceptance.

FSB068 private focused proof is556/556,0fail/skip,5s, including both controlled
D11/D13 race cases now retaining stamina100 and unchanged old packet assertions.
The first public transfer5c4948d0 failed build40036236341 (190warnings/4CS0103,
no tests ran): root placed its manual-clock declaration in another test.
Corrected in publicd218770e; c90122ab merges only reviewed generated parity
assets from534c26ab. No force/reset/stash. An accidental pre-correction workflow
40036614525 is terminal canceled; its source is not the accepted checkpoint.
Earlier full40032574805 on0deb1e46 failed the old Reviver assertion; its exact
historical stamina value/interleaving remains unknown. Those failures are
superseded by this exact accepted run, not silently reclassified as passes.
No live,SQL,original-save,routing or deployment action.

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
  Windows40022478743 on exactpublic306ec9ec failed one native-readiness test:
  core24074passed/1failed/165existing exclusions(total24240),updater301/301;
  launcher/later checks did not run. Build190warnings/0errors. The saved failure
  lacks the full probe report, so historical cause is unconfirmed. A test-only
  diagnostic successor retains all14assertions and unchanged5000/1500/1700
  bounds while including complete actual stdout/stderr on failure. This is not
  a runtime repair, fixed-flake claim or full-CI acceptance.
  Private diagnostic/readiness selection passed23/23,zero failed/skipped,8s;
  the original failure did not reproduce. Full successor acceptance follows.
- D13 offline registered/component fixtures and owned files only. Private-method
  post-roll controls are not registered Epic/Frame/client proof. No live client,
  SQL, crash safety, coherent multi-file atomicity or full-parity acceptance.
  Fourteen equivalent xUnit2031 substitutions in eight test files are local
  hygiene, not a verified CodeFactor portal finding reduction.

- Full Windows [40024325826](https://github.com/vgcman16/wolfconquer/actions/runs/40024325826)
  passed on exactpublicfdec04667add46095956374371ab5faddd3f6af4 in4m40s,
  jobcompleted2026-10-23T21:12:50Z. ActualterminalTRXs:core24075passed/0failed,
  165existing external-fixture exclusions(total24240);updater301/301;
  launcher57/57. All8stagesexit0,build190warnings/0errors. Receipt
  20261023T210825276Z-b839ee31 finished21:12:43.7712151Z. Root read actual
  runnerHEAD,receipt/stdouts/all3terminalTRXs andGitHubjobstate. Parsers20+13
  are synthetic,noHTTP/serviceprobes. The readiness case passed4.897785s;
  earlier40022478743 cause remains unconfirmed,not a proven fixed flake.
  This accepts the coherent offline source batch,notSQL/live/deployment,
  globalUIDactivation,whole transactions or fullgameplayparity.

### FSB-064: Require a real positive Deityland chest key

- Owners: chest-local dialog/admission/debit in Deityland and ChestKeys.
  Shared legacy helpers treated zero stock as one, allowing empty-key rewards
  or charging an empty row instead of later real stock, falsely freeing a slot.
- Dialog and unlock now select the same first matching positive row. Stage
  exactly one unit and its existing removal/update frame from that physical row;
  zero rows remain unchanged and provide neither payment nor capacity.
  Preserve any-binding policy, enumeration order, Rare3303973/Epic3303974,
  one-key cost, random rates/rewards,200000CP and debit-before-reward frames.
- Native Inventory.Contain sums actual stack quantities; community defects are
  not client wire requirements. This is chest-local: other shared-helper callers
  and global graph/UID policy are not changed.

### FSB-065: Validate chest CP saves against the actual character UID

- Owner: chest call to the existing TryPersistLegacyCharacterAttributes overload.
  The convenience overload selected Users/{AccountId}.ini correctly but also
  treated AccountId as the expected character UID. Distinct identities caused
  uidMismatch after memory/output publication, leaving the old CP value on disk.
- Pass both the real account ID and session.Character.Uid to the existing
  explicit-identity owner. No filename convention, loader/auth rule, payout,
  parser, save coordinator or protocol layout changes.
- Shared verification for FSB064/065: tests-first67=31pass/36fail, then the
  disjoint8account cases=4pass/4fail; combined75=35pass/40fail. All25 retained
  chest cases remain;50 new cases added. Repaired private Release **184/184**,
  zero failed/skipped (75chest+95NPC-handler+14packet cases),
  .artifacts/positive-key-safety/results/chest-payment-green.trx, terminal exit0.
  Four distinct-ID positives verify complete actual account301/character1293600
  files and unrelated UID-named-file preservation; four negatives exercise the
  real lower persistence owner only, not whole-handler save-failure atomicity.
  Independent full [Windows40027179762](https://github.com/vgcman16/wolfconquer/actions/runs/40027179762)
  passed exactpublic05e0feb186495b77871c487e8918dda4c7001dd7 in4m55s,
  jobcompleted2026-10-23T21:34:45Z. Root read terminalreceipt all8stagesexit0,
  finished21:34:38.6025377Z (20261023T213005152Z-2aa361a9),actualrunnerHEAD,
  GitHubterminaljob/log andbuild190warnings/0errors. The next tracker checkout
  cleared local reports before terminalTRX/stdout counts were read; exact full
  core/updater/launcher counts are unavailable for this run,not extrapolated.
  This is independent offline CI acceptance,not live/deployment/fullparity.
- Explicit VisibleDx8D13 offline registered/component fixtures and owned files
  only. Post-roll CP calls are component proof, not registered RNG/client proof.
  English key-name evidence remains a SERVER export, not client-table proof.
  At this admission-only checkpoint, separate best-effort saves and whole reward
  commit/recovery remained open. FSB066 below supersedes that debt for Rare/Epic
  only; max+1/global UID, Victory/broader claims, delivery, SQL/live/full parity
  and canonical UID/Host activation remain open.


### FSB-066: Commit chest key/reward saves before publishing success

- Owners: Deityland chest transaction/preparation/publication partials and
  LegacyDeityChestPersistence. The old route published a charged key/reward
  before separate best-effort account/item saves; invalid or locked required
  files still produced success. Consuming a parent key could discard deposits.
- Corrected invariant: validate actual account-keyed Character and character-
  keyed complete Items preimages, native/current identity, positive key,
  deposits, capacity, metadata and wallet bounds before changes. Prepare packets,
  serialize under real Trades/current-session/file gates, commit both files
  through the existing v2 journal, prove afterimages, then publish. Unproven or
  pending outcomes fence real recovery. Keep valid any-binding keys, one-unit
  costs, random rates, rewards and packet order.
- Recheck captured current/native owner and connection-ended state before each
  send. Post-commit send/cancellation/log failures cannot refund or make payment
  retryable. No nested Shared-to-Exclusive world-barrier upgrade.
- Verification: tests-first91=75passed/16failed, then wider202=184passed/
  18failed (two deposited-parent controls added), zero skipped. Private Release
  **530/530 passed**, zero failed/skipped,5s, deity-transaction-green.trx:
  123 chest +95 NPC +14 codec +298 Trade/session/barrier/DragonSkin neighbors.
  All202 baseline names are retained and pass. Forty-eight new chest cases:
  16 required-file +2 deposited-cost +6 journal-boundary +4 changed-file +8
  recovery-owner +4 stale-owner +8 publication controls. Overflow and malformed
  metadata guards are source-reviewed, not separately tested here. Two retained Rare/Epic
  registered Script/Reply facts now use complete real files and ready recovery.
- Successor private Release **534/534 passed**, zero failed/skipped,4s:
  all530 retained plus four actual D13 Frame controls (chest127). Rare/Epic
  success and missing-item-file refusal complete under an independently owned
  shared reader, followed by an actual exclusive probe proving lease release.
  Complete dialog/reward/file/session checks remain; Epic tests its real chosen
  random branch, not exhaustive CP/item Frame outcomes. No accepting owner.
- Public Windows40031267716 FAILED exact14450da2 in4m28s: core24171pass/
  2fail/165NotExecuted, updater301pass; launcher/parsers did not run. Build190
  warnings/0errors. Receipt20261023T220650082Z-7d363ee5 finished22:10:53.9312898Z;
  jobcompleted22:11:02Z. Both failures precede handlers: the raw fixture used a
  private name instead of public's sanitized seed name. It now serializes the
  supplied name; full equality/assertions remain. Successor full CI pending;
  this failed run is not relabeled green.
- Limits: owned-file/registered/Frame component proof, not authenticated login,
  a real TCP listener/client, enabled runtime-loop lifecycle or deployment.
  Recovery covers fresh-owner replay of controlled journal prefixes, not an
  actual killed process, power loss, multi-process
  or uncooperative writers, durable outbox, ABA or every crash point. Current
  max+1 reward UID remains unsafe globally; canonical UID/Host activation HOLD.
  Victory/broader NPC claims, SQL, live/deployment/full parity remain open.
  No original saves, live client/server or routing were changed.

### FSB-068: Serialize player-tick and Reviver publication

- Owners: `WorldServerFrameHandler.RuntimeTicks`, `GuardReviverRuntime` and
  the new physical-connection publication lease. Both loops previously held
  Shared world leases: the player tick could prepare stamina5, pause its send,
  then merge5 after the global Reviver had published100.
- Tests-first: actual Frame login/SetLocation, real runtime delay callbacks and
  one held physical send reproduced final alive/stamina5 in BOTH D11 and D13:
  0passed/2failed/0skipped (834ms behavior). This is a new controlled trace;
  the prior full-CI failure's exact stamina value/interleaving was not recorded.
- Corrected invariant: one per-physical-connection async gate covers fresh tick
  capture through sends/merges and fresh Reviver eligibility through its whole
  revive/animation/publication phase. A skip-only healthy prefilter precedes
  waiting; authoritative checks remain after entry. No Exclusive upgrade,
  world-wide exclusive loop, peer-gate nesting or packet-layout change.
- Real startup workers are registered by their actual returned Tasks. Bounded
  active-task/last-fault diagnostics let fixtures retain both initial pending
  workers and join those same Tasks before disposing their logger/files.
  This diagnostic registry is not a general stop/start fence or Host drain.
- Private Release focused proof: **556/556 passed, 0 failed/skipped, 5s**:
  retained534 chest/NPC/Trade/session/barrier cases,2 controlled race cases,
  4 actual lease cancellation/double-dispose/connection-isolation controls,
  12 ticker,2 merge and2 old Reviver facts. The old failing fact now advances
  actual registered timers after SetLocation; every original packet-order,
  animation, state and no-generic-attack assertion is retained.
- Limits: offline owned fixtures only; saved positive HP is checked, NOT saved
  stamina (not a current scalar-save key). Other runtime/Frame/raw writers,
  recipient delivery recovery, fairness and stalled dead/invisible candidates
  remain open. No general shutdown, live-client or full-parity proof.
  Full Windows successor40036685838 on c90122ab is accepted; see exact proof above.
  No live server/client, SQL, original saves, routing or deployment changed.

### FSB-067: Commit Victory chest quest claim and reward together (scoped verified)

Full Windows [40040302782](https://github.com/vgcman16/wolfconquer/actions/runs/40040302782)
passed on exact `df2881ce5911bab1ad6da9fc6a9400ec31ffc820` in4m19s. Actual
terminal results: core24299 passed/0failed,165 existing NotExecuted rows;
updater301/301; launcher57/57; synthetic parser20+13; build190warnings/0errors.
All8 local-check stages exited0. Receipt20261023T233344597Z-56e69ade finished
2026-10-23T23:37:39.5183865Z; job120709561180 completed23:37:48Z. Root read
exact runner HEAD, receipt, all TRX outcomes and terminal logs before cleanup.
This verifies offline Victory integration, not deployment or complete gameplay.

- Source-confirmed owner: TryUnlockLegacyVictoryChestAsync in
  WorldPacketHandlers.NpcEntrypoints.Deityland.cs. It publishes Finished quest
  3944 and a reward before separate best-effort item/quest saves, then sends
  success even after an ordinary save refusal. One saved half can lose a reward
  or retain a reloadable Accepted claim. Frame sidecar autosave is also separate.
- Required invariant: validate complete current account/item/quest owners,
  prepare exact claim/reward images and output, journal item+quest commitment
  before session/output, and reconcile interrupted claims without duplication.
  Retain valid objective100 threshold, random rewards, capacity and wire order.
- The prior shared journal supported Character/Items only. The private successor
  adds genuine v3 Character(no-op)+Items+Quests with an explicit configured triple,
  one shared pending slot, strict record/domain shape and retained v1/v2 bytes.
  No quests-as-items disguise, second journal, accepting saver or guessed root.
- Tests-first registered D13 proof: **2passed/4failed/0skipped**,822ms behavior.
  Both healthy NPCs passed; actual Items/Quests leaf locks, missing explicit
  Quest authority and durable-Finished/live-Accepted faults all failed the first
  exact-current-session unchanged assertion. Later file/output/retry assertions
  were not reached on those RED rows; no fixture/cleanup failure was reported.
- Private focused Release **690/690 passed,0failed/0skipped,5s**: prior556,
  6 registered Victory cases,41 real v3 journal/recovery,42 strict quest capture,
  36 retained v2 journal and9 retained quest-loader controls. All four fault
  cases now reach unchanged state/files/output assertions, repair/retry and
  same-session/file-reloaded replay. Generic journal controls cover all four
  durable prefixes, fresh recovery, unknown images, shared pending slot,
  malformed domains and a real late Quest replacement failure/rollback/retry.
- The coordinator validates complete account/items/quest captures, prepares
  every image/output, then uses real Trades -> Store persisted CAS -> ordered
  file locks -> v3 journal. It proves full postimages before Store/output.
  Complete malformed quest capture precedes permissive claim lookup.
- One retained old NPC positive now uses genuine full files/triple recovery,
  with all19 original assertions plus complete send-time saved-image controls.
  The two read-only Victory facts remain unchanged; default Rare/Epic fixtures
  retain their pair-only roots and Name-aware seeds. No reward amount/layout
  or daily reset rule was invented.
- Private successor Release: **717/717 passed,0failed/0skipped,5s**. This retains
  all690 cases above, plus6 postcommit/malformed-tail,15 native directory identity
  and6 actual D13 Frame startup/recovery controls. Send failure or owner retirement
  cannot reopen a committed claim; malformed complete quest tails reject unchanged.
- Actual Frame now receives precisely the loader's explicit PlayerQuestsRoot.
  Real handoff/login/SetLocation/NPC controls cover both NPCs, quest-file replacement
  refusal and same-login retry, omitted-root/no-sibling-inference refusal, recovery
  of a genuine interrupted v3 prefix before login, and unknown-image admission
  refusal preserving files/journal. No test-created session or recovery readiness.
- V3 physically rejects same/ancestor configured directories, including actual
  DOS/Volume-GUID aliases. Inspection is scoped to trusted ordinary directories
  and cooperating writers, not retained target/namespace ownership. Initial
 714/715 exposed metadata-only handles not pinning rename; adding directory-read
  access then exposed blocked child replacement (714/715). Final read+attributes,
  read/write sharing and no delete sharing passes BOTH original controls unchanged.
  Required list-directory access fails closed; no write/delete access is requested.
- Status: ordinary/cooperating-writer registered and Frame paths verified offline;
  public source and full Windows CI are verified above. This is not full Victory, hostile
  filesystem, actual-client, outbox, power-loss, all-raw-writer or global UID proof.
  Existing max+1 remains globally unsafe; canonical UID/Host activation stays HOLD.
  Hostile namespace retargeting, handle-relative replacement and whole-domain
  lifetime hardening are separate explicit debt, not silently claimed solved.
  No live/SQL/original-save/routing/deployment action.

## Active audit

### FSB-076: Validate and durably settle complete player-booth purchases

- Root-only booth admission allowed restricted deposited children, descendant UID
  collisions and malformed save tails. Independent session publication could
  overwrite an awaited ordinary writer or a stale item/wallet file; transferred
  children retained mutable seller aliases. Pending escrow could be overwritten.
- Reuse the existing complete save-graph capture; keep Trade's locked-partner
  permission separate from booth policy. Validate transferred roots/children at
  listing and purchase, including current Anima authorization. Nonzero Bound is
  forbidden; Inscribed2 compatibility and unrelated bound stock remain legal.
- Buy takes the real exclusive world-operation lease before lookup. Valid D11/D13
  Frame buys narrowly skip their outer shared lease. Prepare detached item rows
  and original six response frames, then use real participant identities, ordered
  pair CAS, baseline checks and the existing four-file v1 journal. Vendor mode
  requires absent escrow instead of using Trade's claim-clearing policy.
- Preserve 64-bit positive account-file IDs and 32-bit character wire UIDs. Recheck
  complete graph/attributes/permissions under the real commit locks. Send faults
  after commitment propagate without refund/resale; uncertain durable outcomes
  retain the journal and fence recovery rather than claiming unchanged files.
- Tests-first: initial7564 measured8passed/15failed of23. Settlement21158 measured
  12passed/25failed of37; exact TRX counters/outcomes were read. These are failing
  cases, not25 distinct bugs. First integration31702/78453 failed compilation on a
  missing Data import in the new journal partial; no test execution from those runs.
  The import is corrected. First executed integration47729 measured83passed/3failed
  of86: all37 new cases passed. Only two obsolete Bound2 acceptance assertions and
  the old D11 received-row alias assertion failed. Those now require rejection or
  detached full-byte equality; unrelated-row references, six packets, four files
  and exact economics remain. The pure planner's alias contract stays unchanged.
  Broad41883 passed **706/706**,0failed/skipped,9s; actual terminal TRX counters and
  outcome groups were read, including all41new booth cases. Four genuine registered
  PIN controls cover protected descendants, actual creation/activation and PIN
  creation after listing requiring current authorization at purchase. Registered
  Frame and sharing-failure/retry controls pass with retained Trade journal/recovery,
  vendor, item-loader, Warehouse and PIN regression families. Public full Windows
  40051408447 passed exactb8cc5cfa with24569core passes, as recorded above.
- Limits: offline cooperating-writer managed paths only. Live client, SQL, complete
  native host, global UID ownership, raw-alias/ABA, hostile filesystem, multiprocess/
  power-loss and durable notification delivery are not established by this batch.

### FSB-075: Validate complete Trade item closures before transfer or escrow

- Source: registered Trade Exchange/Persistence and Warehouse.Sash, corresponding
  native Trade/MsgWarehouse branches, and the complete PlayerItemSnapshot codec.
  Sash deposit accepts restricted children; the old Trade root-only check then
  transferred those children. Incoming descendant UIDs were also absent from
  collision checks, and malformed unrelated stores could enter new currency escrow.
- Fix: validate both complete seven-store save graphs directly, including unique
  positive identities, parser membership, one encoded child level, non-truncating
  32-byte UTF-8 perfection text and the existing 16MiB image bound. Do not bless
  malformed state by parsing a normalized/lossy serialization.
- Apply permissions only to transferred roots/children; retain the directional
  locked-item partner exception. Recheck captured physical/scalar owners and
  permissions inside genuine ordered pair/vendor/file commit boundaries. Check
  incoming descendant UIDs against all recipient stores. Detach incoming children
  and private persistence images; preserve unrelated published container references.
- Gold/CP admission now validates both current graphs before the real wallet/claim
  debit. Final malformed state retains escrow and resets confirmations; healthy
  stale/forbidden/colliding offers use the existing refund path. Shared cancel/
  disconnect persistence requires save validity, not preview width: Effect256
  refunds remain valid. Existing escrow writer, WAL, capacity rules and wire order
  are retained; no accepting provider, new UID authority or replacement journal.
- Verification: tests-only37423 measured13passed/33failed/0skipped of46 in2s.
  Many failures stopped at the first unwanted preview, not33distinct bugs.
  First production18841 measured45/1; the remaining test appended a warehouse
  row out of the loader's canonical order. Only that setup order was corrected;
  exact refund, reference, byte and wire assertions remain. Broad91557 passed233/233.
  Final30087 passed **245/245**,0failed/skipped,4s; actual TRX counters/outcomes
  read, including58new registered D13 cases and the old Effect256 refund control.
  Coverage retains journal/failure/restart/disconnect/escrow/transport, vendor
  persistence, Trade/TradeInfo/Warehouse codecs and all item-loader controls.
- Public full Windows40048716539 passed exact3f7a5a9f as recorded above.
- Limits: focused/offline managed and public integration proof, not all-trade safety.
  No live client/SQL/original-save/routing/deployment action. Warehouse transactions,
  arbitrary raw-alias mutation after checks, ABA, global cross-player UID ownership,
  hostile collections, multiprocess/power-loss and durable delivery remain open.

### FSB-074: Require Pounce's selected shield before admission

- Matching Pounce.Execute returns before Shift/damage/animation when either
  CanUseSpell or the selected-left shield requirement fails. The old managed
  executor checked the shield only after spending50 stamina/arming800ms cadence.
  The actual source row12770/sort84 and unchanged shield900000..900309 predicate
  establish this required-cast boundary, unlike the independent Tharn proc.
- Generic plan construction now checks the existing equipment selector against
  its captured CURRENT owner before preparing cadence/costs. Main/alternate
  override and absent-alternate fallback rules are unchanged. Ordinary Store
  replacement is fenced by the existing exact-record/native-generation CAS.
  The late executor check remains after awaited publication; shield loss then
  is logged as an admitted stop/no refund, not an unchanged precommit rejection.
- Tests-only94577 measured5passed/6failed/0skipped of11,587ms before production.
  Five rows fail first unchanged-owner assertions; the receive-envelope row
  directly observes the unsafe50 vs100 stamina. All7 retained intentions remain,
  with4 new registered D13 current-vs-stale and alternate-fallback controls.
  After two production paths, Release63487 passed **2833/2833**,0failed/0skipped,
  17s; actual TRX counters/outcomes read, including complete Pounce family and
  the prior2829 selection. Successful empty casts, exact source animation fields,
  damage/progression and receive-envelope ordering controls are retained.
- Limits: raw aliased equipment changes are not versioned/locked by this check;
  postcommit equipment/target/HP/experience/delivery and full-cast transactions
  remain open. No new movement/target/RNG phase or wire format was added. No
  live client, SQL, original saves or deployment. Full Windows verification above
  now accepts this published successor batch.

### FSB-073: Prepare generic magic and Recruit admission before resource publication

- Generic preparation formerly committed AttackStamp before affordability, spent/
  sent stamina before mana rejection, then spent both before active-cold refusal.
  Recruit independently composed the same split resource/cold mutation helpers.
  Five production paths now prepare the complete generic cadence, priced costs,
  cold dictionary and Stamina -> Mana -> ColdTime frames before one genuine
  native-holder-generation/exact-record CAS. No-Store compatibility stays an
  explicit local record, not a fabricated transaction. Shared Root costs are not
  reused, and an already prepared Root invocation is not charged twice.
- Keep generic Scurvy/DragonPierce saturation, alternate MPMaster, map1039,
  Grace's sufficient-priced-mana/dual-epic rule, status cold bypasses, excluded IDs,
  cadence durations and successful wire order. Recruit still ignores cadence.
  The generic clock remains real UTC, sampled once for the coherent plan.
  Negative cold/cadence and preparation arithmetic/date/frame faults refuse
  before commitment. Zero raw mana skips only the irrelevant rune dependency;
  real stamina surcharges still apply. No accepting catalog/provider is invented.
- Postattempt observer/send/cancellation faults propagate with committed/unproven
  diagnostics, never fake rollback/refund. Prepared frames do not reprice or
  overwrite same-client callback edits; actual generation retirement stops later
  publication. This reuses the tested real admission owner/CAS, not a saver stub.
- Tests-only81329:60passed/27failed/0skipped of87,659ms, before production edits:
  20new failures plus7 corrected old defect assertions. Earlier48477 stopped at
  a test lambda `_`/discard name collision; no tests ran; root renamed only that
  parameter. After production, broad23155:2825passed/4failed/0skip of2829,19s;
  all87 generic/class tests and8 retained player-denial corrections passed.
  Four other old debit/stamp assertions were then corrected, retaining all7
  original intentions and physical positive/retirement wire controls. Corrected
  Release78100 passed **2829/2829**,0failed/0skipped,18s; actual TRX read.
- All135 external generic callers were source-audited. Most null paths stop the
  chosen cast, but physical FlashingName/cadence and some token prefixes occur
  earlier; the physical regression deliberately retains its real FlashingName
  output. Native Tharn follows the void spell attempt independently, so its
  affordability dependency is unproven: the proposed physical-tail change stays
  HOLD. Later executor/target/equipment/HP/receive/delivery, raw mutable graph,
  SQL, persistence, live-client and whole-cast transaction boundaries remain open.
  Full Windows verification above now accepts this published successor; test
  totals are not parity.

### FSB-072: Check basic player eligibility before root1000/1001 admission

- Existing Root1000/1001 player casts paid costs and ran manual effects before
  the real executor's deterministic eligibility denial. The source reference is
  CanAttackPlayer.Verified; successful wire formats/rates are unchanged.
- One shared pure block retains existing safe-zone, HP/Dead/Ghost, Invisible,
  NeptuneCurse, AttackInFly, watching, Protect, map700/dynamic0 and Peace checks.
  The full effectful gate calls it at its exact old position; root calls it before
  preparation and during final pure revalidation. CombatGear/rune/counter/RNG
  phases are never preinvoked or duplicated. StarFlow's denial-effect outputs remain.
- Tests-first63354:5passed/26failed/0skipped of31,1s: four healthy/permitted-flying
  controls and the corrected retained low-mana test pass;26 basic/manual-prefix
  denials expose the late admission. Only tests/declared1001 inputs were intaken.
  After four production paths, Release38082 passed496/496,0fail/skip,12s:
  prior401 +30new eligibility +1 corrected low-mana +64 StarFlow controls.
- The retained low-mana Fact keeps the same registered monster cast and costs,
  but now requires unchanged complete session/native/catalog/monster state and
  no output, rather than the rejected stamina debit. The exact one-test public
  CI failure is recorded in FSB071. Successor public230ddf34755598c72cdd9aa33405175f3de1d2f6
  full Windows40045106740 failed eight retained basic-player rejection rows
  that still expected mana493 rather than the unchanged500. Core24432passed/
  8failed plus165existing NotExecuted (24605total); updater305/305. Build191
  warnings/0errors; launcher and later stages did not run. Actual receipt
  20261024T002943194Z-f83e485b finished2026-10-24T00:33:29.8260301Z;
  job120724937902 completed00:33:39Z. Root read exact HEAD, receipt, TRX failures,
  logs and terminal GitHub status. Five retained methods/eight original rows
  now require exact caster/target state, untouched native catalog and no frames;
  their setups, rows, HP/animation and diagnostic assertions are retained.
  Their corrected successor is now verified by full Windows CI above; this
  historical public230 run remains NOT GREEN.
- Limits: only these basic player gates for root1000/1001. Relation/event/final-map
  policies, effectful counter decisions, changing targets/raw metadata and later
  execution/delivery remain open. This is not a target lock, whole-cast/SQL
  transaction, live-client proof or full CanAttackPlayer completion.

### FSB-071: Prepare shared root/CounterKill/Rise admission before commitment

- Reference: matching community MsgAttackPacket.ProcescMagic and
  AttackHandler.CheckAttack.CanUseSpell.Verified. The old local Entry normalized
  shared native cooldown rows, stamped and ran item effects before later refusal.
  Its shared CanUse spent stamina before mana refusal and both costs before cold
  refusal. Source also permitted negative/wrapped cold arithmetic.
- Tests-only RED58227:32 new registered root cases =8passed/24failed,0skipped,1s.
  RED11445 adds2 genuine direct Rise-consumer cases:34=8passed/26failed,0skip,1s.
  Most failures occur at the first unchanged-owner reference assertion; later
  full-value/wire assertions on those rows were not reached. Two caster cases
  directly fail shared catalog-value assertions. Production was unchanged.
- After complete caller/source review, root integrated nine focused production
  paths: pure local cadence/metadata/range/cost/MP preparation, actual holder
  generation plus exact-session CAS, then source-order prepared publication.
  Complete costs/deadline/stamp/token are staged before gameplay mutation.
  Optional MPMaster is skipped for zero mana; valid MP rescue remains intended.
- Old partial-debit/catalog-mutation tests are migrated without removing rows or
  successful wire goldens. Cost callbacks now see the complete admitted image;
  prepared notifications do not reprice or overwrite later same-client changes.
  Focused Release78562 passed365/365,0failed/0skipped,10s. The34 new rejection
  cases and331 retained cases pass. Additional34 metadata/zero-cost and2 actual
  Store component controls pass in combined74649:401/401,0fail/skip,12s. The first
  addendum attempt5522 stopped at one missing test namespace import (no tests
  ran); root added that import, not a production or assertion workaround.
  Public98d0962e full Windows40043985349 failed one old defect-enforcing test:
  HandlerMagicAttackSpendsStaminaBeforeLowManaReject required a stamina frame
  that correctly is now absent (Assert.Single at8505). Actual results:24409core
  passed/1failed,165 existing NotExecuted; updater305/305. Launcher/later stages
  did not run. Build191warnings/0errors. Receipt20261024T001614227Z-3011f1f7
  finished2026-10-24T00:19:53.142736Z; job120721335395 completed00:20:02Z.
  Exact HEAD, receipt, terminal TRXs/logs and GitHub failure were read. This is
  NOT full CI GREEN; the old assertion must be migrated without restoring the bug.
- The34 additional boundary controls include9 registered6003 cases and25 exact
  private-component sigil cases retaining real sorts90/21 and parsed metadata,
  not invented dispatcher routes. Real Store controls prove same-record reinsertion
  retires captured generation, and an observer FormatException after publication
  propagates with complete costs/deadline committed, not null rejection/refund.
  UTC maximum has exact accepted/refused representability controls; no all-zone claim.
- Limits: no-Store local compatibility is explicitly nontransactional. Raw
  in-place writers, outer Focused/Intensify/Ride policies, later target/effect/
  clock/executor faults, delivery, SQL and durable whole-cast safety remain open.
  Real draws are not rolled back; a postcommit fault is not an unspent rejection.
  CodeFactor originally reported complexity here, not this gameplay diagnosis.

### FSB-069: Bound quest structures before allocation and load before expiry writes

- Former PlayerQuestSnapshotLoader.Parse allocated the declared row array before
  proving headers fit; late invalid tails materialized earlier rows, and impossible
  intention sizes escaped as OverflowException. A complete allocation-free
  structural pass now bounds every count by actual remaining bytes before records
  are created. Materialization keeps defensive bounds; valid bytes/order/cache and
  the existing format range are retained, including a valid image above16MiB.
- CharacterSnapshotLoader previously restored/persisted expired nobility before
  quest loading, then let quest faults escape its structured result. Quest load
  now precedes that save and uses existing read/parse error statuses; failed
  snapshots are null. UID mismatch precedence and healthy/missing quest behavior
  are unchanged. All real production callers were inspected for these statuses.
- Tests-first current:26passed/13failed/0skipped,361ms. Four bounded allocation
  controls failed, three parser/loader overflow controls failed, five real
  character-byte preimage controls caught writes before quest failure, and one
  reload fault escaped. Later assertions on those RED rows were not reached.
- Private Release776/776 passed,0failed/0skipped,5s: retained717, new39, existing19
  Character loader and1 legacy-login error-admission control. This is a scoped
  loader repair, not a file-size cap, immutable borrowed-span guarantee, all-sidecar
  transaction, handoff rollback or new-character creation rollback.
- Successor778/778 passed,0failed/0skipped,5s, adds two actual D13 Frame admission
  cases over the existing real fixture: malformed quest count/final intention plus
  expired paid nobility yield exactly one SnapshotError, no session admission and
  unchanged complete User/item/injected-quest files. Real recovery is ready, not
  a missing-owner refusal. No new fixture/provider or ticket rollback claim.
- Public999ce326 and full Windows40041857100 are verified above: all41 added
  core cases retained alongside24299 prior passes and165 existing exclusions.

### FSB-070: Make updater parent-directory rename pins genuine

- ClientDirectoryJunction.DirectoryPins retained metadata-only0x80 handles.
  No-delete sharing alone did not stop tested ordinary link-parent/target renames.
  Parent pins now request list+attribute read0x81 with read/write sharing and no
  delete sharing. Missing list permission refuses; no fallback or elevation.
  Leaf create/delete access, exact-path/reparse checks and manifest logic stay unchanged.
- Genuine private ValidatePair lease tests-first:2passed/2failed/0skipped,95ms.
  Link-parent and target rename unexpectedly succeeded (restored before failure);
  target-ancestor and real child replacement/junction compatibility passed.
  After the two small source hunks, complete updater Release305/305 passed,
  0failed/0skipped,1m10s; all301 prior cases retained plus4 real controls.
- Scope: ordinary directory rename exclusion for that synchronous lease only.
  This is not a child namespace/reparse-metadata lock, native alias closure,
  hostile-writer defense, live updater deployment or whole-update safety proof.
  [Windows sharing rules](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-fsa/8c0e3f4f-0729-49f4-a14d-7f7add593819)
  distinguish metadata access from participating read/write/delete access.
  Public999ce326 and full Windows40041857100 passed all305 updater cases;
  exact shared receipt and remaining verification boundaries are recorded above.

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
| FSB-005 | Trade/booth/inventory disconnect, duplicate UID and retry windows | Validated atomic/idempotent transfer with durable reconciliation | Scoped managed Trade closure FSB075 and cooperating-writer booth settlement FSB076 verified offline; broader inventory/global/delivery lifecycle audit remains open |
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

Current guardrail: defect-first batches, automated bounded cosmetics, no risky
packet-switch rewrites for complexity scores and no per-warning full-CI cycle.
Trade3f7a5a9f and boothb8cc5cfa repair gameplay safety, not a verified static-findings reduction.
The current dashboard count was not read for this batch; older totals below are
checkpoint observations, not the current queue. Passing CI is not backlog closure.

Earth/rank fullCI40002265954 passed with212 warnings. Root batches its8 new
test-only warnings across4 files:6 equivalent xUnit predicate assertions and2
explicit positive-result null checks. No production or protocol change, no new
suppression/exclusion, no separate fullCI per warning. Public Release focused578/578
passes,zero failed/skipped,2s; build204warnings/0errors after the batch. This is a
test-only follow-up to the exact fullCI code snapshot above, not another fullCI.
Latest remote CodeFactor count was not read; compiler warnings are not that count.

Exchange public8abeec78 is full-Windows verified above. Its two mechanical new
findings are corrected in the SuperGuild behavior batch; the new semantic
handler's complexity is deferred. That checkpoint report was3NEW/1FIXED,total2303
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
