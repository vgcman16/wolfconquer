# SAVED-TRADE-1: two-player saved trade observation

## Identity and scope

- 2026-10-29, operator testing tools; Root qualifies and integrates.
- Current owned inspector schema1, with comparison digests and explicit saved
  Hitpoints/MinMana/MaxMana. Client/profile and live execution remain pending.
- Compare two initial and two later genuine saved reports against one retained
  explicit intent. No send, grant, save, client control or source-check command.
- A successful result is `SavedTradeObserved`, with process exit2 at the runner.
  This is an editable local observation, not authenticated gameplay attestation.

## Packet contract

Not applicable: no packet parsing or transmission. Existing real client/server
trade routes remain unchanged. No currency amount here authorizes a trade or
asserts a gameplay exchange rule.

## Catalog/table contract

No new catalog or proprietary data. Item UID/type/raw UInt16 stack values come
from the actual inspector and explicit operator intent. Whole-row UID-preserving
inventory transfers only; split/merge/new UID outcomes are unsupported.

| Input | Type | Validation | Evidence |
| --- | --- | --- | --- |
| Intent schemaVersion | integer1 | exact version, unknown/duplicate keys refused | local contract |
| evidenceId/domain | owned evidence leaf / nonempty lowercase GuidN | retained evidence identity, same domain for all reports | existing runner |
| participants | exactly2 rows | distinct UInt32 account/character IDs, signed integral gold/CP deltas within Int64 range, pair sums0 | actual report widths; expected observation only |
| items | up to40 rows | globally unique positive UInt32 UID/type, raw stack0..65535, exact opposite participant account IDs | inspector item DTO |
| beforeSha256 | exactly2 uppercase SHA256 strings | caller compares original report envelope bytes in participant order | existing retained evidence pattern |
| report files | Configuration/Character/Items/Spells/Proficiencies/Mailbox | complete stable images; absent sidecar has null digest/zero length | inspector FileEvidence |

The initial external selection plan has exactly `schemaVersion`, `participants`
and `items`. Its participant rows omit `characterUid`; that value is filled from
the two real initial inspector reports. The runner copies the plan create-new,
retains its bytes/hash, and creates the final intent with the actual before
envelope hashes. Resume/status use retained bytes and never re-open an external
plan or treat editable hashes as authentication.

## Validation and transaction rules

1. Strict UTF8 JSON, duplicate-property rejection, exact intent members and the
   retained byte hash are checked before consuming the intent. These hashes are
   comparison evidence, not signatures or execution authority.
   All new trade numeric fields and consumed report numeric fields require JSON
   integer representations preserved as bounded integral CLR values. Floating,
   fractional or exponent forms are refused even if they round to an integral
   value. Full Int64 integer literals (including values above2^53) remain exact;
   no Double-to-integer cast can establish an expected delta or report amount.
2. Each report must be complete, with all physical item/mail rows, exact top-level
   container counts, unique nonzero item UIDs and required numeric saved vitals.
   Across the two players, physical UIDs must also be unique at each phase.
3. Require the same explicit domain, participant order, account/character identity
   and Configuration image in all four reports. Preserve map/coordinates/body/
   class/level and all three selected vitals; no inferred optional vital default.
4. Every selected UID must start exactly once in the stated sender's top-level
   inventory and end exactly once in the receiver's top-level inventory, with
   all summarized fields unchanged. Other physical rows remain with their owner
   unchanged. Inventory/physical counts change only by selected physical rows;
   equipment/warehouse/spell/proficiency/mail counts remain unchanged.
5. Gold and CP change by exactly the explicit signed deltas and conserve pair
   totals using decimal arithmetic. At least one item or nonzero wallet delta is
   required; an unchanged pair cannot qualify as a trade observation.
6. Unrelated mail rows and Configuration/Spells/Proficiencies/Mailbox image
   presence, length and digests remain identical. Character must be byte-equal
   when that player's wallets are unchanged. Items must be byte-equal when that
   player has no selected transfers; selected row movement must change its image.
7. Selected rows with unresolved deposited-child ownership or outstanding mail
   attachment references are refused as ambiguous. Item split/merge, opaque
   touched-image preservation and unreported fields are not inferred.

## Expected wire behavior and fixtures

- No wire-success claim. All live/wire/Store/atomicity/source gates stay pending.
- Synthetic data-only tests exercise the actual PowerShell functions with two
  directional item transfers, explicit opposite wallet deltas and unrelated
  equipment/warehouse/mail rows. Fault controls include duplication/loss,
  wrong owner/type/stack, wallet creation, unrelated changes, foreign identity,
  missing vitals/truncation, missing digests and malformed/changed intent.
- Runner integration independently retains raw before envelopes and intent bytes,
  revalidates hashes on Resume/status, and only invokes genuine read-only inspectors.

## Evidence alignment

| Requirement | Actual evidence | Status |
| --- | --- | --- |
| Complete item/mail projection | LegacyOwnedPlayerInspection.Report.cs / Items.cs | source-backed |
| Null vital refusal | LegacyOwnedPlayerInspection.ReadInspectionSavedVital | source-backed |
| Stable bounded image digest | OwnedPlayerFileObservation.Evidence | comparison evidence only |
| Live trade/atomicity | unavailable to this tool | pending |

## Implementation and qualification

- Comparator, runner/status and strict-number drafts compose in the final
  isolated package. Actual helper qualification:23 PS files parse without error;
  27 bounded pure/parser/retained-status/owned-fixture controls passed. Initial,
  unchanged Resume, expected saved UID/wallet Resume and counterparty status used
  Root's preserved full50cf DataTool against a genuine new offline owned fixture.
  Catalog14/guided compatibility also passed. No client trade was performed.
- The isolated successor from public committed e026be7f plus these22 paths passed
  offline local-cache restore and Release test-project Build01 with zero errors;
  no warnings point to the seven new C# files (157 existing graph warnings).
  The actual focused run passed117/117, zero failures/skips, in10m03s:62 new
  trade cases, including real registry-reserved two-way saved transfers and strict
  number/retained-status controls, plus55 existing runner/status/inspector cases.
  The composed23 PowerShell files parse with zero errors. No source defect needed
  correction in this qualification. Logs/TRX are retained in the owned handoff.
  No full CI, live/session control, active checkout edit, credential handling,
  UI, network or Git mutation occurred. Root integrates the complete batch and
  owns its combined full gate; local focused success is not that full gate.
- Touched Character/Items digests cannot prove preservation of opaque bytes;
  complete reported rows and untouched sidecars are the finite guarantee.
- Two independent saved reads are not one cross-player atomic snapshot, a live
  Store proof, transaction journal acceptance, SQL or power-loss atomicity proof.
