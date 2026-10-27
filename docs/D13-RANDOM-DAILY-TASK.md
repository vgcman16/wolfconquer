# D13 RandomDailyTask packet codec

Target: Chinese client 7899 / `VisibleDx8D13`. Opcode **2419** identifies the inspected
`CMsgRandomDailyTaskPB` schema. The reader/writer is a protocol prerequisite;
no dispatcher or daily-task gameplay is activated by this component.

## API and source

`D13RandomDailyTaskPacket(uint action, D13RandomDailyTaskFields fields = default,
ReadOnlySpan<uint> values8 = default)` owns its repeated values. It exposes `Action`,
`Fields`, read-only `Values8`, `ToPayloadBytes()` and `ToFrameBytes()`.
`TryParseKnownPayload` and `TryParseKnownFrame` return false/null on refused input.
The frame API expects a coherent decoder-produced frame; it checks opcode/payload,
not arbitrary header/frame reconciliation.

The three source files are [packet model](../src/WolfConquer.Core/World/D13RandomDailyTaskPacket.cs),
[reader](../src/WolfConquer.Core/World/D13RandomDailyTaskPacket.Parsing.cs) and
[writer](../src/WolfConquer.Core/World/D13RandomDailyTaskPacket.Encoding.cs).

| Field | Known wire contract |
| --- | --- |
| 1 | Required UInt32 action. Presence is required even for zero. |
| 2–5, 7 | Optional UInt32; absent and explicitly present zero remain distinct. |
| 6 | Optional Int32 with ordinary signed varint encoding, **not ZigZag**. Negative output uses ten bytes. |
| 8 | Ordered repeated UInt32; packed, unpacked and mixed input append in encounter order. |

Duplicate scalars use the last value. Canonical output writes present fields 1–7
ascending, then **unpacked** field 8. The observed query payload is
`08 06 10 00 18 00`: action 6 with explicitly present fields 2=0 and 3=0. These values
are packet data, not assignment or reward authority.

## Managed policy and verification

This is a **closed known-schema** API. Unknown fields and wrong wire types reject;
they are never silently discarded. Native generic unknown-field retention is
outside this API. Malformed/truncated input and missing required action produce
no partial packet. Int32 reads accept low-DWORD storage bits and proper negative
Int32 sign extension; unrelated widened values reject under managed policy.

The payload bound is 65,531 bytes. The repeated bound is 32,764 values, derived from
the minimum canonical costs of required action and unpacked elements. Full encoded
size is preflighted, including packed/signed input that expands on output. These
are managed envelope limits, not native allocation, objective or entitlement caps;
existing smaller transport limits remain unchanged.

**71/71 focused codec cases passed**, including in the terminal public-derived
selection 15047 (250/250 total, zero failures/skips). See [validation](../VALIDATION.md).
This remains separate from a new exact-public full CI run. No handler,
assignment/day/reset state, payment,
completion/reward transaction, live response, Chinese 7935 activation or full-parity
acceptance follows. Those require genuine catalogs, owners and durable integration.
