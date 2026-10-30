# Vendor canonical continuations

Revision 1, 2026-10-30. Approved source-only continuation repair.

## Target and observed source gap
Existing D13/D11 vendor response adapters and owned Store pair publication retain their current wire and economic contracts. Native joined item publication can return distinct canonical buyer/seller records. The private vendor commit currently discards both TryCommitPair receipts, leaving committed publication pinned to the proposals.

## Required behavior
Forward both exact successful Store out-final receipts through the private commit API. After successful commitment, clone the prepared purchase with only UpdatedBuyer/UpdatedSeller replaced by the nonnull actual receipts. Preserve original Buyer/Seller sources, their item proofs, physical identities and all prepared Sends. Existing publication already uses these updated fields for committed current-owner pins and logs. A successful boolean without both receipts is a publication fault; never invent a proposal/UID/value fallback.

Do not alter source/WAL checks, prices, listing removal, costs, packet order, retry/refund policy or frame preparation. Do not recapture graphs after IO or relabel the before proofs as proofs of canonical snapshot identity. Warehouse and separate repair frozen scopes remain unchanged.

## Verification boundary
Existing D13VendorTestHarness, MsgItemOperationVendorTests and booth settlement fixtures use unbound real Store/file owners. The real joined pair test exercises TryCommitPair with LegacyVendorPurchasePersistence and native graphs, but does not exercise this registered vendor buy/listing/notification continuation. Genuine bound vendor handler coverage remains a qualification gap; do not fabricate native owners to hide it. Root alone runs/integrates meaningful checks; no helper execution or Git actions.
