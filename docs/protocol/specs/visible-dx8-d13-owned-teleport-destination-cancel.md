# Owned teleport destination persistence and repeat cancellation

## Identity and scope

Revision1, 2026-10-31; Action260, existing7899/VisibleDx8D13. This is the
actual already-reduced destination publication phase, not a new teleport request
permission, terrain fallback or a claim of complete teleport atomicity.

## Packet contract

Keep existing successful leave/transition/visibility order. Complete the actual
destination file/native/holder/cancel phase before its leave or transition
packets. A later send/EOF failure does not undo location or create retry rights.

## Catalog/table contract

Reuse the actual existing teleport reducer and preflight/terrain owners. The
new phase never guesses a map or dynamic instance. Persist only the six actual
location fields Map/X/Y/PMap/PMapX/PMapY. DynamicID remains runtime context; any
old saved spelling/value is preserved. No unrelated HP/default-field rewrite.

## Validation and transaction rules

A private destination-only token binds the already-produced complete expected
and proposed sessions. It is minted only in the real destination publication
method; cleanup, Ride removal and generic Store writes cannot mint it.
Require the same actual Frame/Store/connection/binding/current lifecycle,
complete source maps and item/spell/proficiency graphs. The optional original
teleport identity must still match when supplied; no late foreign rebind.

Under actual lifecycle/connection/native actor gates, capture the configured
account Character file and complete before bytes. Validate complete lossless
encoding/Character schema and actual account-to-character UID plus saved source
location and scalar relation. Prepare only the six location replacements and
validate the complete image again. Atomically replace that exact captured file;
then the same private writer's exact committed pointer/whole afterimage permits
native location, holder and repeat-generation cancellation to publish together.
No second coarse Character write. A read-only/stale-before rejection preserves
the old repeat, stamp, file, native location and success output. Uncertain or
postcommit publication failure fences the actual owner; no file rollback guess.

This does not repair earlier independent teleport cleanup writes or make an
entire multi-phase teleport globally crash atomic. Rejected preflight/cancellation
ownership, broader teleport authorization and instance lifetime remain with their
real existing owners; any remaining gap stays active.

## Expected wire behavior and fixtures

Use a genuine admitted/armed basic physical actor, the real destination phase
and owned files. Verify before first leave/transition: complete location file,
native/Store location and canceled generation; no further repeat. Current-file
read lease rejects without cancel/output; EOF after committed output cannot
refund/replay. Preserve independent existing teleport codec/profile goldens.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
| Destination-only publication | Teleport.Destination reducer and TryPublish method | Existing source owner |
| Cleanup exclusion | Teleport.EntrySideEffects/StatusCleanup share old generic method | Deliberately separate |
| Cancel on teleport | Retained GameClient teleport/attack lifecycle | Actual typed participant join |
| Single file durability | Existing captured-image atomic replacement | No global multi-phase claim |

## Implementation and qualification

Lead owns destination token/writer/typed Store and external Character proof.
Source-only, unbuilt/unrun; Root reviews and executes. No7937 activation.
