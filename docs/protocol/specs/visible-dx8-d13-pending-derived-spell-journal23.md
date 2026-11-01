# Pending XP participation in derived spell journal23

Status: OPEN source implementation; author has not built or run it.
Target: VisibleDx8D13. Journal version and client packet formats are unchanged.

This extends the existing real derived-spell query transaction, rather than
granting a generic Character writer an exception for an XP marker. The actual
private equipment-query execution issues its registered derived preparation.
When pending XP exists, that same execution also registers a participant tied
to its exact invocation, epoch, current holder, concrete reconciliation model,
pending state and allocated stats. A copied query scope or constructed token
does not issue participation.

The participant binds once to the actual `LegacyPreparedDerivedSpellQueryWrite`.
That writer retains the real selected catalog proof, native query snapshot,
saved spell owner, complete Character and Spells images, readonly Items image,
configured roots and real journal owner. It consumes participation once before
publishing journal intent. The existing lifecycle/package/connection/actor and
sorted file-lock order is retained. Pending publication is admitted only by
this writer's concrete `Publication`, not by a callback or standalone bytes.

Every journal boundary continues to require the actual prepared writer and
exact targets. Pending Character images additionally retain the exact decoded
continuation and raw protected UID, level, XP, class/rebirth, allocated stats
and HP/MP lines. Account file ID and character UID are independently joined.
Malformed, duplicate, removed, inserted or changed markers refuse. The same
DATA check runs in version23 image validation before configured cold recovery.
Full fixed-recipe validation, readonly item custody and account mapping still
apply; this DATA check alone is not a recovery permit.

The transaction remains Character followed by Spells with its existing stage,
replacement, rollback and owned-startup recovery rules. Known rejected writes
publish no spell success. Unresolved journal intent blocks the live owner and
keeps recovery evidence. Configured recovery completes the recorded pair while
leaving pending XP unchanged; it does not assert equipment-query completion or
restore HP. The actual continuation later reruns the query and resumes its own
phase protocol. Wire replay is not globally exactly once.

The private participant stays current through the existing native/holder
publication and spell notification, then retires when that execution leaves
the derived phase. The writer's raw-map proofs preserve opaque case-distinct
fields instead of normalizing all keys. Known precommit malformed DATA now
uses the existing preparation-refusal branch explicitly.

The second startup-query producer is also implemented as a one-use successor
of the actual first completed startup receipt. It captures current Character
bytes under the file coordinator and keeps the original admission lineage;
it does not refresh the old admission origin. Only the actual conditional
Revelator callsite may use it, under the same retained login operation.

DATA tests cover actual derived reconciliation preserving a pending marker,
protected scalar/vital drift, account/UID mismatch and malformed/duplicate
presence. These are not positive runtime-journal fixtures. Actual earned-XP
query interruption, configured restart twice and output/native invariants still
require Root qualification. Pending login activation remains separately held
for full detached-candidate feasibility and login spell-publication succession.
