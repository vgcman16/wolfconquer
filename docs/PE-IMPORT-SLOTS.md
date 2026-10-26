# Selected PE import-slot inspection

Use this read-only Python command to identify 1..8 selected **normal IAT**
entries in an explicitly pinned ordinary local PE. It never loads native code
or resolves runtime dependencies.

```powershell
python -B tools/reverse/Inspect-PeImportSlots.py --pe C:\owned\sample.exe --sha256 <64-hex-sha256> --size <decimal-bytes> --iat 00401300 00401304
```

Use exact eight- or sixteen-digit hexadecimal virtual addresses and expected
source length/hash from an independent inventory. Both `Inspect-PeImportSlots.py`
and its adjacent `pe_import_slots.py` are required. Only Python's standard
library is used.

Success emits one JSON report after all checks finish. Refusal exits 1, writes
a generic stderr diagnostic and emits no partial JSON. Named and ordinal normal
imports are supported in x86 PE32 and x64 PE32+ files. Delayed/dynamic imports,
forwarded exports and actual runtime targets are outside this command.

Limits are fixed: 64-byte through 64-MiB input, 1..8 distinct slots, 96 sections,
256 descriptors, 4096 entries per candidate thunk table, 65,536 total thunk-pair
probes, 256 bytes per selected NUL-terminated name, 64 KiB per tool source,
16 KiB output and a cooperative 30-second deadline. Blocking filesystem I/O is
not asynchronously interrupted. Caps refuse; they never silently truncate.

The parser requires unique raw backing, valid header/section bounds and complete
descriptor/thunk terminators. It rejects ambiguous slot ownership, conflicting
IAT/lookup termination and bound imports lacking an original lookup table.
Descriptor/candidate-thunk metadata is checked to establish selected ownership;
only the requested DLL/import names are dereferenced and emitted. This is not a
full import-name audit or a content safety/redistribution approval.

The command verifies source bytes and source/tool identities before and after
inspection. Input/tool files and ancestors must be ordinary; reparse, symlink
and device paths refuse. Windows path and handle ctime APIs are compared against
their own initial identities, using common file/write fields for cross-API
matching. These are bounded point-in-time checks, not hostile-namespace proof.
No PE/payload copy, configuration value or arbitrary binary string is exported.

Run the focused synthetic suite from the repository root:

```powershell
python -B tools/reverse/tests/test_pe_import_slots.py
```

Its 28 tests cover PE32/PE32+, names/ordinals, unbound fallback, selected-name
scope, mapping/ownership/termination/cap refusals, wrong pins, ordinary/reparse
metadata, identity guards and actual CLI success/refusal without partial JSON.
Fixtures live only beneath `.artifacts/pe-import-slot-tests/` and are removed by
the owned temporary-directory fixture. This validates synthetic parser/CLI
behavior; it proves no native execution, configuration semantics or deployment.
