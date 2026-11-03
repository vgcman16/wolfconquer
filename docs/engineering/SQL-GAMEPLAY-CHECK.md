# Owned SQL gameplay check

Use a fresh 64-bit PowerShell7 process on Windows and an explicit unused loopback
port other than3306. Run only after the coordinating agent's current host gate is
terminal. The wrapper shares the normal global check mutex, host-idle checks and
repository check.lock; leave an existing job running.
The mutex coordinates cooperating runners. Host-idle probes do not fence an
independently launched process that starts afterward.

```powershell
pwsh -NoProfile -File .\tools\dev\Invoke-SqlGameplayCheck.ps1 -Port 34381 -Plan
pwsh -NoProfile -File .\tools\dev\Invoke-SqlGameplayCheck.ps1 -Port 34381
```

RepositoryRoot defaults relative to the script. Optional parameters are
`-RepositoryRoot <checkout>`, `-MariaDbHome <installation>` (default
`C:\Program Files\MariaDB 12.2`) and `-Configuration Debug|Release` (defaultRelease).
`-OnlyFact <exact-method-name>` optionally selects one discovered provider Fact
for debugging. There is no arbitrary filter, callback, credential, skip or
no-build parameter.

Plan checks ordinary project/support entry paths and returns not-run JSON command
metadata. It imports no module, parses no helper, queries no host process, creates
no artifacts and starts nothing. It is not SQL or gameplay qualification.
Selected Plan additionally reads the complete current SQL Fact source inventory;
the supplied name must match one method ordinally. Malformed, unknown, wrong-case,
duplicate or incompletely discovered inventories refuse before SQL starts.

Real mode reuses [the owned SQL lifecycle](OWNED-SQL-AUTH-FIXTURE.md). That owner
creates a fresh private MariaDB datadir, authenticates its exact identity and
scopes synthetic credentials before the closed test callback runs. The callback
uses the existing owned command launcher to run the **whole**
`ModernSqlAuthFileRoundtripTests` family, with ordinary restore/build enabled,
one MSBuild node, no node/shared-compiler reuse and180s hang diagnostics. The
lifecycle compiles the shared process owner first; no preliminary build is run.

For a single failing control, use an exact method name, for example:

```powershell
pwsh -NoProfile -File .\tools\dev\Invoke-SqlGameplayCheck.ps1 -Port 34395 -OnlyFact ActualNewCustodyWaitsForOlderPairRowBeforeReceivingCommittedAfterimage -Plan
pwsh -NoProfile -File .\tools\dev\Invoke-SqlGameplayCheck.ps1 -Port 34395 -OnlyFact ActualNewCustodyWaitsForOlderPairRowBeforeReceivingCommittedAfterimage
```

Selected mode uses an exact fully qualified equality filter and independently
revalidates the source inventory in the lifecycle. Its actual TRX must have one
unique matching passed definition/entry/result and no skips or extra results.
Plan and result explicitly mark `fullProviderFamily=false`/`FullProviderFamily=false`
and list the selected name. A selected pass is only that control's qualification;
it never substitutes for the complete provider family or full Windows gate.
Omit `-OnlyFact` for the required full-family integration check. Consumers making
a whole-family claim must require `FullProviderFamily=true` as well as success.

Stdout/stderr and the fresh qualification.trx stay under the actual private
`.artifacts/owned-sql-auth-<GUID>` root. Do not publish private logs or credentials.
The lifecycle accepts only the source-discovered inventory of exact passed SQL
Facts; missing, duplicate, skipped, stale-assembly or failed results refuse
qualification. The count grows with reviewed new tests and is not hardcoded.
It restores the scoped environment and retires its child tree/data under its
existing ownership rules. The wrapper releases repository/global leases even on
failure, retaining primary failures and any secondary cleanup evidence.

An actual successful provider lifecycle proves only its executed SQL/native/file
gameplay controls. Normal full-suite opt-out skips, Plan output and synthetic
tooling checks do not prove SQL acceptance. No live-client, SQL item backend,
power-loss guarantee, complete7937 mapping or full parity claim follows.
