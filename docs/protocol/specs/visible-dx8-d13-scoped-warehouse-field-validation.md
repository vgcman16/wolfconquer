# Scoped warehouse fields and complete persistence inputs

## Identity and scope
- 2026-10-30; `VisibleDx8D13`, exact public base `e0f22c8b`.
- Full Windows run41095165573 exposed warehouse publication rejecting unrelated
  DynamicID/dynamicid state before the drop handler could reject that scope.

## Packet contract
No wire change. Preserve successful warehouse/CP-bank packet bytes and order.

## Catalog/table contract
WHMoney and WarehouseMoney are optional signed64 nonnegative aliases. Present
aliases must agree; case duplicates of either alias remain invalid. This reader
does not validate unrelated field namespaces. Native source proofs retain every
raw BCL dictionary row and detect value-only changes without caller callbacks.

## Validation and transaction rules
Native warehouse publication may preserve unrelated case-distinct fields so
their actual operation owner can validate them. A Character persistence action
must separately reject an ambiguous complete field map before preparing a WAL;
never drop rows or serialize duplicate case-insensitive keys. Bank refusal must
preserve pending confirmation, resources, files and output.

## Expected wire behavior and fixtures
Keep the actual invalid-DynamicID drop refusal test. Add scoped reader/bound
Store controls and real bank ambiguity refusals. Existing invalid warehouse
alias, drift, writer and wire goldens remain required. The two old bank positives
must use a real owned Runtime/Frame/Store instead of an unowned internal login.

## Evidence alignment
Source: LegacyWarehouseMoneyState, both bank plan builders, actual failed Core
TRX and the existing native warehouse/bank fixtures. No new client observation.

## Implementation and qualification
Root's builds passed with zero errors. Six new scoped/bank controls and all four
actual invalid-DynamicID drop cases passed. The two migrated warehouse positives
retain their original balances and complete bytes; an authored1009 test constant
was corrected to the existing D13 opcode2137, after which the13 affected controls
passed. Production packet IDs were unchanged. FSB-242 records the source fix.
The wider run retained737 passes/two test-golden failures; both failures passed
in the corrected13-case rerun. The public-commit full Windows gate is still
required. No SQL, deployment, client execution or global parser-policy change.
