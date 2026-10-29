# Game acceptance runner

This framework records bounded saved-state evidence and guides real-client tests.
Build the matching DataTool before use. Eight scenarios currently guide actions
and retain snapshots; saved walk, mailbox delivery/claim and equip/unequip also have saved-state assertions.
Rendering, usability and complete gameplay remain separate acceptance work.

| ID | Initial execution | Required acceptance beyond snapshots |
| --- | --- | --- |
| login-walk-save | Exact retained saved destination and unchanged projection/image assertions | Correct model/map, real movement, wire, live Store and lifecycle |
| reconnect | Guided snapshots | Same state rehydrated; actual old issuance retired |
| mailbox-gear | Executable request/dispatch and saved claim assertions | Actual client mailbox/model/rendering/claim observation |
| equip-unequip | Saved-state equip and unequip assertions | Eligibility/capacity, live Store, wire, derived stats and client visuals |
| upgrade-socket | Guided snapshots | Real costs, resource rejection, socket/gem fields and durable closure |
| combat | Guided snapshots | Targets, damage/status/XP, admission costs and ordering |
| npc-shop-rewards | Guided snapshots | Real branch/catalog, prices/capacity and one durable reward |
| trade-two-clients | Guided snapshots | Both real owners, atomic swap and cancellation/disconnect controls |
| persistence-restart | Guided snapshots | Root-owned graceful worker/lease drain, restart/recovery and rehydration |
| events-social | Guided snapshots | One implemented lifecycle with real required participants |
| updater | Guided snapshots | Existing updater tooling plus actual isolated update/rollback/local-edit behavior |

Source-backed test filters and remaining gameplay gaps belong in
`docs/testing/GAME-ACCEPTANCE-MATRIX.md`. A scenario remains pending when its
implementation or prerequisites are unavailable. The runner never executes
commands from a catalog, receipt or manifest string. Future executable scenarios
must add reviewed code with real owner/typed outcome/state assertions; editing a
status value or guided checklist cannot create acceptance.

Exit0 means catalog output or the narrow ServerStateVerified assertions only;
even that state retains LiveClientVisualPending. Exit2 means guided/client-action/
prerequisite work is pending. Exit1 means orchestration failure, rejected/unknown
operation or recovery required. A pending scenario never exits as acceptance PASS.

## Safe initial verification commands for Root

Run the catalog only; it does not probe the host, fixture, service or source suite:

```powershell
pwsh -NoProfile -File C:\PATH\tools\testing\Invoke-GameAcceptance.ps1 -List
```

For a read-only saved-state capture, substitute the actual already-built DataTool
DLL, actual owned-admission.json and disposable account. The evidence parent must
already exist. Choose a new absolute leaf `wolf-game-acceptance-<lowercaseGuidN>`:

```powershell
pwsh -NoProfile -File C:\PATH\tools\testing\Invoke-GameAcceptance.ps1 -Scenario login-walk-save -DataToolDll C:\ACTUAL-STAGE\DataTool\WolfConquer.DataTool.dll -OwnedAdmissionConfig C:\ACTUAL-OWNED-FIXTURE\owned-admission.json -AccountId 2 -EvidenceRoot C:\EXISTING-PARENT\wolf-game-acceptance-REPLACE-WITH-NEW-32-HEX-GUID -ExpectedMap 1002 -ExpectedX 411 -ExpectedY 355
```

Replace every placeholder before invocation. No credentials are accepted, stored
or printed. This command creates only fresh evidence, invokes the actual
read-only inspector and returns WaitingForSavedWalk when complete prerequisites
are present. It does not log in, move, save, restart, run source checks, send mail
or drive the client. Use the real client and save lifecycle, then Resume with the
same explicit identity paths/account/evidence root and destination. Resume's only
child command is the read-only inspector.

