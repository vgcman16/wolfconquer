# SQL-authenticated ordinary/native trade roundtrip

## Identity and scope

Revision1, 2026-11-03. This is an owned offline regression and server-safety
milestone using the existing modern SQL account repository and VisibleDx8D13
Runtime/Frame. It does not activate a Chinese7937 profile. Characters/items remain
INI/BIN, not SQL. Use fresh disposable local MariaDB/schema fixtures only.

## Packet and catalog contracts

Trade opcode2119 retains its six protobuf fields: UInt64 Action1, signed Money2,
UInt64 ItemUID3, UInt32 CPS4, opaque UInt64 field5 and UInt64 UID6. Preserve the
strict codec, captured type-only form, full native serialization and successful
request0/accept1/item6/Money7/confirm9/CP12 sequences. Undefined high32 action bits
refuse before state access. Four final frames per recipient retain actual order.

The required stack-cap owner is constructor-captured from actual server66 item
rows and column46, with the existing accessory15/16 fixed-one override. Both raw
and indexed ID rows must agree; duplicate effective caps must be unambiguous.
Native stack0/1 remains one unit. This is not a mapping of Chinese7937's67 fields.
Each actual offered closure must satisfy the immutable cap owner and permissions,
retain it with the issued item lineage, and revalidate before durable intent.

## Validation and transaction rules

Require real current SQL account ID, ordinary/native admission, actual reciprocal
handler-owned trade pair, source inventory closure, capacity, funds, both native
plans and saved-file baselines. Suspicious roots/descendants refuse independently
of the genuine directed locked-partner exception. Both-unbound compatibility is
proved by the actual Store; it cannot authorize a native/mixed pair.

Reuse the existing real escrow and four-target Character/Items v1 WAL; a swap
does not allocate a UID. A recorded absent Items BEFORE means that actual empty
graph only. Require complete physical UID conservation/high-water and reject
unrelated physical owners even when a pair path is absent from the scan. Keep
strict Character images and historical durable producer permissions at recovery.
No accepting saver/callback, synthetic live owner or active-boolean substitution.

## Expected behavior and fixtures

Two synthetic SQL principals authenticate through the real account Frame with
positive SQL IDs different from EntityID. Native actors swap their existing item
UIDs plus10/20 Money and1/2 CP. Before every actual escrow/final pre-socket output,
assert both file/native/holder states; assert exact final frame order, full rows,
boundness, unrelated rows, registry and SQL rows. Cold SQL reauth/native reopen and
repeated confirmation must not duplicate transfers or payouts.

Negative controls cover wrong/ambiguous/missing principals, provider containment,
actual second Character/Items exclusive leases and same-pair retry, invalid funds,
locked/bound/suspicious offers and full-width action aliases. Genuine EOF refunds
escrow without moving items. Actual interrupted v1 intent recovers before both
cold admissions. Separate missing-Items controls exercise replacement0..4 for
settlement/refund and unrelated physical UID collision.

The opted-in SQL lifecycle must discover all source-declared Facts and require
their exact unique Passed TRX rows; stale assemblies, missing tests and skips
cannot qualify. The current composition has5existing plus9new Facts. No count is
hardcoded into its acceptance mechanism.

## Evidence alignment and limits

Source owners: WorldPacketHandlers.Trade.*, WorldSessionStore.TradeItemLineage,
PersistedTransactions.TryCommitPair, LegacyVendorPurchasePersistence and
LegacyTradeJournal.OwnedStartup*. ModernSqlAuthFileRoundtripTests joins these
actual implementations rather than supplying replacement behavior.

The prior actual SQL5 pass proves only its documented auth/native sale scope.
This trade batch is source-reviewed, unbuilt and unrun. Require focused managed,
real owned SQL and appropriate full qualification. Legacy ID forwarding is not
separate ticket redemption. No power-loss, production database, strong legacy
authentication, current7937 gameplay, English rendering or live-client proof is
claimed. Preserve original sessions/saves/routes/restore ZIP and the client hold.
