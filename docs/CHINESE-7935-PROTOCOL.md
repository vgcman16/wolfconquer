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

The fresh activation callback009D72DF and builder011AF9D2 set operation6 and
field4 to UI selection+1. Fresh protobuf initialization leaves response fields
absent. The independent selection0 frame `0800540808062001` passes the actual
codec/packet writer; field2/field10/data8/children are absent. Catalog lookup
uses one-based cost keys, but the callback alone does not prove cost ownership.

`ChineseLuaCatalogTransform.Decode` now provides the separate explicit12-byte-key
byte transform: eleven ThreeWay rounds using the native big-endian block contract,
then XOR54 on the final1..11bytes.43cases pass, including four primary published
[known answers](https://raw.githubusercontent.com/weidai11/cryptopp/CRYPTOPP_8_9_0/TestData/3wayval.dat),
every tail length, ownership and finite bounds. The public-domain reference's
block byte order needs an adapter; native instructions define this API's contract.
No key discovery/default, game key, asset, text/Lua execution or runtime binding
is included. Two pinned private Echo catalogs (501/9114bytes) decode to
UTF8-text candidates. Grammar/schema, runtime binding and gameplay authority
remain unverified; no catalog text or key is bundled.

Before adding a business handler, recover the owned Echo state, matching catalog,
permissions/costs and durable transaction. Existing RelicFuse 2356 is a separate protocol. The new
`XuanBaoEcho.dat` catalog, operation 13/14 meaning, fifth-slot behavior, profile
transport/startup, copied-client rendering and gameplay acceptance remain open.
No complete 7935 compatibility, live deployment or parity increase is claimed.

## C# decoded Echo catalog intake (2026-10-29)

The strict literal-data intake is now implemented in C# as
Chinese7935DecodedEchoCatalog, an immutable typed snapshot and explicit pinned
already-decoded file loading. It retains complete four-field rows in source order,
rejects duplicate row identities/malformed or unsupported data, and produces a
fresh typed projection for the existing pure Type6 threshold calculation.
No Lua/native execution, key discovery, default catalog, startup/profile binding,
handler or gameplay activation is added. Declared7930/7931 and a supplied digest
remain metadata/comparison evidence, not trusted runtime ownership.

Root's actual Release build passed with0errors and no changed-file warnings.
The combined existing/new Chinese patch component selection passed215/215,
0failures/skips:46 new catalog outcomes plus169 existing component outcomes.
A separate C# probe used the real new pinned-file API on both preserved private
decoded inputs without printing table rows:7930 returned0rows;7931 returned85rows,
IDs1..85 and17attribute groups of5, matching the earlier independent parse and
both recorded digests. No table/key/assets are bundled in the source or tests.

This is local source/data qualification; the next full integration gate is pending.
Actual current Echo item/contribution/cache ownership, permissions/costs, Unload,
operations13/14, durable activation and copied7935-client acceptance remain open.

## Full source qualification (2026-10-29)

Full Windows [40886558198](https://github.com/vgcman16/wolfconquer/actions/runs/40886558198)
passed on exact50cf80fa8c5d1c334d7ee6bfea0715040c66fc56, containing source30e793f0
and the preceding decoded-Echo loader dd976947. Core29,929 passed/152 existing
NotExecuted, Updates446/446, Launcher57/57; all eight stages passed with0failures.
Actual local20261029T135633667Z-e23c3f08 finished2026-10-29T14:44:06.9964313Z.
All51 new combined focused outcomes and46 decoded-Echo catalog outcomes are
present as Passed in the full core TRX. Actual logs/receipts/all3TRXs and matching
complete World/DataTool runtime files are preserved outside the runner.
This qualifies the Caprice pair, shared journal, XP/Fly preflight and Echo loader
source. Whole-cast/SQL/native physical Stamina, power-loss/multiprocess safety,
deployment, newer-client/English/full gameplay and live acceptance remain open.
No original-save/route edit, legacy client relaunch or retained gear resend followed.

## Official full-client7937 reference (2026-10-29)

The official7917 full reference was privately unpacked and its own bundled
updater downloaded the intermediate packages through7937. Final7937 package
members and the known7931 DX8 engine identities match. A separate whole-client
staging copy passed actual copy/metadata checks; no proprietary assets are
published. A reversible ASCII alias of the complete existing skin opened the
modern launcher. Local routing/auth, English rendering and7937 gameplay are
still unaccepted; the original working client remains separate.

The final private English UStrRes proposal retains1191 keys and changes1145
values, with16 tests passed. It is not yet applied or live-rendered and covers
only one table. The selected catalog intake passed275 focused tests, but its
first actual7937 run correctly refused the historical65-field item schema.
Complete saved-data audit instead found57093 main and1693 Sub rows of exactly
67 fields with canonical uniqueUInt32 IDs, strictCP936 roundtrip and exact
packed inverse. The explicit7937 typed envelope passed combined Build05 and
294/294 catalog tests. Actual managed intake returned0 and retained both complete
57093/1693-row envelopes. New fields remain opaque. Echo/configuration remain opaque without an exact
decoder/evidence pair. Counts and roundtrips do not establish gameplay parity.

Classic target validation separately passed42 focused/2763 neighbor checks.
The current integration batch still awaits its combined full Windows gate.
Whole-cast durability, real SQL ownership, newer runtime/cost/profile wiring,
complete English support and full gameplay acceptance remain open.
