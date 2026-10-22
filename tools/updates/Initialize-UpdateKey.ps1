#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$SecretDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or -not [IO.Path]::IsPathFullyQualified($SecretDirectory)) {
    throw 'An explicit local Windows secret directory is required.'
}
$keyRoot = [IO.Path]::GetFullPath($SecretDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)
if ($keyRoot.StartsWith('\\') -or $keyRoot -eq [IO.Path]::GetPathRoot($keyRoot).TrimEnd('\')) {
    throw 'Do not use a share or volume root for signing keys.'
}
for ($ancestor = $keyRoot; $ancestor; $ancestor = [IO.Path]::GetDirectoryName($ancestor)) {
    if (Test-Path -LiteralPath $ancestor) {
        if ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse path refused.' }
        if (Test-Path -LiteralPath (Join-Path $ancestor '.git')) { throw 'Signing keys must stay outside Git checkouts.' }
    }
}
if (Test-Path -LiteralPath $keyRoot) { throw 'Choose a new dedicated secret directory; existing directories are not modified.' }
$null = New-Item -ItemType Directory -Path $keyRoot
$identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
$system = [Security.Principal.SecurityIdentifier]::new('S-1-5-18')
$acl = [Security.AccessControl.DirectorySecurity]::new()
$acl.SetOwner($identity)
$acl.SetAccessRuleProtection($true, $false)
foreach ($principal in @($identity, $system)) {
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($principal,
        [Security.AccessControl.FileSystemRights]::FullControl,
        [Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit',
        [Security.AccessControl.PropagationFlags]::None, [Security.AccessControl.AccessControlType]::Allow))
}
Set-Acl -LiteralPath $keyRoot -AclObject $acl
$key = [Security.Cryptography.ECDsa]::Create([Security.Cryptography.ECCurve+NamedCurves]::nistP256)
try {
    $privatePath = Join-Path $keyRoot 'release-private.pem'
    $publicPath = Join-Path $keyRoot 'release-public.pem'
    [IO.File]::WriteAllText($privatePath, $key.ExportPkcs8PrivateKeyPem(), [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($publicPath, $key.ExportSubjectPublicKeyInfoPem(), [Text.UTF8Encoding]::new($false))
    [ordered]@{ privateKeyCreated = $true; privateDirectoryAclRestricted = $true
        publicKeySha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($key.ExportSubjectPublicKeyInfo()))
        warning = 'Back up the private key securely. Never put it in Git or the served download directory.' } | ConvertTo-Json
} finally { $key.Dispose() }
