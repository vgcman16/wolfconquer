# Owned runtime inspection v1 — diagnostic source candidate

## Identity and scope

Default OFF. A future opted-in World update (`WOLFCONQUER_RUNTIME_INSPECTOR=1`)
adds a separate CurrentUserOnly/FirstPipeInstance local Windows named pipe,
`wolfconquer-runtime-inspect-<actual-domainN>`. Existing operator-mail pipe,
JSON/WCM2 receipt and grant semantics remain unchanged. No game/admin packet,
TCP listener, login/auth bypass, grant, save, repair, reseed or generic callback.

This is a DIAGNOSTIC, not acceptance. **atomicNativeTupleProven=false and
liveStoreSynchronized=false in every result.** Native Mana setter is a direct
unlocked physical write (LegacyNativePlayerRuntime.SpellResources.cs:15), and
raw item fields/maps likewise do not universally require actor locks. A
quiescent matching test does not prove all writers cooperate. No gameplay Mana
setter/owner change is included. CurrentUserOnly filters access; it is not signed
host/code attestation. Saved player snapshots are not read by this endpoint and
no file/runtime equality is claimed.

## Validation and transaction rules

Require the actual bound owned-admission runtime/domain and D13 Frame, live
recovery/registry binding, one current account+UID session, genuine ordinary
binding/native Player/PIN admission owners and transport
routes. Acquire the real World exclusive operation lease for a read-only copy,
then existing package-before-Store gate, current holder/issuance and native actor
gates in their established order. No mutation lease or publication is invoked.
Recheck exact holder/binding/routes/owners before and after copying. Missing,
retired, foreign, duplicate, malformed or detected-changing required graphs refuse.

Ordinary Frame currently does not initialize the full native inventory/equipment
owners or hydrate native Mana. Do not fabricate those owners or report absent
containers as empty. The diagnostic still reports actual bound Player vitals and
managed roots; nativeItems=null with one explicit missing-owner reason, and
selectedValuesEqual=null. Available-but-invalid selected graphs still refuse.
Explicit source-phase component tests may exercise initialized native owners,
but they do not establish ordinary-login wiring.

## Catalog/table contract

No new catalog or game packet. Existing item placement/family rules are read
from the current save-graph and item-position implementations.

Copy detached scalar values only: current managed Character projection (no
names/raw attributes/passwords), native UID/HP/Mana separately, and the selected
inventory/equipment ROOT row projection using the existing saved inspector's
numeric row fields. Maximum200 roots per managed/native projection; overflow
refuses, never pretends omitted rows are empty. Deposited items, warehouse,
runes, bags, wardrobe, derived native maxima and other runtime systems are
explicitly outside v1 coverage. Preserve native map-key/UID and owner checks.
Repeat the native selected scalar/row witness and compare managed references
under the same owners; detected drift refuses. Raw-writer/restore-ABA and tuple
atomicity remain unproven even when both bounded observations match.

Report observed managed/native values with selectedValuesEqual as a comparison
of the complete selected detached values only when native roots are available;
otherwise it is null with nativeItemsUnavailableReason, runtimeObserved=true and the two false proof
flags above. A disagreement is an observed diagnostic, not repair or a fake zero
fallback. Capture time marks the observation; it does not promise the connection
remains current when transport arrives. Disconnection/retirement before capture
or failed final owner revalidation refuses. All gates/leases release before IO.

## Packet contract

Requests: strict schemaVersion1, nonempty queryId/expectedDomain and positive
accountId/characterUid. Max1KiB, maxdepth8, unknown/duplicate/wrong-case/missing
properties refuse. Responses max256KiB with bounded arrays and typed statuses
Observed/Rejected. Positive little-endian32 body length plus strict camelCase
JSON. Separate receipt20bytes: ASCII WRI1 + query GUID ordinary Guid byte layout;
never WCM2. One peer at a time; one ten-second cancellation budget covers peer
IO and the asynchronous World-lease wait. Existing synchronous owner Monitor/
registry validation calls cannot be forcibly interrupted by that token. Partial/
wrong receipts/EOF/cancellation are transport failure, no mutation or retry.

The host joins both independently enabled local endpoints and the actual server
on shutdown; a failed endpoint stops the host through its cancellation path.
Startup failure disposes already-created endpoints; shutdown joins actual
workers, never treats elapsed time as drain. Owner shutdown cancellation links
to the endpoint and any waiting operation lease. No locks held across transport.
An already-running synchronous gate holder must actually release before joined
shutdown can finish; this diagnostic does not invent a separate owner or claim
a timeout retires that holder.

