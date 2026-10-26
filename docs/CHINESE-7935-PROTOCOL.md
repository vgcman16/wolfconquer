# Chinese 7935 patch research

The current working target remains Chinese 7899 / `VisibleDx8D13`. The latest
[official manual patch list](https://down.99.com/html/list/72/list_addtime_1.shtml)
checked on 2026-10-26 lists 7935, released 2026-10-24. The complete 7892–7930 bundle
and increments 7931/7932/7933/7935 were downloaded, matched against publisher MD5,
recorded with local SHA256 and extracted as archive data. No installer was run.
These isolated files are not an assembled, tested or distributed 7935 client.

The selected DX8 engine comes from 7931; the later downloaded increments do not
replace it. `Env_DX8/Conquer.exe` is 30,085,432 bytes, SHA256
`a7e1aac88a999b1f4723e6a1f197b96924e24022fd681fb2c9cf79c1bb413422`.
Static inspection of its actual parser, writer, constructors and consumers gives
the following bounded source increment, independent of the community emulator.

## Explicit packet codec

`Chinese7935XuanbaoOperationPayload` reads and writes the confirmed known fields
of packet 2132 (`CMsgXuanBaoOptPB`). The older D13 message has fields 1–8; this
engine has fields 1–10.

| Field | Confirmed wire contract |
| --- | --- |
| 1 | Required UInt32 operation |
| 2–7,10 | Optional UInt32 varints; absent and present zero remain distinct |
| 8 | Optional length-delimited string bytes, retained without guessing encoding |
| 9 | Ordered repeated `CXuanBaoAccInfoPB` children, each with two optional UInt32 fields |

The reader deliberately accepts only this known schema. It refuses unknown tags,
wrong wire types, overflow and malformed input; native protobuf can preserve
unknown fields, so this is not complete unknown-field parity. Payload/child limits
are offline allocation guards. Frame reads expect a coherent decoder-produced
`PacketFrame`. No default profile, handler registry, Host or old DTO is changed.

Primary physical locations: outer parse 011AE1E6, parent parse 011C5D4A,
writer 011D14BC–011D15E9, required-field check 00DA3248, child factory 011C657B,
child parse 00DFD6AE and child writer 00E0EAF9. These are selected-engine virtual
addresses observed statically; they are not live-client evidence.

Actual owned-candidate tests pass 31/31 with independent wire goldens, UInt32
limits, optional presence, ordered children, duplicate scalar behavior, ownership,
malformed input and finite bounds. A prepublication parser defect that converted
absent field 8 into present empty was exposed and corrected; see FSB-111.

## Consumer finding and remaining work

For operation 6, Process 011CB94A dispatches to 011CBEAE: zero field 10 gates the
`STR_XUANBAO_ECHO_ACTIVE_SUCCESS` lookup and `ANIMATE_TIP` call; nonzero skips
that success-tip block. The actual wide-string constructor/import establishes
those literals. Both paths subsequently request a window message. This supports
an Echo activation result discriminator for that branch; field 10 is not labeled
a slot count. Other operation consumers and their final receivers remain under
investigation. These static effects do not prove displayed text or delivery.

Before adding a business handler, recover the actual operation 6 request producer,
activation target, owned Echo state, matching catalog, permissions/costs and
durable transaction. Existing RelicFuse 2356 is a separate protocol. The new
`XuanBaoEcho.dat` catalog, operation 13/14 meaning, fifth-slot behavior, profile
transport/startup, copied-client rendering and gameplay acceptance remain open.
No complete 7935 compatibility, live deployment or parity increase is claimed.
