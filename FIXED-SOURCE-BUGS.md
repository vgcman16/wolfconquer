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
| FSB-014 | Active managed title receiver changes memory before save success, mishandles wing4001 and untrusted subtype/catalog fields | Validated save-before-session commit, authoritative metadata and lifecycle-bound publication | Implementation and failure-proof tests in progress |
| FSB-015 | Repeated ranking scans crash on invalid rows; Rune.Free can reopen occupied211 after byte-count wrap | Single current-owner queries and fail-closed capacity/occupancy checks | Affected queries fixed; broader owner audit open |

## CodeFactor cleanup

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

The next eight CA1827 findings across five ranking/rune owners are locally
repaired and verified in both 754-case selections. Only these named eight were
deeply inspected from the report's first page; other findings remain queued.
Remote reanalysis is pending; do not subtract eight speculatively.
Report: https://www.codefactor.io/repository/github/vgcman16/wolfconquer/issues

No blanket warning suppression or exclusion is authorized. An A grade is not
gameplay completion; retain behavior and protocol tests through quality refactors.
