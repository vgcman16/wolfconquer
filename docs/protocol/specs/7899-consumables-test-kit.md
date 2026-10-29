# KIT-CONSUMABLES-1: retained local consumables mailbox recipe

## Identity and scope

Revision1, 2026-10-29. Extend the existing operator request generator with the
`consumables` recipe: one physical row each of health1000000, Mana1001000 and
FullRestore723726. These IDs are the existing source classifier/test contracts,
not evidence that every installed client has matching metadata or artwork.
Target Chinese7899/VisibleDx8D13 through the existing default-off owned mailbox
operator service. Preserve existing gear/material recipes and retained operations.

## Packet contract

No gameplay opcode, layout or client-profile change. Generate the existing strict
operator request only; generation neither reserves UIDs nor sends. Delivery and
claim use the real existing mailbox implementation. No automatic retry or login.

## Catalog/table contract

No catalog decoder change. Every requested type must resolve in the actual
server catalog and pass existing row/stack/durability validation before the real
delivery owner reserves UIDs. Missing rows refuse; never synthesize production
metadata. Consumption separately validates its own HP/Mana amounts and caps.

| Recipe | Type IDs | Quantity and enhancements | Source contract |
| --- | --- | --- | --- |
| consumables | 1000000,1001000,723726 | One unit each, unbound, plus0, no sockets | LegacyConsumableItemUse.Classify and owned consumable fixtures |

Read-only2026-10-29 observation of the preserved local
`.setup_work/seed-json/items.json` also resolves exactly one row of each type:

| ID | Seed name | ItemHP(field18) | ItemMP(field19) | Durability(field21) | Stack cap(field46) |
| --- | --- | --- | --- | --- | --- |
| 1000000 | Stancher | 70 | 0 | 1 | 5 |
| 1001000 | Agrypnotic | 0 | 70 | 1 | 5 |
| 723726 | LifeFruit | 0 | 0 | 1 | 5 |

This is saved catalog provenance, not a live-server catalog attestation. The
FullRestore amount comes from its existing rule, not zero amount columns.

## Validation and transaction rules

Keep operation/domain/account/character requirements, fresh-output-only CLI,
same-user pipe and actual runtime/UID/journal ownership unchanged. Unknown recipe
names reject. Recipe creation has no authority to deliver. A lost/uncertain result
retains the same operation; a fresh operation is not a safe retry. Delivery does
not consume or equip items, alter currency or settle client rendering acceptance.

## Expected wire behavior and fixtures

Exercise the actual generator child command and strict request parser. Then use
the genuine admitted owned-mail fixture to deliver all three rows, correlate
allocated UIDs with saved mailbox/attachments, reject replay without another
allocation, and claim each on the same live fixture connection. Also test a
missing actual catalog row refusing before UID/file/session/output changes.
These are owned managed fixtures, not the original game executable or live PC.

## Evidence alignment

| Requirement | Existing evidence | Limit |
| --- | --- | --- |
| Correct resource categories | Existing classifier and admitted health/Mana/FullRestore regression inputs | Not new client-wire discovery |
| Real delivery/claim | Actual operator owner, v2 journal, registry and Frame fixtures | Requires target catalog; no live rendering claim |
| No repeat logout required | Existing same-connection mailbox implementation | Current host execution hold still applies |

## Implementation and qualification

Extend LegacyOperatorTestKits and CLI help. Keep independent literal recipe and
real child/owner tests. Root qualifies this coherent consumable publication batch
with focused tests and the full Windows gate. No real mail is sent by this work.

Root qualification2026-10-29: actual build08 zeroerrors and combined selection
205passed/2existing external-health skips/0failed in1m18s. Original FullRestore
refusals2/2 and restoration/Mana controls now pass. This includes genuine journal,
recovery, trade, reload and mailbox-kit child/delivery/claim checks. The full
Windows publication gate and live-client acceptance are still separate/pending.

Acceptance-runner companion: mailbox-gear also accepts explicit -Kit consumables.
Without -SendTestKit it only retains the ordinary request and reports
RequestPreparedNotSent. An actual child test must verify all three requested
types, unchanged owned player files, no delivery result and exactly inspection
plus request generation. Resource-observation scenarios still forbid sends.

Root qualification2026-10-29: all11 changed PowerShell files parse without errors;
actual Release build02 zeroerrors/no changed-code warnings. Selected actual runner,
status, saved-inspector, old health/walk/mail/equip and new resource/kit child tests
passed56/56, zero failures/skips, in6m55s. Both old unsupported-entry controls now
pass; their original FAIL2/2 receipts are retained. The mailbox runner also accepts
-Kit consumables and proves request preparation without a send or player-file
mutation. Full Windows CI and real client/wire/Store acceptance remain separate.
