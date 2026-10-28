# Game acceptance runner

This framework records bounded saved-state evidence and guides real-client tests.
Build the matching DataTool before use. Ten scenarios currently guide actions
and retain snapshots; mailbox delivery/claim also has executable state assertions.
Rendering, usability and complete gameplay remain separate acceptance work.

| ID | Initial execution | Required acceptance beyond snapshots |
| --- | --- | --- |
| login-walk-save | Guided snapshots | Correct model/map, real movement, saved coordinates and lifecycle |
| reconnect | Guided snapshots | Same state rehydrated; actual old issuance retired |
| mailbox-gear | Executable request/dispatch and saved claim assertions | Actual client mailbox/model/rendering/claim observation |
| equip-unequip | Guided snapshots | Eligibility/capacity, UID relocation, wire and derived stats |
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
pwsh -NoProfile -File C:\PATH\tools\testing\Invoke-GameAcceptance.ps1 -Scenario login-walk-save -DataToolDll C:\ACTUAL-STAGE\DataTool\WolfConquer.DataTool.dll -OwnedAdmissionConfig C:\ACTUAL-OWNED-FIXTURE\owned-admission.json -AccountId 2 -EvidenceRoot C:\EXISTING-PARENT\wolf-game-acceptance-REPLACE-WITH-NEW-32-HEX-GUID
```

Replace every placeholder before invocation. No credentials are accepted, stored
or printed. This command creates only fresh evidence, invokes the actual
read-only inspector and returns GuidedActionsPending. It does not log in, move,
save, restart, run source checks, send mail or drive the client. Resume with the
same explicit identity paths/account/evidence root captures another snapshot and
keeps GuidedAssertionsPending until an executable scenario is implemented.

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
