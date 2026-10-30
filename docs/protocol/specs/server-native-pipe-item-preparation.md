# FSB-225: Whole native pipe item and optional-extra preparation

## Identity and scope
- Revision 1, 2026-10-29; Root source-only assignment. Exact source pointer captured in package DEPENDENCY.json.
- Evidence: native component codecs and independent PipeTransport goldens. Current confirmed live profile remains 7899 / VisibleDx8D13; no new client or patch acceptance claim.
- Only GameItem.Send(PipeClient, Packet), at most two focused plan/publication partials and assigned tests. No GameClient normalization/unlock, dispatcher, pipe lifecycle/connection pump, SQL, original data or live client changes.

## Packet contract
- Genuine captured ItemCreate / WriteCapturedItem opcode2116 followed by optional ItemExtra.CreateArray opcode2134, tointer:true. Retain exact valid fields, fixed names, raw refinement effect IDs, conditional item clocks and seal semantics.
- Direct Pipe.Send has no GameClient 1032-byte cap. Preserve the independent 1386-byte/300-relic item golden; real target capacity and UInt16 framing still bound both complete outputs.
- Both stored extra ItemUIDs zero => do not evaluate unused expiry/dates or send extras. Otherwise evaluate actual copied purification then refinery, with existing type decisions and clock expressions. Retained nonzero expired IDs still produce the actual zero-count extra frame.

## Catalog/table contract
Not applicable. The actual physical item and actual extra structs are the data source. No fake item or owner/metadata/cost is created.

## Validation and transaction rules
1. Null pipe rejects clearly. Dead pipe returns before socket/item/packet metadata. Active null socket rejects. Dead socket returns before unused item/packet metadata. A live pipe must retain its actual socket and the constructor-established socket.Client identity. No optional GameClient Owner/Player/Fake state becomes an admission requirement.
2. Hold the actual item monitor for cooperating item writers. Capture the actual supplied target full-width pointer/cursor, Size and Capacity before preparation; require valid live storage. Capture LegacyNativeItemPacketSnapshot (actual source reference, original relic array identity and immutable scalar/relic values), genuine conditional item timer values and value-copy extras.
3. Prepare complete owned native item and optional-extra frames and managed final-frame bytes before any caller target mutation, actual extra Available/Typ mutation or network queue publication. Actual encoders, captured seal/header validation, all frame capacities and UInt16 bounds apply; no generic 1032-limited wrapper.
4. Revalidate the same actual item and original pipe/socket identity/liveness and target witness before commitment. If a now-dead transport is observed before commit, return with no effects/output. Other invalid/rebound state refuses before all publication.
5. Publish the last actual frame into the caller target once, then commit only prepared actual extra structs using no-throw field assignments. Send owned item frame then optional extra through real Pipe.Send. Native Send synchronously copies when it succeeds; it filters/catches failures and does not acknowledge delivery.
6. Before each postcommit real Pipe.Send, recheck the captured transport binding/liveness; observable postcommit failures use PipeItemPublicationException with ItemStateCommitted=true and the number of completed Send calls (not deliveries). Never fake rollback or turn this into a retryable uncommitted rejection. Native swallowed/filter failures remain delivery unknown.

## Expected wire behavior and fixtures
- Preserve all independent literal item/extra goldens, 1386-byte no-cap positive, raw effect identities, zero-UID expiry bypass, expired empty extra, live timers and absent/Fake owner cases.
- Replace six unsafe fault assertions and three dead-transport outcomes with complete prior target/rawbuffer/queue/actual extra preservation controls, retaining outcome counts.
- Add late-date/capacity/UInt16/seal/null/refused-snapshot repair controls and actual array/transport aliases using owned isolated components. No unsafe old oversized execution or fake callbacks/queues.

## Evidence alignment
| Requirement | Evidence | Bound |
| --- | --- | --- |
| Active socket identity | Pipe constructor sets Socket.Client=this | real pipe/socket binding only; no invented owner admission |
| Exact item/extra fields/order | ItemCreate snapshot encoder, CreateArray(tointer:true), independent PipeTransport fixtures | component wire evidence, not client acceptance |
| Availability/type semantics | real struct InLife/SecoundsLeft on value copies | clocks/date errors retained, mutation delayed |
| Capacity and seal | actual Packet allocation/Finalize, captured header+seal | exclusive lifetime required |
| Delivery | actual Pipe.Send and SecuritySocket.Send | synchronous copy when successful; no delivery receipt |

## Implementation and qualification
- SendPipe.cs, PipeSendPlan.cs and PipeSendPublication.cs; three existing test partials plus one ItemPreparation partial.
- Helper is source-only: no compile/build/restore/test/native/live/process/SQL/Git. Existing assigned files retain 21 outcomes (all six unsafe faults and three dead outcomes corrected); one new partial adds 22 authored unrun outcomes. Positive test regions, including the 1386-byte golden, remain unchanged. Reflection controls invoke genuine private prepare/revalidate/publication/commit/send phases, with real array/constructor changes; they do not prove arbitrary whole-call race scheduling. Root reviews/integrates and owns the serialized gate.
- Exclusive target/frame/pool lifetime and cooperating actual item monitor required. Public item/extra/array/pipe/socket/Client/seal mutations, ABA and transport rebinding cannot be made globally atomic by bounded checks. No SQL/crash transaction or atomic durable queue/delivery claim. Success of a component codec is not full Pipe lifecycle or new-client implementation.
- Ledger fragment FSB225 is for Root to merge with actual qualification.
