# Windows local checks and status

These tools run checks on a trusted local checkout. They do not configure SQL,
launch or stop game/server processes, deploy files, change routing, or edit saves.
Use PowerShell 7 and the .NET SDK selected by `global.json`. Restore requires the
configured NuGet sources to be reachable. Do not run untrusted branches or pull
requests on a PC holding credentials, game assets, or private server data.

## Inspect the plan first

From the repository root:

```powershell
pwsh -NoProfile -File ./tools/dev/Invoke-LocalCheck.ps1 -Plan
```

`-DryRun` is an alias for `-Plan`. The JSON plan shows the exact executable and
separate arguments for solution restore/build/test, Windows launcher restore/build/test,
then the two receipt-parser self-tests. It measures the selected PS7
executable and SHA256 but starts none of those commands and writes no artifacts.
Paths containing spaces or shell punctuation remain literal arguments.

## Run checks

```powershell
pwsh -NoProfile -File ./tools/dev/Invoke-LocalCheck.ps1 -Configuration Release
```

Both scripts default to the checkout two directories above their own location,
not the current working directory. Supply `-RepositoryRoot 'D:/work/wolf checkout'`
to select another root containing `WolfConquer.sln`, `Directory.Build.targets`, and
the separate Windows launcher test project. The check runner also accepts
`-DotNetPath` and `-PowerShellPath` as explicit executable paths. By default,
.NET uses the first application found by `Get-Command` (`dotnet.exe`/`dotnet`),
while PowerShell uses the actual executable under the running engine's `$PSHOME`.
This avoids treating the Windows Store app-execution alias as an engine file.
An explicitly supplied PowerShell path is still measured and rejected if invalid;
there is no silent fallback from an invalid explicit selection. PS7 file metadata
and a freshly measured SHA256 supply the native
probe variables `WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH` and
`WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH_SHA256` to child check processes only.

Restore, build (`--no-restore`), and test (`--no-build --no-restore`) are sequential.
The Windows-only launcher stays outside the portable main solution but is checked
next through its own project. The updater tests run once, with the solution; their
actual terminal TRX supplies the separate updater smoke receipt. No test count or
finish time is guessed. The status/parser self-tests use owned synthetic files only.
The generated run ID opts into per-project/framework TRX subdirectories through
`Directory.Build.targets`; ordinary builds do not enable that setting. This avoids
same-second report filename collisions without running the updater tests twice.
The first nonzero child exit code stops the sequence and is returned unchanged.
Tool/setup failures return 1. There is no retry that turns a failure into success.
A file lease prevents two check runners from sharing the same artifact directory.

Artifacts are confined to ignored `.artifacts/local-dev/`: `last-check.json`,
`last-updater-smoke.json`, fresh per-run TRX directories, an inert `check.lock`
file, and per-run/per-step stdout/stderr logs. Logs are retained
for local inspection and may contain build diagnostics; they are not published.
The status file starts as `running`, then records actual step exits and the final
outcome. An interrupted process may leave `running`; that is not a passing check.
Current receipts name the eight-step `windows-local-v2` suite; historical three-step
receipts remain readable but do not claim the newer launcher checks.
Passing checks do not establish deployment, full parity, or live-client readiness.

## Read status

```powershell
pwsh -NoProfile -File ./tools/dev/Get-LocalStatus.ps1
```

This read-only JSON command returns `lastCheck.state` (`unknown`, `running`,
`passed`, or `failed`) and a `services` summary. A missing, malformed, foreign-root
or inconsistent passing status is `unknown`, never an invented pass. Log references
are repository-relative; log contents, process command lines and extra saved JSON
properties are not returned. The command creates no status/log files.

On Windows, process discovery attributes only exact WolfConquer AccountServer,
Gateway, LoginWeb and WorldServer executable/DLL paths beneath the selected root.
To observe a separately deployed local stack, pass an explicit absolute
`-ServiceRoot 'D:/local-deployment'`; this does not change the repository used for
saved checks. Both roots are reported, and no ownership is inferred outside the
chosen service root.
It associates TCP listeners by those measured process IDs, not by guessed ports.
No matching process means an empty list, not proof about other checkouts. Discovery
or permission failures remain unavailable/unknown. A process/listener is not proof
of successful login, database readiness, or healthy gameplay.

`-SkipServiceProbe` omits process/TCP discovery for synthetic tests or offline
inspection. The tools add no HTTP endpoint. Any separate dashboard must bind only
to loopback and remain read-only; any GitHub self-hosted job must be manually
requested on a trusted ref, never automatically execute untrusted PR code.

## Local dashboard

```powershell
pwsh -NoProfile -File ./tools/dev/Start-LocalDashboard.ps1
```

Open `http://127.0.0.1:48236/`. Use an explicit absolute `-ServiceRoot` to observe
another installed server copy. `-Port` selects another unprivileged loopback port.
Only GET and a fixed asset list are supported; there are no run/stop/deploy/login
endpoints or log contents. Keep its PowerShell process running; Ctrl+C in its own
terminal stops only this observer. It is not installed as a Windows service and
does not automatically start at boot.

The Updater card separately shows loopback HTTP health, signed-release metadata
and bundle/launcher endpoint checks, and actual saved updater test results. Old
receipts become stale after ten minutes; HTTP availability is not payload integrity
or game-client proof. See the [update-tool guide](CLIENT-UPDATES.md).

## Trusted GitHub checks

The `Windows local checks` workflow is `workflow_dispatch` only, with a read-only
repository token. Its job additionally requires trusted `main` and the repository
owner as the initiating actor. It uses a separately configured Windows x64 runner
with the `wolfconquer-trusted` label; runner binaries and registration credentials
are outside Git. The runner needs .NET 9, Git, and PowerShell 7. Select Actions →
Windows local checks → Run workflow.

There is no automatic deployment and no pull-request trigger. The dedicated PC
still executes code with its Windows account's permissions; these guards are not
a sandbox. Review workflow changes before merging them. CodeFactor is independent
static analysis, not a runtime dependency or replacement for tests. Existing
complexity/duplication warnings remain development work; do not hide them merely
to improve a badge.