SavedWalkObserved requires the saved map/X/Y to equal the retained destination
and differ from the original position. Body/class/level/gold/CP, all reported
item/mail fields and selected counts must remain unchanged. Complete physical
item rows include all seven inspector containers and their deposited rows, with
unique UIDs; top-level inventory/equipment/warehouse counts match their own rows.
Items/Spells/Proficiencies/Mailbox must have unchanged presence, length and exact
observed-image digests. Older digestless reports, truncation, unknown containers
or changed images cannot qualify, even when counts match. Resume does not move,
send, save or grant. SavedWalkObserved returns exit 2 and retains live visuals,
wire, live Store synchronization and source gate as pending. It establishes only
the selected saved-file observation, never the actual movement path or live game.

## Mailbox workflow

Initial `-Scenario mailbox-gear` captures `before.json` and creates a retained
`request.json` using the real create-operator-test-kit CLI. Default is
RequestPreparedNotSent. Only initial `-SendTestKit` calls the real send CLI once.
Use the flag with an explicitly selected disposable fixture; it distinguishes
a snapshot run from an item grant. Kit names are
basic-trojan and socket-materials. No arbitrary item/UID/packet command is added.

A matching exit0 plus typed CommittedNotificationCompleted/Stopped and exact
operation/account/domain/character/UID correlation produces WaitingForClientAction.
The actual result is retained in delivery-result.json; after-delivery.json is a
separate real inspection when available. Lost/uncertain responses remain
OperationOutcomeUnproven, not rejection or retry authorization. Root must inspect
the retained operation; never regenerate/resend automatically.

After the actual client claims those items, invoke the same scenario/paths/account
with `-Resume` and the original EvidenceRoot. Resume rejects SendTestKit and
RunSourceChecks, never generates a new request and never sends. It requires the
original committed receipt and unchanged retained request/configuration/tool.
Gold and CP must remain unchanged, original items must be retained unchanged,
and the physical item count must increase by exactly the delivered row count.
Every delivered UID must occur exactly once across the complete item summary,
inside inventory, with its requested item ID/stack. Its delivered mail must be
absent or have attachedItemUid0; no mail may still reference that UID. Truncated,
unstable, foreign or incomplete snapshots cannot qualify the claim.

Successful assertions report **ServerStateVerified** with the narrow bound
"saved-file boundary inspection only". The real inspector explicitly reports
liveStoreSynchronized=false and does not exclude cross-process restore ABA or live
memory drift. Rendering/usability remains **LiveClientVisualPending** until Root's
actual computer-use observation is separately recorded. The runner does not turn
a manually edited receipt or visual checkbox into live-client proof.

## Equip and unequip workflow

Begin with a legitimately issued item in the disposable character's inventory.
The caller supplies its exact UID, item type ID and expected equipment slot; the
runner pins all three with the existing configuration, tool and evidence identity.
Choose a fresh absolute `wolf-game-acceptance-<lowercaseGuidN>` evidence folder.
For example, replace every placeholder before use:

```powershell
$argsForEquip = @('-Scenario','equip-unequip', '-DataToolDll','C:\ACTUAL-STAGE\WolfConquer.DataTool.dll', '-OwnedAdmissionConfig','C:\ACTUAL-OWNED-FIXTURE\owned-admission.json', '-AccountId','REPLACE-ACCOUNT-KEY', '-EvidenceRoot','C:\EXISTING-PARENT\wolf-game-acceptance-REPLACE-WITH-NEW-32-HEX-GUID', '-ExpectedItemUid','REPLACE-UID', '-ExpectedItemId','REPLACE-TYPE', '-ExpectedEquipmentPosition','REPLACE-SLOT')
pwsh -NoProfile -File C:\PATH\tools\testing\Invoke-GameAcceptance.ps1 @argsForEquip
```

After the actual client equips the item and the real lifecycle has saved it, run
that same command and arguments with `-Resume`. A matching complete saved-file
snapshot reports `SavedEquipObserved` and gives a unique resume receipt leaf.
After the actual client unequips and saves, run the same command with
`-Resume -EquippedReceipt <that-receipt-leaf>`. The runner revalidates the retained first
observation and reports `SavedEquipUnequipObserved` only if the same UID and type
returned to inventory, other summarized items remain unchanged, and gold and CP
are preserved. It never sends mail or generates a grant during Resume. A mismatch,
missing or truncated snapshot, changed pinned identity, or altered evidence does
not qualify the transition.

