# Local test tools

These tools support disposable Windows test fixtures using Chinese7899 and
VisibleDx8D13. The operator sender is disabled by default and requires the
server's actual configured owned-admission runtime.

## Give a connected test character gear

Start a qualified World build with its existing owned-admission configuration
and `WOLFCONQUER_OPERATOR_MAIL=1`. Installing this feature requires one server
update. Subsequent deliveries use the running process and its real mail cache;
the character can remain logged in.

Use the numeric account key and character UID from the owned fixture. The account
key is not necessarily the text typed into the client's login box. The domain
is the explicit ExpectedDomain from the same fixture's configuration.

```powershell
dotnet .\src\WolfConquer.DataTool\bin\Release\net9.0\WolfConquer.DataTool.dll create-operator-test-kit C:\Tests\gear-request.json <domainN> <accountKey> <characterUid> basic-trojan
dotnet .\src\WolfConquer.DataTool\bin\Release\net9.0\WolfConquer.DataTool.dll send-operator-gear-mail wolfconquer-operator-mail-<domainN> C:\Tests\gear-request.json
```

The parent directory must exist; the request file must be new. The generator
creates a complete request with one retained operation ID. It does not send
automatically. `basic-trojan` contains seven low-level gear rows, one DragonBall
and one Meteor. `socket-materials` contains DragonGem, RainbowGem, ToughDrill,
StarDrill, DragonBall and Meteor, one physical row each. These recipes pre-open
no sockets or enhancements. The sender checks the active catalog and reserves
real item UIDs from the current server owner.

After delivery, open mail and claim the attachments. Claiming, equipping,
changing stats and saving are separate test steps. An empty eligible-item list
in a forge view can be expected when no applicable gear/materials are owned.

The pipe uses Windows CurrentUserOnly. It introduces no gameplay administrative
packet. Bounded strict JSON carries domain and operation ID; no password or
caller-selected item UID is accepted.

## Mail, claim and equipment acceptance sequence

Use the same disposable account, owned configuration, qualified matching DataTool
and staged server throughout. Choose a fresh `wolf-game-acceptance-<guidN>`
evidence folder for the `mailbox-gear` scenario. The initial runner invocation
captures `before.json` and creates one retained kit request; only an explicit
initial `-SendTestKit` dispatches it. A committed typed result identifies the
mail IDs and item UIDs. Preserve the request, original receipt and delivery
result; an uncertain result is not authority to issue another operation.

For example, replace the placeholders with actual local paths and a new GUID:

```powershell
$mailArgs = @('-Scenario','mailbox-gear', '-DataToolDll','C:\ACTUAL-STAGE\WolfConquer.DataTool.dll', '-OwnedAdmissionConfig','C:\ACTUAL-OWNED-FIXTURE\owned-admission.json', '-AccountId','REPLACE-ACCOUNT', '-EvidenceRoot','C:\EXISTING-PARENT\wolf-game-acceptance-REPLACE-WITH-NEW-32-HEX-GUID')
pwsh -NoProfile -File C:\PATH\tools\testing\Invoke-GameAcceptance.ps1 @mailArgs -SendTestKit
# After an observed committed delivery and the real client claim:
pwsh -NoProfile -File C:\PATH\tools\testing\Invoke-GameAcceptance.ps1 @mailArgs -Resume
```

Claim the attachments in the actual connected client, then invoke `mailbox-gear`
with the same paths, account and evidence folder plus `-Resume`. Resume never
sends. Its `ServerStateVerified` result checks the saved-file inventory and cleared
mail references for those exact delivered UIDs; it still leaves client visuals,
live Store synchronization and gameplay acceptance pending. Record the observed
client result separately. See the [runner workflow](../tools/testing/SCENARIOS.md)
for complete parameters and failure handling.

The equip/unequip saved-state assertions are **available in this source;
full Windows qualification and real-client acceptance remain pending**. The
successor starts a
separate fresh evidence folder with the exact claimed item UID, item type and
equipment slot. After real client equip and save, the first resume retains an
equipment snapshot and receipt; after real unequip and save, a second resume
references that first receipt. It is intended to check both saved relocations,
other reported items and gold/CP without sending another kit. Caller-supplied
UID/type/slot are not themselves proof of delivery: retain the mailbox receipt to
correlate provenance. Even a matching pair leaves client rendering, live memory,
wire, eligibility, derived stats and unreported opaque bytes as separate evidence.

The [scenario catalog](../tools/testing/SCENARIOS.md) and [acceptance matrix](testing/GAME-ACCEPTANCE-MATRIX.md)
cover eleven families across login, inventory, equipment, combat, NPC/rewards,
trade, recovery, events and updater. A guided or pending result is not an
end-to-end pass; full-game, SQL and crash/power-loss acceptance remain open.

## Interpret the sender result

