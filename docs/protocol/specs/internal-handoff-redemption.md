# Internal handoff redemption ownership

## Identity and scope

Revision1, 2026-11-03. This corrects an internal account-to-world ticket race;
it does not define a native client login grant or Chinese7937 authentication.
Target existing AccountHandoffService and its actual Memory/File stores.

## Packet contract

No packet ID, payload, encoding or successful sequence changes. Existing internal
world-enter request/response types remain authoritative for their wire contract.
Issue retains the account, username, endpoint, timestamps and random token.
Redeem must validate and consume that token in one actual storage operation
before returning Success. Unknown, Expired and AlreadyRedeemed statuses retain
their established ordering and values; no synthetic success is introduced.

## Catalog/table contract

No item table applies. Memory uses its actual dictionary and shared store gate;
File retains its signed ticket image, exclusive ticket lock and persisted update.
The Memory store is ephemeral/process-local, not durable or cross-process SQL.

## Validation and transaction rules

- Service construction requires IAccountHandoffTicketRedeemer from the actual
  store, with clear ArgumentException for unsupported non-null stores and the
  existing ArgumentNullException for null. No ticket is issued/read/saved first.
- Service delegates directly; separate TryGet/check/Save is never an atomic
  redemption fallback. A per-service lock would not protect two service owners.
- Memory lookup, replay/expiry checks and consumed-record replacement use the
  same existing store gate. Only one caller receives Success for that transition.
- Blank/unknown and inclusive expiry do not mutate a record. A replay returns
  the original consumed record, including its original redemption timestamp.
- Atomic capability is an implementer contract, not certification of arbitrary
  custom stores. Such stores must supply a real validate-and-consume operation.

## Expected wire behavior and fixtures

Existing AccountHandoffServiceTests and AccountHandoffWorkflowTests remain.
Reuse the existing signed File parallel-use/cold-instance/tamper controls.
Five new Facts use actual Memory/services:16 settled parallel calls across two
services, exact identity/time/replay, blank/unknown nonconsumption, inclusive
expiry, independent tickets and unsupported/null constructor guards. The
negative unsupported store never accepts authority or performs successful IO.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
| Source race | Old Memory implements only separate locked Save/TryGet; old service performs fallback read/check/write | Source finding; no executed exploit demonstration |
| Atomic Memory transition | Actual shared gate covers lookup/check/replace | Reviewed, unbuilt/unrun |
| File behavior | Existing real File redeemer and unchanged parallel/cold/tamper tests | Prior behavior retained; successor qualification pending |
| Native authenticated login | No matching native grant is added here | Remains separate and unresolved |

## Implementation and qualification

Production: Accounts/AccountHandoffService.cs, InMemoryAccountHandoffTicketStore.cs
and the existing IAccountHandoffTicketRedeemer.cs contract. Tests:
AccountHandoffRedemptionSafetyTests.cs plus unchanged service/workflow neighbors.
The five new Facts and existing service/file/workflow controls passed within
229focused tests. [FSB-339](../../../FIXED-SOURCE-BUGS.md) awaits the successor
full public CI. No client execution, backend switch,
cross-process Memory guarantee or host-trust/live acceptance claim follows.
