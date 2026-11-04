# SQL connection custody and cleanup qualification

Verified 2026-11-04 UTC on the reviewed successor of `f88da998`.
The source is component/provider qualified; its new full Windows publication
check remains pending. Native gameplay is still file-backed.

## Implemented behavior

- Bind real successful SQL redemption custody to an already-established exact
  Frame/TCP lifetime. Retain true account ID separately from character UID;
  reject synthetic, foreign, initialized or unregistered connections.
- Keep bound or indeterminate connections fenced from legacy/file/GM fallback;
  recheck real ownership and lifetime around same-custody complete-bundle reads.
- Join actual EOF with SQL and legacy cleanup. Retain failed transaction,
  connection, auth, offer and world resources in disposal-only ownership while
  preserving known/unknown COMMIT truth, original stacks and ordered errors.
- Report the first actual joined offer-monitor fault, then allow a real later
  disposal retry. Retain pre-owner raw cleanup and nested pending resources.
- Strongly retain failed TCP retirement after producer-task removal; observe
  actual faults, attempt reader cleanup, and serialize retries and shutdown.
- Publish the existing trade/GM ended marker before the first awaited SQL world
  lease. A closing character cannot remain usable by a queued GM/mail action.

## Actual checks and retained failures

| Check | Actual result | Limit |
| --- | --- | --- |
| First affected build/test | Build passed; 407 passed, 6 failed, 0 skipped | Five new half-close fixture failures and one real delayed EOF-denial regression |
| Corrected managed run | 102 completed cases passed, 0 failed/skipped | Its remaining long history case was stopped by the 180-second inactivity watchdog; the overall run was aborted |
| Isolated long history rerun | 1 passed, 0 failed/skipped, with 420-second watchdog | Performs all 129 distinct saved-character TCP/DH/login lifetimes; no scenario was shortened |
| Full owned SQL family | **134 passed, 0 failed/skipped** | All 134 unique source Fact names matched actual terminal results |

The corrected completed rows include all 18 ordinary SQL cleanup cases, all 9
TCP retirement cases and 58 GM cases. The fixture now retains each exact peer's
original stream before half-close; actual EOF, task/pending counts, error identity
and real retries remain asserted. Existing queued-GM assertions caught and then
verified the synchronous denial correction. Original failures and the watchdog
abort are retained in local results.

The complete provider family used fresh owned MariaDB 12.2.2 on explicit loopback
port 34398. `Qualified` and `FullProviderFamily` are true. All commands and the
actual child tree settled; the listener, database data, private client options
and PID file are gone. Primary and cleanup error lists are empty. Only ordinary
logs, non-secret initialization options and empty temporary/blame folders remain.
No original database or port 3306 was used.

The long history row took 204.299549s. Finer optional phases locate 201.736420s
inside admission dispatch across 129 logins; client DH/cipher preparation totals
0.309871s. This identifies the next profiling boundary. It is not a speedup claim,
and no safety checks, lifetimes or cryptographic work were removed.

## Scope and remaining work

The earlier published [f88da998 full Windows gate](FULL-WINDOWS-20261104-F88D.md)
passed 35,758 tests with 271 skips. It qualifies the earlier source; it is not the
new successor's full gate. Root will publish this reviewed batch and start one
new complete Windows check.

Actual physical provider Dispose-fault injection, failed real network reader
completion, arbitrary process/OS teardown and all race windows remain unproved.
Failure-only disposal witnesses and real loopback/provider tests do not prove
those broader properties. Native graph/Store publication, normalized/Online CAS,
all gameplay writers, UID custody, recovery and the account/world wire bridge
remain required. No client launch, route or backend activation follows.
[Connection contract](../protocol/specs/sql-connection-custody.md),
[cleanup contract](../protocol/specs/sql-and-transport-cleanup-ownership.md).
