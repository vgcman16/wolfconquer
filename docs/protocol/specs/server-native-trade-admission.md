# server-native-trade-admission: truthful queries and an unbound currency fence

## Identity and scope
- Revision1, 2026-10-29; sanitized-parity correction, reserve FSB-204.
- Clean committed public generated910403989ca8326f3bf4fa1f8c22c99e3b791634, source731d8e54e435a6411361869a8a361f6731634705. No dirty checkout input.
- Production changes are confined to LegacyNativeTradeRuntime.State.cs, Queries.cs and AddMoney.cs. Native MsgTrade remains HELD/unregistered. No managed World trade or packet codec edits.
- No new target profile is selected. The current live boundary remains Chinese7899/VisibleDx8D13; packet evidence here is the native2119 source contract and independent goldens. Compatibility with7937 remains unproved. This adds no client-confirmed compatibility claim. Community decompile establishes the mismatched server query, not new client authority or native exchange activation.

## Packet and item contract
- Preserve all six Trade protobuf fields, required tags, opcode2119, signed Money encoding and successful small-packet goldens. Encoding a valid message remains independently testable; it does not admit a trade.
- Original MrRayzo.Game.MsgServer/MsgTrade.cs resolves requested ItemUID through Inventory.TryGetItem and passes that actual item to Trade.AddItem. Original MrRayzo.Role.Instance/Trade.cs inserts Items.TryAdd(DataItem.UID, DataItem); settlement consumes those values. Inventory resolves and inserts by UID. ItemInTrade's ITEM_ID lookup contradicts those insertion/consumer paths.
- Current and bounded original source have no ItemInTrade caller beyond its definition. Distinguishing malformed state from valid absence is therefore a defensive query contract, not a claimed observed permission caller.
- No catalog, tradability, binding, account authorization, capacity or UID allocation rule is invented by these queries. Those remain consuming-owner requirements.

## Ownership and read-only query rules
- Reject null construction before publishing state. The real owner and escrow collection references are readonly; construction does not allocate an inventory or bind MyTrade.
- ItemInTrade answers UID membership, independently of item type. Reject null/zero-UID input explicitly. A different object with the same offered UID still answers present, which prevents an alias from masquerading as a second identity.
- Canonical offer rows have positive UID keys equal to the physical row UID. Null, rekeyed, duplicate-identity or changing rows are invalid. ItemInTrade throws InvalidOperationException for a malformed offer rather than returning permission-like false. False denotes valid UID absence only.
- ValidItems returns false for missing/foreign Player or Inventory ownership, missing inventory map, noncanonical offer/inventory rows, absent UID or a different inventory object under the same UID. An empty offer is valid only with a valid real inventory owner. Two distinct UIDs of the same item type remain valid.
- Capture real collection snapshots and revalidate numeric keys, physical row references, UIDs and owner bindings before returning a positive result. Do not mutate items, allocate substitute owners, normalize identities or rely on arbitrary source-map comparer callbacks. This is a bounded observation of mutable native objects; raw changes after return or unobserved ABA are not a transaction witness or authority to mutate resources.

## Currency admission and missing owner
- The native trade object has no durable escrow identity, persisted wallet claim, character/item pair journal, recovery owner or coordinator binding. Reciprocal-looking MyTrade/Target/window fields and a socket are insufficient authority.
- The separate managed World exchange first prepares checked wallet/claim state, commits the real session pair and persists the claim with the Character debit. Settlement/refund consume that claim through the owned Character/Items pair persistence journal and recovery path. Native Trade has no genuine connection to those owners. Matching UIDs must not invent one.
- AddMoney keeps its signature and validates null request/packet arguments, then explicitly throws InvalidOperationException for the unbound native path. Positive, zero, negative and extreme amounts all refuse before wallet access/setters, escrow arithmetic, confirmations, request Action, packet buffer/cursor or sends. No positive partial debit or unchecked arithmetic remains.
- Full native exchange activation stays OPEN until a real native escrow/persistence/recovery owner is designed and connected. A local pair lock, test callback, synthetic transport or accepting binder cannot close this gap. No SQL, crash-safe or cross-process atomicity claim is made.

## Authored controls and qualification boundary
- Replace tests requiring null owners, item-type keys, substituted inventory objects, negative-credit acceptance and debit-before-packet failure with safe invariants.
- Retain genuine live-object positives for construction, valid UID membership, same-type/different-UID ownership and empty owned inventory; keep the real2119 codec golden independent of money admission.
- Currency controls use actual native clients with no accepting socket and actual packet memory. Preserve both wallets/warehouse money, escrow, confirmations/windows/pair references, item rows, all request fields and the entire packet allocation/cursor/size on refusal. Keep the original negative-offer counterexample, a positive offer against a populated reciprocal-looking pair, and null request/packet controls. The unconditional fence implements no arithmetic or pair admission rules; multiplying input cases would not qualify such unimplemented rules.
- Source and authored tests only while Root's full Windows gate owns the lane. No build/test/restore was run by this helper. Root must build and qualify the focused native trade/lifecycle and neighboring managed trade selections after integration; existing managed persistence/refund positives and packet contracts must remain green.
