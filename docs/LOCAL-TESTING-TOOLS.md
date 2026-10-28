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
never retries automatically. Revision1 prevents duplicate materialization; it
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

## Qualification

The mail/pipe/kit/inspector candidate builds with zero errors. Its focused
selection passed76/76; existing mailbox/UID/admission controls passed284/284.
These are isolated server/tool checks, including a same-connected-test-session
claim. They are not real game-client mail acceptance or a full gate for this batch.

The broader [scenario runner](../tools/testing/SCENARIOS.md) covers eleven test
families in the [acceptance matrix](testing/GAME-ACCEPTANCE-MATRIX.md). Its three
PowerShell files parsed, catalog output listed11 scenarios, and an actual
read-only capture/resume against the running fixture retained pending exit2.
Two additional actual child-PowerShell tests cover catalog output, genuine
fixture capture/resume, retained evidence and refusing resume-plus-send. Only
mailbox has executable saved-state assertions today; other actions remain guided.
Full CI and actual game-client delivery/equip acceptance remain pending.

The exact tools commit8e84946e failed full Windows40771334759 in one existing
StarVolcano repeat/expiry timing test: Core29,334 passed/one failed/164 existing
skips; Updater446 passed. Launcher/parsers were not reached. The FSB179 fixture
successor has a zero-error build,15/15 affected/neighbors passes and three separate
1/1 repaired-case repeats. Its full gate is still required; no failed build was
deployed and no real game-client mail/claim/equip acceptance is implied.
