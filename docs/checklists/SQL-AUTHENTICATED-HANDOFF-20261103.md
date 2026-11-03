# Authenticated SQL handoff qualification

2026-11-03. Integrated source on base `e6f1361c`: 56 C# paths across the
authentication and handoff batches. The focused build and ordinary account,
Frame and handoff selection passed **124 tests, 0 failed, 0 skipped**. The complete
owned MariaDB provider family passed **119 tests, 0 failed, 0 skipped**: 59
predecessor, 32 authentication and 28 handoff Facts. Root matched every unique
source-discovered method to an actual passing TRX result. These are component
and provider results, not live-client completion.

- [x] Review concrete credential ownership, distinct account/character identity,
  actual domain and six-image/UID proof, durable one-use row and retained offer.
- [x] Review explicit two-table schema installation, strict read-only verification,
  installer exclusion, current account locks/engine, expiry and outcome handling.
- [x] Author28 actual-provider handoff controls:25 main cases plus held installer
  lock, rejecting-update-trigger and unsupported account-engine controls. The UID
  mutation control proves complete valid raw custody before identity rejection.
- [x] Build and run ordinary account/frame/handoff neighbors: focused run
  `20261103T223900640Z-b7f98028`, 124 passed. Build168.96s/test11.175s.
- [x] Execute all 119 provider Facts on one fresh owned MariaDB12.2.2 instance,
  separate port34397, without an individual-test selection. The actual TRX
  finished22:46:08UTC. Receipt: Qualified=true, FullProviderFamily=true, child
  tree/commands settled, owned data removed, no primary/cleanup errors. Root also
  checked the listener closed and private options/processes were absent.
- [ ] Root reviews privacy/diff and publishes the coherent tested batch normally;
  qualify the exact source with one appropriate full Windows integration gate.

The [published predecessor's full Windows gate](FULL-WINDOWS-20261103-E6F.md)
passed separately. Its outcome does not qualify this successor source; the new
full Windows integration gate is still required. No duplicate gate was started.

Second physical-server isolation, separate-process acceptance, COMMIT/ACK-loss and
mid-COMMIT cancellation injection for these new APIs, DDL races, an observed
duplicate-insertion gap-lock witness, clock faults and live malformed-row/watcher
races remain unproved. A killed-issuer control accepts only an observed exact
provider fault after joined disposal and real session/claim/reacquisition checks;
aggregate or unexpected cleanup faults remain failures.

No startup/Frame/native activation, original data mutation, client execution,
English resource application, routing change or deployment occurred. The full
English source/client objective remains active. [Contract](../engineering/SQL-AUTHENTICATED-HANDOFF.md).
