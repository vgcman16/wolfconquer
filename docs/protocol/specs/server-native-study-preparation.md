# Native study preparation and publication

Status: authored source specification; no managed build, tests, live client, SQL or deployment proof.
Target: existing native 7899 / VisibleDx8D13 source prerequisite, not a new-client acceptance claim.

## Evidence and boundary

The actual component source is LegacyNativeSubClassRuntime.Study.cs. A source-only call map finds AddStudyPoints, RemoveStudy and UpdateStudyPacket used only within this component; no production receiver activation is added. The original Add awards points and queues Talk before null stream/view failure. Two existing tests preserved that defect. Player.SubClass is real but is not born by this change; study fixtures bind the genuine constructed runtime explicitly.

## Inputs and ownership

Require actual client, its current Player, Player.Owner identity, Player.SubClass identity, View with its actual client Owner, and Map.View when Map exists. Capture actual UID/map/coordinates/dynamic identity, map/view bindings and visible player recipients. Lock the existing client and player SpellAdmissionSyncRoot gates, then the runtime study gate. StudyPoints becomes a stable gated UInt16 property, retaining persistence setter and numeric wire semantics. Recheck graph, resource revision, physical target and seal before commit. No placeholder owner, fake callback, domain clone or accepting queue is introduced.

The caller exclusively owns its packet storage during the operation. Capture the full physical allocation and cursor/Size before preparation; invalid storage/capacity fails before mutation. Public graph/packet fields have noncooperating writers, so this is cooperative serialization and explicit revalidation, not globally atomic graph ownership or crash-safe persistence. Map membership can change after its prepared visibility snapshot.

## Prospective frames and commit

Prepare complete Talk (2179), effect (2036; Effect=10, zf2-e300), and animation (2033; Animation=8) with the actual encoders into owned scratch allocations. Verify each prepared declared-length/trailing-seal combination against the captured native seal, rather than relying only on a final global seal check. Preserve native cap 65535, actual requested-amount Talk text, UInt16 animation value, optional native seal, valid byte goldens and order. Ensure both caller frames and the 1032-byte actual sender bound before renting actual native chat storage last. Retain the actual successful recycler identity and Reuse/Dispose semantics. If the pool returns the caller packet, dispose only the rental wrapper and restore its incidental Take/Reuse cursor change; never copy into the aliased packet or free caller storage.

Invalid rental refuses before gameplay state, caller bytes or network queue mutation. Taking an invalid native pool slot can consume that internal allocation slot; unchanged pool count is expressly outside this guarantee. No global pool reservation/reenqueue API is introduced. Preparation allocation is not a gameplay debit.

Commit prospective points once after all preparation and revalidation. Publish actual Packet sends: Talk, effect self then captured actual roles, animation self. Caller ends with the genuine animation frame and cursor 4. The byte-array sender is not used because it rewrites seals. Postcommit graph/publication exceptions are reported as a distinct committed-notification exception, never as rejection, rollback or complete delivery. Actual NativeSend can silently drop/swallow transport failures, so successful return is not delivery proof.

## Preserved unknown rule

RemoveStudy retains the existing strict StudyPoints > cost comparison, including equality no-op and zero-cost positive-balance notification. The intended gameplay admission comparison is unverified by client evidence. No guess or policy change is made. A no-op remove does not require unused notification owners.

## Validation plan

Retain all independent successful frame goldens and four Add/four Remove/one Update outcomes. Replace both partial-commit controls with whole state/caller/queue preservation. Add real binding/capacity/rental failures, cooperating concurrency and a deterministic postcommit rebind control using the real sender gate and queue. Root must build/run the combined candidate; authored tests alone establish no passing result.
