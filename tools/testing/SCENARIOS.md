# Game acceptance runner

This framework records bounded saved-state evidence and guides real-client tests.
Build the matching DataTool before use. Fourteen families are listed. Eight scenarios currently guide actions
and retain snapshots; saved walk, health/Mana/FullRestore consumption, mailbox delivery/claim and equip/unequip have saved-state assertions.
Rendering, usability and complete gameplay remain separate acceptance work.

The earlier twelve-family source qualifications remain historical records.
Mana/FullRestore are newly implemented read-only source scenarios awaiting Root's
qualification; adding catalog entries is not new execution or live acceptance.

| ID | Initial execution | Required acceptance beyond snapshots |
| --- | --- | --- |
| login-walk-save | Exact retained saved destination and unchanged projection/image assertions | Correct model/map, real movement, wire, live Store and lifecycle |
| consume-health | Exact potion UID/type, expected saved HP and one-unit transition | Actual consumption admission, amount, cooldown, wire, live Store and durable transaction |
| consume-mana | Exact Mana potion UID/type, increased expected saved Mana and one-unit transition | Actual admission/formula, cooldown, wire, live Store and durable transaction |
| consume-full-restore | Exact FullRestore UID/type and nondecreasing expected saved HP/Mana with one improvement | Actual HP-trigger intent/formula, admission, wire, live Store and durable transaction |
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


## Read-only saved health consumption

Use a new evidence leaf and the actual built DataTool/owned configuration:

```powershell
pwsh -NoProfile -File C:\PATH\tools\testing\Invoke-GameAcceptance.ps1 -Scenario consume-health -DataToolDll C:\ACTUAL-STAGE\DataTool\WolfConquer.DataTool.dll -OwnedAdmissionConfig C:\ACTUAL-OWNED-FIXTURE\owned-admission.json -AccountId 2 -EvidenceRoot C:\EXISTING-PARENT\wolf-game-acceptance-REPLACE-WITH-NEW-32-HEX-GUID -ExpectedItemUid 8013201 -ExpectedItemId 1000000 -ExpectedHitpoints 80
```

The explicit UID/type and changed expected saved HP are retained. Initial capture
only inspects saved files and records WaitingForSavedHealthConsumption. It requires
real non-null saved HP/MinMana/MaxMana, complete unique item/mail summaries and
stable complete image digests. An absent saved MaxMana remains unknown even when
the game parser would default runtime MaxMana to MinMana. Missing prerequisites
do not become a zero value. The type must belong to the current source health
classification; that check provides no catalog amount or client proof.

Use exactly one unit through the real client and complete its real save lifecycle.
Invoke the identical arguments with `-Resume`. Both invocations are read-only:
only the inspector child command runs. SendTestKit and RunSourceChecks are refused.
No client input, item grant, HP write, save or lifecycle transition is performed.
Changing UID/type/expected HP or retained intent refuses continuation.

SavedHealthConsumptionObserved requires exact changed expected HP, unchanged
selected mana/currency/location/appearance fields, the exact selected one-unit
transition and unchanged other summarized rows/counts. Stack zero and one both
remove the last UID; larger stacks decrement once with the same UID and other
fields. All seven real containers and deposited rows participate in uniqueness
and equality checks. Spells, Proficiencies and Mailbox image presence/length/digest
must remain equal; Character and Items must have valid changed image digests.
These modified-image checks do not prove preservation of their opaque fields.
Truncated or old incomplete evidence refuses qualification.

Every health observation exits 2 with LiveClientVisualPending, WirePending,
SourceGatePending and liveStoreSynchronized=false. Saved-file equality is not
runtime HP/Items atomicity, live Store agreement, D13 wire/client acceptance,
SQL, power-loss safety or an authenticated attestation. The independent runtime
health implementation and its qualification are outside this tooling batch.

## Read-only saved Mana and FullRestore consumption

`consume-mana` requires `-ExpectedItemUid`, `-ExpectedItemId` from the closed Mana
class and `-ExpectedMana`. Mana must increase; saved HP stays unchanged.
`consume-full-restore` requires exact UID/type723726 plus both
`-ExpectedHitpoints` and `-ExpectedMana`: neither may decrease and at least one must
increase. Either may remain unchanged. This captures explicit saved outcomes;
it does not settle the runtime HP-full/Mana-low trigger or prove a formula.

Use the same ordinary paths/account/evidence and exact arguments initially and
with `-Resume`. Initial captures one actual saved inspection and a fresh
`mana-intent.json` or `full-restore-intent.json`. Resume runs only the real saved
inspector and retains a new after/receipt; it never resends, saves, relogs,
dispatches a kit or runs source checks. Use separate fresh evidence per scenario.
For example, use actual already-reviewed values and a new GUID leaf:

```powershell
$mana = @('-Scenario','consume-mana','-DataToolDll','C:\ACTUAL-STAGE\WolfConquer.DataTool.dll','-OwnedAdmissionConfig','C:\ACTUAL-FIXTURE\owned-admission.json','-AccountId','2','-EvidenceRoot','C:\EXISTING-PARENT\wolf-game-acceptance-REPLACE-WITH-NEW-32-HEX-GUID','-ExpectedItemUid','8013201','-ExpectedItemId','1001000','-ExpectedMana','90')
pwsh -NoProfile -File C:\PATH\tools\testing\Invoke-GameAcceptance.ps1 @mana
# After the actual client action and its real save, use identical arguments:
pwsh -NoProfile -File C:\PATH\tools\testing\Invoke-GameAcceptance.ps1 @mana -Resume
```

For FullRestore choose another new leaf and use `-Scenario consume-full-restore
-ExpectedItemUid <actualUid> -ExpectedItemId 723726 -ExpectedHitpoints <exactHp>
-ExpectedMana <exactMana>` with the same identity/path arguments. Missing saved
vitals, old digestless data, incomplete rows, unknown containers, wrong selected
UID/type/stack, altered intent or unrelated selected state cannot qualify.

`SavedManaConsumptionObserved`/`SavedFullRestoreConsumptionObserved` still exit2.
Comparisons cover all complete reported rows and unchanged Spells/Proficiencies/
Mailbox image digests. Changed Character/Items digests are required, but their
opaque bytes are not revealed: this proves no full opaque item/Character
preservation. Live visuals, wire, Store, authenticated runtime and atomicity stay
pending. Existing health schema1/intents/statuses remain supported.

## Read-only acceptance status overview

`Get-GameAcceptanceStatus.ps1 -EvidenceParent <absolute-existing-parent>` reads
only direct `wolf-game-acceptance-<lowercaseGuidN>` children. Optional
`-AccountId <id>` filters known owners; `-Format Table` gives a compact view
(default JSON). It lists all fourteen families and each selected account/evidence
ID, recorded saved-state result, pending/invalid reason and visual boundary.
No grants, game/tool/inspector execution, source checks, writes or probes occur.

Latest means local receipt-file modification time per scenario/account. Ties,
malformed newest receipts, mismatched identities/references, reparse paths,
missing required data and configured count/size bounds cannot fall back to an
older success. Owner-only evidence stays Pending. Invalid folders are also
reported as diagnostics, even when their owner cannot be identified.

`RecordedSavedObservation` summarizes the existing saved receipt and retained
schema references. Saved walk and health/Mana/FullRestore additionally recheck their pure before/after/intent
transitions over retained JSON, including currency, rows and sidecar image
equality (and the exact retained vital/one-unit change for each consumption scenario). No inspector or live command is run. The overview does not rerun
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
