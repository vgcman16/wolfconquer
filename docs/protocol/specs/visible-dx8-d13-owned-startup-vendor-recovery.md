# Owned startup recovery of existing v1 booth settlement intent

## Identity and scope
- Revision1, 2026-10-30; Root requirement, derived from the actual existing vendor Frame/Store producer. Successor of the frozen owned trade-pair candidate; the full English server/client goal remains active.
- Target profile `VisibleDx8D13`; no wire, catalog or WAL schema change. Existing v1 Version/TransactionId/Changes records remain unchanged.
- Recover genuine complete booth purchase intent under existing configured namespace custody, without allocating physical UIDs or consuming escrow. Preserve trade/v2 behavior and ordinary post-recovery scan/same-lease transfer.

## Packet contract
No packet changes. Genuine fixtures use actual StartVendor, AddVendingItemGold/AddVendingItemConquerPoints and BuyVendingItem Frame handlers and actual listing/catalog owners. Existing live validation remains the only authority for new purchases.

## Catalog/table contract
- `WorldPacketHandlers.MsgItemOperation.Vendor.PurchasePreparation.cs` resolves the actual visible seller/booth, typed current owner, listing, item graph and currency/amount; actual planner and commit validators enforce funds, capacity, tradability, PIN where applicable, no active trade/escrow, exact listing removal and detached item closure.
- `LegacyVendorPurchasePersistence.Journal.cs` writes Character(buyer account), Character(seller account), Items(buyer Character UID), Items(seller Character UID) using the same strict v1 record shape as Trade.
- V1 contains no listing, amount, currency, producer-kind or authentication metadata. Exact captured wallet deltas derive settlement currency/amount; they do not independently attest original listing price, original producer authenticity or mutable listing permission. Root explicitly retains the trusted configured recorded-intent boundary instead of inventing a new tag/schema.

## Validation and transaction rules
- Retain existing lease/domain/state/archive/configuration custody; complete nonnull before/after images; actual embedded UID mapping in producer order; exact pending/current-target/stage proof at every existing write boundary; full raw UID set equality/uniqueness and actual pinned high water.
- Vendor direction is fixed: first participant is buyer, second seller. Exactly one unchanged complete inventory root closure moves seller->buyer; buyer retains its prior roots, seller retains all unlisted roots and both retain every noninventory root. Deposits and perfection data remain byte exact. Buyer BEFORE inventory count must be below40; outgoing state cannot manufacture space.
- Moved root/deposits satisfy actual vendor static restrictions: unbound, uninscribed, unlocked and not exact prohibited item types. Original mutable booth visibility/listing/PIN checks belong to the live producer before durable intent, not startup reauthorization.
- Strict canonical Money/CP/claim parsing applies to every image. All before/after claims are absent/cleared, with no consumption of an active claim. Gold settlement debits buyer and credits seller by the same nonnegative signed64 amount, keeping CP unchanged; CP settlement does the same within uint32 while Money stays unchanged. Reject reverse payment, mixed currencies, mismatched debit/credit, negative amounts or overflow. Zero price leaves both currencies unchanged; its original currency is inherently ambiguous in v1.
- Character afterimages equal the actual vendor wallet-only projection, preserving missing/cleared claim keys and every unrelated line. Reuse existing optional warehouse currency alias validation; do not broaden global parsing.
- Claim-free trade gifts may be observationally identical to zero-price vendor intent. Preserve valid existing trade behavior; do not claim producer discrimination where v1 has no tag. Derived deltas are recovery validation only and never fresh purchase/listing authority.
- Only complete conserved v1 pairs use the closed captured logical afterimage scan. Unrelated namespace files/stages/aliases and ordinary full rescan remain unchanged. No generic pending bypass or new saver callback is introduced.
- Add only a closed internal VendorJournalInterruption value through Frame/context/real vendor persistence, supporting actual replacement0..4 and flush-complete stages1..4. It supplies no paths, images, callback or successful result.

## Expected wire behavior and fixtures
- Real owned Runtime/native Frame/Store, real booth/listing/catalog and genuine purchase producer for Gold, CP and actual zero-price modes. Assert exact WAL-derived afterimages, root/deposit/perfection ownership and unchanged UID registry/union, real native projections and second restart without replay.
- Interrupt every replacement prefix and actual flushed stage; prefix3 ordinarily duplicates the delivered UID physically and requires the closed pair view.
- Negative mutations of genuine retained intent only: reverse/mixed/mismatched currency, escrow, unrelated Character state, multiple/wrong-direction/altered/forbidden closures, insufficient before-capacity, UID birth/loss/duplicate/high-water, unsupported/malformed record, foreign/tampered stages, unknown namespace and custody refusal.
- Live negative controls still reject missing listing, stale/invalid owners, funds/capacity and forbidden items through existing actual handlers; do not use recovery-derived deltas to request a new sale.

## Evidence alignment
| Requirement | Evidence | Status |
| --- | --- | --- |
| Actual listing price/currency validation | Vendor PurchasePreparation, CommitPlan and Commit validators | Live source contract retained |
| Recorded settlement projection | Shared existing v1 producer and four images | Derived amount/currency only; listing/authenticity not attested |
| Physical item conservation | SaveGraph/raw catalog + exact closure and producer projections | Closed source validation |
| Startup ownership/readiness | Existing OwnedStartupCustody, captured journal fences, same lease and postscan | Preserved prerequisites |
| Managed runtime recovery |90 genuine producer cases passed | Bounded owned fixture evidence |
| Live client/full SQL | No client/SQL execution | Unproven |

## Implementation and qualification
- Root integrated this successor with the recorded trade and warehouse corrections. All90 new genuine Runtime/Frame/Store vendor cases passed, covering replacement/stage boundaries, complete item closures, fresh admission/restart and new-action refusals.
- Wider qualification retained737 passed/two authored test-golden failures; corrected13-case rerun passed, including both failed cases. Builds passed with zero errors.
- FSB-241 records the bounded fix. The exact public-commit full Windows gate remains required. Process-restart fixtures do not prove live-client, power-loss, multiprocess/SQL/global economy, host cleanliness, deployment or full parity.
