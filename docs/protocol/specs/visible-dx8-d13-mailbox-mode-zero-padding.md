# VisibleDx8D13 opcode2264 observed zero trailers

## Identity and scope

Target: Chinese7899 / VisibleDx8D13, incoming game connection after owned login.
Pinned D13 SHA256:
`D13B45BD2681DB7D3D8548EA575D89E0D05613BA00E8F9763DBF3F89030402A5`.
Revision1, 2026-10-28. Wire alignment only; no economic or persistence rule change.

Status: evidence-backed candidate source, unqualified. Root captured current D13
“Claim This Page” pages1/2 and Content from an attachment icon; all were rejected
before claim and saved files remained unchanged. The fixed fields are unchanged:
u16 mode, u32 ID, u32 count, count u64 IDs. This is not a protobuf schema change.

## Expected wire behavior and fixtures

Root-supplied exact decrypted modern frames (4-byte header included):

```text
page7=4D00D80807000100000007000000010000000000000002000000000000000300000000000000040000000000000005000000000000000600000000000000070000000000000000000000000000
content1=1600D808010001000000000000000000000000000000
page2=2500D808070001000000020000000800000000000000090000000000000000000000000000
```

## Packet contract

All offsets below are relative to payload; fields are little-endian.

| Field | Offset | Width | Meaning/bounds | Evidence |
| --- | --- | --- | --- | --- |
| Mode | 0 | u16 | Content1 or ClaimAll7 in captured forms | Three captures |
| Request ID | 2 | u32 | Preserve verbatim; both pages use1 | Three captures |
| Count | 6 | u32 | Exact physical u64 count, bounded before allocation | Captured0,2,7 |
| Mail IDs | 10 | count*u64 | Preserve order; handler validates real owner | Pages1..7 and8,9 |
| Zero trailer | 10+8*count | Mode-specific | Content0+8zero / ClaimAll+7zero | Three captures |

The page frames select IDs1..7 and8,9, with mode7/ID1 and exactly7 zero trailer
bytes. Content mode1/ID1/count0 has exactly8 zero trailer bytes.
Both page requests retain ID1, including page2 whose selected IDs are8/9. Preserve
that request ID verbatim; it is not inferred from the first selected mail.
## Evidence alignment

Observed lengths and request/action correlations are below. Generalizing the
constant ClaimAll trailer across other counts is a deduction from the two
differently sized pages, tested within physical bounds.

Capture provenance is Root's `.artifacts/root-mailbox-wire-next/current-world-receipt.txt` pointer
and its current World trace, retained by Root. Root subsequently exported all3
exact frames to `.artifacts/root-mailbox-wire-next/observed-2264.json`, source
lines417/446/474; that read-only export corroborates the three supplied literals
and77/22/37 frame lengths (73/18/33 payload lengths). It labels its own source
authenticity/profile as caller-reported, not an attestation. This helper does not inspect host
state or claim a new live observation. Prior unknown-schema notes remain history;
these actual bytes resolve only this bounded incoming padding mismatch.

## Validation and transaction rules

Accept unchanged canonical10+8n payloads for the two supported profiles. Add only
VisibleDx8D13 Content(count0)+8 zero bytes and ClaimAll+7 zero bytes. For ClaimAll,
the declared count must exactly account for the bounded item region before the
seven bytes; the observed counts2/7 establish the variable-list format, not an
arbitrary rule restricting future page sizes to those two values. Other modes,
lengths, nonzero trailers, unknown profiles and incoherent envelopes refuse.
Bound count against the already-captured physical payload before any multiply,
allocation or item read. Canonical bytes that happen to include zero-valued IDs
retain canonical semantics; padding is never used to forgive a canonical count
mismatch outside the two explicit D13 forms.

## Implementation and qualification

The no-profile parser remains strict RootD11 behavior. Add a profile overload and
propagate the actual selected profile through the registered handler, Frame's
exclusive-mutation classification, predispatch mailbox debt admission, registered
context admission and after-world-lease invocation revalidation. A parser-only
change that leaves a default-profile recovery gate is incomplete and risks a
shared/exclusive ownership conflict. Do not infer profile from payload length,
wire mode or caller-supplied booleans.

Normalize accepted requests to the existing mode/ID/item command. Outbound
ToFrameBytes acknowledgement encoding remains byte-for-byte canonical; actual
D13 ack acceptance is still unproven and outside this patch. Exact recovery
request identity (including ID, selected IDs/order and mode), ownership/PIN,
registry, trade/capacity, journal and uncertainty/no-retry behavior stay intact.
Padding creates no new grant or replay authority.

Test source must use the captured literals independently of the encoder, retain
default/D11 strictness and canonical acknowledgement goldens, reject nonzero or
wrong-size padding, wrong modes/profiles/count overflow and envelope spoofing,
exercise every meaningful segment split and existing visit budgets, and admit
only the matching padded replay through actual recovery/registry/Frame owners.
Root alone builds/tests/full-gates and rechecks actual client acknowledgement,
saved/live state and replay behavior. No runtime, SQL, power-loss, full parity or
live claim success is asserted by this source package.

## Catalog/table contract

Not applicable: no catalog rows or prices change. The inbound frame contract
comes from controlled client requests rather than item table guesses.
