# Disposable MariaDB fixture for modern SQL authentication

This tool is for one explicitly owned test instance, not an existing installation's
data or a production schema. Root must use reviewed source, finish any current
check, and select one idle execution window. It does not establish host cleanliness
or permit legacy client execution.

Local qualification2026-11-03: the actual03d5607d launcher initialized/authenticated
MariaDB12.2.2 and passed5real SQL/native/file Facts with0failures/skips. Normal
shutdown/exact child settlement/private-option+datadir cleanup succeeded. A prior
4P/1Fcallback failure also settled/cleaned its owned instance. Six scripts parsed,
three launcher C# files compiled, and18synthetic identity/environment controls
passed separately. Absent/empty process variables restore explicitly. Other
failure/OS/runtime/Unicode horizons below remain unqualified; no client/host trust
or production database claim follows. See the
[actual SQL qualification](../checklists/OWNED-SQL-AUTH-20261103.md).

## Prerequisites and invocation

Use a fresh 64-bit PowerShell7.2+ process on Windows10/Server2016 or later, the
reviewed checkout containing SQL6 and the current job support, and an installed
MariaDB12.2 Windows binary distribution. The default binary directory is
`C:\Program Files\MariaDB 12.2\bin`. The tool checks required files; only its later
authenticated server query verifies the actual running12.2 version. It installs
no service and uses no elevation, existing configuration or original credentials.

Root chooses one explicit port1024–65535 excluding3306. There is no port search or
existing-listener SQL query. An exclusive loopback bind fails if that requested
port is unavailable. The lease must be released immediately before server start;
the tool then requires its retained child, private ready log and exact PID file
before authenticating. An authenticated query additionally checks version, port,
bind address, datadir and PID-file path before enabling tests and before shutdown.

After intake, Root can run this **future** command from an idle gate, adjusting
the checkout/port and build policy to the reviewed candidate:

