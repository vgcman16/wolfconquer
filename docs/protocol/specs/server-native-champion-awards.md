# FSB-227: Coherent native Champion Point awards

## Identity and scope
Revision1, 2026-10-29; baseline public7bb9156c. Correct the actual native
Player.AddChampionPoints operation and its genuine ChampionPoints setter.
Original native Player3161..3170 and current two partials are reference evidence.
Client-confirmed old7899/VisibleDx8D13 wire remains; no7937/SQL/live activation.

## Packet contract
Accepted awards currently send no packet. Preserve that behavior. Existing
settodayvalue && TodayChampionPoints >650 rejection uses the actual owner's
SendSysMesage and existing Talk2179 bytes/defaults. Do not invent a new message,
cap, client rule or accepting sender. Rejected overflow publishes no output.

## Catalog/table contract
Not applicable: three real uint counters, no item catalog or binary table.
The old threshold and false-option behavior are provisional existing eligibility,
not new client-confirmed gameplay policy. Preserve these while fixing arithmetic.

## Validation and transaction rules
Calculate both uint additions before assigning either balance or history. Refuse
any overflow before field/output mutation; checked OverflowException is explicit
and leaves genuine counters unchanged. No saturation, wrapped balances, refund,
partial prefix or fabricated persistence. Publish current, today and maxhistory
coherently under the actual player's existing SpellAdmissionSyncRoot. The actual
ChampionPoints setter must cooperate with this same gate for its current/history
pair; preserve its valid direct-assignment semantics and public signature.
Do not add an unused owner/UID/metadata prerequisite to a no-output successful
award. Existing threshold rejection still uses its real required output owner.
Public direct Today/History fields remain raw noncooperating writers; do not
claim global/crash/SQL atomicity or independently thread-safe caller += queries.

## Expected wire behavior and fixtures
Retain the five existing eligibility outcomes and exact rejection Talk2179
frame count. Replace the existing test requiring uint.MaxValue +2 =>1 with
complete overflow refusal, preserving nonzero max history. Add both first/second
addition overflow cases, valid near-limit boundary and retry/unchanged output,
and genuine concurrent cooperating awards/setter coverage where practical.
Owned native component fixture only; no executable/client/SQL/build/runner action
by helpers. Root owns all qualification/staging/commits.

## Evidence alignment
| Requirement | Source evidence | Status |
| --- | --- | --- |
| unchecked counter wrap | AddChampionPoints uses += on both uint fields; existing ChampionOverflow test requires wrap | confirmed server defect |
| partial award if second sum invalid | current setter/history publication occurs before today's addition | complete preparation required |
| genuine owner gate | player's existing SpellAdmissionSyncRoot used by adjacent admissions | cooperating in-memory scope |
| current eligibility | strict >650, false option still updatesToday | unchanged provisional source behavior |
| actual lifecycle callers | original LoadCharacter, RoleInfo and operator command | Root review; target currently no production caller |

## Implementation and qualification
Helper production scope: World/LegacyNativePlayerRuntime.AddChampionPoints.cs
and .ChampionPoints.cs only (one focused partial if truly necessary).
Helper tests: existing LegacyNativeCharacterReadTests.Leaves.cs Champion tests
only plus one new focused ChampionAwards partial. Root owns spec, ledger, caller
review, composition and actual builds/tests. Authored/unrun until qualified.

Root local qualification for the combined40-path candidate: Release build01
passed with0errors/157existingwarnings and no new changed-path warnings. Actual
focused01 passed423 with7existing skips; native-broad01 passed13278/13278 with
0failures/skips. Selections overlap. Receipts are in root-native-item-load-next.
The old public7bb fullrun failed one timing-sensitive startup test; its actual
failedTRXs/logs remain. Exact-commit successor fullCI, SQL, deployment and live
new-client/fullparity acceptance are pending. This note supersedes authored/unrun
labels in this spec/entry without expanding the verification scope.
