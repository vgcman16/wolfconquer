# Captured native preparation and admission-image results

The integrated batch adds strict captured Character parsing, preparation against
real native participants, complete SQL admission-image normalization and safe
paid-nobility validation/saving. It does not activate a SQL-native login backend.

The first affected run `20261103T102915484Z-a3f84ef7` built successfully and
finished **700 passed / 10 failed / 0 skipped**. The ten failures were older
login assertions that required malformed values or truncated strings to be
accepted. Root reviewed the isolated test corrections: complete valid and
missing-value positives remain; original bad inputs now require genuine
encrypted-login refusal with unchanged native owners/values, files and session.
Production guards were not relaxed to repair these expectations.

Repair/component run `20261103T104604967Z-a15a920b` then passed **221 tests,
0 failed, 0 skipped**; build88.800s/test17.525s. Actual TRX rows comprise:

| Family | Passed |
| --- | ---: |
| Captured Character data | 34 |
| SQL ordinary-admission images | 47 |
| Paid-nobility input and writer safety | 33 |
| Existing native prefix | 46 |
| Captured native preparation and repaired login controls | 61 |

This is a targeted retry, not a rerun of all710 earlier rows. The unchanged
129-login history test had already passed in196.162s; full integration retains
it. Counts from overlapping runs are not added into a synthetic suite total.

The closed real-provider family then passed **42 actual tests / 0 failed /
0 skipped** on fresh owned MariaDB12.2.2. The two added cases prove actual SQL
read-plan-save-cold-read into genuine native preparation, and second-participant
write failure rolling back all six images plus the UID ledger, followed by a
successful retry of the same operation. These tests do not publish a native
session. All prior40 provider cases also executed.

Root read the actual42 TRX rows and terminal owner result: Qualified=true,
PassedSqlFacts=42, ChildTreeSettled=true, CommandsSettled=true,
DataRetained=false, with no primary or cleanup exception types. The actual
temporary datadir and private options file were absent after completion.

The coherent successor full Windows gate and exact public-commit CI remain
required. The previous [f483 full result](FULL-WINDOWS-20261103-F483.md)
qualifies the earlier source only. Authentication, privately issued Store/SQL
revision/UID custody, complete native sidecars/writers, recovery and live7937
acceptance remain unfinished. The paid uncertain-outcome branch is still
source-reviewed, not fault-injected; one-file save tests are not power-loss or
multi-file transaction proof. Existing execution holds remain unchanged.
