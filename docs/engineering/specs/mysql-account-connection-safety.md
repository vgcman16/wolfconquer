# SQL-CONFIG-1: account connection construction and provider fix

## Identity and scope

Revision1,2026-10-29. Modern MySqlConnector2.5.0 and the pure
LegacyGuardShieldAccountConfig conversion helper. The separately pinned native
MySql.Data6.3.6 compatibility assembly is unchanged. No production helper caller
was found beyond the method definition; existing tests call it. No existing
route/service/private config/database/account is changed by the source patch.

## Packet contract

Not applicable: no Conquer opcode, schema or successful client sequence changes.
Database TLS behavior follows the actual configured endpoint/provider contract.
Primary upstream GHSA-473q-m89c-ghf8 affects MySqlConnector<2.6.1;2.6.1 fixes
password transmission before certificate verification for the documented remote
zero-configurationTLS case. Upstream excludes loopback/Unix-socket cases.

## Catalog/table contract

Input is the already-decoded immutable account config strings, not a new catalog.
Host/username/database must be nonblank; password must be explicitly nonnull,
with an empty value allowed. Keep database/user/password literal values, including
quotes/semicolons; never concatenate them into connection-string syntax.

## Validation and transaction rules

- Use the real MySqlConnectionStringBuilder and fixed port3306.
- Only the literal localhost name (case-insensitive) or an IPAddress.IsLoopback
  address selects existing local SslMode=None/public-key-retrieval development
  behavior. Trim host outer whitespace; perform noDNS resolution/connection.
- Other hosts, lists and ambiguous names select VerifyFull with public-key
  retrieval false. No fallback to unvalidated encryption or cleartext.
- Pin modern package2.6.1. Keep nativeMySql.Data6.3.6/Protobuf identity intact.
- Redact the record textual representation. This is not a claim that arbitrary
  serializers/loggers cannot inspect its public credential properties.
- Pure construction only: no resource debit/save, database open, settings write,
  certificate import, runtime route switch or original database operation.

## Expected wire behavior and fixtures

Isolated tests parse actual builder outputs with synthetic values. Cover explicit
loopback variants, other endpoints/lists, delimiter/quote injection in each field,
null/blank required state, empty password and textual redaction. Existing parser,
account and SQL-source tests remain meaningful. No live remoteTLS acceptance or
fullSQL persistence claim follows a builder test. Appropriate next coherent full
CI gate follows focused verification; serialize the next full job after focused verification.

## Evidence alignment

- Existing helper concatenates all fields and forces SslMode=None for any host;
  compiler-generated record text includes its password.
- https://github.com/advisories/GHSA-473q-m89c-ghf8
- https://github.com/mysql-net/MySqlConnector/releases/tag/2.6.1
- https://mysqlconnector.net/overview/version-history/

## Implementation and qualification

Current full source gate: [Windows40826474342](https://github.com/vgcman16/wolfconquer/actions/runs/40826474342)
completed successfully on exact tracker child82882bd6 of source dda332e6.
All eight stages passed: core29,545 passed/156 existing skips, Updates446 passed
and Launcher57 passed, zero failures. Actual receipts, logs and all three TRXs
were retained. Managed World/DataTool binaries were copied outside the runner;
no server/client was deployed or launched. Native hydration, live acceptance,
SQL/power loss and complete gameplay parity remain open.

Account config class, modern package reference and isolated connection-safety
tests are the implementation scope. Root alone
reviews/integrates/builds/tests/publishes. Release restore resolved 2.6.1 and
build08 has zero errors; all 18 pure connection-safety cases passed in the
104-case selection. No new warning originated in changed code. The neighboring selection and full CI above also passed.
No credentials, production database connection, live deployment or source fix
acceptance is claimed by this specification.

Root neighboring account/mail/save/tool selection also passed 194/194, zero
skips, in 3m31s. It overlaps the 104-case selection. The full successor CI above passed; live acceptance remains open.
