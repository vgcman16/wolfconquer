# Full Windows result: f88da998

Verified 2026-11-04 UTC. The [public Windows run41734823791](https://github.com/vgcman16/wolfconquer/actions/runs/41734823791)
completed successfully on exact `f88da9989b5b66f35d2ae5126604308eb84b785a`,
the generated-only parity-tracker child of source commit `5ecee685`.
The trusted checkout matched that head and had no tracked/index changes.

| Actual test report | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Core | 35,255 | 0 | 271 |
| Updates | 446 | 0 | 0 |
| Windows launcher | 57 | 0 | 0 |
| Total | **35,758** | **0** | **271** |

All664 Chinese7937-named Core rows passed. They are regression rows, not complete
playable features. The Core report's271 actual NotExecuted rows comprise119
opt-in SQL provider Facts and152 external-fixture cases. The report's aggregate
notExecuted attribute is0; counting actual row outcomes agrees with total minus
executed and avoids incorrectly treating those skips as passes.

All eight local receipt stages passed: solution restore/build/test, launcher
restore/build/test and the two updater receipt/parser suites. The actual local
run `20261103T225501505Z-a26be0bb` started22:55:01.6196849UTC on November3 and
finished01:00:38.8172309UTC on November4. The GitHub job concluded success at
01:00:49UTC. Its check workers settled; the runner listener was preserved.

The separately executed full119-provider family previously passed with no
failures/skips on owned MariaDB. Those provider outcomes are separate from this
general run's SQL skips. See [handoff qualification](SQL-AUTHENTICATED-HANDOFF-20261103.md).
The next connection/cleanup source successor was not part of this published
commit and requires its own focused/provider/publication checks.

## Timing and limits

The retained Core TRX reports7309.018s wall time. Existing actual history controls
were the two slowest rows:300 real package/mail claims255.943s and129 distinct
real saved-character login lifetimes203.747s. Durations are measurements of this
run, not a controlled speedup comparison. Overlapping row-duration sums are not
wall time. Coverage is retained while finer optional phases identify safe
optimization opportunities.

This gate does not prove complete7937 gameplay, native SQL publication, every
persistence writer, client stability or rendered English. No legacy client,
updater, routing, original save or antivirus setting was changed by these checks.
The execution hold remains. [Current7937 state](7937-STATUS-20261103.md).
