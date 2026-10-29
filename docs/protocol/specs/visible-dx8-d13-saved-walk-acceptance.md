# VisibleDx8D13 saved walk acceptance observation

## Identity and scope
-2026-10-28, next isolated tooling increment assigned by Root against published80fa53c9.
- Existing login-walk-save scenario gains an executable SAVED-FILE assertion. It does not send packets, move/log in a client, save a live Store, restart services or establish client visuals. Existing11 scenario IDs, mailbox/equip flows and default source-check behavior stay unchanged.
- Root alone integrates, runs tests/builds/Git/live actions. This source has not executed.

## Packet contract
Not applicable: this tool only invokes existing inspect-owned-player DataTool on explicit configured owned paths. Resume never invokes grant/request/send/source-check commands. Live wire remains pending.

## Catalog/table contract
- Reuse schema1 LegacyOwnedPlayerInspectionReport and existing complete item/mail projections.
- File evidence adds backward-compatible optional ContentSha256, computed from the existing bounded observed byte image only after the existing before/after stability check. Present files report uppercase64-hex digest; absent files reportnull. No additional probes or build/source inventories.
- Digests compare the exact observed saved images, including bytes the projection does not interpret. They are local editable comparison evidence, not an ownership token, signature or proof of simultaneous live consistency.

## Validation and transaction rules
- Scenario requires explicit ExpectedMap(uint32>0), ExpectedX/ExpectedY(uint16 including0). Retain exact destination together with scenario/evidence/account/config/DataTool identity and actual report domain/characterUID. Other scenarios refuse walk-only parameters.
- Initial creates existing owned evidence root/owner/before/receipt using the existing create-only guards. Save a bounded strict walk-intent.json containing exact schema/evidence/domain/account/character/destination, hash the retained before snapshot and intent. Unknown/duplicate/malformed intent fields refuse.
- Initial report and every Resume must be complete, stable inspector results with same domain/account/character identity, liveStoreSynchronized=false, reported item/mail lengths agreeing with physical/active counts, no zero/duplicate row identities, and complete Items/Spells/Proficiencies/Mailbox digest evidence. Item container names are exactly inventory/equipment/warehouse/relicFuse/mythsoulBag/rune/wardrobe and each name's .deposited suffix. The three exposed top-level container counts match their respective rows; their sum need not equal the physical count because the other groups and deposits are included. Absent digest support is pending/refused, never counts-only acceptance.
- Require requested destination differs from the original map/X/Y. Resume accepts ONLY actual saved after map/X/Y exactly equal to the retained intent and different from before.
- Selected unchanged character projections: body/class/level/gold/conquerPoints. All reported inventory/equipment/warehouse/physical/spell/proficiency/mail counts and every reported item/mail field remain unchanged. Selected complete sidecar image presence/length/ContentSha256 must remain unchanged for Items/Spells/Proficiencies/Mailbox; opaque fields therefore cannot silently change while known projections stay equal.
- Initial/Resume do not debit, grant, send/control a client or mutate saved data. The operator/client performs the intended walk/save separately. Resume reuses original intent/evidence and never regenerates/resends.

## Expected output and fixtures
- Initial WaitingForSavedWalk exit2 with explicit destination/guided action.
- Matching changed saved destination plus all unchanged invariants -> SavedWalkObserved exit2, assertions empty. Wrong destination/unchanged position/state drift/truncated or missing digest -> pending/mismatch exit2. Malformed/foreign intent or retained identity mismatch -> existing stopped/unproven exit1.
- Every result keeps LiveClientVisualPending, liveStoreSynchronized=false, wire/source-gate pending. The narrow saved result never becomes ServerStateVerified or a whole-game PASS.
- Read-only status supports WaitingForSavedWalk/SavedWalkObserved only with matching retained references/schema. Before displaying RecordedSavedObservation it rechecks the pure Test-GaWalkTransition over retained before/after reports and intent, including unchanged currency, rows and sidecar images. It invokes no inspector/source jobs and does not authenticate editable records. Existing saved statuses remain unchanged.
- Tests invoke ACTUAL PowerShell runner/status with existing harness and real owned fixture/inspector DataTool. Quiescent test file edits establish saved-schema test inputs only, never gameplay movement proof. Cover a positive with all seven groups and their deposits, unknown container refusal, wrong destination, unchanged position, foreign identity, truncation, missing digests, malformed/foreign/duplicate intent and no-send Resume. Status negatives retain valid hashes/references while changing currency or a valid-format sidecar digest, and must remain Invalid.

## Evidence alignment
- Existing paths: tools/testing/Invoke-GameAcceptance.ps1, GameAcceptance.Common.ps1, GameAcceptance.Status.*; Core LegacyOwnedPlayerInspection.Report/Files; actual DataTool inspect-owned-player.
- Source-derived position fields are saved Character.Map/X/Y; gold/CP and sidecars come from the real inspector. No new packet/schema/cost inference.

## Implementation and qualification
- Isolated helper-owned-walk-acceptance-next postimages with exact MANIFEST.txt and HANDOFF.md.
- Root qualification: all7 selected PowerShell files parse; actual Release build01 has0errors/158historicalwarnings, no selected new-file warnings. Actual testing-tools-walk-01.trx passes70/70 with0failures/0skips in59s, including real PowerShell/DataTool, prior mail/equip/status/pipe cases and new walking/digest regressions. Counts overlap earlier selections. Full successor CI and actual client movement acceptance remain pending.

## Limits
- Existing inspector only excludes selected-file changes at read boundaries; cross-process restore ABA and live Store drift are explicitly not excluded. Character fields outside selected projections, uninspected quest/combat-gear/other domains and the actual path/wire/visual journey remain unknown.
- Matching sidecar digests prove equality of the observed images under normal hash assumptions; they do not interpret unknown bytes or attest who produced them. Local records can be edited.
