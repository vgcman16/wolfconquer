# Read-only pinned7937 Addition/Resolve inspection command

Revision1,2026-11-02. Exact source base: Root168pending candidate, fast-forwardedb06eaa99→5ccca124
by generated-tracker changes only; source/API preimages remain unchanged.
Additive offline DataTool/Core/tests only; preserve frozen12 and all other scopes.
Root alone builds/tests/intakes; CI41495012492 solely owns the host. No commands,
native/client/Lua tools, network, Git or candidate edits execute in this scope.

Command:
client-xuanbao7937-addition-resolve-inspect 7937 <absolute-plain-info> <SHA256> <absolute-packed-addition> <SHA256>

Exactly6argv entries including command, literal version7937, two64hex pins. No
output option, directory scan, implicit path, default key or installation. Existing
client-xuanbao7937-inspect and all other routes retain their behavior. Program.cs
gets only dispatch/IsCommand/help hunks; implementation lives in a new partial.

Core adds TryLoadResolveAndAdditionFiles to the existing decoded-Info partial.
It reuses that class's ordinary-file/ancestor guard and the frozen Resolve and raw
Addition parsers. Require explicit7937, both32byte pins and absolute ordinary
local paths; reject UNC/device/reparse/nonfile inputs. Hold both read-only leases
(deny write/delete sharing) through bounded complete capture/hash/parsing, recheck
length/EOF/path and return both immutable snapshots only after lease cleanup.
Info bound65536B, Addition bound1048576B from actual parser constants. Capture
pins defensively, compare SHA fixed-time, never transcode/decode the encoded Info
or accept partial results. Reader reports bounded static reason codes only;
normal filesystem guards are not hostile-namespace or atomic snapshot proof.

Success: exit0, oneJSON stdout object, empty stderr. Fields exactly:
FormatVersion1, ClientVersion7937, Status="parsed", ResolveFieldCount,
AdditionRowCount, AdditionNumericColumnCount5, PlainInfoSha256,
PackedAdditionSha256, DecodedAdditionSha256, followed by false boundary flags.
No actual field values, row text, names, caller paths, private exceptions or writes.

Refusal: exit1, empty stdout, oneJSON stderr object containing FormatVersion1,
ClientVersion7937, Status="refused", Failure staticcode and the same falseflags.
Arguments invalid =>Arguments. Core codes: UnsupportedVersion/PinRequired/
PathRequired/UnsafePath/InputBounds/SourceChanged/PinMismatch/FileRead,
ResolveParser:<existing enum>, AdditionParser:<existing enum>, optional bounded
Cleanup suffix if closure also fails. No partial counts/hashes leak on refusal.
Unexpected command-level exception =>InspectionFailed, never exception.Message.

Falseflags: RuntimeBinding, GameplayActivated, AdditionMeaningsEstablished,
ResolveBranchOwnerEstablished, CurrentActionEstablished, RewardOwnershipEstablished,
InputsWritten, LuaExecuted, NativeClientExecuted. Parsed means only retained bytes
match suppliedpins and supported syntax. It does not prove provenance, bGold/item/
currency/current action/reward owner or active runtime/client acceptance. Addition
column4/three extra raw codes and Common/Gold versus old9 remain unresolved.

Author real Core owned-file controls for pair success/no partial refusal/strict
bounds/path/pins/complete tails/deep storage and released leases. Real process
command controls use existing TestProjectProcess.Create("WolfConquer.DataTool")
harness, never direct private method reflection. Check exact JSON schema/redaction,
input bytes/timestamps/file-set preservation, valid/malformed/missing/locked paths,
bad argument counts/pins/version, and old command/help compatibility. All fixtures
are synthetic; actual retained-data C# qualification occurs later through this
permanent command after Root builds. No test/build/command was run by authors.

Coordinator owns reader/module/three Program hunks/Core tests. Helper owns new
process tests/fixture only. Manifest pins exactbase preimages and every postimage;
freeze after source review. Suggested Root selectors/new command are review inputs,
not a claim that C# or actual data has executed.
