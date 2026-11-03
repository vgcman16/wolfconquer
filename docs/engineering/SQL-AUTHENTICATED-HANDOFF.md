# Authenticated SQL world handoff

Source integrated and component/provider qualified 2026-11-03: 124 ordinary
account/Frame/handoff tests and the complete119-Fact owned SQL family passed,
with no failures/skips. The successor's full Windows integration gate remains
required. This separate internal SQL handoff does not change native wire layouts, activate a
backend, or reinterpret the legacy file ticket's character identifier as an SQL
authentication account ID.

The concrete credential repository acquires real retained authentication custody.
The issuer also requires real current world bundle custody, matching account and
character identities, complete six-image/UID state and actual SQL-domain proof.
Only then may it commit a hashed random token and independent issuer-held child
claim in SQL. The public offer retains the issuer until redemption, expiry or
disposal; its async watcher and all cleanup failures remain owned and observable.

Redemption locks the exact token row, checks complete stored fields and one-use
state, endpoint, server UTC, live issuer claims, current credentials/permissions
and actual world custody. Account and character IDs remain distinct. A matching
ticket row alone does not establish authority. Both credential transactions use
Repeatable Read and require the actual accounts base table to use InnoDB around
its locking read. No account schema or global setting is changed automatically.

The redemption update is constrained by its actual server-time window. The result
is prepared before COMMIT and exposed only after commitment and cleanup settle.
Known commitment followed by loss/cleanup and an unknown COMMIT outcome are
distinct errors. Neither becomes an ordinary refusal or an invented success.

Explicit schema installation owns only wolf_sql_auth_handoff_schema/version1 and
wolf_sql_auth_handoff. It verifies actual installer-lock ownership, exact bounded
table/column/index/engine/version state and rejects partial schemas before DDL.
Complete schemas are verified without marker writes. There is no auto-healing,
account-table migration, file fallback or implicit startup installation.

Tokens and private claim/fingerprint metadata are not logged or rendered through
generated value ToString methods. Source constraints and no-grant boundaries are
part of the contract; testing must use concrete repositories and real owned SQL.

Frame/transport lifetime, native/Store/sidecars and all gameplay writers, global
UID issuance, normalization/recovery, actual separate-process acceptance and the
7937 client profile remain open. The current native gameplay backend still saves
files. [Current source and qualification limits](../checklists/SQL-AUTHENTICATED-HANDOFF-20261103.md).
