#requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TraceLogPath,
    [Parameter(Mandatory)][string]$OutputPath,
    [Parameter(Mandatory)][ValidateCount(1,16)][uint16[]]$Opcode,
    [Parameter(Mandatory)][ValidateRange(1,65535)][int]$ClientBuild,
    [Parameter(Mandatory)][ValidateSet('VisibleDx8D13','RootD11_7899')][string]$ProtocolProfile,
    [ValidateSet('Inbound','Outbound')][string]$Direction = 'Inbound',
    [ValidateLength(0,512)][string]$Observation = '',
    [ValidateRange(1,128)][int]$MaximumFrames = 32,
    [ValidateRange(1024,67108864)][int]$MaximumLogBytes = 16777216,
    [ValidateRange(0,2147483647)][int]$AfterLine = 0
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-WireLocalPath([string]$Path) {
    if ($Path.StartsWith('\\', [StringComparison]::Ordinal) -or $Path.StartsWith('//', [StringComparison]::Ordinal)) {
        throw 'networkOrDevicePathRejected'
    }
    if (-not [IO.Path]::IsPathFullyQualified($Path)) { throw 'absolutePathRequired' }
    $full = [IO.Path]::GetFullPath($Path)
    $suffix = if ($full -cmatch '^[A-Za-z]:[\\/]') { $full.Substring(2) } else { $full }
    if ($suffix.Contains(':', [StringComparison]::Ordinal)) { throw 'namedStreamPathRejected' }
    return $full
}

function Get-WireOrdinaryPath([string]$Path, [switch]$Directory) {
    $full = Get-WireLocalPath $Path
    $item = Get-Item -LiteralPath $full -Force
    if ([bool]$item.PSIsContainer -ne [bool]$Directory) { throw 'pathKindMismatch' }
    for ($entry = $item; $null -ne $entry; $entry = $(if ($entry -is [IO.FileInfo]) { $entry.Directory } else { $entry.Parent })) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'reparsePathRejected' }
    }
    return $full
}

function Read-WireLogPrefix([string]$Path, [int]$Limit) {
    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    try {
        $size = $stream.Length
        if ($size -gt $Limit) { throw 'logSizeLimitExceeded' }
        $bytes = [byte[]]::new([int]$size)
        $stream.ReadExactly($bytes, 0, $bytes.Length)
    } finally { $stream.Dispose() }
    # Logs may be appended during this read. A partial final record is not evidence.
    # Cut at the byte boundary first: a writer may have only emitted part of UTF-8.
    $end = [Array]::LastIndexOf($bytes, [byte]10)
    $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes, 0, $end + 1).TrimStart([char]0xFEFF)
    return @{ bytes = $size; text = $text; partialFinalLineIgnored = $end -ne ($bytes.Length - 1) }
}

function Get-WireSelectedFrame([string]$Line, [int]$LineNumber, [Collections.Generic.HashSet[uint16]]$Selected) {
    if ($Line -cnotmatch '^LegacyGameWireTrace (inbound|outbound)-frame(?: |$)') {
        return $null
    }
    if ($Matches[1] -ine $Direction -or $Line -cnotmatch '(?:^| )opcode=([0-9]+)(?: |$)') { return $null }
    $selectedOpcode = [uint16]0
    if (-not [uint16]::TryParse($Matches[1], [ref]$selectedOpcode) -or -not $Selected.Contains($selectedOpcode)) { return $null }
    if ($Line -cnotmatch '^LegacyGameWireTrace (inbound|outbound)-frame frameLength=([0-9]+) opcode=([0-9]+) payloadLength=([0-9]+) (.+)$') {
        throw 'selectedFrameMetadataInvalid'
    }
    $kind = $Matches[1]; $lengthText = $Matches[2]; $opcodeText = $Matches[3]
    $payloadText = $Matches[4]; $detail = $Matches[5]
    $field = if ($kind -ceq 'inbound') { 'frameHex' } else { 'plainHex' }
    if ($detail -cnotmatch ('(?:^| )' + $field + '=([^ ]+)(?: |$)')) { throw 'selectedFrameHexMissing' }
    $hex = $Matches[1]
    if ($hex.Length -gt 13688 -or $hex.Length % 2 -ne 0 -or $hex -cnotmatch '^[0-9A-Fa-f]{8,}$') {
        throw 'selectedFrameTruncatedOrInvalidHex'
    }
    $frame = [Convert]::FromHexString($hex)
    $length = [int]$lengthText; $payloadLength = [int]$payloadText; $id = [uint16]$opcodeText
    $embeddedLength = [int]$frame[0] -bor ([int]$frame[1] -shl 8)
    $embeddedOpcode = [int]$frame[2] -bor ([int]$frame[3] -shl 8)
    if ($frame.Length -ne $length -or $length -lt 4 -or $length -gt 6844 -or
        $payloadLength -ne ($length - 4) -or $embeddedLength -ne $length -or $embeddedOpcode -ne $id) {
        throw 'selectedFrameEnvelopeMismatch'
    }
    return [ordered]@{ sourceLine = $LineNumber; direction = $kind; opcode = $id;
        frameLength = $length; payloadLength = $payloadLength; frameHex = [Convert]::ToHexString($frame) }
}

