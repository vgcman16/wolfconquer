function Set-OwnedSqlFixtureProcessValue(
    [ValidateSet('WOLF_SQL_AUTH_FIXTURE_ENABLED', 'WOLF_SQL_AUTH_FIXTURE_ADMIN_CONNECTION')][string] $Name,
    [AllowNull()] $Value) {
    # Keep null untyped: method binding must not turn absence into an empty
    # string. Current PowerShell/.NET supports genuinely present empty values.
    $path = 'Env:' + $Name
    if ($null -eq $Value) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -ErrorAction Stop }
    }
    else { [Environment]::SetEnvironmentVariable($Name, [string]$Value, [EnvironmentVariableTarget]::Process) }
    $present = Test-Path -LiteralPath $path
    $actual = [Environment]::GetEnvironmentVariable($Name, [EnvironmentVariableTarget]::Process)
    if ($null -eq $Value) {
        if ($present -or $null -ne $actual) { throw 'Owned SQL process environment absence was not restored.' }
    }
    elseif (-not $present -or $null -eq $actual -or -not [string]::Equals($actual, [string]$Value, [StringComparison]::Ordinal)) {
        throw 'Owned SQL process environment value was not restored exactly.'
    }
}

function Invoke-OwnedSqlQualificationCallback([scriptblock] $Run, $Context, [string] $ConnectionString,
    [Collections.Generic.List[Exception]] $CleanupFailures) {
    if ($null -eq $Run -or $null -eq $CleanupFailures) { throw 'The owned SQL callback and cleanup observer are required.' }
    $previousEnabled = [Environment]::GetEnvironmentVariable('WOLF_SQL_AUTH_FIXTURE_ENABLED')
    $previousConnection = [Environment]::GetEnvironmentVariable('WOLF_SQL_AUTH_FIXTURE_ADMIN_CONNECTION')
    $previousExitVariable = Get-Variable -Name LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue
    $hadExitVariable = $null -ne $previousExitVariable
    $previousExitValue = if ($hadExitVariable) { $previousExitVariable.Value } else { $null }
    $exitTouched = $false; $primary = $null; $restoreFailed = $false
    try {
        if ([string]::IsNullOrWhiteSpace($ConnectionString)) { throw 'The owned SQL callback connection is missing.' }
        Set-OwnedSqlFixtureProcessValue 'WOLF_SQL_AUTH_FIXTURE_ADMIN_CONNECTION' $ConnectionString
        Set-OwnedSqlFixtureProcessValue 'WOLF_SQL_AUTH_FIXTURE_ENABLED' '1'
        $global:LASTEXITCODE = 0; $exitTouched = $true
        & $Run $Context
        if ($global:LASTEXITCODE -ne 0) { throw 'The requested qualification command returned a nonzero exit code.' }
    }
    catch { $primary = $_.Exception }
    finally {
        # Attempt every independent restoration before TRX/identity/cleanup work.
        try { Set-OwnedSqlFixtureProcessValue 'WOLF_SQL_AUTH_FIXTURE_ENABLED' $previousEnabled }
        catch { $CleanupFailures.Add($_.Exception); $restoreFailed = $true }
        try { Set-OwnedSqlFixtureProcessValue 'WOLF_SQL_AUTH_FIXTURE_ADMIN_CONNECTION' $previousConnection }
        catch { $CleanupFailures.Add($_.Exception); $restoreFailed = $true }
        if ($exitTouched) {
            try {
                if ($hadExitVariable) { $global:LASTEXITCODE = $previousExitValue }
                elseif (Get-Variable -Name LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue) {
                    Remove-Variable -Name LASTEXITCODE -Scope Global -ErrorAction Stop
                }
            }
            catch { $CleanupFailures.Add($_.Exception); $restoreFailed = $true }
        }
        $ConnectionString = $null; $previousConnection = $null
    }
    if ($primary) { throw $primary }
    if ($restoreFailed) { throw 'The scoped SQL callback environment could not be restored.' }
}
