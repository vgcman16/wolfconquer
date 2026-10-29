# Retained two-player saved trade observation

Target: VisibleDx8D13 testing workflow. This changes no packet or gameplay rule.
The existing fourteen-family catalog remains fourteen families. Trade without
the new pair parameters retains its existing guided workflow.

Pair mode requires both `-CounterpartyAccountId` and an absolute `-TradePlan`
path, distinct nonzero account keys, the same explicit qualified DataTool and
owned configuration, and a fresh owned evidence directory. The strict bounded
plan has schemaVersion 1, exactly two ordered participants (accountId,
goldDelta, conquerPointsDelta) and up to forty full-row UID transfers (uid,
itemId, stackSize, fromAccountId, toAccountId). The participant order is the
primary account then counterparty. Signed currency deltas conserve each
currency; raw ushort stack zero remains valid. Splits, merges and new UIDs are
outside this comparison. No credentials, source checks or sending are accepted.

Initial execution retains the exact plan bytes, both actual inspector envelopes,
then creates one strict intent using their actual common domain, character UIDs,
ordered envelope hashes and explicit selection. All writes are create-new.
Complete bounded reports, all six stable file projections and required saved
vitals are prerequisites; missing or uncertain data never becomes an empty row
or zero. The comparator package owns the exact selected transition rules.

Resume requires the same pair, tool/config paths and hashes and original plan
path. The external plan is not reopened; its exact retained copy and original
intent are revalidated before either child command. Resume performs precisely
two `inspect-owned-player` calls and retains unique after envelopes and receipt.
It never regenerates intent, sends, saves, grants, repairs, creates accounts or
checks source. Initial evidence remains unchanged. Status reads retained files
only and repeats strict intent/hash/identity and pure comparison checks, including
the second player; an editable observed status alone has no authority.

`SavedTradeObserved` exits 2. LiveClientVisualPending, WirePending,
SourceGatePending, liveStoreSynchronized=false and AtomicityPending remain.
The two sequential reads are not a simultaneous runtime snapshot. JSON is
editable consistency evidence, not authenticated runtime, wire, SQL, crash-safe
transaction or atomic-swap proof. Complete reported rows are compared; opaque
bytes inside touched Character/Items images are not proven unchanged.

Regression source invokes real PowerShell/DataTool against new owned temporary
saved inputs. Coverage includes a two-way whole-row/currency transition,
retained evidence, two inspectors only, deleted external plan on Resume,
mismatches/tamper/second identity or sidecar drift, pre-child intent refusal and
status without available tool/config paths. The frozen helper parsed23 PS files
and passed27 bounded PowerShell/owned-fixture controls against Root's preserved
qualified50cf DataTool, plus catalog/guided compatibility. Its genuine one-way
saved observation did not perform a live trade.

The isolated successor from committed public e026be7f plus these22 paths passed
local-cache-only restore and Release test-project Build01: zero errors, no
warnings in the seven new C# files (157 warnings across the existing build
graph). All117 focused cases passed, zero failures/skips, in10m03s:62 new trade
cases and55 existing runner/status/saved-inspector neighbors. Actual registry-
reserved two-way saved transfers now ran; the new PowerShell composition again
parsed23 files with zero errors. No test required a code correction. These are
offline tool checks; the combined full-CI/integration gate, gameplay trade,
client acceptance, SQL and runtime atomicity remain separate. Root integrates.