```powershell
& '.\tools\dev\Invoke-OwnedSqlAuthFixture.ps1' `
  -RepositoryRoot (Get-Location).Path -Port 34381 -Run {
    param($fixture)
    Push-Location $fixture.RepositoryRoot
    try {
      & dotnet test 'tests/WolfConquer.Tests/WolfConquer.Tests.csproj' `
        --no-restore --filter 'FullyQualifiedName~ModernSqlAuthFileRoundtripTests' `
        --results-directory $fixture.ArtifactRoot `
        --logger 'trx;LogFileName=qualification.trx'
      if ($LASTEXITCODE -ne 0) { throw 'Requested SQL test command failed.' }
    }
    finally { Pop-Location }
  }
```

The callback must run the selected checkout's genuine tests, finish its children,
throw on command failure and write the exact fresh `$fixture.TrxPath`. Root still
owns the build/input/assembly gate and the test timeout. This tool does not claim
that an old assembly qualifies newly edited sources.

## Ownership, secrets and retirement

Each invocation creates a new `.artifacts/owned-sql-auth-<GUID>` with a successful
native directory-creation receipt, owner marker and ACL restricted to the current
Windows identity and SYSTEM. No secret is written before that ACL is applied.
The datadir is new and absent before initialization. No existing datadir can be
selected. Unsupported initializer command/ANSI path characters and reparse paths
are rejected. Private logs/TRX/result metadata remain in that owned root.

A32-byte random synthetic root password is generated for this disposable server.
The Windows initializer accepts it in its owned child argv, so local privileged
process inspection could see it briefly. The tool never prints it or saves it in
public source, a command transcript or result metadata. Later client credentials
are in a private options file. Only the test callback receives the temporary
`WOLF_SQL_AUTH_FIXTURE_ADMIN_CONNECTION` and
`WOLF_SQL_AUTH_FIXTURE_ENABLED=1` environment; previous values are restored in
`finally`. The private options file is removed during cleanup. Do not print those
environment values or the initializer argument list.

The existing `FocusedCheckProcess.cs` Job owner is reused. Its small additive API
exposes a same-assembly suspended-launch handle and exact job settlement. The new
launcher attaches that unnamed, non-inherited, no-breakaway Job during suspended
CreateProcess, before any initializer/bootstrap child can execute, and inherits
only explicit standard handles. It keeps the actual process handle, image and
creation time; it never reopens by PID or searches by executable name. The
initializer's normal `cmd`/bootstrap descendants remain owned by its Job.

Normal cleanup rechecks the server identity, requests authenticated shutdown,
then terminates/settles only the retained Job. A failed graceful shutdown makes
the wrapper fail even if forced settlement succeeds. Parent hard termination
closes its Job handles; it cannot perform filesystem cleanup afterward.
Datadir removal requires successful command/child-tree settlement plus the exact
root/marker receipt and a complete no-reparse descendant check. A failed launch
or unproven settlement retains data. `-KeepData` also retains data deliberately.
There is no resumable PID-based cleanup command: an interrupted fixture is private
evidence for Root to review, never permission to kill a same-name process or
recursively remove a computed path. Original databases/configurations are untouched.

## Qualification boundary

The wrapper discovers all `[ModernSqlAuthFixtureFact]` methods from the selected
checkout's `ModernSqlAuthFileRoundtripTests*.cs` files. Unsupported/ambiguous
discovery fails. It reuses `Read-FocusedTrx` for terminal row/counter validation,
then joins actual result IDs to that class's test definitions. Every required
Fact must occur exactly once and pass; missing, zero, duplicated, skipped,
NotExecuted, failed or unknown rows refuse qualification even with exit0. A
default full-suite opt-out skip does not qualify this SQL route. The count is
discovered, not hardcoded to four or five.

The proposed SQL6 boundary remains real modern MySqlConnector2.6.1 SQL auth,
exact ordinary/native account-ID admission, an existing joined character/item
file transaction, retry/reopen and a second untouched actor. This tooling does
not add production account DDL/allocation, migrate character files into SQL,
prove old MySql.Data6.3.6 compatibility or establish live-client acceptance.

## Flag and API evidence

The [official Windows initializer documentation](https://mariadb.com/docs/server/server-management/install-and-upgrade-mariadb/installing-mariadb/installing-system-tables-mariadb-install-db/mariadb-install-db-exe)
documents datadir, port, password, config-template and silent options. No service,
default-user, remote-root or large-pages option is supplied. The
[MariaDB12.2 initializer source](https://github.com/MariaDB/server/blob/12.2/sql/mysql_install_db.cc)
shows its generated datadir `my.ini` is the bootstrap's explicit defaults file;
the Unix initializer's option set is not assumed for Windows.

[Option-file documentation](https://mariadb.com/docs/server/server-management/install-and-upgrade-mariadb/configuring-mariadb/configuring-mariadb-with-option-files)
requires defaults-file first and limits reading to that file. The
[server options](https://mariadb.com/docs/server/server-management/starting-and-stopping-mariadb/mariadbd-options)
document console operation; [system variables](https://mariadb.com/docs/server/server-management/variables-and-modes/server-system-variables)
document bind address, basedir, datadir, port and PID file. The
[client](https://mariadb.com/docs/server/clients-and-utilities/mariadb-client/mariadb-command-line-client)
uses explicit TCP/options-file authentication; [mariadb-admin](https://mariadb.com/docs/server/clients-and-utilities/administrative-tools/mariadb-admin)
documents shutdown and its deadline. `ping` is deliberately insufficient because
it can succeed on access denied.

The [12.2 main entry](https://github.com/MariaDB/server/blob/12.2/sql/main.cc)
and [Windows build linkage](https://github.com/MariaDB/server/blob/12.2/sql/CMakeLists.txt)
show same-process server.dll linkage rather than an inferred detached daemon.
[Windows Job documentation](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects)
and [process attributes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute)
support child-tree ownership, kill-on-close and atomic job/handle-list creation;
unsupported flags or nested-job constraints fail closed at launch.

The baseline parse/compile, fresh12.2 initialization, authenticated identity,
opt-in SQL Facts, shutdown/settlement and one actual callback-failure cleanup
are qualified above. Occupied-port, bad initialization and interrupted-lifecycle
controls remain future runtime checks. Zero/missing/skipped and identity-invalid
TRX cases have synthetic acceptance coverage, not additional real SQL proof.
