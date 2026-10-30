# FSB-224: Bound native send storage and prepare before queue/cipher publication

## Identity and scope
- Revision1,2026-10-29; Root-authorized offline source repair.
- Base91040398 plus captured Root overlays in DEPENDENCY. Existing native components; no new7937/client/transport activation.
- Production: GameClient packet/byte send, SecuritySocket.Send, focused Packet.SendBounds and the actual Packet.Dispose gate. The current byte-array constructor already allocates max(1024,buffer.Length) and copies with Marshal.Copy; preserve it. The old1025..1032 warning test is stale evidence, not a current constructor defect.

## Packet contract
- Preserve actual GameClient1032-byte admission frontier. Huge messages are warning/refusal, not automatic cap raises. Direct SecuritySocket/Pipe paths currently have no1032 filter; bound their actual storage without inventing a new delivery rule.
- Preserve plaintext bytes and actual TQCast5 encryption sequencing across successful queued entries. No guessed header/opcode validation or new format.
- Byte-send path keeps zero-header UInt16 LE(length-8) and trailing literal TQServer transformation. The established four-byte header plus eight-byte trailer require length>=12; reject shorter arrays before any mutation. A nonzero declared length must equal actual length-8. This fixes source-confirmed header/trailer overlap and inconsistent length, without guessing an opcode or new schema. Length<=1032 already bounds the UInt16 representation.
- Packet send copies from allocation offset0, independent of current stream cursor. Require live owned storage and Size in0..Capacity. Size0/short raw component buffers are not given a new framing rule.

## Catalog/table contract
Not applicable; no catalog or gameplay metadata involved.

## Validation and transaction rules
- Keep GameClient Fake/dead/null filter order. Validate and snapshot native storage before copying/encrypting. Oversize warnings read only a bounded diagnostic prefix, not unvalidated Size bytes.
- Add an actual private per-packet send lifetime gate shared by CaptureSendBytes/diagnostic inspection and Dispose. It protects these captures against actual Dispose freeing storage during their copy. No fallback owner/gate is created for uninitialized objects. Other public raw pointers, writers and foreign free/mutation remain outside this guarantee.
- SecuritySocket keeps its actual existing locckss gate. Resolve the real queue, bound/capture an owned managed plaintext copy and allocate/reserve actual output/queue capacity before advancing the real cipher. Missing queue, invalid packet/count or other preparation refusal must leave cipher and queue unchanged.
- Real internal TrySend returns Refused, Queued or CipherStateIndeterminate; public void Send remains. Queued means actual native queue admission, never connection/send-pump/network delivery.
- If Encrypt unexpectedly throws after possible partial cipher progress, return typed CipherStateIndeterminate and permanently fence further sends on that socket. Repairing/replacing its public Crypto cannot clear the fence. No rollback or safe retry is claimed; recovery requires a new actual connection/socket owner. Crypto implementation is unchanged. Do not trigger the broad Disconnect/OS lifecycle as a hidden repair.
- Byte-send prepares a managed clone and temporary actual properly sized Packet. Commit the caller transformation only after Queued; dispose that owned Packet on all outcomes. Ordinary refusal leaves the caller array unchanged. No caller-array concurrency guarantee is added.
- Do not catch AccessViolationException or execute unsafe old-native ranges. Valid owned copies and checked managed exceptions are separate controls.
- Socket public Alive/Crypto changes and Disconnect do not share locckss, and other raw Packet pointer/writer changes do not share the send storage gate. This does not claim whole-connection lifecycle or whole-Packet concurrency. Caller buffers must be exclusively owned during byte preparation/commit.

## Expected wire behavior and fixtures
- Retain independent valid byte transforms,1033 refusal, Fake/dead/null behavior and genuine two-packet cipher continuation goldens. Correct old8..10 overlap tests to refusal invariants while retaining all3 cases; the2 header-positive cases now use zero versus a correct existing0C00 header. Add11/12 boundary and inconsistent nonzero declared-length cases.
- Correct tests that required short/deep-constructor/missing-queue caller mutation or missing-queue cipher advancement. Keep their outcomes and real queue/owner checks.
- Add corrected-only actual1025/1032 allocation/copy/disposal proofs, negative/over-Capacity/disposed/zero Size and oversize GameClient controls, direct socket bounded-copy/cipher preservation and Dispose/capture cooperation where practicable.
- Unexpected Encrypt control uses only a genuine initialized cipher's managed state: EncNum[0]=6 and an actual7-byte managed EncIvec. The first byte legally changes IV[6]/counter; the second checked IV[7] access throws before any EncryptBlock call. Plaintext is an owned pinned array>=2 bytes; no native pointer corruption, mock override or unsafe crash baseline.

## Evidence alignment
| Requirement | Actual current source/test | Status |
| --- | --- | --- |
| Constructor capacity | max(1024,buffer.Length), Marshal.Copy | Already correct; retain and test actual1025..1032 |
| Unbounded native send | GameClient huge diagnostic and SecuritySocket copy/encrypt use public Size without Capacity/disposal | Confirmed defect |
| Rejected byte transformation | Caller mutated before short/constructor/queue failure | Confirmed defect |
| Byte header/trailer overlap and inconsistent length | Old8..10 accepted tests overwrite header; old3412 header on20-byte input disagrees with actual12-byte declared frame | Confirmed defect; reject before mutation |
| Missing queue consumes cipher | Existing tests explicitly require continuation after failed queue | Confirmed defect |
| Unexpected crypto rollback | No cipher transaction/rollback API | Do not invent; typed indeterminate fence |
| Network delivery/broader frontier | No real Create/pump/new client evidence | Unproved |

## Implementation and qualification
- Root alone reviews/integrates/builds/tests; helper source only. No build/test/dotnet/native/process/live/SQL/Git execution or actual checkout changes.
- Two corrected-only Safety partials author20 unrun outcomes; the finite existing two test files retain their outcome counts. New header11/12 and inconsistent nonzero cases supplement corrected overlap/refusal controls.
- Real owned component queues in tests are explicit prestate, not fabricated native Create/transport completion. Positive cipher goldens remain separate from malformed managed-state controls.
- This does not implement broader1032 delivery, host registration, whole native packet concurrency or durable gameplay/SQL transactions. Qualification remains pending.
