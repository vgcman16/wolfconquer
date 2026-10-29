# SAVED-RESOURCES-1: read-only Mana and FullRestore consumption evidence

## Identity and scope

Revision1, 2026-10-29, Root approved testing-tool scope. Extend current saved
acceptance tools with consume-mana and consume-full-restore for VisibleDx8D13.
Use current actual inspect-owned-player saved projection; no packet send, login,
grant, save, repair, source check, host/runtime/visual or atomicity verification.
Root alone executes this source package. Existing health schema1/function/status
names and validation behavior, walk/mail/equip evidence remain compatible.

## Packet contract

Not applicable: initial/Resume only call the actual read-only saved inspector.
Resume invokes exactly that child; listing/status execute no inspector, host
probe or other child. No client packet, kit request/delivery or wire capture is
produced. Saved observations retain exit2, LiveClientVisualPending/WirePending/
SourceGatePending and liveStoreSynchronized=false. They never become PASS or
ServerStateVerified/runtime proof because editable JSON appears consistent.

## Catalog/table contract

Reuse actual health's complete bounded saved report validator: real nullable
saved hitpoints/minMana/maxMana must be present integers, exact identity/domain,
all seven named item containers plus declared deposited rows, unique UIDs,
complete counts/rows and stable six file projections. Missing/truncated/malformed
or counts-only data refuses; no absent vital becomes zero. No new inspector or
catalog amount/max calculation is invented.

Closed type classes match current source: health ranges unchanged; Mana1001000..
1001040 or1002030/1002040/725066/1004010; FullRestore only723726. This classification
is source selection, not client evidence or a resource formula proof.

## Validation and transaction rules

All resource scenarios require exact item UID/type and a selected inventory row
at position0/warehouse0. Health retains existing expected changed HP semantics.
Mana requires explicit expected saved Mana strictly above before and unchanged
HP. FullRestore requires explicit expected saved HP and Mana, neither decreases
and at least one increases; either may remain unchanged. Do not infer or require
the unresolved runtime HP-full/Mana-low trigger from saved observations.

Retain create-new owner/before/initial receipt plus strict closed-key intent.
Keep health-intent.json/schema1/status/pins compatible; new distinct mana-intent
and full-restore-intent leaves retain exact expectations. Resume checks original
hashes, owner/domain/account/character and identical explicit arguments BEFORE
its sole inspector child, then creates new after/receipt leaves without replacing
original evidence. Disallow send/source/walk/equip/mixed resource arguments.

Pure transition requires exact selected one-unit use (stack0/1 removes UID;
stack>1 decrements only stack), unchanged all other complete reported item/mail
rows, correct count changes, exact expected selected vitals and unchanged selected
identity/map/X/Y/body/class/level/currency/MaxMana/other vital. Compare unchanged
Spells/Proficiencies/Mailbox byte digests/presence/length. Character/Items images
must differ, but their opaque bytes are not revealed: never claim full opaque
Character/item preservation from these summaries. Status reruns this same pure
transition and strict retained intent/hash correlation; editable PASS/assertions
or refreshed outer hashes cannot replace the comparison. No automatic retry.

## Expected wire behavior and fixtures

No wire behavior exercised. Genuine future tests invoke actual PowerShell child
runner/status and actual DataTool against real owned GUID JSON/file fixtures.
Cover both scenarios, last/zero/stack, full restoration with only one improving
vital, complete extra containers, pending-before-action, exact retained originals,
no data mutation, sole inspector step and pending flags. Invalid expected vitals,
wrong type/UID/stack/identity/location/money/other vital/sidecar, missing/null fields,
real truncation and unknown containers/duplicate rows must refuse.

Tamper cases refresh editable hashes but alter after/intent/state: status remains
invalid through pure comparison. Malformed/duplicate/unknown/foreign/mixed intents
and changed arguments must stop before Resume's child. Listing/status remain
read-only and old health evidence remains accepted. Update current catalog to14
families only after implementation; preserve historical12-family documentation.

## Evidence alignment

| Requirement | Existing mechanism | Change |
| --- | --- | --- |
| Genuine saved observations | inspect-owned-player + bounded complete reports | Reuse; no new runtime projection |
| Preserve health consumers | health-intent/schema1 and old names/status | Thin compatibility wrappers around shared resource flow |
| One-unit/unchanged selected state | Existing health pure comparison | Shared closed-resource transition with resource-specific vital rules |
| No editable status promotion | Existing health status pure recheck | Same recheck for Mana/FullRestore |
| Opaque/runtime trust limit | Summary selected items/Character, full untouched sidecar digests | Explicitly bounded; no full opaque item/Character or runtime claim |

## Implementation and qualification

Focused resource assertions/intent/orchestration/status helpers with health
compatibility entry points; narrow runner/status/catalog branches. New actual
PowerShell/DataTool child test source and current guide/matrix updates. Root alone
reviews/intakes/parses/builds/tests and records actual results; no execution here.
No source/JSON result establishes gameplay, authenticated runtime, visual, wire,
Store equality, durable transaction atomicity, SQL, power loss or full parity.

Root qualification2026-10-29: all11 changed PowerShell files parse without errors;
actual Release build02 zeroerrors/no changed-code warnings. Selected actual runner,
status, saved-inspector, old health/walk/mail/equip and new resource/kit child tests
passed56/56, zero failures/skips, in6m55s. Both old unsupported-entry controls now
pass; their original FAIL2/2 receipts are retained. The mailbox runner also accepts
-Kit consumables and proves request preparation without a send or player-file
mutation. Full Windows CI and real client/wire/Store acceptance remain separate.

Latest qualification, superseding the pending full-gate note above:

Full Windows [40839078556](https://github.com/vgcman16/wolfconquer/actions/runs/40839078556)
passed on exact ad23e71e2ed25da043f1dcbdfd0517c88200358f: Core29,632 passed,
152 existing external-fixture skips, Updates446/446 and Launcher57/57, zero
failures; all eight stages passed. The actual local check finished
2026-10-29T08:42:46.3993684Z. Logs, all three TRXs and matching World/DataTool
outputs are preserved outside the runner. This qualifies the published offline
consumable/tool checkpoint; it does not prove live rendering, SQL/power-loss
durability, physical Mana ownership or full gameplay parity. No deployment,
client relaunch or retained gear resend followed.
