# Explicit saved-state admission

Target: Chinese client7899 / `VisibleDx8D13`. This option connects the existing
ordinary saved-state transaction to the real WorldServer startup. It does not
complete the native loader, account authentication, SQL, mailbox reward producers
or live-client acceptance.

## Create a disposable fixture

The offline command below creates a new synthetic character and one potion. It
creates no account/password, login bypass, admin permission or server process.
Use a new GUID-named directory outside all existing save trees:

```powershell
$taskFixture = Join-Path $env:TEMP ('wolf-owned-admission-' + [guid]::NewGuid().ToString('N'))
dotnet run --project src/WolfConquer.DataTool -- seed-owned-admission-fixture $taskFixture 1399701 1399701 2399701
$env:WOLFCONQUER_OWNED_ADMISSION_CONFIG = Join-Path $taskFixture 'owned-admission.json'
```

The first two IDs must be the same positive account/character UID. The item UID
must be positive, distinct and below UInt32.MaxValue. These are file identifiers;
they do not register an authenticated account. The generated character uses
Twin City1002/410/354, level120/class10, money100, CP50, no bank balance and one
unbound/unlocked item1001000. Existing data is never imported or replaced.

For a new fixture targeting the D13 client, append the exact optional selector:

```powershell
dotnet run --project src/WolfConquer.DataTool -- seed-owned-admission-fixture $taskFixture 1399701 1399701 2399701 --profile VisibleDx8D13
```

Use this command instead of the earlier creator command, with a fresh root.
Explicit D13 selects Class1000/InternTrojan; omitted profile or explicit
`RootD11_7899` retains Class10. Other initial values remain the same. Only those
two exact profile names are accepted. The creator ignores ambient profile
settings; WorldServer's `WOLFCONQUER_PROTOCOL_PROFILE` remains separately explicit.
The initial spell file stays empty. Actual ordinary admission subsequently commits
the legitimate missing Trojan1110 before publishing success and its learned row;
relogin does not regrant it. Casting and rendered class/skill UI remain unverified.

The command writes the six canonical snapshot files, creates the exact new UID
history, releases its lease and publishes `owned-admission.json` last. The initial
historical floor is that one known synthetic item UID. A stopped/failed creation
retains its partial directory and refuses reuse; inspect it and choose a fresh
GUID for another fixture. Repeating the command cannot reset its UID history.

## Open an existing configured domain

WorldServer reads `WOLFCONQUER_OWNED_ADMISSION_CONFIG` before listening. Its bounded,
closed Version1 JSON records the expected domain GUID, six explicit snapshot
routes, mailbox/redeem/claim routes and historical high-water/evidence. The
generated configuration is an example for its own disposable data only.

For a real existing dataset, establish the complete historical UID authority and
quiescent ownership separately. Discovering `state.json`, guessing a high-water
mark or copying this synthetic evidence does not authorize bootstrap or import.
WorldServer never bootstraps the registry. Missing/conflicting routes, a different
domain, a held lease or a pending joint snapshot journal refuse startup. If
`WOLFCONQUER_USERS_ROOT` is also set, it must identify the configured Users root.
Other catalog, authentication, protocol and network settings remain explicit.

One runtime binds one actual Frame handler. Admission preserves saved economic,
item, spell and quest state through the existing complete-image transaction; it
does not regrant the legacy starter inventory. Normal later gameplay/startup
queries can still save their legitimate state changes. The owner retains its
registry lease while tracked workers and world operations drain on shutdown.
Applications embedding this API must await `DisposeAsync` when actors are active;
synchronous disposal refuses rather than releasing a lease still in use.

## Verification scope

The focused controls cover configuration rejection, real registry contention,
saved Frame admission, actual worker/world-gate drain, raw/encrypted WorldServer
process startup and abrupt restart, plus the actual offline DataTool command.
See [publication validation](../VALIDATION.md) for the executed source and outcomes.
Process tests use owned synthetic files and the existing key-based world entry;
they do not prove account-password authentication or client rendering.
