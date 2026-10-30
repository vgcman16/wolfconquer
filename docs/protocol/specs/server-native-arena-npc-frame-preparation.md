# FSB-213: Prepared native Arena and NPC output

## Identity and scope
- Revision 1, 2026-10-29; Root offline native packet qualification batch.
- Base: public91040398 plus the Root overlay snapshot in DEPENDENCY.json. FSB-209's shared PrepareFrame/ReadPrepared implementation is a separate required dependency.
- Target: existing native component and retained 7899/VisibleDx8D13 successful wire fixtures. No new 7937 profile, field semantics or handler registration is inferred.
- Initial owners: ArenaRankingPacket, ArenaScorePacket and NpcReply.Codec. Root explicitly approved the sole ranking-response caller LegacyNativeMsgArenaRanking.cs and one new focused Packet.PreparedAppend partial.

## Packet contract
| Output | Contract retained |
| --- | --- |
| Arena ranking2093 | Header body UInt16 rank/page, UInt32 total nonnull rows, fixed UInt32 marker10, UInt32 ArenaID; each row is56 bytes with fixed32-byte native name; finalized once after all selected rows |
| Arena score2059 | Exact96-byte unsealed frame; both server IDs use one captured actual configured MyServerInfo.ID; native fixed-name UTF8 truncation, terminal UInt32Max and seal rules unchanged |
| NPC reply2255 | Header/body reserved UInt32, UInt16 InputMaxLength, byte OptionID/type; displayfalse omits the entire string list, displayed null text remains one empty slot |

Ranking's public component sequence remains Create -> zero/more AddItem -> Finalize. Create ends at20 and preserves Size; each AddItem appends56 and preserves Size. A helper must not finalize each segment or replace that valid sequence with a different layout. Unknown enum values remain representable. Native fixed strings preserve character-count-based UTF8 byte truncation, including partial code points.
The existing ranking query remains two UInt16 fields; its local tuple now prepares through ReadPrepared so an incomplete second field cannot publish the first output or consume a prefix.

## Catalog/table contract
Not applicable: no catalog meanings or new schema fields.

## Validation and transaction rules
- Full score/NPC builders capture their scalar/string/actual server inputs and call the shared FSB-209 PrepareFrame. All managed allocation, encoding and complete capacity/framing checks precede target publication.
- Missing required configured server or required names refuse before target bytes/Size/cursor change; no placeholder server identity is supplied. NPC optional null text/display rules remain valid.
- A dedicated raw-segment helper prepares controlled sequential writes in genuine scratch storage, snapshots their actual written bytes, then rechecks the actual live/full-width target and complete Capacity before one copy. It preserves Size and changes cursor only after successful copy. Header preparation writes its complete16-byte body at offset4; append uses the original cursor. It does not infer input authority from Capacity.
- The sole ranking Handler consumes the valid query, snapshots nonnull source rows in array order, preserves total count/page/filter/global-rank/Today-vs-History field choices, and validates selected required Info/name before preparing the whole response. PrepareFrame2093 commits only the complete selected response; Send occurs afterwards.
- Page0 is rejected explicitly because the existing one-based offset becomes negative; it is not clamped or advertised as a valid dirty response. Large pages retain a header-only response. Unknown rank remains a consumed query without a response.
- Response failure preserves the post-query packet state. This is not a claim that valid query parsing is unconsumed on later server-state failure, nor a transport-delivery transaction.
- Packet preparation assumes exclusive caller ownership. Public pointer/Size/Dispose changes lack a common lifetime gate. No cross-process, crash or SQL atomicity is claimed.
- Ranking array references and selected row scalar/string values are captured for this response, but public metadata writes have no shared source-wide lock; no globally atomic ranking snapshot is claimed.

## Expected wire behavior and fixtures
- Retain all independent valid Arena ranking/score/NPC reply goldens, successful caller Send queues, fixed marker10, duplicate physical source rows/order and TodayRank filtering.
- Replace null-name dirty-prefix and65536-byte wrapped-string tests with unchanged complete owned target bytes, initial cursor and Size on ordinary refusal. Never execute the old unsafe oversized builder as a red baseline.
- New controls cover null required state, exact/tiny allocation limits, row append complete-segment refusal, malformed/disposed targets and actual seal room. Optional NPC displayfalse/null text and the native name truncation remain positive controls.

## Evidence alignment
| Requirement | Actual source/caller evidence | Status |
| --- | --- | --- |
| Ranking is incremental | ArenaRankingCreate/AddItem/ArenaRankingFinalize and sole two-branch Handler | Confirmed; preserve sequence |
| Null names leak rank/UID/server prefixes | Current builder order and retained neighbors01 failures | Confirmed source defect |
| Oversized displayed NPC text can leave outer prefix | NpcReplyCreate initializes/writes before string-list rejection | Confirmed source defect |
| Safe whole ranking response | Handler writes header before selected Info/name validation | Requires approved caller preparation |
| Current client/transport acceptance | Component fixtures only | Unproved |

## Implementation and qualification
- Shared PrepareFrame/ReadPrepared is consumed from FSB-209 without editing its frozen file. New unframed segment preparation is confined to LegacyNativePacket.PreparedAppend.cs.
- No native/client execution, restore/build/test, process/SQL/live configuration or Git mutation in this helper task. Root reviews and qualifies the combined source.
- FSB-213 ledger fragment and HANDOFF record finite paths, exact qualification and remaining boundaries. Other native builders/readers and full gameplay remain outside this package.