try {
    $selected = [Collections.Generic.HashSet[uint16]]::new()
    foreach ($id in $Opcode) { if ($id -eq 0 -or -not $selected.Add($id)) { throw 'uniqueNonzeroOpcodesRequired' } }
    $source = Get-WireOrdinaryPath $TraceLogPath
    $destination = Get-WireLocalPath $OutputPath
    $null = Get-WireOrdinaryPath ([IO.Path]::GetDirectoryName($destination)) -Directory
    if ([IO.Path]::GetExtension($destination) -ine '.json' -or (Test-Path -LiteralPath $destination)) {
        throw 'newJsonOutputRequired'
    }
    $prefix = Read-WireLogPrefix $source $MaximumLogBytes
    $frames = [Collections.Generic.List[object]]::new()
    $lineNumber = 0; $limitReached = $false
    foreach ($line in $prefix.text.Split("`n")) {
        $lineNumber++
        if ($lineNumber -le $AfterLine) { continue }
        $frame = Get-WireSelectedFrame $line.TrimEnd("`r") $lineNumber $selected
        if ($null -eq $frame) { continue }
        if ($frames.Count -ge $MaximumFrames) { $limitReached = $true; continue }
        $frames.Add($frame)
    }
    if ($frames.Count -eq 0) { throw 'noSelectedCompleteFrames' }
    $fixture = [ordered]@{ schemaVersion = 1; sourceReadOnly = $true; liveCapturePerformed = $false;
        sourceGateAccepted = $false; liveGameplayProven = $false; exportedUtc = [DateTime]::UtcNow.ToString('O');
        callerReportedClientBuild = $ClientBuild; callerReportedProtocolProfile = $ProtocolProfile;
        observation = $Observation; sourceLog = $source; sourceSnapshotBytes = $prefix.bytes;
        direction = $Direction; opcodes = @($Opcode); afterLine = $AfterLine;
        maximumFrames = $MaximumFrames; maximumLogBytes = $MaximumLogBytes;
        frameLimitReached = $limitReached; partialFinalLineIgnored = $prefix.partialFinalLineIgnored;
        evidenceBound = 'Selected log frames only; caller labels and source authenticity are not independently verified.';
        frames = $frames.ToArray() }
    $json = $fixture | ConvertTo-Json -Depth 6
    $output = [IO.FileStream]::new($destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try {
        $encoded = [Text.UTF8Encoding]::new($false).GetBytes($json)
        $output.Write($encoded, 0, $encoded.Length); $output.Flush($true)
    } finally { $output.Dispose() }
    @{ status = 'WireFixtureExported'; exportedFrames = $frames.Count; frameLimitReached = $limitReached;
        outputPath = $destination; liveGameplayProven = $false } | ConvertTo-Json -Compress
    exit 0
} catch {
    @{ status = 'WireFixtureExportFailed'; reason = $_.Exception.Message; liveGameplayProven = $false } | ConvertTo-Json -Compress
    exit 1
}
