# TEST-WIRE-EXPORT: bounded game-wire fixture export

## Identity and scope

Revision1, 2026-10-28. Export already-recorded game frames for reproducible
parser tests. The caller supplies the client build/profile and UI observation;
these labels are provenance, not independently verified client identity.
No process attachment, capture activation, authentication, packet submission,
server mutation or gameplay acceptance occurs.

## Packet contract

Read only exact `LegacyGameWireTrace inbound-frame` or `outbound-frame` records
from an explicit local log. Select explicit ushort opcodes and one direction.
Extract only inbound `frameHex` or outbound `plainHex`; never copy encrypted hex,
handshake records, arbitrary log messages or command text. Validate complete hex,
embedded little-endian ushort length/opcode and diagnostic length/payload length.
Truncated, inconsistent or malformed selected records fail without a fixture.
Do not decode or infer unknown fields. The existing trace truncates at1024bytes;
a truncated record must be recaptured with separately reviewed diagnostics.

## Catalog/table contract

Not applicable: this tool exports observed frames rather than gameplay tables.
Its output does not establish a catalog or schema mapping.

## Validation and transaction rules

Require absolute ordinary input/output paths (no UNC/device or named streams),
existing output parent, new JSON
leaf, bounded log snapshot (default16MiB, maximum64MiB), maximum128frames and
at most16 unique nonzero opcodes. Read a fixed initial byte-length prefix so an
appending server cannot make the scan unbounded. Ignore a final incomplete line;
cut at the byte boundary before strict UTF-8 decoding so a split multibyte
character cannot invalidate earlier complete lines.
Retain source line numbers, snapshot byte count and the explicit bounds.
Create the output only after all selected records within that prefix validate;
never overwrite an existing fixture. No source file writes or commands occur.
When matching records exceed the frame limit, mark the export incomplete.
No selected records is a failure, not an empty successful test fixture.

## Expected wire behavior and fixtures

Actual D13 mail claim/content frames from the2026-10-28 owned fixture motivate
the tool. Tests use those literal frames and unrelated fabricated log messages
to verify selection, no encrypted/handshake export, malformed/truncated denial,
bounded selection, original log preservation and overwrite refusal.
Export success means a bounded fixture exists, never a passing handler or game.

## Evidence alignment

`ConquerTcpServer` emits outbound diagnostics and limits each hex to1024bytes.
`LegacyGameServerPacketPipeline` emits inbound frames after removing Client seal.
Both use the standard four-byte packet header. Live mailbox2264 rejection remains
open until separately tested profile-aware parser/handler work is qualified.

## Implementation and qualification

`tools/testing/Export-GameWireFixture.ps1` and
`GameAcceptanceRunnerTests.WireExport.cs`. Root records actual test results in
the handoff; this spec alone is not execution or acceptance evidence.