Both observed states return exit 2: they are saved-file observations while live
Store synchronization, wire, derived statistics, eligibility, rendering and
client usability remain pending. The inspector does not expose derived stats or
all opaque item bytes. Caller-supplied UID/type/slot is not proof of item delivery;
retain the separate mailbox receipt where delivery correlation is needed. Evidence
is local and editable, not a signed attestation or authority to mutate the game.

## Evidence and failure boundaries

Evidence is create-only: owner.json, concise before/after inspector JSON, original
request/result, initial receipt.json and uniquely named resume/failure receipts.
Receipts contain actual command exit/typed status/step metadata, not raw logs,
passwords, configuration contents or process-health PASS labels. There is no
recursive cleanup/move. A timed-out child is recorded as not joined with PID/birth;
the runner does not force-kill it or claim completed work. Existing evidence is
retained on all failures. Missing final receipt or uncertain send outcome requires
inspection, never a fresh grant. Local evidence is editable observation, not a
signed attestation or runtime authority.

## Optional source checks

Default/List/snapshot operation never runs a full suite. Only explicit
`-RunSourceChecks -SourceRepositoryRoot <ACTUAL-CI-CHECKOUT> -PowerShellPath <actual-pwsh.exe>`
invokes the existing tools/dev/Invoke-LocalCheck.ps1; it does not reproduce its
build commands. Saved running state, missing/held actual check.lock or an active
acceptance source-check mutex defer execution. The existing checker acquires that
same real repository lock, including a race after the precheck. Get-LocalStatus
is reused with service/updater probes skipped. Exit0 and its validated saved
passed state are required for SourceChecksVerified, a separate source-only scope.
If that explicitly requested gate is deferred or not verified, SendTestKit is
blocked. Read-only snapshots remain independent. An unjoined source-check timeout
does not start a follow-on source command or imply actual worker drain.

The caller must select the actual CI checkout; another checkout's lock cannot
prove global CI quiescence. No broad process/service audit or automatic GitHub job
is started. Root remains responsible for not duplicating an unrelated host job.


## Read-only acceptance status overview

`Get-GameAcceptanceStatus.ps1 -EvidenceParent <absolute-existing-parent>` reads
only direct `wolf-game-acceptance-<lowercaseGuidN>` children. Optional
`-AccountId <id>` filters known owners; `-Format Table` gives a compact view
(default JSON). It lists all eleven families and each selected account/evidence
ID, recorded saved-state result, pending/invalid reason and visual boundary.
No grants, game/tool/inspector execution, source checks, writes or probes occur.

Latest means local receipt-file modification time per scenario/account. Ties,
malformed newest receipts, mismatched identities/references, reparse paths,
missing required data and configured count/size bounds cannot fall back to an
older success. Owner-only evidence stays Pending. Invalid folders are also
reported as diagnostics, even when their owner cannot be identified.

`RecordedSavedObservation` summarizes the existing saved receipt and retained
schema references. Saved walk additionally rechecks the pure before/after/intent
transition over retained JSON, including currency, rows and sidecar image
equality. No inspector or live command is run. The overview does not rerun
mailbox/equip scenario assertions or prove the receipt was created by the runner.
Local JSON and timestamps are editable;
reading files is not a synchronized transaction with the running game. No
source-check field grants source/full-CI acceptance. The current runner has no
standard live-visual receipt: every summary row retains LiveClientVisualPending,
and an edited visual PASS is Invalid. Actual screenshots/observations remain
separate evidence. The equip-unequip receipt does not retain the prior equipped
receipt link, so this overview cannot reconstruct that exact observation chain.

Exit0 means the bounded overview was read, including legitimate Pending states;
it never means gameplay passed. Exit1 means InvalidEvidence. Limits default to
128 evidence folders, 128 receipts per folder and 2 MiB per JSON file. Ordinary
path and per-file metadata checks bound reading; cross-process replacement/ABA
or edits after reading are not excluded. No receipt is runtime authority.
