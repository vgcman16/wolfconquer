function Assert-OwnedSqlPlainPath([string] $Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $cursor = $full
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            $entry = Get-Item -LiteralPath $cursor -Force -ErrorAction Stop
            if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'Owned SQL paths cannot traverse reparse points.'
            }
        }
        $parent = [IO.Path]::GetDirectoryName($cursor)
        if ($parent -eq $cursor) { break }
        $cursor = $parent
    }
    return $full
}

function New-OwnedSqlRoot([string] $RepositoryRoot) {
    $repository = Assert-OwnedSqlPlainPath $RepositoryRoot
    $tests = Join-Path $repository 'tests\WolfConquer.Tests\WolfConquer.Tests.csproj'
    if (-not (Test-Path -LiteralPath $tests -PathType Leaf)) { throw 'The selected test checkout is missing.' }
    $parent = Assert-OwnedSqlPlainPath (Join-Path $repository '.artifacts')
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw 'The checkout artifacts directory must already exist.' }
    $nonce = [Guid]::NewGuid().ToString('N')
    $root = Join-Path $parent ('owned-sql-auth-' + $nonce)
    [WolfConquer.Tools.OwnedSql.OwnedSqlChild]::CreateFreshDirectory($root)
    # No secret is written before inheritance is removed and the ACL is applied.
    $acl = [Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    $rights = [Security.AccessControl.FileSystemRights]::FullControl
    $inherit = [Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit'
    $propagate = [Security.AccessControl.PropagationFlags]::None
    $allow = [Security.AccessControl.AccessControlType]::Allow
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
    foreach ($sid in @($identity, [Security.Principal.SecurityIdentifier]::new('S-1-5-18'))) {
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid, $rights, $inherit, $propagate, $allow))
    }
    Set-Acl -LiteralPath $root -AclObject $acl -ErrorAction Stop
    $marker = Join-Path $root 'owner.txt'
    $stream = [IO.File]::Open($marker, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try { $bytes = [Text.Encoding]::ASCII.GetBytes($nonce); $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) }
    finally { $stream.Dispose() }
    return [pscustomobject]@{ Root = $root; Nonce = $nonce; Repository = $repository; Marker = $marker; Data = (Join-Path $root 'data'); CommandsSettled = $true }
}

function Assert-OwnedSqlRoot($Owner) {
    $root = Assert-OwnedSqlPlainPath $Owner.Root
    $expected = Join-Path (Join-Path $Owner.Repository '.artifacts') ('owned-sql-auth-' + $Owner.Nonce)
    if ($Owner.Nonce -notmatch '^[a-f0-9]{32}$' -or $root -cne [IO.Path]::GetFullPath($expected) -or
        [IO.File]::ReadAllText($Owner.Marker) -cne $Owner.Nonce) { throw 'Owned SQL directory receipt does not match.' }
}

function Remove-OwnedSqlData($Owner) {
    Assert-OwnedSqlRoot $Owner
    $data = Assert-OwnedSqlPlainPath $Owner.Data
    if ($data -cne (Join-Path $Owner.Root 'data')) { throw 'Owned SQL data path changed.' }
    if (-not (Test-Path -LiteralPath $data)) { return }
    # Check every descendant before the one exact recursive removal.
    $pending = [Collections.Generic.Stack[string]]::new(); $pending.Push($data)
    while ($pending.Count -gt 0) {
        $current = $pending.Pop()
        foreach ($entry in Get-ChildItem -LiteralPath $current -Force -ErrorAction Stop) {
            if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Owned SQL data contains a reparse point; retained.' }
            if ($entry.PSIsContainer) { $pending.Push($entry.FullName) }
        }
    }
    Remove-Item -LiteralPath $data -Recurse -Force -ErrorAction Stop
}
