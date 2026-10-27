# D13 prestige wire contract

Target: Chinese client7899 / VisibleDx8D13; exact executable SHA256
D13B45BD2681DB7D3D8548EA575D89E0D05613BA00E8F9763DBF3F89030402A5.
This is a bounded static receive-contract finding and a
raw codec boundary. The saved-prestige query is **not activated**; the ordinary
GenericRanking handler retains its existing empty response.

## Client identity and layout

Exact `CMsgRank` RTTI links type descriptor `013C1F60` (name at `013C1F68`),
complete-object locator `01240A60` and vtable `011A82B8`. Receive slot `00C50C8A`
checks opcode `0x875` (2165). Process slot `00C50CB6` dispatches action 6 to
`00C50F7A`.

The D13 action-6 consumer advances **113 bytes per row**. The existing/reference
GenericRanking writer uses 109-byte rows and omits D13's final uint32. Keep the
successful 109-byte simple/Chi codec and its 130-byte frame unchanged.

All offsets below exclude the four-byte packet header. Integers are little-endian.

| Payload offset | Field |
| --- | --- |
| 0 | action 6, byte |
| 1 | rank type, uint32 |
| 5 | registered count, uint16 |
| 7 / 8 | page / finish, byte each |
| 9 | row count, int32 |
| 13 | explicit raw header value, uint32 |
| 17 | first row |

| Row offset | Field |
| --- | --- |
| 0 | rank, byte |
| 1 | amount, eight wire bytes; this consumer reads the low uint32 |
| 9 / 13 | category code / character UID, uint32 each |
| 17 / 49 | raw name slots, 32 bytes each |
| 81 / 85 / 89 | level / class / mesh, uint32 each |
| 93 | explicit opaque tail, 16 bytes |
| 109 | required explicit opaque value, uint32 |

Action 6 compares the first UID slot with category code 80000000 for World-specific
class-zero handling and reads the second slot as the character UID. Category comes
first. The eight-byte amount and second name subdivision retain the existing wire
structure; this branch directly consumes only the low 32 amount bits and first name.

## Codec admission and unresolved source

`LegacyD13PrestigeRankingPacket` handles complete raw action-6 frames only. It
preserves raw names and opaque fields without text conversion or default values.
Caller Count must be 0–60 before snapshot allocation or indexing; null rows are
checked before writer allocation. Parsing requires payload length exactly 17 + 113 × Count.
The 60-row bound derives from the existing 6,844-byte transport maximum. It is
**not** proof of client-supported cardinality or continuation behavior.

The row+109 value reaches stack-record+`0x14`, then `0051289B` copies it unchanged
to destination-record+`0x14`. The established singleton's map is at owner+`0x34`,
keyed by `(Class % 100000) / 1000`; its record begins at node+`0x14`, placing the
value at **node+`0x28`**. No meaningful reader, label or required source owner was
established. No saved/runtime field or invented zero is authorized as its source.

Activation still requires that source, complete request/reset/continuation behavior,
and name-to-client-slot conversion evidence. This branch loops Count rows without
reading Page/Finish; the reference's eight-row split alone does not establish the lifecycle.

## Verification limit

Root executed the focused raw-codec suite: **27 passed, 0 failed**. Its independent
synthetic golden is 247 bytes (21 + 2×113), not a client capture. This result is not
full new-source CI, deployment, rendering or live-client proof. The unactivated
query has no fixed-source-bug completion claim.
