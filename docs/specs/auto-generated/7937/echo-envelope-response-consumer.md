# 7937 Echo retained-envelope response consumer

## Identity and scope

Source-only proposal against Root's frozen `9060769192709e16ce4fcc9da31764f5b41b63ae`
candidate. Target explicitly declared 7937 opcode 2132. Same-engine retained
parser/dispatch text is derived support, not a matching 7937 capture. The
retained engine identity is SHA256
`a7e1aac88a999b1f4723e6a1f197b96924e24022fd681fb2c9cf79c1bb413422`.
Root alone integrates and qualifies; no helper execution is authorized.

Existing generated specs `echo-response-event-decoder.md` and permanent
`7937-echo2132-extension-retention.md` establish the separate raw response and
opaque-retaining envelope contracts. This joins those existing DATA consumers;
it does not introduce a state owner, receiving handler, activation or cost rule.

## Packet contract and evidence

The existing response maps operation 8 to raw fields 7 then 5 (internal event
0x36), and operation 10 to raw fields 2, 3 then 4 (internal event 0x37).
Provenance: retained `helper-chinese-echo-state-next/response8-and-record-operations-001.txt`,
`helper-chinese-echo-selection-next/operation13-14-shared-001.txt` and
`helper-chinese-7935-consumer-next/operation-dispatch-table-013.json`, as traced
in the existing response spec. Field presence remains distinct from native raw
zero projection. No meaning is assigned to raw argument values or field 10.

Existing `Chinese7937EchoOperationEnvelope` fully validates explicit version,
known-field widths/wires, bounded parent and child9 records, required operation
and complete tails before its private constructor publishes an immutable object.
It retains original payload spellings/order and every opaque extension record.
Known scalar duplicate last-value semantics and child append order stay intact.
Unknown balanced-group contents remain opaque; existing limits count complete
retained extension records, not nested group records.

## Consumer API and ownership

`Chinese7937EchoResponse.TryProjectEnvelope(parsedEnvelope, out response, out failure)`
accepts only the existing sealed parsed envelope. Null refuses `SourceRequired`;
valid operations other than 8/10 refuse existing `UnsupportedOperation`.
The response stores that immutable `SourceEnvelope`, owns an exact payload copy,
and uses the same private raw-argument selector as the unchanged strict APIs.
`CopyPayloadBytes()` and new `ToFrameBytes()` serialize that owned capture,
never the canonical known projection. Full frame output is the established exact
four-byte length/opcode header and original payload, including extensions.

Existing strict `TryParsePayload`/`TryParseFrame` acceptance and failure behavior
remain unchanged; extensions continue to refuse there. Their `SourceEnvelope`
is null. The existing failure enum gains `SourceRequired` only at its end.
No new parser, recursive interpretation, callback, catalog default, registry,
profile, runtime effect, file write or send is added.

## Controls and limits

Independent literal payload and full-frame goldens cover both observed response
operations, interleaved parent/child extensions, duplicate opaque/known records,
nonminimal valid varints, known child order, absent versus present zero and
exact replay. Tests must preserve strict refusals, wrong declared build,
unsupported operation, complete malformed tails, caller-built frame mismatch,
null envelope, defensive bytes/arguments and prior-object preservation.
No real/private rows or game assets enter source. Tests remain authored/unrun.

## Remaining gameplay evidence gap

The actual Main65->67 comparison lacks complete text/column correspondence;
value alignment cannot establish field semantics. Echo request6 is outbound
preparation only. Response8/10 arguments and item2350 fields2/3 still lack a
matching-profile request/reply plus authenticated pre/post item, selection,
activation/cost and persistence-owner relation. No runtime/economy behavior is
authorized by this join. It closes only the explicit extension-safe response
projection/wire replay gap and claims no newly decoded gameplay or live proof.
