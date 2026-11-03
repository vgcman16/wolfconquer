function Invoke-OwnedSqlChild([string] $Executable, [string[]] $Arguments, $Owner, [string] $Name, [int] $TimeoutSeconds) {
    Assert-OwnedSqlRoot $Owner
    $stdout = Join-Path $Owner.Root ($Name + '.stdout.log')
    $stderr = Join-Path $Owner.Root ($Name + '.stderr.log')
    $Owner.CommandsSettled = $false
    $child = [WolfConquer.Tools.OwnedSql.OwnedSqlChild]::Start($Executable, $Arguments, $Owner.Root, $stdout, $stderr)
    $errors = [Collections.Generic.List[Exception]]::new()
    try {
        if (-not $child.Wait($TimeoutSeconds * 1000)) { throw 'Owned SQL command exceeded its deadline.' }
        $exit = $child.ExitCode
        if ($exit -ne 0) { throw 'Owned SQL command failed; inspect its private log.' }
    }
    catch { $errors.Add($_.Exception) }
    finally {
        # A timeout must settle the actual job, including initializer bootstrap descendants.
        try { $child.Settle(10000); $Owner.CommandsSettled = $true } catch { $errors.Add($_.Exception) }
        try { $child.Dispose() } catch { $errors.Add($_.Exception) }
    }
    if ($errors.Count -eq 1) { throw $errors[0] }
    if ($errors.Count -gt 1) { throw [AggregateException]::new('Owned SQL command and cleanup failed.', [Exception[]]$errors.ToArray()) }
    return $stdout
}

function Wait-OwnedSqlReady($Owner, $Server, [int] $TimeoutSeconds) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $pidFile = Join-Path $Owner.Root 'server.pid'
    while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $Server.RequireCurrent()
        $ready = $false
        foreach ($name in @('server.stdout.log', 'server.stderr.log')) {
            $file = Join-Path $Owner.Root $name
            if ((Get-Item -LiteralPath $file).Length -gt 1048576) { throw 'Owned SQL startup log exceeded its bound.' }
            $stream = [IO.FileStream]::new($file, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]'ReadWrite,Delete')
            $reader = [IO.StreamReader]::new($stream)
            try { if ($reader.ReadToEnd() -match 'ready for connections') { $ready = $true } }
            finally { $reader.Dispose() }
        }
        if ($ready -and (Test-Path -LiteralPath $pidFile -PathType Leaf)) {
            if ([IO.File]::ReadAllText($pidFile).Trim() -cne $Server.Id.ToString([Globalization.CultureInfo]::InvariantCulture)) {
                throw 'Owned SQL PID file does not name the retained child.'
            }
            return
        }
        Start-Sleep -Milliseconds 100
    }
    throw 'Owned SQL server did not reach readiness before its deadline.'
}

function Assert-OwnedSqlServer($Owner, $Server, [string] $Client, [string] $ClientOptions, [int] $Port, [string] $Name) {
    $Server.RequireCurrent()
    $query = 'SELECT @@version,@@port,@@bind_address,@@datadir,@@pid_file'
    $output = Invoke-OwnedSqlChild $Client @(('--defaults-file=' + $ClientOptions), '--batch', '--raw', '--skip-column-names', ('--execute=' + $query)) $Owner $Name 15
    $Server.RequireCurrent()
    if ((Get-Item -LiteralPath $output).Length -gt 8192) { throw 'Owned SQL identity result exceeded its bound.' }
    $rows = @([IO.File]::ReadAllLines($output) | Where-Object { $_.Length -gt 0 })
    if ($rows.Count -ne 1) { throw 'Owned SQL identity returned an unexpected row count.' }
    $fields = $rows[0].Split("`t")
    if ($fields.Count -ne 5 -or $fields[0] -notmatch '^12\.2\.' -or $fields[1] -cne $Port.ToString() -or $fields[2] -cne '127.0.0.1' -or
        [IO.Path]::GetFullPath($fields[3]).TrimEnd([char[]]'\/') -ine $Owner.Data -or
        [IO.Path]::GetFullPath($fields[4]) -ine (Join-Path $Owner.Root 'server.pid')) {
        throw 'Authenticated SQL endpoint does not match the fresh owned server.'
    }
    return $fields[0]
}
