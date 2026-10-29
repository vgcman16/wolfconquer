Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'GameAcceptance.Resources.ps1')

function Read-GaHealthIntent([string]$Root) { return Read-GaResourceIntent $Root 'consume-health' }
function New-GaHealthState([string]$Status, [string[]]$Issues) { return New-GaResourceState $Status $Issues 'consume-health' }
function Set-GahPins($State, $Intent) { Set-GarPins $State $Intent 'consume-health' }
function Start-GaHealth($Context, $Before, [uint32]$Uid, [uint32]$Type, [long]$Hitpoints) {
    return Start-GaResource $Context $Before 'consume-health' $Uid $Type @{expectedHitpoints=$Hitpoints}
}
function Resume-GaHealth($Context, $Initial, $Before, [string]$AfterLeaf, [uint32]$Uid, [uint32]$Type, [long]$Hitpoints) {
    return Resume-GaResource $Context $Initial $Before $AfterLeaf 'consume-health' $Uid $Type @{expectedHitpoints=$Hitpoints}
}
