# SQL acquisition and transport cleanup ownership

## Identity and scope

Revision1, 2026-11-03. Close the lower cleanup gaps recorded as FSB-350 before
activating SQL-backed gameplay. Source is not qualified until its actual checks
run. This extends [connection custody](sql-connection-custody.md), preserving
authentication, transaction and transport provenance rather than issuing a new
gameplay capability.

## Packet and catalog contracts

Not applicable: no opcode, wire format, encoding, response, data table or routing
configuration changes. Ordinary native DH, dispatch and listener behavior remain.
Cleanup evidence does not establish 7937 protocol semantics or client acceptance.

## Raw SQL acquisition

When authentication/world acquisition has not returned an owner, its actual raw
connection still needs an owner if disposal fails. Retain that exact resource in
a disposal-only exception with retryable asynchronous cleanup. Do not manufacture
an acquired auth/world result, accepting callback or replacement connection.

Use a common internal cleanup contract (`ISqlPendingCleanup`) with only
`HasPendingCleanup` and `IAsyncDisposable`. The public resource-cleanup exception
exposes no resource, account authority, secret, SQL command or read/write method.
Successful acquisitions, refusal enums, actual queries and locking stay unchanged.
Non-cleanup errors keep their original object/type/stack when cleanup succeeds.

Handoff error propagation must retain nested actual pending cleanup even if the
handoff's own local resources close successfully. Preserve direct known-COMMIT,
unknown-COMMIT and never-attempted cleanup distinctions. Collect each actual
pending handle once and do not descend into a handle that already owns its nested
resources. The Frame retains only disposal ownership when no SQL custody returned.

## Actual TCP retirement

The concrete TCP producer must attempt all safe cleanup steps after its pipeline
unwinds. A failed EOF callback cannot skip reader completion, replace the original
failure or abandon later cleanup. Preserve all failures in stable order and retain
sole-failure stacks. Do not await the world barrier while holding an incompatible
publication/send gate.

Failed retirement belongs strongly to the actual server instance through concrete
entries holding the real pending connection/reader references. Those entries are
created only by the producer's failed cleanup, not by a public registration API,
caller-supplied record or accepting callback. A weak-key Frame table or completed
task by itself is insufficient after the transport task retires.

Observe faulted client tasks before removal. Explicit retry and server shutdown
use the same actual pending entries, attempt every entry and retain failures for
further retry. Remove a resource only when its actual cleanup succeeds. Completed
resources are not needlessly repeated. Repeated/concurrent server disposal must
not cancel an already-disposed cancellation source or overlook an accept/client
producer that has not settled. Errors remain visible; a retry does not invent a
clean initial shutdown or make a historical failure disappear.

## Validation and fixtures

Ordinary failure-only witnesses may verify resource identity, error/stack order,
all-resource attempts, retry and concurrency. They establish no SQL/native owner.
Use a real socket-born producer fixture for retirement/task teardown and retry,
with bounded failure-only cleanup behavior and actual EOF. Existing owned SQL
provider coverage remains required for real acquisition/handoff positives and
complete six-image ownership. Physical provider Dispose-fault injection and
arbitrary process/transport races remain unproved unless actually exercised.

## Qualification and remaining work

Root reviews isolated helper scopes, integrates exact current preimages and runs
appropriate ordinary cleanup/auth/Frame/GM/transport checks, the complete owned
provider family and a single appropriate full Windows integration gate. Preserve
real primary/cleanup errors and all original session/data/route boundaries.

Retirement correctness is not backend activation. Durable normalized/Online CAS,
current native graph/Store publication, every reachable writer, global UID custody,
recovery and matching-client acceptance remain required before SQL gameplay starts.
