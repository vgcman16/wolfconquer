# Owned startup recovery of complete v1 trade pairs

## Identity and scope
- Revision 1, 2026-10-30; derived from the actual checked-in trade producer and owned startup lifecycle, for the active full English server/client objective.
- Target profile: `VisibleDx8D13`. This change adds no client packet or catalog behavior.
- Successor scope: [owned vendor settlement recovery](visible-dx8-d13-owned-startup-vendor-recovery.md) adds the exact claim-free buyer/seller projection under the same trusted recorded-intent boundary. The vendor exclusion below describes this original trade-only revision, not the combined successor implementation.
- Source base: public `e0f22c8b0a23a67cbd2e2d2f78344610c8ad6f0c`.
- Recover a genuine complete two-player Character/Items v1 trade journal while the configured startup holds the existing exclusive physical-UID namespace lease. Preserve the existing v2 single-owner path.
- Exclude missing item baselines, UID birth/loss, arbitrary v1 currency changes, retained producer work, other journal kinds/versions, SQL, power-loss durability, deployment and live client acceptance.

## Packet contract
Not applicable: recovery consumes the existing private file WAL. Trade wire IDs, fields and successful sequences are unchanged. Regression fixtures explicitly select `VisibleDx8D13`.

## Catalog/table contract
- `LegacyVendorPurchasePersistence.Journal.TryPersistJournal` writes v1 in order Character(account A), Character(account B), Items(character UID A), Items(character UID B). Account filename and Character UID are distinct identities.
- `LegacyTradeJournal.Format` permits only Version/TransactionId/Changes for v1, a nonempty N-format GUID and exactly four unique typed targets. Each change has Kind/FileId/Before/After; paths derive from configured roots and numeric identities.
- Startup requires all four before and after images and the actual producer's Character/Character/Items/Items order. Strict single Character sections/fields establish an unchanged positive uint UID for each account, bijectively mapped to the corresponding distinct Items FileId; account FileIds remain positive long identities.
- Both complete raw pair views must parse without duplicate physical UIDs, malformed deposits/perfection references or hidden trailing bytes. Their global physical UID sets must be identical and each UID must be at most the actual pinned registry high water.

