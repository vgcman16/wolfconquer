# Sanitized Parity

Effective 2026-10-23 by explicit user instruction. This policy supersedes older
instructions to preserve every decompiled behavior, exception or mutation order.
The full English client/server objective remains active; existing work is retained.
New feature, packet and catalog work also follows the [protocol workflow](PROTOCOL-WORKFLOW.md).

## Compatibility and authority

The selected target client executable and its matching data tables define the
protocol contract. Preserve confirmed packet IDs, widths, offsets, Protobuf fields,
encoding and successful message sequences. Record build/profile and evidence for
suspect structures. Do not guess opaque constants or change wire formats merely
because server code looks unusual. Use bounded binary/table inspection or an
authorized isolated client test when source evidence is insufficient.

The community emulator is a useful behavioral reference, not a specification of
correct resource accounting, authorization, object ownership or error handling.
Distinguish legitimate gameplay rules from defects. Document deliberate changes.

## Validate, prepare, commit, publish

1. Resolve authenticated ownership and validate all applicable inputs, targets,
   permissions, metadata, ranges, cooldowns, resource costs, locks and capacity.
2. Prepare immutable changes and required serialization/allocations without
   mutating live state. Rejection leaves resources, authorization, cooldowns,
   outputs, inventory and success-packet queues unchanged.
3. Commit the validated change coherently. Serialize competing operations and
   revalidate mutable balances/versions under the same ownership boundary.
4. Publish only committed outcomes. A notification failure after commitment must
   not be reported as an uncommitted rejection or permit a duplicate retry.

Spell admission alone does not prove a complete cast transaction: later executor
eligibility gates must move into preflight before admission spends resources.
Zero-cost actions must not query unrelated optional owners. Clamp/reject invalid
costs and reductions; never permit negative debits, refunds through overflow or
a malformed cooldown in the past. Preserve valid intended resource rules.

## Economy and persistence

- Validate item UID uniqueness, location, ownership, equipped/locked/tradable
  state, stack bounds and positive amounts on the server.
- Trade, booths, rewards and inventory moves need explicit transaction ownership,
  ordered participant locking, idempotency and disconnect/replay reconciliation.
- SQL multi-row changes require a real database transaction and constraints, not
  independent successful writes. Test failures at transaction boundaries.
- File saves need validated complete data and recoverable atomic replacement.
  Multi-file or database/file coordination requires a journal/reconciliation
  design; do not call a per-file replacement globally atomic.
- Parse/validate temporary loader state before publishing a coherent live graph.
  Preserve the previous valid state on parse or validation failure.
- Test rollback, crash/retry, duplicate submission and disconnect windows using
  owned fixtures before claiming the corresponding safety property.

## Null, ownership and memory safety

Reject invalid required constructor arguments clearly before creating an unusable
object. Initialize real owners at their valid lifecycle boundary; never fabricate
an accepting substitute. Optional absent systems must not break unrelated actions.
Validate packet/frame bounds and counts before unsafe reads, allocations or writes.
A recycled packet remains owned until all use ends; never use a returned buffer.

## Tests and source-bug records

Audit tests that intentionally require partial debits, dirty rejected outputs,
arbitrary null traps, privilege-by-name, duplicate item identity, partial graph
publication or lost claims. Replace defect assertions with rejection invariants,
transactional controls and positive valid-path coverage. Keep independent protocol
goldens. Historical source observations may remain labeled research evidence, not
acceptance criteria. Do not delete a failing test solely to obtain green.

Each fixed defect gets a FIXED-SOURCE-BUGS.md entry containing source/location,
unsafe behavior, corrected invariant, exact verification and remaining boundary.
Keep open defects separate from verified fixes. Do not promise a bug-free server.

## Code quality

CodeFactor cleanup is authorized for new and existing code. Triage actual current
findings, then fix readability, maintainability, complexity, duplication and other
actionable issues in reviewable batches. Verify behavior after refactors.

Prioritize genuine defect findings (null/lifetime/resource faults, unreachable
logic and invalid state) over cosmetic debt. Batch mechanical style fixes by
rule across a safely bounded explicit file set, using automation where suitable;
preserve unrelated/held edits. Do not turn every warning into a separate
edit-test-commit-full-CI loop. Use focused checks per coherent batch and full CI
for behavioral changes or a meaningful integration/publication checkpoint.

Do not split or rewrite a working protocol/packet switch solely to meet a
cyclomatic-complexity threshold when doing so risks message ordering, formulas
or runtime side effects. Defer and record low-value/risky cosmetic findings;
static issue-count reduction must not displace unfinished gameplay or safety
transactions. Zero CodeFactor findings would not prove gameplay completion.

Inspect every caller before changing a query's failure behavior or replacing a
complete scan with early exit. Returning false for corrupt state is not fail-closed
when a caller treats `!query()` as permission to mutate. Preserve the distinction
between valid absence/occupancy and invalid state, or validate the complete real
owner before the consuming transaction. Test malformed tails through those callers.

Keep failure diagnostics truthful: cleanup must not mask the original assertion,
skip or operation failure. Preserve primary and cleanup errors, attempt each safe
owned cleanup, and retain original stacks; do not merely move a finally-thrown
exception into a helper to hide the warning.

Do not hide problems through blanket exclusions/suppressions or weaken protocol
and safety coverage to improve a grade. Static quality and gameplay correctness
are separate measurements; record unavailable reports instead of inventing counts.

## Delivery boundaries

Keep code modular, retain rollback points and preserve unrelated work. The root
integrator alone stages/publishes reviewed changes. Offline tests use guarded,
owned temporary inputs, never original saves or live SQL. Existing client/server
sessions and the protected restore point remain unchanged unless the user
authorizes the relevant live step. Keep updates here/local as requested.
