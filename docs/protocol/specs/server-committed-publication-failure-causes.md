# Committed publication failure causes

Revision2, 2026-11-01; proposed FSB289. SOURCE ONLY, UNBUILT/UNRUN.
Rebased into a separate intake from Root's current proc qualification candidate,
including its real managed Character/item/spell receipt and owned proc/HP joins.
The prior8996 intake is historical and remains unchanged. This correction does
not activate a client, start a host check, replace newer source context or claim
that the prior full-suite failure has been cleared.

## Observed defect

Store single/pair persistence, prepared mailbox persistence and spell autosave
catch LegacyCommittedSpellPublicationException and call a fencing method which
throws a new parameterless exception. The original rethrow is unreachable, so
the original persistence cause/stack is lost. The current fence also stops its
remaining cleanup if an earlier block/retirement throws.

The native joined-spell saver also forwards both a typed committed exception and
an arbitrary postcommit exception through a no-cause native mutation lease method.
Retain those primaries through the real lease forwarding path as part of the
same correction; a generic postcommit exception remains the original inner cause
of the typed committed failure, with its original stack.

## Required behavior

- Preserve the existing committed/uncertain failure classification and diagnostic
  reason strings. Never convert uncertainty to a clean false/refusal, refund,
  accepting publication or unblocked owner. Keep all existing actual owner/gate
  prerequisites and perform no new file writes or rollback.
- Pass the actual caught committed exception into the same real Store fence.
  If cleanup succeeds, rethrow that exact exception with its original stack.
  Existing no-cause callers continue to get the same typed recovery exception.
  Native mutation lease forwarding retains an already typed primary directly;
  it wraps a generic actual postcommit primary once, with the original as inner.
- Attempt each required real recovery block and holder retirement independently.
  In the pair path, attempt both blocks and both holder clears. In prepared
  mailbox failure, preserve the package recovery block as well as the binding
  recovery block and retirement. No fake cleanup callback/owner is introduced.
- Preserve primary and all cleanup failures in execution order. Keep an outer
  LegacyCommittedSpellPublicationException when aggregation is required, with
  the primary exception first in its inner aggregate. A sole primary retains
  identity and original stack. Do not remove any retirement/block obligation.
- Add only the required cause constructor and a small dedicated Store partial;
  move the existing single/pair failure implementations there. Inspect every
  caller, update the actual catch-and-fence sites, and preserve no-cause calls.

## Verification

Use real admitted saved/native owners and actual Store single/pair boundaries.
A declared throwing persistence callback exercises exception propagation, not
proof of a completed disk transaction. Assert exact primary/cause/stack, all
participant recovery blocks and retirement, no native/holder success, full saved
file preservation where no writer ran, and no success output/repeat. Tests must
not fabricate accepting save owners or describe the component as a real crash.
Exercise genuine available cleanup faults only if the real owner supports them;
do not add production injection APIs merely to create a test.

Retain existing transaction, prepared mailbox, autosave and DemonBox neighbors
for Root qualification. No client wire fields change. This does not fix item/
world atomicity, fresh-request deduplication, SQL or live-client acceptance.

Root alone reviews/integrates/builds/tests/commits. Helpers author assigned
isolated source files only; no compiler, host, Git, live, routes, original saves,
mail, backup or antivirus action. Record actual execution before a fixed claim.


## Rebase scope and compatibility

The corrected exact publication scope is12 paths:9 production,2 test and this
spec. The earlier combined manifest omitted the writer path even though the
producer scope contained it. The new rebase manifest includes it explicitly.

Only the failure helper at the end of WorldSessionStore.SpellPublication.cs is
moved; current Dragonhowl death constructor checks and its native HP writer joins
remain intact. LegacyPreparedSpellFileWrite.Writers.cs keeps the actual
ManagedCharacterCommit.Create preparation and committedManagedCharacter pointer
publication. Its only correction is the uncertain replacement catch: retain the
actual writer/verification error, and retain a subsequent failed baseline
observation second if both operations threw. Known unchanged images still return
false with the existing ordinarySpellWriteFailed reason.

The added LegacyCommittedSpellPublicationException(Exception) constructor is
compatible with the existing DemonBox intake. Integrate that exact additive
constructor once; do not duplicate it when the two batches meet.

Existing tests are retained on actual admitted owners. The pair test now also
throws/captures its declared inner cause in a no-inline named method, so its
original cause stack is an explicit regression assertion as well as identity.
No production cleanup injection point, accepting save result or simulated owner
was added. Five authored cases cover single/pair and native lease forwarding;
they do not execute a completed disk transaction or independently failing
cleanup. The available real FileShare-based unchanged-writer refusal neighbor
remains relevant. Uncertain changed-image and dual-observation-failure writer
branches, prepared mailbox/autosave exceptional paths, cleanup-fault aggregation,
full integration and live acceptance remain execution-unproven.

## Root focused qualification - 2026-11-01

Build12 passed0errors/162warnings1m09.61s. All5new declared-failure controls passed
in focused13 (124P/12F); the12 unrelated startup-observer failures then passed
in17 after requiring real absent-before Profs stays absent. No completed disk or
cleanup-fault injection proof is claimed. Full CI/mailbox/autosave exceptional
paths and cleanup-fault aggregation remain open.
