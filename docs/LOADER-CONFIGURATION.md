# LoginWeb loader configuration

LoginWeb serves explicitly configured files. This source snapshot includes no launcher payloads. Each environment variable below names a file; relative paths resolve from the server process's working directory. Bytes are served unchanged, including binary and empty files.

| GET route | Payload file environment variable | Content type |
| --- | --- | --- |
| `/lsj/games/63/cqimiver.txt` | `WOLFCONQUER_LOGINWEB_VERSION_FILE` | `text/plain; charset=us-ascii` |
| `/lsj/games/63/cqimi.txt` | `WOLFCONQUER_LOGINWEB_MANIFEST_FILE` | `application/octet-stream` |
| `/games/zf/reg/version.shtml` | `WOLFCONQUER_LOGINWEB_REGISTRATION_VERSION_FILE` | `text/html; charset=us-ascii` |
| `/ndlogin/skin/zf.dat` | `WOLFCONQUER_LOGINWEB_SKIN_ZF_DAT` | `application/octet-stream` |
| `/driver/config.txt` | `WOLFCONQUER_LOGINWEB_DRIVER_CONFIG_FILE` | `text/plain; charset=us-ascii` |
| `/ndlogin/zfplay/update.txt` | `WOLFCONQUER_LOGINWEB_UPDATE_TXT` | `text/plain; charset=us-ascii` |
| `/ndlogin/zfplay/autopatch.dat` | `WOLFCONQUER_LOGINWEB_AUTOPATCH_DAT` | `application/octet-stream` |
| `/telprogram.aspx` | `WOLFCONQUER_LOGINWEB_SUPPORT_HTML_FILE` | `text/html; charset=utf-8` |

A supported route returns **503 Service Unavailable** when its variable is unset or its file cannot be read. There is no fallback body. Unknown paths return 404 and methods other than GET return 405. Route matching ignores case and query strings. Startup output reports each route as configured or unavailable without printing file paths or payloads.

## Local synthetic example

Run these PowerShell commands from the repository root with the .NET SDK specified by `global.json`. This example supplies authored test bytes to exercise HTTP transport and readiness; it does not provide a usable game launcher manifest.

```powershell
dotnet build WolfConquer.sln --configuration Release

$fixtureDir = Join-Path $PWD '.artifacts/loader-example'
New-Item -ItemType Directory -Force -Path $fixtureDir | Out-Null
[IO.File]::WriteAllBytes((Join-Path $fixtureDir 'served-version.bin'), [Text.Encoding]::ASCII.GetBytes("example-version-v1`n"))
[IO.File]::WriteAllBytes((Join-Path $fixtureDir 'served-manifest.bin'), [byte[]](0, 255, 65, 0, 13, 10))

# Independent expected inputs: do not derive these from an HTTP response.
[IO.File]::WriteAllBytes((Join-Path $fixtureDir 'expected-version.bin'), [Text.Encoding]::ASCII.GetBytes("example-version-v1`n"))
[IO.File]::WriteAllBytes((Join-Path $fixtureDir 'expected-manifest.bin'), [byte[]](0, 255, 65, 0, 13, 10))

$env:WOLFCONQUER_LOGINWEB_VERSION_FILE = Join-Path $fixtureDir 'served-version.bin'
$env:WOLFCONQUER_LOGINWEB_MANIFEST_FILE = Join-Path $fixtureDir 'served-manifest.bin'
dotnet run --project src/WolfConquer.LoginWeb --configuration Release --no-build -- --listen --address 127.0.0.1 --port 8080
```

Leave that terminal running. In a second PowerShell terminal at the repository root, run:

```powershell
$fixtureDir = Join-Path $PWD '.artifacts/loader-example'
dotnet run --project src/WolfConquer.DataTool --configuration Release --no-build -- loginweb-loader-readiness http://127.0.0.1:8080/ --timeout-ms 5000 --expected-version-file "$fixtureDir/expected-version.bin" --expected-manifest-file "$fixtureDir/expected-manifest.bin"
```

The check probes the version and manifest routes using the protocol Host header. Both must return status 200 and exactly match their independently supplied expected bytes. It does not validate the other six routes or prove game-client compatibility. Unconfigured routes in this example remain unavailable. Press Ctrl+C in the first terminal to stop the server.

Without `--listen`, LoginWeb prints its planned configuration and exits. `--address` defaults to `127.0.0.1`, and `--port` defaults to `80`; the example explicitly selects port 8080.

## Readiness expectations

The two command-line file options can instead be supplied through these environment variables in the **checking process**:

```powershell
$env:WOLFCONQUER_LOGINWEB_EXPECTED_VERSION_FILE = Join-Path $fixtureDir 'expected-version.bin'
$env:WOLFCONQUER_LOGINWEB_EXPECTED_MANIFEST_FILE = Join-Path $fixtureDir 'expected-manifest.bin'
```

Explicit command-line paths take precedence. These expectation variables are separate from the server's payload variables. `local-live-stack-smoke` also reads them before proceeding to its gateway, account, and world checks; it still requires those services and its normal arguments.

Missing or unreadable expected files produce an unconfigured, not-ready result. A 200 response with a different length or different bytes also fails. Readiness exits with code 0 when ready, 2 when not ready or when the HTTP probe fails, and 1 for invalid command-line arguments. Reports include status and byte counts, without dumping expected or received content.