| Status | Meaning |
| --- | --- |
| CommittedNotificationCompleted | Grant committed and notification sends completed; rendering/claim still needs observation. |
| CommittedNotificationStopped | Grant committed but notification stopped; do not repeat as a new operation. |
| PreviouslySubmittedOutcomeUnproven | This operation reserved IDs already. No second grant was issued; inspect mailbox/items. |
| Rejected | No grant committed in this attempt; reason can identify a burned reservation. |
| RecoveryRequired | Persistence/publication needs investigation; affected further mutations are fenced. |

A timeout or lost response can occur after commitment. Keep the original file
and operation ID. Do not regenerate it to retry an uncertain grant. The sender
never retries automatically. Storage operation revision1 prevents duplicate materialization; it
does not reconstruct a request interrupted after reservation automatically.

## Inspect saved state before and after a test

```powershell
dotnet .\src\WolfConquer.DataTool\bin\Release\net9.0\WolfConquer.DataTool.dll inspect-owned-player C:\Tests\wolf-owned-admission-<guidN>\owned-admission.json <accountKey>
```

The read-only report includes selected character values, item containers, mail
attachment UIDs and spell/proficiency counts. It omits PINs, arbitrary attributes,
names, mail text and private paths. It can run while the server holds its real
UID registry lease and does not acquire another lease or runtime owner. Only a
GUID-named disposable fixture is accepted. Pending journals or changed selected
files cause refusal.

The report compares bytes and metadata at its read boundaries. It is not a live
Store transaction; transient changes, rewrite-and-restore and memory drift remain
possible. Profile is explicitly unknown because the configuration does not store
it. Counts are complete; item/mail detail shares a200-row cap with a truncation flag.

## Check a saved walking destination

Use `login-walk-save` with explicit `-ExpectedMap`, `-ExpectedX` and `-ExpectedY`
on both initial and `-Resume` calls. Choose a different destination and a fresh
`wolf-game-acceptance-<guidN>` evidence folder. Move and save using the actual
client between the calls; the runner itself never moves, saves or logs in.

The retained intent pins the expected destination. `SavedWalkObserved` checks
that saved map/X/Y changed to that exact value, selected gold/CP/body/class/level
and complete reported item/mail rows stayed unchanged, and the actual selected
Items/Spells/Proficiencies/Mailbox images stayed equal. The inspector adds an
optional content digest from its already-read bounded bytes; old reports without
it cannot qualify this scenario. It supports all seven real item groups and their
deposited rows, and refuses truncated summaries. Status rechecks the same pure
saved transition; editable receipts remain comparison evidence only.

Both waiting and matching saved results return exit2. Client rendering, actual
movement/wire, live Store and the full source gate remain pending. The runner now
has saved-state assertions for mail, equip/unequip and walking; eight other
families retain guided snapshots/actions. See the scenario catalog for examples.

## Qualification

Current walking/tooling selection on the public99503716 equipment behavior plus
this source batch: Release build0errors,7selected PSparses, actual70/70passed,
0fail/skip in59s. This includes the earlier mail/equip/status/pipe families and
new walking/digest regressions; selections overlap and are not additive. Full
successor CI and real-client acceptance remain pending. No NativeHP change is
included in this tooling batch.

Earlier public80fa53c9 tooling built with zero errors; seven selected PowerShell
files parsed. Its final focused selection passed60/60, with no failures/skips:
real pipe/Frame delivery, actual DataTool first and same-operation commands,
fragmented response reading, wrong receipts, shutdown join, kits, inspector,
mail/equip runner and the actual status script. A first59/60 run exposed a blank
table view; casting its dictionary rows to display objects fixed it without
weakening the assertion. Earlier76/76 and284/284 selections are separate,
overlapping historical evidence, not additional current passes.

The [scenario runner](../tools/testing/SCENARIOS.md) covers eleven families in
the [acceptance matrix](testing/GAME-ACCEPTANCE-MATRIX.md). Mailbox claim and
equip/unequip have saved-state assertions; eight other families retain guided
snapshots and actions. The read-only status overview lists all eleven families,
pending/invalid records and narrow recorded saved observations. Neither a table
row nor exit0 proves client rendering, gameplay or a full source gate.

Full Windows40781201208 on actual publica62bb717 failed one operator-pipe
duplicate-response body read: Core29,334 passed/one failed/164 existing skips,
Updater446 passed. Restore/build passed; launcher/parsers were not reached.
FSB179 StarVolcano passed that run. Transport revision2 now retains the server
connection until a matching response receipt within the same10s deadline; see
FSB180 and the protocol spec. Actual full80fa53c9 run40790881796 was interrupted when its worker disappeared
and ended cancelled; its incomplete output is unqualified. The trusted runner
was restarted once and is online/idle. A new successor full gate and actual
game-client hot delivery/claim/equip acceptance remain pending. No failed build was deployed.