DataTool command `inspect-runtime-player <pipe-name> <domainN> <accountId>
<characterUid>` uses the actual local pipe, exact identity/query correlation and
WRI1 receipt. It outputs typed diagnostic JSON; exit0 means a complete observed
diagnostic response, never PASS/ServerStateVerified or gameplay acceptance.
Rejected exits2; transport/invalid response exits1. No automatic retry or server
start. Existing inspect-owned-player stays file-only and unchanged. Repeated
connected-client queries are independent read requests, not grant operations.

## Expected wire behavior and fixtures

Genuine test source: existing ordinary-owned fixture/login/registry/native
objects; compare returned values with actual Store/native fields and unchanged
files/UID high-water/queued output; real grant+claim may prepare a legitimately
issued inventory row. Test foreign/disconnected/retired/missing graphs, strict
codec bounds, default-off, actual CurrentUserOnly pipe/DataTool, partial/wrong
receipts, cancellation and joined disposal. No fake owner/saver/queue, reflected
HP seed or claiming equal values prove lock discipline. Root alone integrates,
builds/tests/full-gates and performs any future host update after live hold.

One real-owner test invokes the ordinary native Mana setter, captures the actual
post-write managed/native values, and compares the diagnostic against those
values without requiring them to disagree. Snapshot files are captured after
setup. An eventual repaired setter is allowed to make the values agree. Separate
pure-codec fixtures cover differing values; neither outcome changes either false
proof flag. A future gameplay slice must establish a shared publication owner
for legitimate Mana/raw-item writers before stronger atomicity can be claimed.

## Evidence alignment

| Requirement | Existing source | Bounded wire fixture | Status |
| --- | --- | --- | --- |
| Owned domain/current holder | Owned admission, registry, ordinary binding and Store | None required: local diagnostic | Derived from real owner APIs; isolated tests required |
| Inventory/equipment placement | LegacyItemSaveGraph.ValidPlacement and LegacyNativeItemPosition | No gameplay opcode changed | Existing placement constraints; selected wardrobe aliases require real owner witnesses |
| Native HP/Mana consistency | LegacyNativePlayerRuntime spell-resource publication | None | Repeated selected observations; raw-writer/ABA and tuple atomicity unresolved; ordinary native Mana hydration absent |
| Client profile | Frame VisibleDx8D13 | No client request added | Exact profile required; no other build compatibility claim |

## Implementation and qualification

Current full source gate: [Windows40826474342](https://github.com/vgcman16/wolfconquer/actions/runs/40826474342)
completed successfully on exact tracker child82882bd6 of source dda332e6.
All eight stages passed: core29,545 passed/156 existing skips, Updates446 passed
and Launcher57 passed, zero failures. Actual receipts, logs and all three TRXs
were retained. Managed World/DataTool binaries were copied outside the runner;
no server/client was deployed or launched. Native hydration, live acceptance,
SQL/power loss and complete gameplay parity remain open.

Runtime/LocalRuntimeInspectionPipe*.cs and LocalRuntimeInspectionProtocol*.cs;
World/LegacyRuntimeInspection*.cs, Frame/Store RuntimeInspection partials;
DataTool inspect-runtime-player and WorldServer local endpoint host. Isolated
LocalRuntimeInspectionTests partials use real temporary owned admission and
managed host components. Response parsing also rejects malformed placement,
duplicate equipment positions, duplicate UIDs and noncanonical UID ordering in
each detached projection; valid ordinary and equipped wardrobe shapes remain
accepted. Detached shape checks cannot attest a remote live wardrobe alias.

The endpoint host has direct lifecycle coverage for inspector-only completion,
both endpoint completion and real inspector startup collision after mail starts.
No original client executable or live service is started by those cases.

Root Release build08 completed with zero errors and no new changed-code
warnings. The combined runtime/SQL/mail selection passed 104/104 with zero
skips in 36 seconds. Earlier prerequisite and transport failures are retained;
source corrections do not erase them. The neighboring 194-case selection and full CI above also passed.
Current checklist/handoff keep live deployment and client acceptance separate;
no whole-server completion, file/runtime equality or host cleanliness is implied.

After every accepted connection, reset connected or broken pipe state before the
next peer. An interrupted header/body/receipt is one failed exchange, not an
automatic host shutdown. Retain real red/green controls. Existing mail receipt
compatibility and its no-duplicate replay invariants remain unchanged.

A missing native container does not exempt another present container from shape,
key/UID, owner, bounds, alias and repeated-reference/value validation. Present
rows are retained as internal witnesses even when the incomplete public native
projection remains null. Real half-graph negatives cover this refusal boundary.

Root neighboring account/mail/save/tool selection also passed 194/194, zero
skips, in 3m31s. It overlaps the 104-case selection. The full successor CI above passed; live acceptance remains open.
