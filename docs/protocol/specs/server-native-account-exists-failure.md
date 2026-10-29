# server-native-account-exists-failure: preserve lookup failure versus absence

## Identity and scope
- Revision 1, 2026-10-29; Root-assigned sanitized-parity defect FSB-200.
- Clean public baseline `233bd0d0ffcbdf131c638dd03b9f49fff76a83de`, containing public source `a4380ba1`.
- Target client/profile and catalog: not applicable; server-side SQL lookup failure semantics only.
- Change only native `LegacyNativeDialog.Exists(string username)` and focused regression/documentation. Preserve the integrated FSB-199 parameterized command and buffered reader package unchanged.
- Connection Open, live SQL/account mutation, dispatcher/host registration, client execution and deployment remain outside these offline controls. Root authorized the source/data qualification recorded below.

## Packet contract
Not applicable: no packet identifiers, schema, encoding or response sequence changes.

## Catalog/table contract
- Existing query remains `SELECT * FROM accounts WHERE Username = <bound username>` with actual provider parameter values.
- The actual buffered SELECT result determines existence: a successfully read row means true; successfully completed empty result means false.
- No schema/table/catalog changes and no in-memory account repository substitute.

## Validation and transaction rules
- Required username is non-null and non-whitespace. Evidence: `AccountHandoffService.Issue` uses `ArgumentException.ThrowIfNullOrWhiteSpace`; `AccountLoginService` rejects whitespace-only usernames before repository lookup.
- This does not establish a game-name policy. Do not invent length, punctuation, Unicode or normalization restrictions. Preserve every valid supplied character in the parameter; do not trim or rewrite.
- Invalid required input throws before constructing the query/reader or consulting SQL configuration.
- Keep the genuine parameterized builder, actual SQL-backed reader constructor and using-owned cleanup. Return the actual `Read()` result directly.
- Configuration/provider/query/reader failures propagate to the caller. Do not catch and translate them to false, true, success, not-found, or an accepting callback. Do not log provider exceptions from this method because they may contain operational details.
- No transaction, mutation or success notification is added. Caller review must preserve the distinction between an absent account and failed lookup before installing a future caller.

## Expected wire behavior and fixtures
- Wire behavior: unchanged; no installed production caller was found by full source search. The only existing test reference inspects the method signature.
- Offline controls call the actual method with invalid required inputs, missing configuration, and synthetic unsupported provider configuration. Those errors occur before any connection Open; no hostname, credentials, original DB or accepting provider is used.
- Successful boolean semantics use the existing real buffered reader over sole-owned DataSet results and are cross-referenced to the method's direct `Read()` return. No alternate reader/result callback is introduced into production.
- Source review confirms the direct Read result and normal using cleanup; behavioral tests exercise real early lookup failures. This is not real SQL-I/O success proof.

## Evidence alignment
| Requirement | Evidence | Status |
| --- | --- | --- |
| Failure confused with absence | baseline broad catch logs Exception then returns false | Observed source defect |
| Required username | current account handoff/login owners | Observed non-whitespace prerequisite; no broader game-name rule inferred |
| Parameterized query and real reader | integrated FSB-199 builder/reader | Retained unchanged |
| Installed consumer | complete src/test name, alias and reflection-reference searches | No production Exists call found; one signature test |
| Config/provider failures | actual Exists path, missing config and unsupported-option fixtures | 15 focused outcomes passed within combined direct556; real SQL I/O remains unverified |
| Valid SQL execution | no owned SQL fixture authorized | Unverified; remains fenced |

## Implementation and qualification
- Production: `src/WolfConquer.Core/World/LegacyNativeDialog.Exists.cs`.
- Tests: new `LegacyNativeDialogExistsSafetyTests.cs`; update the stale signature-test comment without changing packet assertions.
- Source bug: FSB-200 candidate in `FIXED-SOURCE-BUGS.md`.
- Root Release build01 completed with0errors. The15 account outcomes passed within combined direct556/556, which also included41 Info and20 new catalog-decoder outcomes.
- Initial neighbors01 completed2095passed/2failed and is retained as pre-fix evidence. Root corrected the cache test and separate Ninja FSB-203 defect. Release build02 completed with0errors (157warnings); combined02 passed2657/2657,0failures/skips, covering direct556 plus neighbors2101. Targeted6/6 are a subset, not additional outcomes. Integration is reviewed; the new full Windows gate remains pending at this qualification checkpoint. Root alone integrates, commits and dispatches that gate.
- No claim of live SQL, account-registration safety, complete persistence, gameplay parity, deployment or host cleanliness follows from offline failure/reader controls.
