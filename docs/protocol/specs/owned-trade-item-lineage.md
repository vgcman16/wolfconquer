# Owned trade item lineage across canonical refresh

Source correction,2026-10-30; VisibleDx8D13. Authored before implementation.
Root alone executes qualification and integrates. This is not live-client proof.

## Defect and preserved boundary

Trade offers currently pin LegacyItemSaveGraph.Closure.Source DTO references.
The actual owned item join isolates every canonical saved row when another item
changes. An unchanged native root Node is deliberately reused, but its canonical
DTO row is copied. An unrelated potion use therefore incorrectly cancels an offer.
Sharing mutable DTO rows again or weakening global Closure.MatchesSource is wrong.

## Closed real issuer

Capture a bound lineage only inside the existing actual offer TryCommitPair
callback, under the real connection/actor/item owner gates and exact current
Store holder/binding. Anchor the original closure row to the actual published
Image.FinalSnapshot inventory row and retain the genuine native root Node,
Store, holder, binding and physical graph. Missing/stale bound evidence refuses;
no UID/value-only fallback or caller-created lineage is accepted.

Finish all callback-bearing tradability/relation reads for both participants,
then revalidate both bound lineages in a pure pass. Under the actual pair writer
gates this includes complete physical graph membership, not only slot identity.

At confirmation and the actual final persistence validator, consume that same
lineage against the exact current expected session, actual canonical inventory
row, retained holder/binding/graph and the same native Node. Whole root/child
lineage matters. Changed/remapped/recreated roots cannot regain an old offer by
reusing identical UIDs or values. Raw public-field corruption remains fail-closed
under the existing complete native owner proof. No shared mutable enumerators.

Original offer bytes/closure copies remain preparation data. They are never
installed as new canonical rows. The wholly unbound managed path keeps the
original exact Closure.MatchesSource contract without relaxation.

## Existing safety remains

Keep current PIN/participants, tradability/locked-partner checks for every child,
inventory capacity, global root/child collision checks, currency/escrow, exact
raw-file baselines, actual pair WAL, final Store publication and replay rules.
No permission, fake owner, accepting persistence callback, UID issuance or new
delivery claim is introduced. Postcommit sends remain delivery-unknown on failure.

## Controls

Retain all three actual owned health/mana/full-restore trade positives, including
offered-potion refusal and independent wire/file/resource assertions. Replace
their obsolete post-refresh DTO-Same claim with exact native lineage plus deep
canonical isolation. Retain all unbound item-closure, children, WAL and replay
goldens. Add actual registered warehouse out/back controls for identical-value
inventory removal/re-add and location change/revert, with genuine UID and child
ownership preserved; the old offer must refuse/refund. Add actual owner retirement
and detached old DTO mutation controls. No new UID is fabricated for those moves.