## Validation and transaction rules
- Keep the actual owner.lock acquisition, expected domain/evidence/floor, exact state/archive/configuration pin, retained producer refusal and same-lease transfer in `LegacyItemUidRegistry.OwnedStartup*`.
- Validate every current target against exactly its captured before or after bytes before scanning and at each existing recovery write boundary. Keep exact pending path/bytes and deterministic stage bounded-prefix validation; reject every unrelated stage/alias/reparse entry.
- Strict canonical wallet/escrow parsing uses the actual trade escrow parser. Before claims, when present on both participants, share one TradeId. After claims are cleared. Money/CP afterimages must match either exchange (each receives the other's durable claim) or refund (each receives its own durable claim), with checked arithmetic and existing bounds. Claim-free item exchange leaves both wallets unchanged. V1 also serves a vendor producer; arbitrary claim-free vendor currency transfer remains fenced by this bounded trade extension.
- Missing claims contribute zero escrow; item-only barter/gifts and one-sided currency offers remain supported. Character afterimages must equal the actual producer's wallet/cleared-claim projection, preserving every unrelated line. Every Items image must roundtrip losslessly through the actual save graph. Preserve complete item roots, deposited children and perfection data; move unchanged inventory closures only between the two mapped owners. Refund-only arithmetic cannot move items.
- Each v1 Character image reuses `LegacyWarehouseMoneyState.TryRead`: optional missing warehouse amounts retain existing behavior, while present WHMoney/WarehouseMoney fields must parse as nonnegative signed64 and agree. This scoped recovery check is not a global parser policy. PIN admission and other native/login prerequisites remain owned by their existing lifecycle gates; this batch does not certify every unrelated native prerequisite.
- When any closure moves, each participant's BEFORE inventory root count plus incoming roots must be at most the actual trade producer's40slots; outgoing roots do not create incoming capacity. Moved roots/deposits must satisfy offline-provable static tradability (unbound, uninscribed, permitted item type).
- The real live producer validates mutable partner relations before publishing durable intent, including relationtype3 for locked item closures. Owned recovery completes that exact trusted configured-journal intent; it preserves every Locked byte and does not reconstruct or re-authorize mutable relationship state. Removing the relation after publication cannot strand the recorded swap. New locked trade actions still require the actual live relation. V1 has no producer authentication metadata for either locked or unlocked actions; configured journal custody is a trusted intent boundary, not cryptographic producer provenance.
- A partial item replacement may physically duplicate a transferred UID. Only for v1, the raw namespace scan validates physical target bytes then reads the exact captured afterimages for both known target paths as one logical pair. Charge substituted image bytes to existing bounds; every unrelated Items/mail/detained input is read and checked normally. Exact pair UID conservation makes the logical scan cover every captured before UID too. V2 keeps its original physical scan, including retiring-UID duplicate refusal.
- Recovery retains ordered complete snapshot file locks then the journal domain gate. The exact captured owner permission reaches existing mutation fences only for its captured paths/images. After roll-forward, clear only captured deterministic stages, delete the exact pending record and perform the existing ordinary raw namespace rescan before readiness.
- Generic registry open/bootstrap still refuse pending journals. No producer intent, fake saver, callback, general allow-pending flag or alternate lease is introduced.
- Closed test interruption controls flow through the genuine Frame/Store/trade producer: replacements0..4 and actual flush-complete stages1..4 for v1; existing v2 stages1..2 stay valid. Controls supply no paths, bytes, saver or successful result.

## Expected wire behavior and fixtures
- Real owned Runtime/Frame/Store fixtures create trade offers, actual persisted Money/CP escrow and exchange/refund, then stop the real WAL at every prefix and real flushed-stage boundary.
- Genuine locked roots/deposits/combined closures use actual parsed relationtype3 SocialAssociations before the live offer; clear that actual relation after durable intent, then recover and admit with a relation-free fresh Frame. Separate actual newly requested locked offers without a relation remain rejected and cancellation refunds real escrow.
- Assert exact captured afterimages, unchanged registry/high water, UID conservation, both ownership mappings and restart without replay. Prefix3 specifically contains the physical transfer duplicate before recovery.
- Refuse unknown/new/duplicate UID, misbound identity, malformed or changed captured evidence, foreign/tampered stages, retained intent/archive debt, wrong roots/domain/evidence and unavailable actual lease. Rejection preserves all evidence and target bytes.
- Managed Program child companions cover raw/encrypted admission and second restart, subject to the root's serialized qualification gate.

## Evidence alignment
| Requirement | Source evidence | Status |
| --- | --- | --- |
| Account to Character UID mapping | `LegacyVendorPurchasePersistence.cs` paths and `.Journal.cs` v1 producer | Observed source contract |
| Currency and escrow settlement | `WorldPacketHandlers.Trade.Exchange.cs`, `.Lifecycle.cs`, `LegacyTradeEscrowClaims*` | Observed exchange/refund arithmetic |
| Existing custody and fences | `LegacyItemUidRegistry.OwnedStartup*`, `LegacyTradeJournal.RecoveryCapture.cs`, mutation fences | Preserved source prerequisites |
| Whole namespace uniqueness | `LegacyItemUidRawCatalog*` complete raw parser | Closed v1 logical pair scan; ordinary postscan retained |
| Client acceptance | No current client execution; actual handoff hold remains | Unresolved; no live claim |

## Implementation and qualification
- Root integrated the real capture, image validation, logical pair scan and genuine producer controls. Existing v2 guards and protocol goldens remain.
- Actual focused qualification passed all97 owned trade cases and4 new managed Program-host raw/encrypted restart cases, including locked intent, external-store refusal and second restart without replay.
- The wider run retained737 passed/two authored test-opcode failures; the corrected13-case rerun passed, including both failed cases. Solution and corrected test builds passed with zero errors.
- The exact public-commit full Windows gate remains required. FSB-240 records the bounded source fix; no live-client, SQL/global economy, multiprocess, power-loss or full-parity claim.
