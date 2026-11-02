# Player ticker and Reviver transport ownership

## Identity and scope

Target: existing VisibleDx8D13 managed server path. This changes publication
ownership, not a client profile or packet schema. Base is local46f3db0c, with
reviewed joined17 and typed-writer4 source proposals. The implementation is
unqualified until Root runs its actual producer and socket controls.

## Packet contract

Keep the existing serializers, IDs, framing and byte layouts. Owned stamina
publishes its effect to the owner, effect to visible peers, then its scalar.
The closed XP path accepts only the actual single XP-circle ticker update.
Reviver preserves its owner HP pair, stamina, status vector, mesh and map status,
then animation and visible-peer spawn sequence. No new opcode or field is added.
Existing protocol goldens remain required; no 7937 acceptance is inferred.

## Catalog and table contract

No new table format. Use actual current loaded magic/equipment/monster metadata,
the selected live monster incarnation and native completed maximum-query owner.
For Reviver the existing strict distance is below13 tiles. A cached equal maximum
does not replace the completed query. Missing required query state defers the
native path and is an unresolved availability seam, never a successful revival.

## Validation and transaction rules

Prepare and commit under genuine lifecycle, Store, native and short world leases.
Release world and per-client runtime-publication leases before awaiting socket IO.
At the installed connection's actual write gate, require each producer's private
one-use permit/start, real identity/binding and current relevant state. Preserve
ordered Store/mailbox/native/vector locking and validate current visibility.
Use the existing uniquely owned key rules for XPCount; reject ambiguous aliases.

Native Reviver also retains the actual monster lease through its native/session
writer, preflights frames before commitment and uses its actual commit receipt
for later stages. Notification failure cannot refund or replay committed state.
Interrupted output fences only the still-current matching owner. A stale prior
step never grants authority over an unrelated successor. This is not SQL/crash
atomicity or a durable notification outbox.

### Prepared spawn ownership

TIMER27's near-monster case retained a genuine current completed maximum query,
but refused with `ordinarySpawnActionOwnerChanged`: the existing current-session
spawn entry was given an unpublished final session during frame preparation.
Preflight must instead serialize through that exact Store publication's prepared
action owner, tied to its original/current holder, native actor and exact future
session. Recheck the whole preparation and raw future attributes around encoding.
The resulting bytes are data only. After commitment, actual socket output still
requires the real receipt/write permit and the existing current-holder spawn
entry. Do not weaken that entry or use the permissive snapshot overload.

Qualify with the existing genuine near-positive/far-negative Reviver cases,
stale/changed prepared ownership controls and action/spawn neighbors. This repair
alone does not establish selective native flag removal or full death lifetimes.

## Expected wire behavior and fixtures

Use owned saved inputs, actual public queries/casts, real ticker/Reviver producers
and local TCP backpressure. Cover stamina success and later-cost supersession,
actual XP permit rejection when ungated, unique-case/duplicate XP keys, actual
Reviver monster/range, native/saved state and independent combat during a stall.
Keep the four existing owned stalled-peer controls; do not narrow them to pass.
The shared writer has closed typed cases, with no caller-supplied validator API.

## Evidence alignment and qualification

The prior Root expiry batch is local46f3db0c. WIRE11 failed30P/5F; the corrected
third-observer component dispel/reinstall case later passed1/1. Those scopes do
not qualify this new implementation. Retain failed logs and diagnostic limits.
Run a combined build, new producer controls and affected timer/native neighbors,
then the appropriate complete gate before publishing gameplay changes.

Vote, multi-frame ticker/status/invitations, Undying, standalone stamina, unbound
Reviver and other global output families remain unsplit. Native post-death query
availability, interruption handling and legitimate recipient/owner succession
need execution evidence. This is not whole-loop liveness, full parity, an encrypted
login proof or live deployment. The client/updater execution hold remains active.

## Focused qualification - 2026-11-02

BUILD28 passed0errors/180warnings; TIMER29 passed55/55 (0 failed/skipped),
including genuine near/far Reviver, independent combat during a blocked socket,
Root recipient/generation sequences, stamina/XP and spawn-packet neighbors.
BUILD30 passed0errors/114warnings; NATIVE31 passed91/91 (0 failed/skipped),
including real prepared-publication success, raw future-map change, genuine
holder replacement and binding retirement plus native/query/action neighbors.
These are overlapping offline scopes. The full local gate remains pending;
selective native status/death lifetimes and transformation joins remain open.
