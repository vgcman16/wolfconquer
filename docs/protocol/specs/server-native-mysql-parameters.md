# server-native-mysql-parameters: native SQL command ownership

## Identity and scope
- Revision 1, 2026-10-29; authorized offline server safety increment.
- Baseline public commit: `e026be7f221847cccd8c3b3b0a8a69a85becdf82`.
- Target client/profile: not applicable; no packet, catalog, dispatcher or gameplay rule changes.
- Preserve the fluent SELECT/UPDATE/INSERT/DELETE/COUNT API and successful query meaning. Replace value interpolation with real `MySql.Data.MySqlClient.MySqlCommand` parameters.
- No database connection, live fixture, deployment, client execution, schema or transaction changes are authorized in this candidate.

## Packet contract
Not applicable: this change constructs server SQL only and preserves existing wire behavior.

## Catalog/table contract
Not applicable: there is no new table layout. Observed source callers use `accounts`, `Username`, `ID`, and `State`. Existing offline coverage also exercises `configuration.PlayersOnline`.

## Validation and transaction rules
- Fluent values remain data, including apostrophes, backslashes, controls, Unicode, signed/unsigned boundaries and culture-sensitive numbers. Strings are not escaped or encoded into SQL text.
- Bind each value with a fresh deterministic parameter name and appropriate provider type. Boolean API values retain numeric 0/1 meaning. Boxed primitive values retain their types; other object values use one `ToString()` result as bound text. Missing values and malformed identifiers reject before changing builder state.
- Fluent identifiers accept simple ASCII SQL names (letter/underscore followed by letters, digits or underscores). ORDER accepts a simple name and optional ASC/DESC, with no expression/operator injection. Comparisons are fixed `=`, `>` or `<` chosen by the existing overloads.
- Type assignment starts a new template and clears old bindings, insert fields, comma and predicate state. Explicit raw Command assignment remains operator-owned SQL and also clears those states. Disposed owners reject reuse.
- Materialize INSERT fields/placeholders without mutating builder state or byte-sized indexing. COUNT means `count(*)`; incomplete fluent statements reject before connection access.
- Execute/Execute2 and SELECT adapter all consume the same real provider-command creation method. Every call receives independent parameter instances. External connections stay caller-owned; internally created connections and provider commands are disposed. Execute, Execute2 and the SQL reader share a required configured connection-string guard before the connection getter, and never write deployment settings. Execute with an explicit caller-supplied connection does not consult unrelated global configuration.
- Reader owns its buffered DataSet, provider command, adapter and internal connection, including constructor failures. Public Dispose, interface Dispose and Close perform the same idempotent cleanup. The caller retains ownership of the builder.
- This is parameter construction/resource cleanup only. It does not establish transactional/idempotent persistence, authorization or crash-safe multi-row updates.

## Expected wire behavior and fixtures
- Wire behavior: unchanged; no new successful response is published.
- Owned offline fixtures inspect actual provider CommandText, parameter names/types/values, independent snapshots, reset/reuse, invalid-state rejection, reader preparation and cleanup. They never open a connection.
- Replace historical defect assertions (identity escape, concatenation, invalid templates, stale state, byte wrap and cleanup no-ops) with safe invariants while retaining successful operation/caller coverage.
- Future owned SQL qualification must independently check execution against the pinned provider; command inspection is not SQL-I/O or gameplay proof.

## Evidence alignment
| Requirement | Source evidence | Status |
| --- | --- | --- |
| Value injection | builder Set/Insert/Delete/Where/And/Or concatenate values; identity MySqlEscape | Observed unsafe source |
| Reader parameter ownership | reader constructs adapter from command.Command string | Observed parameter-loss seam |
| Real caller meaning | Dialog.Exists and GameClientRuntime.AccountBan | Observed SELECT/UPDATE constants; no live execution |
| Configured Execute2 | public guard uses DataHolder getter; private deployment override reported by Root | Public guard preserved; private integration must use public safe implementation, never settings |
| SQL execution behavior | no owned SQL fixture authorized | Unverified, fenced |

## Implementation and qualification
- Focused implementation: `Data/LegacyNativeMySqlCommand*.cs`, `Data/LegacyNativeMySqlReader.cs`.
- Offline tests: native builder/reader tests plus existing account/configuration query-contract tests.
- Root released the serialized host slot. Local-cache-only restore01 succeeded; final Release build02 passed (0 errors/157 warnings). Focused01 passed 48/48, 0 failures/skips. Actual logs, JSON receipts and TRX are retained in the helper root. Root alone reviews, integrates, runs the combined/full gate and commits.
- Source-bug record: candidate `FIXED-SOURCE-BUGS.md`, FSB-199. Qualification is real provider-object/owned-DataSet offline evidence; constructor SQL-I/O cleanup is source-reviewed only. No connection was opened, no full gate was dispatched and no deployment or gameplay proof is implied.
