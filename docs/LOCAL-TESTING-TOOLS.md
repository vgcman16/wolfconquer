# Local test tools

These tools support disposable Windows test fixtures using Chinese7899 and
VisibleDx8D13. The operator sender is disabled by default and requires the
server's actual configured owned-admission runtime.

The current saved-resource extension passed56/56 actual offline tool tests,
zero skips/failures, including existing health/walk/mail/equip/status compatibility.
Full Windows [40839078556](https://github.com/vgcman16/wolfconquer/actions/runs/40839078556)
passed on exact ad23e71e2ed25da043f1dcbdfd0517c88200358f: Core29,632 passed,
152 existing external-fixture skips, Updates446/446 and Launcher57/57, zero
failures; all eight stages passed. The actual local check finished
2026-10-29T08:42:46.3993684Z. Logs, all three TRXs and matching World/DataTool
outputs are preserved outside the runner. This qualifies the published offline
consumable/tool checkpoint; it does not prove live rendering, SQL/power-loss
durability, physical Mana ownership or full gameplay parity. No deployment,
client relaunch or retained gear resend followed.

## List tests and view progress

The current source runner covers fourteen test areas. Saved movement, health/Mana/FullRestore consumption,
mail claims and equip/unequip have specific state assertions. Eight other areas
provide guided steps and retained snapshots. The [scenario catalog](../tools/testing/SCENARIOS.md)
and [acceptance matrix](testing/GAME-ACCEPTANCE-MATRIX.md) show the remaining work.

```powershell
pwsh -NoProfile -File C:\PATH\tools\testing\Invoke-GameAcceptance.ps1 -List
pwsh -NoProfile -File C:\PATH\tools\testing\Get-GameAcceptanceStatus.ps1 -EvidenceParent C:\TESTS\acceptance -Format Table
```

Listing tests performs no fixture or host probe. The progress view reads existing
evidence folders, so it can run while the server is stopped. Pending and invalid
evidence remain visible; a status row cannot establish client rendering or full
gameplay acceptance. Use a disposable owned fixture for actions below.

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
no sockets or enhancements. `consumables` contains one health1000000, one
Mana1001000 and one FullRestore723726 item, each unbound and without enhancements.
Choose it as the final kit argument in the same create/send commands. The sender
checks the active catalog and reserves real item UIDs from the current owner;
missing required catalog rows refuse before allocation. Consumption and client
rendering remain separate tests. Never resend an uncertain operation as a new kit.

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

The published tools and D13 mailbox parser passed full Windows
run40818321676 on8d4bdabb: core29,462pass/156skip, Updates446/446 and
Launcher57/57; all eight stages passed. **Real-client claim/equipment acceptance
remains pending.** The tool uses a separate fresh evidence folder with the exact
claimed item UID, item type and
equipment slot. After real client equip and save, the first resume retains an
equipment snapshot and receipt; after real unequip and save, a second resume
references that first receipt. It is intended to check both saved relocations,
other reported items and gold/CP without sending another kit. Caller-supplied
UID/type/slot are not themselves proof of delivery: retain the mailbox receipt to
correlate provenance. Even a matching pair leaves client rendering, live memory,
wire, eligibility, derived stats and unreported opaque bytes as separate evidence.

The [scenario catalog](../tools/testing/SCENARIOS.md) and [acceptance matrix](testing/GAME-ACCEPTANCE-MATRIX.md)
covered twelve families at that historical checkpoint across login, inventory, equipment, combat, NPC/rewards,
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
movement/wire and live Store remain separate; this saved receipt does not
establish a full source gate. The runner now
has saved-state assertions for mail, equip/unequip and walking; eight other
families retain guided snapshots/actions. See the scenario catalog for examples.

## Qualification

Current full source gate: [Windows40826474342](https://github.com/vgcman16/wolfconquer/actions/runs/40826474342)
completed successfully on exact tracker child82882bd6 of source dda332e6.
All eight stages passed: core29,545 passed/156 existing skips, Updates446 passed
and Launcher57 passed, zero failures. Actual receipts, logs and all three TRXs
were retained. Managed World/DataTool binaries were copied outside the runner;
no server/client was deployed or launched. Native hydration, live acceptance,
SQL/power loss and complete gameplay parity remain open.

The published8d4bdabb source passed all eight Windows stages in
[run40818321676](https://github.com/vgcman16/wolfconquer/actions/runs/40818321676):
core29,462pass/156existing skips, Updates446pass and Launcher57pass, zero
failures. The D13 mailbox/wire exporter selections passed196/196 and223/223;
these selections overlap. Earlier failure receipts remain in local evidence and
Git history. Source qualification does not prove complete gameplay, SQL,
power-loss recovery or real-client claim/equipment acceptance.

The runtime-diagnostic/source-safety selection passed 104/104 and the neighboring
account/mail/save/tool selection passed 194/194, zero skips (overlap). The full
successor gate above also passed. No changed server has been deployed just to
enable this diagnostic.

## Check saved health-potion consumption

The `consume-health` scenario records the exact potion UID/type and expected
saved HP. Use the same explicit values on initial and `-Resume` invocations;
`-ExpectedHitpoints` is required. Initial and Resume only inspect saved files.
Use one potion in the actual client between them. The tool verifies the selected
HP and one-unit stack/UID change, unchanged reported currency/mana/items, and
unchanged spell/proficiency/mailbox images. Missing actual saved vital fields,
truncated summaries or a changed retained intent cannot qualify.

`SavedHealthConsumptionObserved` still exits2: live rendering, wire, Store and
runtime atomicity remain separate checks. The tool neither clicks nor saves nor
sends gear. The earlier health tool batch had four saved assertion families and eight guided families; the current source adds Mana/FullRestore for six saved and eight guided families,
not a complete game acceptance pass. Use matching qualified DataTool/server
binaries; earlier inspector builds do not supply the required saved vitals.

## Check saved Mana or FullRestore consumption

The current source adds `consume-mana` with exact UID/type and `-ExpectedMana`, and
`consume-full-restore` with exact type723726, UID, `-ExpectedHitpoints` and
`-ExpectedMana`. Mana must increase while HP stays unchanged; FullRestore allows
one vital unchanged but neither decreases and at least one increases. No runtime
HP-trigger/formula rule is inferred. The new two source scenarios await Root's
qualification; historical twelve-family run results above are unchanged.

Initial and Resume each run only the real saved inspector. Retain the same
arguments/evidence; Resume writes a new after/receipt and preserves original
before/intent/receipt. It never sends a kit, saves, repairs or relogs the client.
The [complete invocation workflow](../tools/testing/SCENARIOS.md) lists parameters.
Missing vital fields, incomplete rows, wrong one-unit transition, changed selected
state or untouched sidecar bytes refuse. Status only rechecks retained JSON and
never opens retained tool/config paths or probes the host.

Saved Mana/FullRestore observations retain exit2 and pending live/wire/Store/source
flags. Only complete reported item/Character fields are compared; their changed
image digests do not prove preservation of opaque selected bytes. Untouched
Spells/Proficiencies/Mailbox use exact image digests. Editable JSON establishes no
authenticated runtime synchronization, visual acceptance or transaction atomicity.

## Save a reproducible protocol fixture

This exporter is included in the qualified8d4bdabb source batch above.

`Export-GameWireFixture.ps1` reads an existing local game-wire log and exports
only selected complete packet frames into a new JSON file. It leaves the log
unchanged and never enables tracing, attaches to a client, logs in, sends a
packet or grants an item. Select the actual client build/profile and record the
UI action. Those labels remain caller reports, not verified client identity.

```powershell
pwsh -NoProfile -File C:\PATH\tools\testing\Export-GameWireFixture.ps1 -TraceLogPath C:\TEST\WorldServer.out.log -OutputPath C:\TEST\mail-claim.json -Opcode 2264 -ClientBuild 7899 -ProtocolProfile VisibleDx8D13 -Observation 'Claim This Page in the owned test mailbox'
```

The default selects inbound frames; `-Direction Outbound` selects plaintext
responses. Handshake records, other opcodes and encrypted-log fields are omitted.
Truncated or inconsistent selected records fail rather than becoming goldens.
The default input limit is16MiB and32frames, with explicit maximum bounds.
`-AfterLine` selects a later observation. Existing outputs cannot be overwritten;
keep fixtures local and review their contents before any public publication.
An export proves recorded bytes were retained, not a correct schema or gameplay.

## Inspect a connected test character without changing it

The runtime inspector is disabled by default. A matching qualified World
build must opt in with `WOLFCONQUER_RUNTIME_INSPECTOR=1` and use its genuine
owned-admission configuration. It has a separate Windows current-user pipe from
the mail sender. Once enabled, repeated queries need no client relog:

```powershell
dotnet .\src\WolfConquer.DataTool\bin\Release\net9.0\WolfConquer.DataTool.dll inspect-runtime-player wolfconquer-runtime-inspect-<domainN> <domainN> <accountKey> <characterUid>
```

The numeric account key and character UID must come from that same disposable
fixture; the domain uses32hex digits without dashes. No login password is used.
The response reports managed and native HP/Mana separately, plus at most200
inventory/equipment roots from the managed projection. Native item roots are
reported only when their actual owners exist. Ordinary login currently leaves
those native owners uninitialized: nativeItems and selectedValuesEqual are null
with an explicit reason. Actual native Mana may be unhydrated too; its observed
value is reported separately. Foreign, retired, malformed or detected-changing
required owners refuse. Other containers and
derived native maxima remain outside coverage; an equipped wardrobe alias is
checked against its actual owner but the whole wardrobe is not reported.

Exit0 means an observed diagnostic, exit2 a typed refusal, exit1 an invalid or
incomplete exchange. None is gameplay acceptance. `selectedValuesEqual`
compares only complete reported values when native roots are available; it is
null when they are unavailable. `atomicNativeTupleProven` and
`liveStoreSynchronized` are always false: raw native writers, rewrite-and-restore
and changes after capture remain possible. The endpoint never grants, repairs,
saves or changes the character; a saved-file inspection is still a separate
observation. A transport failure never retries an action automatically.
