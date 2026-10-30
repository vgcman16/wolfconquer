# Warehouse canonical continuations

Revision 1, 2026-10-30. Root-approved source-only integration repair.

## Target and observed gap
VisibleDx8D13 real Store/native item-graph publication may return a canonical WorldSession and item snapshot with distinct identity from the proposed record. Warehouse item transfer, inventory sash upgrade and warehouse lock refresh currently discard TryCommitSession out-final, then stop delivery because the proposal is not the actual published holder. Focused03's seven warehouse security controls observe persisted changes but zero frames.

## Required owner and proof separation
Capture the actual successful TryCommitSession canonical receipt and use its exact identity for all postcommit current-owner and delivery pins. Keep each original prepared candidate snapshot and LegacyItemSaveGraph proof separately; do not recapture a graph after IO, alias authority by UID/value equality, or replace a proof's source identity with the canonical snapshot. Joined delivery must also require the actual current binding/native graph to remain fresh. Preserve genuine existing managed Store/file paths without creating fake bindings.

## Successful behavior
Retain all precommit owner/PIN/trade/recovery/file validations and existing real writers. Prepare frames before persistence and retain their order. Lock refresh sends the original locked Warehouse chunks, then exact ordered unlock/item/extra followups after durable publication. Sash transfer and sash upgrade retain their original successful frames. A changed/retired owner or changed prepared source stops remaining delivery without refund/replay. No new opcodes, layouts, resource policy or PIN bypass.

## Bounded callsite audit and validation
The three TryCommitSession calls in warehouse partials are ItemTransfer, SashUpgrade and Show.Refresh.Commit; all must consume out-final. Root alone runs existing real Frame/native/managed persistence tests and qualifies/integrates. No helper execution, SQL, client/services or Git actions. Existing focused failures, authored source and static review are not passing/live/full-parity claims.
