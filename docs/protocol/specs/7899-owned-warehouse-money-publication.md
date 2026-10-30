# 7899-owned-warehouse-money-publication: owned warehouse gold

## Identity and scope
- Revision 1, 2026-10-30; source-only warehouse-gold milestone, FSB-238.
- Target: Chinese 7899 `VisibleDx8D13`, using the existing managed request and
  response fixtures. This does not qualify 7937 or a current live client.
- Extend the actual Store-issued admission/publication over native `Player.WHMoney`.
  Preserve its public signed-64 field shape and all successful packet sequences.
- No global Character parser/currency-cap change; no fabricated login queue,
  callback, saver, SQL owner or packet sender. No deployment or host execution.

## Packet contract
- No new packet, field or output. Existing MsgItemUsage 1009 action 9 shows
  warehouse gold, action 10 deposits, action 11 withdraws.
- Existing successful deposit: Money update, Money update, original request ACK.
- Existing successful withdrawal: Money update, Money update, WarehouseMoney
  update, original request ACK. WarehouseMoney is MsgUpdate field 9.
- Amount and response widths/order remain those of the existing packet classes.
  Native physical signed-64 representation is not a newly inferred wire cap.

| Field/path | Number/offset | Wire type/width | Meaning | Presence/repetition | Bounds/default | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| Player.WHMoney | Not a wire field | public Int64 | actual native warehouse-gold slot | required participant | 0..Int64.MaxValue | LegacyNativePlayerRuntime.Money.cs |
| Character.Attributes.WHMoney | INI key | invariant signed-64 text | canonical saved gold | optional legacy key | absent with absent alias means zero | LegacyNpcSalePersistence.Disk.cs; LegacyFloorPickupWallet.cs |
| Character.Attributes.WarehouseMoney | INI alias | invariant signed-64 text | saved alias | optional | if both exist, equal values | same strict readers |
| MsgUpdate WarehouseMoney | 9 | existing UInt64 scalar encoding | response display | existing withdraw sequence | existing packet contract | LegacyMsgUpdatePacket; FrameHandler warehouse goldens |

## Catalog/table contract
- No new binary catalog/table lookup. Existing Character attributes are read
  case-insensitively, with duplicate case-folded keys and null values rejected.
- A present malformed/negative/out-of-native-range alias or unequal pair refuses;
  never silently choose one or normalize corrupt/legitimate wider global data.
- Absence-zero is supported only by the established wallet format: both strict
  readers return zero when both keys are missing. Disk item-removal preserve mode
  explicitly retains that absence. Publication alone does not add or rewrite keys.

## Validation and transaction rules
- The closed participant can be constructed only through actual admission or
  current bound Store paths, under the real connection gate then client/player
  actor gates. It owns no persistence callback or output route.
- Admission requires actual prepared prefix/UID/current native owner. Bound
  publication requires exact current holder/binding/identity and saved warehouse
  value equal to actual native before value.
- Parse expected/proposed alias state during preparation. Complete all
  callback-bearing preparation before pure before-writer and final field proofs.
- Native alias proofs admit actual BCL Dictionary enumeration, including an exact
  ReadOnlyDictionary wrapper that yields its real BCL Dictionary enumerator.
  Other callback-bearing map shapes refuse native publication. Capture raw rows
  and compare them without fresh wrapper calls in current/final proofs, including
  value replacement that does not increment the Dictionary version.
- Capture actual field and attachment; check them after catalog/map callbacks,
  before a real writer, after it and before publication. Direct Volatile field
  writes publish the complete prepared value with no setter side effects.
- Deferred mail must validate the proposed warehouse format before the actual
  producer reserves UIDs, then prepare again over final materialized session.
- A prewriter mismatch refuses without files/resources/UID intents/output. A
  mismatch after a genuine durable writer retires the actual owner and blocks
  recovery; it must not refund, publish success or repeat the committed operation.
- Public WHMoney compatibility remains: noncooperating raw field writers can
  race after a final proof. This participant detects observed drift, not arbitrary
  concurrent writes or a global transaction across all server systems.

## Expected wire behavior and fixtures
- Admission publishes saved warehouse gold before the first existing reply.
- Real repair, floor pickup and combined mail use their existing persistence
  writers and first-reply/replay checks. No accepting writer stand-ins.
- Cover canonical-only, alias-only, coherent pair, absent-both, long maximum,
  invalid/negative/overflow/ambiguous aliases, real callback drift, prewriter drift
  and post-real-writer retirement. Compare successful bank/pickup packet sequences.
- Bank source audit found loose alias fallback, canonical-only update and a
  generic Store Set that saved scalar state after publication on a best-effort
  basis. Replace the routed/native bank path with the actual single-owner
  Character + unchanged Items v2 WAL writer and actual Store commit. Validate
  current PIN/trade/escrow/connection ownership under the actual Exclusive,
  without retaining a Frame Shared lease. Refuse missing real roots/owners.
- Prepare all bank response bytes before the WAL. Validate exact decimal wallet
  conservation after the existing inventory cap so a wider intermediate aggregate
  cannot reject a valid signed-64 final result. Amount must be positive; retain
  the established strict deposit rule Money > amount. Existing aliases update
  coherently. Release operation ownership before sending committed replies.
- Interrupted/uncertain WAL work blocks recovery and emits no success. Actual
  fresh-owner reconciliation must apply the intended transfer once. Repeating a
  completed request is checked against current funds; this is not request-ID
  deduplication. Retain only the existing entirely in-memory/no-native/no-routes
  compatibility path, explicitly without a durability claim.

## Evidence alignment
| Requirement | Spec clause | Decoded catalog reference | Bounded wire fixture | Status/conflict |
| --- | --- | --- | --- | --- |
| actual public Int64 field | identity | not applicable | no new output | observed source |
| nonnegative coherent aliases; absence-zero | table | strict wallet/Disk readers | owned saved fixtures | observed source |
| deposit/withdraw ordering | packet | not applicable | existing FrameHandler tests | source golden, not live |
| mail before-UID validation | transaction | actual delivery catalog | combined owned fixtures | authored validation required |
| bank real single-owner persistence | transaction | exact saved Character/Items images | owned first-reply/interruption fixtures | focused source verified; no SQL/global durability claim |

## Implementation and qualification
- Implementation: `LegacyWarehouseMoneyState`, native OwnedWarehouseMoney partial,
  actual Store admission/bound/deferred/pre-reservation publication hooks and
  scoped bank/alias handling and actual v2 WAL bank writer. Preserve unrelated
  Money/CP overlay improvements.
- Authored tests: owned admission/repair, combined mail and real floor pickup /
  bank partials in `tests/WolfConquer.Tests`.
- Root integrated and compiled this source with zero errors. The combined
  currency/CI batch passed387/387 focused outcomes, zero failures/skips. Its
  actual public-commit full Windows gate remains pending; the earlier a17 gate
  failed and its exact receipts/outputs remain retained.
- No current-client proof, host-cleanliness proof, SQL durability, complete parity
  or complete warehouse/economy transaction claim follows from this source change.
