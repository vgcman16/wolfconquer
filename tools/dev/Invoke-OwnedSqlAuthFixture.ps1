#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $RepositoryRoot,
    [Parameter(Mandatory)][ValidateRange(1024, 65535)][int] $Port,
    [Parameter(Mandatory)][scriptblock] $Run,
    [string] $MariaDbHome = 'C:\Program Files\MariaDB 12.2',
    [ValidateRange(10, 180)][int] $StartupTimeoutSeconds = 90,
    [AllowEmptyString()][string] $OnlyFact,
    [switch] $KeepData
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$fullProviderFamily = -not $PSBoundParameters.ContainsKey('OnlyFact')
$selectedFactNames = @()
if (-not $IsWindows -or -not [Environment]::Is64BitProcess) { throw 'The owned MariaDB fixture requires 64-bit PowerShell on Windows.' }
if ($Port -eq 3306) { throw 'Port 3306 is forbidden for this disposable fixture.' }
$private = Join-Path $PSScriptRoot 'owned-sql'
foreach ($name in @('Paths.ps1', 'Server.ps1', 'Results.ps1', 'Environment.ps1')) { . (Join-Path $private $name) }
if ('WolfConquer.Tools.OwnedSql.OwnedSqlChild' -as [type]) { throw 'Use a fresh PowerShell process for each owned SQL lifecycle.' }
if ('WolfConquer.DevChecks.OwnedProcessJob' -as [type]) { throw 'Use a fresh PowerShell process so the reviewed job support is compiled together.' }
$support = Join-Path ([IO.Path]::GetFullPath($RepositoryRoot)) 'tools\dev\FocusedCheckSupport.psm1'
$processSupport = Join-Path ([IO.Path]::GetFullPath($RepositoryRoot)) 'tools\dev\FocusedCheckProcess.cs'
[void](Assert-OwnedSqlPlainPath $support); [void](Assert-OwnedSqlPlainPath $processSupport)
Import-Module $support -Force -ErrorAction Stop
# Compiles only this small process owner at the future authorized execution gate.
Add-Type -Path @($processSupport, (Join-Path $private 'OwnedSqlNative.cs'), (Join-Path $private 'OwnedSqlChild.cs'))
$owner = $null; $server = $null; $portLease = $null; $settled = $false; $qualified = $false
$identityVerified = $false
$stage = 'preflight'; $primaryTypes = [Collections.Generic.List[Exception]]::new(); $cleanupTypes = [Collections.Generic.List[Exception]]::new(); $passed = 0; $version = $null
function Add-OwnedSqlFailureTypes([Collections.Generic.List[Exception]] $Target, [Exception] $Failure) {
    if ($Failure -is [AggregateException]) {
        foreach ($inner in $Failure.InnerExceptions) { Add-OwnedSqlFailureTypes $Target $inner }
    }
    else { $Target.Add($Failure) }
}
try {
    $ownedMariaDbDirectory = Assert-OwnedSqlPlainPath $MariaDbHome
    foreach ($launcherPath in @($ownedMariaDbDirectory, [IO.Path]::GetFullPath($RepositoryRoot))) {
        # The documented Windows initializer internally invokes cmd/popen.
        # Keep its selected paths compatible with that ANSI/command boundary.
        if ($launcherPath -match '[^\x20-\x7e]|[%&|<>^!"\r\n]') { throw 'Initializer paths contain unsupported command or encoding characters.' }
    }
    $bin = Join-Path $ownedMariaDbDirectory 'bin'
    $initializer = Join-Path $bin 'mariadb-install-db.exe'
    $daemon = Join-Path $bin 'mariadbd.exe'
    $client = Join-Path $bin 'mariadb.exe'
    $admin = Join-Path $bin 'mariadb-admin.exe'
    foreach ($file in @($initializer, $daemon, $client, $admin, (Join-Path $bin 'mysqld.exe'), (Join-Path $bin 'server.dll'))) {
        [void](Assert-OwnedSqlPlainPath $file)
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw 'The selected MariaDB 12.2 installation is incomplete.' }
    }
    $inventory = Get-OwnedSqlRequiredFacts ([IO.Path]::GetFullPath($RepositoryRoot))
    $required = Select-OwnedSqlRequiredFacts $inventory $OnlyFact (-not $fullProviderFamily)
    if (-not $fullProviderFamily) { $selectedFactNames = @($required) }
    # One explicit requested bind, never a port scan or connection to an existing listener.
    $portLease = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port)
    $portLease.Server.ExclusiveAddressUse = $true; $portLease.Start()
    $owner = New-OwnedSqlRoot $RepositoryRoot
    $options = Join-Path $owner.Root 'client.private.ini'
    $template = Join-Path $owner.Root 'initialize.ini'
    $pidFile = Join-Path $owner.Root 'server.pid'
    $temp = Join-Path $owner.Root 'tmp'; [IO.Directory]::CreateDirectory($temp) | Out-Null
    $secret = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    [IO.File]::WriteAllText($template, "[mysqld]`nbind-address=127.0.0.1`nskip-name-resolve`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($options, "[client]`nprotocol=tcp`nhost=127.0.0.1`nport=$Port`nuser=root`npassword=$secret`nconnect-timeout=3`n", [Text.UTF8Encoding]::new($false))
    $stage = 'initialize'
    if (Test-Path -LiteralPath $owner.Data) { throw 'The initializer datadir must be absent inside the newly owned root.' }
    [void](Invoke-OwnedSqlChild $initializer @(('--datadir=' + $owner.Data), ('--config=' + $template), ('--port=' + $Port), ('--password=' + $secret), '--silent') $owner 'initialize' $StartupTimeoutSeconds)
    if (-not (Test-Path -LiteralPath (Join-Path $owner.Data 'my.ini') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $owner.Data 'mysql') -PathType Container)) { throw 'The fresh datadir initialization is incomplete.' }
    $stage = 'start'
    # The lease cannot transfer to mariadbd; release immediately before its owned start.
    # No SQL connection is attempted until its own readiness log and exact PID agree.
    $portLease.Stop(); $portLease = $null
    $serverArguments = @(('--defaults-file=' + (Join-Path $owner.Data 'my.ini')), ('--basedir=' + $ownedMariaDbDirectory),
        ('--datadir=' + $owner.Data), ('--port=' + $Port), '--bind-address=127.0.0.1', '--max-allowed-packet=134217728',
        ('--pid-file=' + $pidFile), ('--tmpdir=' + $temp), '--console')
    $owner.CommandsSettled = $false
    $server = [WolfConquer.Tools.OwnedSql.OwnedSqlChild]::Start($daemon, $serverArguments, $owner.Root,
        (Join-Path $owner.Root 'server.stdout.log'), (Join-Path $owner.Root 'server.stderr.log'))
    $owner.CommandsSettled = $true
    Wait-OwnedSqlReady $owner $server $StartupTimeoutSeconds
    $stage = 'identity'
    $version = Assert-OwnedSqlServer $owner $server $client $options $Port 'identity-before'
    $identityVerified = $true
    $stage = 'qualification'
    $trx = Join-Path $owner.Root 'qualification.trx'
    if (Test-Path -LiteralPath $trx) { throw 'Qualification requires a fresh TRX path.' }
    # This secret exists only in private memory/options and the explicitly scoped test environment.
    $context = [pscustomobject]@{ RepositoryRoot = $owner.Repository; TrxPath = $trx; Port = $Port; ArtifactRoot = $owner.Root }
    Write-Host ('Owned SQL fixture ready on explicit loopback port {0}; private results: {1}' -f $Port, $owner.Root)
    Invoke-OwnedSqlQualificationCallback $Run $context `
        "Server=127.0.0.1;Port=$Port;User ID=root;Password=$secret;Pooling=False;SslMode=None;Connection Timeout=10;Default Command Timeout=10" $cleanupTypes
    $passed = Assert-OwnedSqlTrx $trx $required (-not $fullProviderFamily)
    [void](Assert-OwnedSqlServer $owner $server $client $options $Port 'identity-after')
    $qualified = $true
}
catch {
    Add-OwnedSqlFailureTypes $primaryTypes $_.Exception
}
finally {
    if ($portLease) { try { $portLease.Stop() } catch { Add-OwnedSqlFailureTypes $cleanupTypes $_.Exception } }
    if ($server) {
        try {
            if ($identityVerified -and -not $server.Wait(0)) {
                # Re-verify retained child AND owned endpoint before sending SQL SHUTDOWN.
                [void](Assert-OwnedSqlServer $owner $server $client $options $Port 'identity-stop')
                [void](Invoke-OwnedSqlChild $admin @(('--defaults-file=' + $options), '--shutdown_timeout=10', 'shutdown') $owner 'shutdown' 15)
                [void]$server.Wait(10000)
            }
        }
        catch { Add-OwnedSqlFailureTypes $cleanupTypes $_.Exception }
        finally {
            try { $server.Settle(10000); $settled = $true }
            catch { Add-OwnedSqlFailureTypes $cleanupTypes $_.Exception }
            finally { try { $server.Dispose() } catch { Add-OwnedSqlFailureTypes $cleanupTypes $_.Exception } }
        }
    }
    else { $settled = $true }
    if ($owner) {
        try {
            Assert-OwnedSqlRoot $owner
            if (Test-Path -LiteralPath $options -PathType Leaf) { Remove-Item -LiteralPath $options -Force -ErrorAction Stop }
            if ($settled -and $owner.CommandsSettled -and -not $KeepData) { Remove-OwnedSqlData $owner }
            $report = [ordered]@{ Scope = 'Owned modern SQL auth to native admission and file transaction'; Qualified = ($qualified -and $cleanupTypes.Count -eq 0);
                FullProviderFamily = $fullProviderFamily; SelectedFactNames = @($selectedFactNames);
                Stage = $stage; PassedSqlFacts = $passed; Version = $version; Port = $Port; ChildTreeSettled = $settled;
                CommandsSettled = $owner.CommandsSettled; DataRetained = (Test-Path -LiteralPath $owner.Data);
                PrimaryExceptionTypes = @($primaryTypes | ForEach-Object { $_.GetType().FullName }); CleanupExceptionTypes = @($cleanupTypes | ForEach-Object { $_.GetType().FullName }) }
            [IO.File]::WriteAllText((Join-Path $owner.Root 'result.json'), ($report | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
        }
        catch { Add-OwnedSqlFailureTypes $cleanupTypes $_.Exception }
    }
    $secret = $null
}
if ($primaryTypes.Count -gt 0 -or $cleanupTypes.Count -gt 0 -or -not $qualified) {
    $location = if ($owner) { $owner.Root } else { 'no owned directory was created' }
    throw "Owned SQL qualification failed at $stage; $location. No secret or native error text is emitted. Review private logs and exact child settlement before retry."
}
if ($fullProviderFamily) {
    Write-Host ('Owned SQL full provider family passed {0} actual SQL Facts; its child tree was retired. Results: {1}' -f $passed, $owner.Root)
}
else {
    Write-Host ('Owned SQL selected Fact {0} passed; full provider family was not run. Its child tree was retired. Results: {1}' -f $OnlyFact, $owner.Root)
}
