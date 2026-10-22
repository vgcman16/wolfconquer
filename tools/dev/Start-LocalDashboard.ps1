#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'),
    [string]$ServiceRoot,
    [ValidateRange(1024, 65535)][int]$Port = 48236
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$taskRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
if (-not (Test-Path -LiteralPath (Join-Path $taskRoot 'WolfConquer.sln') -PathType Leaf)) {
    throw 'RepositoryRoot must contain WolfConquer.sln.'
}
$taskStatusScript = Join-Path $PSScriptRoot 'Get-LocalStatus.ps1'
$taskAssets = Join-Path $PSScriptRoot 'dashboard'
$taskFiles = @{
    '/' = @('index.html', 'text/html; charset=utf-8')
    '/index.html' = @('index.html', 'text/html; charset=utf-8')
    '/app.js' = @('app.js', 'text/javascript; charset=utf-8')
    '/style.css' = @('style.css', 'text/css; charset=utf-8')
}
$taskListener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port)
$taskListener.Start()
Write-Output "WolfConquer read-only dashboard: http://127.0.0.1:$Port/"
try {
    while ($true) {
        $taskClient = $taskListener.AcceptTcpClient()
        try {
            $taskClient.ReceiveTimeout = 2000
            $taskClient.SendTimeout = 2000
            $taskStream = $taskClient.GetStream()
            $taskReader = [IO.StreamReader]::new($taskStream, [Text.Encoding]::ASCII, $false, 1024, $true)
            $taskRequest = $taskReader.ReadLine()
            $taskLength = 0
            $taskHost = ''
            while ($null -ne ($taskLine = $taskReader.ReadLine()) -and $taskLine.Length -gt 0) {
                $taskLength += $taskLine.Length
                if ($taskLength -gt 8192) { throw 'Request headers exceed limit.' }
                if ($taskLine.StartsWith('Host:', [StringComparison]::OrdinalIgnoreCase)) {
                    $taskHost = $taskLine.Substring(5).Trim()
                }
            }
            $taskStatus = '200 OK'
            $taskType = 'text/plain; charset=utf-8'
            $taskBody = [Text.Encoding]::UTF8.GetBytes('Not found')
            if ($taskHost -notin @("127.0.0.1:$Port", "localhost:$Port")) {
                $taskStatus = '403 Forbidden'
                $taskBody = [Text.Encoding]::UTF8.GetBytes('Loopback Host header required.')
            } elseif ($taskRequest -notmatch '^GET (/[^ ]*) HTTP/1\.[01]$') {
                $taskStatus = '405 Method Not Allowed'
                $taskBody = [Text.Encoding]::UTF8.GetBytes('Read-only dashboard; GET only.')
            } else {
                $taskPath = $Matches[1]
                if ($taskPath -eq '/health') {
                    $taskBody = [Text.Encoding]::UTF8.GetBytes('ok')
                } elseif ($taskPath -eq '/status.json') {
                    $taskStatusArgs = @{ RepositoryRoot = $taskRoot }
                    if ($ServiceRoot) { $taskStatusArgs.ServiceRoot = $ServiceRoot }
                    $taskJson = & $taskStatusScript @taskStatusArgs
                    $taskType = 'application/json; charset=utf-8'
                    $taskBody = [Text.Encoding]::UTF8.GetBytes(($taskJson -join "`n"))
                } elseif ($taskFiles.ContainsKey($taskPath)) {
                    $taskAsset = $taskFiles[$taskPath]
                    $taskType = $taskAsset[1]
                    $taskBody = [IO.File]::ReadAllBytes((Join-Path $taskAssets $taskAsset[0]))
                } else {
                    $taskStatus = '404 Not Found'
                }
            }
            $taskHeaders = "HTTP/1.1 $taskStatus`r`nContent-Type: $taskType`r`nContent-Length: $($taskBody.Length)`r`nConnection: close`r`nCache-Control: no-store`r`nX-Content-Type-Options: nosniff`r`nContent-Security-Policy: default-src 'self'; frame-ancestors 'none'; base-uri 'none'`r`n`r`n"
            $taskHeaderBytes = [Text.Encoding]::ASCII.GetBytes($taskHeaders)
            $taskStream.Write($taskHeaderBytes)
            $taskStream.Write($taskBody)
            $taskStream.Flush()
        } catch {
            # A malformed/abandoned local request must not stop observation.
            Write-Warning ('Dashboard request failed: ' + $_.Exception.GetType().Name)
        } finally {
            $taskClient.Dispose()
        }
    }
} finally {
    $taskListener.Stop()
}
