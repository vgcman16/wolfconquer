# Continuation query mutation ownership

Status: OPEN, source implementation only; not built or run by the author.
Target: VisibleDx8D13. This adds no packet layout or wire sequence.

This is the query mutation prerequisite for
[durable XP continuation](visible-dx8-d13-experience-continuation.md) and the
[level-up operation owner](visible-dx8-d13-level-up-operation-owner.md).
The original level-up order remains level/stat publication, actual equipment
query with `CallItems:false`, actual HP restoration, then final XP. A pending
marker is DATA, not permission to mutate or to claim query completion.

## Concrete ownership

Only the real `PreparedLevelUpRebuild` execution, after consuming its registered
continuation step or saved-admission purpose, registers a query invocation. The
registration pins the exact context, initial holder and publication scope. The
actual query entry point binds that invocation to one private
`LegacyEquipmentQueryExecution` instance. Copying its scope, retaining a plan,
or calling the entry point again cannot bind another execution.

Only that private execution computes and registers
`OwnedContinuationQueryMutation`. Each proposal pins its exact before and
concrete after holders, binding, pending marker, item image, raw attribute and
paid-nobility maps, query scope and actual epoch. A prefix before the epoch
exists is covered by the same registered execution; it does not manufacture an
epoch. Once the epoch exists, the final proof also captures the actual native
query snapshot. An empty proposal token has no registration or authority.

The private execution may use its own calculation functions. The dedicated
Store method receives only the registered proposal, never a function supplied
by a caller. The proposal remains current only while it is the active proposal
of that exact execution and while the real level-up owner, exclusive operation
lease, current holder, native binding and used metadata remain current.

## Store and file boundary

`TryCommitContinuationQueryMutation` reacquires the existing lifecycle,
package, connection and native actor gates in their established order. It
requires the actual Frame context and exclusive lease. It prepares the real
native/holder publication, compares the exact concrete proposed image with its
canonical result, and consumes the proposal once before the real writer.
Before registering an HP/MP change, the producer uses `BoundHitpointsInputs`
to prepare the same canonical scalar attributes as that writer.

HP persistence still uses the real bound HP writer and its exact before/after
file proof. Its continuation-specific file permit is issued by the Store
consumer; a pending marker or borrowed query scope is not a permit. The marker
is unchanged by these intermediate query mutations. A proposal with other
persisted scalar changes and no real writer refuses. Post-write proofs and
native/holder publication retain the existing failure fences. Output follows
the committed query phase and uses the existing team/self HP notifications.

The proposal does not change Spells, Items or Profs participants. Derived spell
changes require their existing genuine journal owner and a separately reviewed
pending-continuation join; they are not flattened into a Character-only write.
The ordinary nonpending query path retains its existing producer and writer.

## Remaining joins and verification

The generic Store/file pending-marker fences, dedicated consumer, admission
origin and continuation driver are separate integration work. A normal pending
startup query still refuses without a distinct genuine admission startup
purpose. Derived-spell journal participation remains held. These source files
do not activate either missing owner or establish a complete restart path.

Required integrated checks include a genuine awarded continuation completing
its query and HP restore, interruption at intermediate HP writes and output,
configured restart without duplicate credit, and refusal of unregistered or
reused proposals, borrowed scopes, stale holders/epochs and changed native
owners. Negative tests may construct inert tokens; positive tests must enter
through real award/admission ownership. Root owns compilation, execution and
qualification. No successful query, crash atomicity across all phases,
exactly-once wire delivery, or live-client acceptance is claimed here.
