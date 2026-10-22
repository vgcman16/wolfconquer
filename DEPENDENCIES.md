# Direct dependencies

The source projects reference the following exact NuGet package versions. Package source, binaries, and caches are not bundled in this repository. The declared license information was checked against the package metadata; it does not establish a license for WolfConquer source or external game material.

| Package | Version | Declared license |
|---|---|---|
| [MySqlConnector](https://www.nuget.org/packages/MySqlConnector/2.5.0) | 2.5.0 | MIT |
| [MySql.Data](https://www.nuget.org/packages/MySql.Data/6.3.6) | 6.3.6 | Not declared in the restored package metadata; review upstream terms before redistribution |
| [protobuf-net](https://www.nuget.org/packages/protobuf-net/2.0.0.668) | 2.0.0.668 | Legacy license URL only (Google Code); review upstream terms before redistribution |
| [System.IO.Pipelines](https://www.nuget.org/packages/System.IO.Pipelines/9.0.0) | 9.0.0 | MIT |
| [System.Text.Encoding.CodePages](https://www.nuget.org/packages/System.Text.Encoding.CodePages/9.0.0) | 9.0.0 | MIT |
| [coverlet.collector](https://www.nuget.org/packages/coverlet.collector/6.0.2) | 6.0.2 | MIT |
| [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/17.12.0) | 17.12.0 | MIT |
| [xunit](https://www.nuget.org/packages/xunit/2.9.2) | 2.9.2 | Apache-2.0 |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio/2.8.2) | 2.8.2 | Apache-2.0 |

NuGet resolves additional transitive dependencies during restore. A binary distribution would need its own inventory and the applicable upstream licenses and notices for everything distributed. This source-only table is not that inventory.

`MySql.Data` is deliberately pinned for the reconstructed legacy data owners;
the existing modern runtime uses `MySqlConnector`. The legacy package targets
.NET Framework and emits NU1701 compatibility warnings on .NET 9. Its DLL differs
from the private reference DLL despite the same assembly version; package-binding
tests do not establish byte equivalence or database execution parity. No package
binaries are bundled, and production suitability or security is not established.

`protobuf-net` is deliberately pinned for the reconstructed native table's real
runtime registrations. The explicit `lib/net40` desktop asset matches the
preserved reference DLL byte-for-byte; the package's separate `netcore45` asset
does not. Package compile/runtime assets are excluded from automatic selection,
then the exact desktop assembly is referenced explicitly. It targets .NET
Framework and emits NU1701 on .NET 9. Narrow runtime checks are not a claim of
general support, production suitability or security. No package DLL is committed.
