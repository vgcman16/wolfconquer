# Legacy DH peer validation

## Identity and scope

- Revision1, 2026-11-03; Sanitized Parity source correction.
- Target: existing managed LegacyGameServer transport, including the established
  VisibleDx8D13 path. This does not create a Chinese7937 profile.
- Preserve established P/G, login encryption, framing, seals, key derivation,
  uppercase unpadded public output and successful coalesced frame handling.
- Reject unusable peer contributions without changing an earlier valid exchange.
  This is transport validation, not account authentication or native admission.

## Packet contract

No opcode or field-layout change. The existing handshake reader extracts the
length-delimited UTF8 public-key text and retains its existing NUL-prefix rule.

| Field | Representation | Validation | Evidence |
| --- | --- | --- | --- |
| Peer public key | Existing hexadecimal text | ASCII hex, nonempty, raw length at most PacketHeader.DefaultMaxFrameSize; significant width no greater than modulus; 1 < Y < p - 1 | Existing reader/cipher and independent arithmetic controls |
| Public request | Existing uppercase hex without padding | Nontrivial and in range before publication | Existing six DH goldens |
| Shared result | Existing unsigned big-endian bytes | Nontrivial and in range before key derivation | Existing derivation golden and new degenerate-result controls |

Accept case and leading zeros within the protocol ceiling. Check significant
width before allocating a BigInteger. Extraction failure or unusable peer/shared
value raises the dedicated LegacyGameServerPeerKeyRejectedException.

## Catalog/table contract

Not applicable: the transport uses existing cipher parameters and no item catalog.
Constructor validation requires odd p>3, 1<g<p-1 and gcd(g,p)=1. This deliberately
does not certify primality, a subgroup, custom-group strength or TLS compliance.

## Validation and transaction rules

- Preserve positive-exponent guards. Prepare public/shared values and output
  spelling before publishing one immutable exchange state.
- Refusal preserves the previous private exponent, public value and shared value.
  Serialize HandleResponse against exchange commits; derive from one snapshot.
- Default generation uses fresh CSPRNG bytes on every attempt, at most128 attempts,
  and clears scratch bytes. Never cache private keys to speed up tests.
- Catch only the dedicated rejection around the server handshake await. Log the
  expected refusal and use existing connection cleanup before game-frame dispatch.
  Unrelated errors retain existing diagnostics. No game resources are involved.

## Expected wire behavior and fixtures

Keep the existing six arithmetic/format/derivation tests unchanged. New component
controls cover invalid inputs, raw/significant bounds, state preservation,
degenerate local/shared results and genuine RNG recovery. The composite15/g2
example tests arithmetic refusal only, not supported-group certification.

Owned TCP controls send actual login-cipher replies for empty/0/1/p-1/p/nonhex
keys. Peer1 is coalesced with a real shared1-derived encrypted/sealed probe.
Require actual server EOF while the client send side stays open, no registered
transport callback, no erroneous PARITY-BUG diagnostic, then a fresh valid RNG
handshake and real probe on the same listener. Cancellation/reset is not EOF.
Keep actual socket/drain ownership aligned through teardown.

## Evidence alignment

| Requirement | Source/fixture | Status |
| --- | --- | --- |
| Existing packet/parameter/KDF contract | LegacyGameServerHandshakeReply, LegacyGameServerCipher and original DH tests | Retained unchanged |
| Elementary peer bounds | [RFC7919 section5.1](https://www.rfc-editor.org/rfc/rfc7919.html#section-5.1) | General validation reference; its TLS known-group authority does not certify this legacy group |
| Prior input flaw | Managed DH accepted peer1 and computed shared1 | Source finding; no original-client/deployed exploit demonstration |
| Rejection/state and listener continuity | New independent component and owned TCP controls | All35 passed in229focused tests |

## Implementation and qualification

- Networking/LegacyGameServerDiffieHellman.cs and .Validation.cs own validation
  and state; LegacyGameServerPeerKeyRejectedException.cs defines the refusal.
- ConquerTcpServer.cs changes only handshake rejection handling.
- LegacyGameServerDiffieHellmanSafetyTests.cs passed29 rows; networking
  LegacyGameServerDhPeerWireTests.cs passed6, alongside existing goldens.
- Reviewed source passed229focused tests; no duplicate host job or checked-tree edit.
- [FSB-338](../../../FIXED-SOURCE-BUGS.md) awaits successor full public CI.
  Full English/7937 gameplay, authentication binding and live acceptance remain
  separate. Source tests do not clear the client execution hold or host trust.
