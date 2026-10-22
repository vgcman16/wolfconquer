# WolfConquer client and source update tools

Status: experimental Windows tooling. There is **no official full-client download feed published yet**. The game assets, account data, private configuration, saves and signing keys are not included in this repository. Chinese client7899 / cached DX8 D13 is the current investigation target; this does not establish compatibility with every7899 executable or a finished English client.

The standalone `WolfConquer.Updater` does not depend on or start the game servers. Its automated tests exercise synthetic clients and real temporary Git repositories. They are not real-client login proof.

## Build and test

```powershell
dotnet test tests/WolfConquer.Updates.Tests/WolfConquer.Updates.Tests.csproj -c Release
dotnet build src/WolfConquer.Updater/WolfConquer.Updater.csproj -c Release
dotnet src/WolfConquer.Updater/bin/Release/net9.0/WolfConquer.Updater.dll help
```

Windows and the .NET9 runtime are required for the current framework-dependent CLI. Plan a supported-runtime upgrade before .NET9's November10,2026 end of support; self-contained packages also need runtime security updates. See [Microsoft's support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

## Windows launcher

`WolfConquer.Launcher` is a separate Windows WPF application with original wolf artwork, an emerald theme, animated busy indicators, measured progress and a bounded activity log. Its Client and Source pages use the same typed update engine as the CLI; the GUI does not invoke or parse a second CLI process. The Windows-only launcher and its presentation tests deliberately stay outside the portable main solution.

```powershell
dotnet test tests/WolfConquer.Launcher.Tests/WolfConquer.Launcher.Tests.csproj -c Release
dotnet run --project src/WolfConquer.Launcher -c Release -- --config C:\WolfConquerTools\wolfconquer-updates.json
# Optional explicit installation-folder override; the configuration is not rewritten:
dotnet run --project src/WolfConquer.Launcher -c Release -- --config C:\WolfConquerTools\wolfconquer-updates.json --client-root C:\WolfConquerClient --fresh
# Source-only mode needs no client configuration:
dotnet run --project src/WolfConquer.Launcher -c Release -- --source-root C:\WolfConquerSource
```

Nothing updates or launches merely because the window opens. Choose an explicit local target and an action. Client controls provide check/update, full verification and a separately requested launch; login stays manual. A metadata-only current-release check does not infer launch readiness: use Verify files to confirm the configured signed launch candidate. The executable is hashed again before a requested launch. Local client edits and dirty source copies are preserved by refusal, not silently overwritten.

Progress is phase-local: transferred bytes, verified files, backups or applied files. Git stages without reliable counters remain indeterminate. No estimated overall percentage or ETA is fabricated. Cancel requests wait for safe engine completion; closing the window cannot interrupt critical rollback/recovery. A preview render has an explicit visual-preview banner and disabled operational controls; its sample bars are not a download result.

For an offline layout render of this application's own XAML only:

```powershell
dotnet src/WolfConquer.Launcher/bin/Release/net9.0-windows/WolfConquer.Launcher.dll --render-preview C:\WolfConquerPreviews\client.png
# Supplying --source-root selects the Source page in preview mode, without reading/updating it:
dotnet src/WolfConquer.Launcher/bin/Release/net9.0-windows/WolfConquer.Launcher.dll --render-preview C:\WolfConquerPreviews\source.png --source-root C:\WolfConquerSource
```

Preview output must be a new explicit local file, with no linked ancestors. It does not capture the desktop or interact with a game window.

## Client installation and updates

The operator must first publish a separately curated, tested release and provide local bootstrap configuration with a pinned public signing key. Never point an updater at an arbitrary existing client folder.

```powershell
dotnet C:\WolfConquerTools\WolfConquer.Updater.dll client --config C:\WolfConquerTools\wolfconquer-updates.json --install
dotnet C:\WolfConquerTools\WolfConquer.Updater.dll client --config C:\WolfConquerTools\wolfconquer-updates.json --launch
dotnet C:\WolfConquerTools\WolfConquer.Updater.dll client --config C:\WolfConquerTools\wolfconquer-updates.json --repair
dotnet C:\WolfConquerTools\WolfConquer.Updater.dll recover --config C:\WolfConquerTools\wolfconquer-updates.json
```

