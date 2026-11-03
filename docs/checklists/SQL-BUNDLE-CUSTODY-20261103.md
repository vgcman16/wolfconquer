# Retained SQL bundle ownership qualification

The final reviewed single/pair batch passed **59 actual provider Facts,0 failed,
0 skipped**, on owned MariaDB12.2.2/34396. Run finished20:16:25UTC on2026-11-03.
Root matched all59 source-discovered names to59 unique actual TRX rows. The
replacement-owner row-wait case passed in0.9798987s and required real matching
waiter/blocker/schema/PRIMARY-row metadata before releasing the original COMMIT.
FullProviderFamily=true; child/commands settled, data/private options absent,
listeners closed and no primary/cleanup exceptions. No original database changed.

Earlier attempts remain:21P/38F at the strict Int64-only check, then58P/1F while
25ms polling retained stale MariaDB lock metadata. Actual scalars were Int32;
the corrected classifier accepts only exact integral0/1. Polling now leaves200ms
idle for the matching server's100ms cache rule; no deadline or assertion weakened.
Temporary diagnostic bulk stays private. See FSB-344/345 in the source-bug tracker.

- [x] Retained repository-issued nonpooled connection owner; private construction,
  complete validated format-2 acquisition, cancellation before issuance, per-use
  server-lock verification, safe retirement and retained commit classification.
- [x] All six raw core/bundle write gates; ordered account locks, partial-prefix
  cleanup through actual close, transaction-aware held-lock verification and
  replay-preserving no-new-UID restriction for bound single-account saves.
- [x] Actual-provider controls authored for successful save, refusal preservation,
  independent account, raw bypasses, partial pair prefix, disposal, cancellation,
  server connection loss, lost ACK and exact replay; no synthetic grants.
  Invalid/pre-cancelled acquisition must issue no owner; a pre-cancelled bound
  save must preserve the real owner and rows before that same request commits.
- [x] Integrate real two-owner pair transactions, private nonce/physical domain
  proof, actual acquisition row locks, current UID-union limits and shared known/
  unknown outcome retention. Nine controls cover real transfers, rollback,
  acknowledgement boundaries, replacement-claimant row waits, cancelled second-gate
  waits and dual disposal during the same actual pair COMMIT.
- [x] Final59-Fact provider family and actual cleanup inspected.
- [x] Affected normal SQL-world/data/event neighbors:217 passed,0failed/skipped.
- [x] Selected-SQL tools:9 passed,0failed/skipped, plus one actual selected provider
  pass in1.6784563s. Its FullProviderFamily=false receipt and one-name inventory
  prevent a debug pass being reported as the whole family. Default still requires
  all current discovered Facts; skips/identity mismatches cannot count as passes.
- [ ] New public full Windows integration gate; local selections are not that gate.

The separate public Windows run41683147081 on exact `df6f42dc` finished with one
event fixture failure. [Actual result](FULL-WINDOWS-20261103-DF6.md). Its workers
settled before owned qualification started. The reviewed event collection repair
passed the affected selection, including the exact earlier failure. Keep subsequent
checks serialized; no duplicate host jobs.

[Single-owner contract](../protocol/specs/sql-world-bundle-custody.md),
[pair contract and evidence](../protocol/specs/sql-world-pair-custody.md).
Full native authentication, Store/UID/sidecar/startup ownership, all later writers,
native backend migration and 7937 gameplay activation remain unfinished.
Original data, sessions, routes, restore ZIP and English client are preserved;
legacy client/updater/native execution hold and unchanged antivirus settings apply.
