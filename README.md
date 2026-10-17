# WolfConquer

WolfConquer is a C# implementation of server, packet, and data tooling for researching Conquer-compatible protocols. This repository contains a sanitized source snapshot, with a new Git history.

## Build and test

Install a .NET SDK compatible with `global.json` (9.0.314, with roll-forward enabled). From the repository root:

```sh
dotnet restore WolfConquer.sln
dotnet build WolfConquer.sln -c Release --no-restore
dotnet test WolfConquer.sln -c Release --no-build --no-restore
```

Tests include local process and loopback HTTP/socket checks. The source was developed on Windows; platform-specific coverage and the exact publication validation results are recorded in [VALIDATION.md](VALIDATION.md). Tests requiring omitted external data are [explicitly skipped](docs/EXTERNAL-FIXTURE-TESTS.md).

## Projects

| Project | Purpose |
|---|---|
| `WolfConquer.Core` | Packet handling, server behavior, data readers, and reusable client-file utilities |
| `WolfConquer.AccountServer` | Account service entry point |
| `WolfConquer.WorldServer` | World service entry point |
| `WolfConquer.Gateway` | Gateway entry point |
| `WolfConquer.LoginWeb` | Local HTTP service for explicitly configured loader responses |
| `WolfConquer.DataTool` | Command-line data, diagnostics, and client-file utilities |
| `WolfConquer.Tests` | Unit and integration tests using constructed fixtures |

Use the data tool's help command to see its commands:

```sh
dotnet run --project src/WolfConquer.DataTool -- help
```

The repository does not include a game client, game assets, account database, captured network payload files, or a preconfigured live deployment. Supply your own authorized data and configuration where a command needs external files. See [PUBLICATION-SCOPE.md](PUBLICATION-SCOPE.md) for the snapshot boundary and changed optional features, [text catalogs](TEXT-CATALOGS.md) for client patch inputs, and [loader configuration](docs/LOADER-CONFIGURATION.md) for HTTP payloads and readiness checks.

Direct NuGet dependencies and their declared licenses are listed in [DEPENDENCIES.md](DEPENDENCIES.md). Those dependency licenses apply to the named packages; this snapshot does not add a blanket license for the project source.