- The same pipeline supports a fresh empty/updater-only destination and later updates. Local configuration, public-key trust and the updater itself stay outside the payload directory.
- Small signed discovery metadata identifies an exact signed full manifest. ECDSA P-256/SHA256 signatures, product/channel/sequence/expiry, lengths and SHA256 digests are checked. HTTPS redirects, unsafe paths, linked paths, ZIP extras and aliases are refused.
- Fresh installs can use a complete verified ZIP. Small updates download changed blobs rather than the whole ZIP. Every downloaded file is checked before application.
- Target processes must be stopped. Existing custom/modified files are refused rather than overwritten. Unlisted local files are not deleted. Backups, durable journals and recovery copies are retained; allow disk space for the client, staging, bundle and rollback copies. No automatic pruning is implemented.
- An unchanged-release check is metadata-only; it does not rehash the entire client. `--repair` performs the full verification/precondition pass but does not overwrite user edits. A launch executable is individually verified against the signed release before launch.
- Launch executable and arguments come from local configuration, never remote commands. Authentication remains manual. Do not disable antivirus or certificate validation.
- Local configuration may additionally set `launchWorkingDirectoryRelativePath`, such as `"Env_DX8"`, when the engine requires its own directory. Omitted/null retains the payload root for existing configurations. The path must be an ordinary contained directory with no traversal, aliases or reparse ancestors; launch requires it to exist. It is not supplied by the signed remote manifest.

## Clean source copies only

```powershell
dotnet C:\WolfConquerTools\WolfConquer.Updater.dll source --root C:\WolfConquerSource
dotnet C:\WolfConquerTools\WolfConquer.Updater.dll source --root C:\WolfConquerSource --watch-minutes 5
```

The default trusted origin is `https://github.com/vgcman16/wolfconquer.git`, branch `main`. The operator explicitly starts the watcher; it is not installed silently. It fetches the trusted advertised commit and fast-forwards only a matching clean checkout. Local edits, untracked files, hidden index flags, local commits/divergence, submodules, hooks/filters/URL rewrites and unfinished Git operations are refused or preserved. It never resets, stashes, builds, deploys or restarts a server. Concurrent external edits/Git operations still require operator coordination.

## Operator publishing and hosting

Use a new dedicated signing-key directory outside Git, the curated source and the served directory:

```powershell
./tools/updates/Initialize-UpdateKey.ps1 -SecretDirectory C:\WolfConquerSecrets
dotnet C:\WolfConquerTools\WolfConquer.Updater.dll publish --plan C:\WolfConquerOperator\curated-plan.json --key C:\WolfConquerSecrets\release-private.pem
dotnet C:\WolfConquerTools\WolfConquer.Updater.dll serve --root C:\WolfConquerDistribution --port 48237
```

Publication uses an explicit file list, not recursive packaging of the original client. Blobs are content-addressed; an existing corrupt blob is never replaced silently. The mutable signed discovery feed is written last. Keep the private key securely backed up and never include it in either source or served files. Updating an ephemeral host uses `repoint`; it preserves the exact full release identity.

The HTTP origin binds only `127.0.0.1`. It serves fixed health/feed/blob routes and optional explicitly allowlisted bootstrap downloads; there is no listing, upload, execution or game-control endpoint. Health and HEAD responses are metadata/reachability observations, not full byte-integrity proof. Clients must still verify every download.

An account-free [Cloudflare Quick Tunnel](https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/) can provide development HTTPS without a paid domain. Its hostname changes, availability is not guaranteed and it is not production hosting. The PC and both processes must remain running. A stable, signed discovery index can be published separately after a real release is curated; neither GitHub nor the tunnel runs the game server.

## Local dashboard

The Updater card sits beside Windows Local Checks. It separately reports current loopback HTTP health, the last signed metadata/bundle-and-launcher endpoint check, and saved smoke tests. Receipts older than10 minutes are marked stale.

```powershell
dotnet C:\WolfConquerTools\WolfConquer.Updater.dll check --config C:\WolfConquerTools\wolfconquer-updates.json --receipt C:\WolfConquerSource\.artifacts\local-dev\last-updater-check.json --watch-seconds 60
./tools/dev/Start-LocalDashboard.ps1 -RepositoryRoot C:\WolfConquerSource
```

`check` verifies the signed metadata and HEAD reachability/length of the full-install bundle and launch blob. It does not download/hash the whole client or certify every incremental endpoint. Only actual test results should populate a smoke receipt. The dashboard is loopback/read-only and never installs, logs in or restarts anything.
