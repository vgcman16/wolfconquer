# Owned native Money publication

## Identity and scope

Revision 1, 2026-10-30. Target: saved ordinary admission and the actual Store's
`VisibleDx8D13` / Chinese 7899 native participant. This fixes a source gap exposed
by owned startup recovery: Character and managed Money were 700 while the real
native Player Money slot remained zero. It adds no 7937 feature or live proof.

## Packet contract

No new packet, opcode or output sequence. Existing Money updates use MsgUpdate
2176, field type 4; existing repair goldens remain authoritative. Hydration and
Store publication write the private physical slot without calling the emitting
Money setter, spilling to warehouse/poker or producing an extra update/chat.

## Catalog/table contract

No new catalog layout. The existing strict Character parser supplies nonnegative
signed 64-bit Money. Saved values are retained without applying the legacy
setter's 500-billion inventory spill rule during load. Warehouse, CP and bound-CP
projection remain separate unfinished work.

## Validation and transaction rules

- Prepare only through the actual ordinary admission owner or current Store
  binding, with the real client/player gates and exact session identity.
- Admission captures the existing private native slot and prepares the parsed
  saved value. Bound publication requires native Money to equal the exact current
  managed Character value before preparing its replacement.
- Check the same private slot and binding during callback-bearing preparation,
  before the actual writer and at the final plain proof. Publish only after the
  Store's successful outcome, without allocation, lookup, callback or output.
- Deferred mailbox preparation must prepare Money from its final actual proposal,
  preserving the original baseline instead of reusing the constructor's unchanged
  proposal. A failed/replaced actor cannot publish a new balance.
- A direct Money setter on an ordinarily bound or retired Player refuses before
  any side effect. Unbound component behavior remains separate, serialized in the
  existing client-then-player actor order.
- Reject negative setter values before mutation/output. A legitimate unbound
  overflow spill checks required warehouse state and checked total before changing
  Money, warehouse, poker or output. Ordinary values do not query the unused
  warehouse. This replaces earlier tests requiring negative balances or wraparound.

## Expected wire behavior and fixtures

Use real ordinary Frame/admission fixtures and paid/five-Meteor repair handlers.
Check parsed saved Money, managed state and actual native Money at first output,
unchanged output count/order, rejected costs and direct setter/retirement refusal.
Corrupt only owned native slots for drift controls. Keep valid unbound Money,
warehouse spill, poker and exact packet goldens. Rejected negative/overflow values
must preserve slots, real packet pool and output.

## Evidence alignment

The existing native Money getter/setter exposes the real `_Money` slot and the
legacy successful output sequence. The actual `OwnedHitpointsPublication` is the
closed owner/proof model; existing Store admission and publication own the gates.
The only other native Player.Money writers are the private, unregistered Hairface
Buy/BuyColor receiver, which debits before adding ownership. It remains unregistered.

## Implementation and qualification

Implementation: `LegacyNativePlayerRuntime.OwnedMoney.cs`, Money setter, and the
Store's existing ordinary and subsequent prepared publication participants.
Root owns compilation/tests/integration; authored source is not qualification.
The retained startup test requires native Money 700 and is not weakened.

This is cooperating physical Money publication, not global currency durability.
Generic Set/CAS still have existing postpublication best-effort scalar saves;
the repair and mailbox paths retain their real durable writers. Other wallet
slots, SQL, general native currency transactions, power loss, host trust and live
client acceptance remain open. Public raw fields/reflection are not cooperating
writers and cannot establish whole-world atomicity.
