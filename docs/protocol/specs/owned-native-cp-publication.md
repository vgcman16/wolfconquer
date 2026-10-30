# Owned native ConquerPoint family publication

## Identity and scope

Revision1, 2026-10-30. Source-only successor to public2ff18f12 plus the reviewed
Money/repair/testing candidate. Profile: ordinary `VisibleDx8D13` / Chinese7899.
No7937 protocol, runtime deployment or live-client acceptance is implied.

The actual native Player lacks its inventory `_cps` slot and ordinary publication
does not join CP, bound CP or bank CP to the managed saved Character. Add the
real slots and a closed participant in the existing Store admission and bound /
deferred publication paths. Do not create a separate wallet facade or producer.

## Packet contract

No new packet ID, shape, encoding or successful handler ordering. Existing
MsgUpdate2176 fields are inventory CP29 (`0x1D`) and bound CP45 (`0x2D`). Native
component setters emit only when the actual client `FullLoading` is true. Owned
hydration/publication writes the prepared physical slots directly and emits no
extra packets, dialog, spill or log. Existing handler goldens remain unchanged.

## Catalog/table contract

No new catalog. Evidence is the decompiled Player1135/1141, Player2520..2550 and
2584..2605, GameClient49, the existing strict Character parser and typed owners.

| Saved value | Actual native slot | Owned representable range |
| --- | --- | --- |
| `ConquerPoints` signed64 | Player `_cps` UInt32 | 0..UInt32.MaxValue |
| `BoundConquerPoints` signed64 | Player `_bountCps` Int32 | 0..Int32.MaxValue |
| `BankCps` signed64 | actual client `ConquerPointsSave` UInt64 field | 0..Int64.MaxValue |

These ranges are current native representation prerequisites, not client-confirmed
wire or gameplay limits. Global Character/parser/protocol bounds stay unchanged;
future type/profile mapping for larger saved values remains open.
Reject unsupported saved/proposed values before a writer. Never clamp, wrap,
truncate or silently substitute zero. The native setter's two-billion spill rule
does not run during hydration. Bank remains its real public field; this work
cannot exclude noncooperating raw field writers.

## Validation and transaction rules

- Admission requires the actual Store/client/player gates, prefix-issued owner,
  prepared UID and fresh unbound physical participant. Capture all three slots
  and prepare the exact validated saved values before publication.
- Subsequent publication requires the same current binding/holder/record, and
  all native slots must match the exact managed before-values. Retain the actual
  binding and complete physical before-tuple through callback and writer proofs.
- Deferred mailbox preparation must reprepare the CP tuple from its final real
  proposal under the original baseline, rather than retaining unchanged values
  prepared before claim planning.
- The actual Store-issued deferred-mail capability rechecks its Money and CP
  tuple after callback-bearing catalog/recipe preparation, before the actual
  producer's first UID reservation. A currency fault cannot burn an item intent
  merely because final native preparation follows materialization.
- Publish the three values and attachment only with the Store's successful
  outcome; no callback, allocation, sender or arbitrary writer is accepted here.
- Direct inventory/bound-CP setters on attached or retired players refuse before
  any mutation/output. Explicit unbound component setters use client-then-player
  actor order. Bound CP rejects negative values before any packet/pool mutation.
- A new inventory-CP component setter ports the real UInt32 slot and <=2b
  FullLoading update behavior. Its >2b spill requires the absent genuine
  ServerDatabase.LoginQueue owner as well as bank/dialog ownership. That branch
  stays unavailable before all effects; no accepting queue is fabricated.
- Managed CP floor pickup uses a checked wide total before applying the existing
  two-billion inventory cap and transferring all excess to the actual bank.
  The old unchecked UInt32 sum could silently lose earned CP. The internal
  transferred amount is Int64 because excess can exceed UInt32; its only
  consumer formats an existing dialog string, not a wire-width field. Required
  bank overflow refuses before floor claim/commit and leaves all values intact.

Current candidate has no production callers of native inventory CP, bank-CP
assignment or BoundConquerPoints setter beyond their own definitions. DTO/trade /
floor item fields and managed Character proposals are different owners. Do not
infer authority from similarly named fields.

## Expected wire behavior and fixtures

Use actual ordinary Frame fixtures for saved/boundary loading, invalid admission,
bound/retired setter refusal and negative/out-of-range proposals that never reach
an accepting writer. Use real combined ordinary/mailbox claim transactions for
deferred tuple publication and replay. Preserve first-reply managed/native/saved
equality and established packet bytes/order. Inject raw drift only into owned
genuine slots to prove prewriter refusal or post-real-writer retirement.

Native component controls retain independent exact MsgUpdate goldens, loading
gates and pool state. Migrate earlier negative bound-CP acceptance tests to
unchanged-state refusal. Above-cap CP spill must refuse unchanged until its real
missing owner exists; no successful spill claim is made.

## Evidence alignment

The source establishes native types, field numbers, loading gates and the
missing logging prerequisite. Managed Character ownership supplies the actual
saved values. The existing closed Money/HP publication participants establish
the real Store lifecycle and proof sequence. Raw protobuf or binary disassembly
is unnecessary for these already represented contracts.

## Implementation and qualification

New native ConquerPoints and OwnedCps partials plus existing BoundConquerPoints,
ordinary admission and bound/deferred Store publication partials. New tests use
owned temporary fixtures only. Root alone builds/tests/integrates/commits/pushes.
Root qualification: production/tests compiled with zero errors; the integrated
currency/CI batch passed387/387 focused outcomes, zero failures/skips. The actual
public-commit full Windows gate remains pending. This supersedes the authored
candidate's earlier UNRUN label; it does not establish live-client acceptance.

This is cooperating CP-family synchronization, not general currency transaction
safety. Generic Set/CAS best-effort scalar persistence, SQL, unbound native spill,
other native currency transactions, raw writers, power loss, host trust and live
client acceptance remain unfinished. Actual client/updater execution hold stays.
