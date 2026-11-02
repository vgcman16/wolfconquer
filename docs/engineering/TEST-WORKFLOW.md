# Development and publication checks

Use focused checks for an implementation slice and its dependency neighbors.
Use the full Windows gate for a reviewed integration/publication checkpoint.
Keep all behavioral coverage; changing selection does not declare omitted
tests passed. Do not enable concurrent native fixtures until their mutable
global metadata, files and lifetimes are genuinely isolated.

```powershell
# Preview only: no process, artifact, build or test is started.
./tools/dev/Invoke-FocusedCheck.ps1 -Profile Quick -Plan

# Build incrementally once, then run the packet-codec subset.
./tools/dev/Invoke-FocusedCheck.ps1 -Profile Quick

# Choose a reviewed affected filter, including relevant shared-owner neighbors.
./tools/dev/Invoke-FocusedCheck.ps1 -Profile Affected -Filter 'FullyQualifiedName~Ghost|FullyQualifiedName~Reviver'

# These history checks retain the real 300-operation replay/restart case.
./tools/dev/Invoke-FocusedCheck.ps1 -Profile PackageHistory

# Enable observational fixture phase timings in this child process.
./tools/dev/Invoke-FocusedCheck.ps1 -Profile Affected -Filter 'FullyQualifiedName~MoreThan128DistinctExistingLogins' -PhaseTiming -HangSeconds 600

# Full solution, separate launcher and updater/parser gates, unchanged.
./tools/dev/Invoke-LocalCheck.ps1

# When changing this runner: pure plans/TRX controls, then owned-process cleanup.
./tools/dev/Test-FocusedCheckPlan.ps1 -RepositoryRoot .
./tools/dev/Test-FocusedCheckProcess.ps1
```

Quick is a curated packet subset. Ticker and GhostReviver are convenience
profiles; they cannot infer every dependency of an arbitrary source change.
Affected requires an explicit filter. PackageHistory has a 600-second per-test
hang budget for its measured long case. Other profiles have smaller budgets;
increase explicitly for a genuinely long selected test. Hang collection uses
no automatic memory dumps.

Focused runs build normally so changed dependencies restore when necessary,
then use `--no-build --no-restore` for testing. Results go only to
`.artifacts/local-focused`, never the full-check/updater badge receipts. Empty
or skipped-only selections fail. At least one test must actually pass, and any
failed/unknown outcome makes the focused run fail. A focused pass is not a full
gate, live acceptance, full parity or a guarantee that every bug is fixed.
Every selector in a named profile must produce a passing row; a partly stale
profile fails. Completed TRX summaries and actual row counts must agree.

Both runners share the repository check lease. Focused runs additionally use
a machine-wide focused-check mutex and refuse an existing host test/build/runner
worker. Their own command tree belongs to a kill-on-close Windows job, with
explicit process cleanup before releasing the leases. The coordinator still
serializes full checks across separate checkouts; the full runner does not
participate in the focused mutex. Never restart a live job because it is quiet.

Read terminal timings with `tools/dev/Get-TestTiming.ps1`. Test/class duration
sums can overlap and are not wall time. Use `WOLF_TEST_PHASE_TIMING=1` or
`-PhaseTiming` to attribute real fixture work before reusing any owners or
changing isolation. Preserve replay, restart, malformed-input and failure-path
checks while optimizing repeated work. Measure gains in the same environment.
