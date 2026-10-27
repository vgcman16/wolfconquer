# Optional world wire diagnostics

Diagnostics are off by default. The observer never registers a handler, sends a
response, grants gameplay authority or determines a packet's business meaning.
It observes the selected protocol profile after the existing dispatch/recovery
gates; early login and other pre-dispatch routes are outside this hook. Follow
the [protocol workflow](engineering/PROTOCOL-WORKFLOW.md) and
[bounded diagnostic specification](protocol/specs/7899-world-wire-diagnostics.md)
before extending its opcode contracts.

To opt in at host startup, set `WOLFCONQUER_WORLD_WIRE_DIAGNOSTICS=1` and
`WOLFCONQUER_WORLD_WIRE_DIAGNOSTICS_DIRECTORY` to an existing absolute directory
without reparse ancestors. The logger creates one exclusive `packets.ndjson`
inside its own GUID child directory with private permissions. Existing output is
never overwritten or rotated. Operators own retention and deletion.

Unknown opcodes receive frame metadata only. Account/authentication, PIN,
character naming and chat/text families are excluded. Records contain no
account ID, character UID/name, endpoint, decoded scalar value or text body.
The two older world frame formatters also retain only opcode and payload length.

Structural scanning requires a separate
`WOLFCONQUER_WORLD_WIRE_DIAGNOSTICS_PROTOBUF_OPCODES=2419` opt-in. The sole
contract is exact **VisibleDx8D13 / 2419**, known fields 1–7 with wire type 0
and field 8 with wire type 0 or 2. D11 opcode coincidence remains opaque;
2132 and other profiles have no inferred schema. The flat scan reports numeric
tags, wire types, offsets and lengths, never recursive content or values.
`encodedLength` spans the available complete/partial field including its tag;
`valueOffset` starts immediately after the tag. A length body stays opaque.

Raw payload requires independent explicit
`WOLFCONQUER_WORLD_WIRE_DIAGNOSTICS_RAW_OPCODES=2419` opt-in and a positive,
current world session read from the actual connection's session store. The
observer checks the same session reference again after capture. Neither raw
opt-in nor scanning authorizes another opcode/profile. An inconsistent frame
supplies no structural or raw payload. A local reference check does not prove
arbitrary concurrent-writer safety or semantic payload sensitivity.

Version-1 JSONL distinguishes complete scans, payload/field limits, truncation,
malformed data and unsupported groups. Unknown fields are observations, not an
automatic codec or handler. Structural signatures omit timestamps and raw/scalar
values for metadata-only records. After explicit authorized raw capture, only
the bounded observed prefix also contributes to the private signature digest:
distinct prefixes survive, while repeated prefix/shape pairs dedupe. No digest
is emitted. Beyond-prefix differences dedupe when the structural evidence also
stays identical; the opted structural scan has its own bound. Default limits
are 1,000 records, 1 MiB, 60 records/minute, 256 signatures,
1,024 scanned bytes, 64 fields and 64 raw bytes. In-process options can reduce
these bounds; finite maxima apply. Output/configuration failures disable or
refuse diagnostics without changing gameplay results. A failed physical write
may leave a partial final line; delivery and crash durability are not guaranteed.

Root's public focused qualification passed **229/229**, zero failures/skips.
All 138 tests-only baseline names are retained; that baseline had 16 failures
and 122 passes exposing the old payload-prefix output. The 91 additional cases
exercise the actual new logger API, including genuine ordinary-session raw
capture, distinct-prefix dedupe and bounded output. Private focused qualification also passed **229/229**, zero failures/skips. A later mechanical test cleanup retains the same ACL assertions in an
OS-guarded loop; exact-source full CI is pending. No live capture, deployment or
new protocol meaning is claimed.
