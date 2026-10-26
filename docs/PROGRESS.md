# Development checklist

Updated 2026-10-26. Not a percentage or a complete feature inventory.

- [x] Sanitized C#/.NET 9 projects and constructed fixtures included.
- [x] Later loader, player-state, equipment, spell, packet, and transport source changes imported with publication exclusions preserved.
- [x] Latest tracked-source LOCAL Release gate: 25,193 core passed, 0 failed, 165 existing exclusions; updater305/305 and launcher57/57. Build157warnings/0errors. See [validation](../VALIDATION.md); this is separate from client acceptance.
- [x] Public816f3a20 passed GitHub Windows40304697861:25,299 core passes,165unchanged existing exclusions, updater434/434 and launcher57/57; all8stages0. Full-bag failures send only an error; unsupported package payouts remain fenced.
- [x] D13 mailbox strings use client-confirmed fields9/10; default D11 bytes remain. Durable UID intent and four-file Gear journal prerequisites have focused proof:1593/1593,0skips, all included in the passed full CI. Payouts remain fenced; no Host activation.
- [x] Sequential check command and read-only local dashboard implemented and exercised.
- [x] Owner-requested trusted-main Windows workflow defined; its results are separate from local validation.
- [x] Actual publisher/CLI quarantine and exact native-asset review tooling are implemented. Full updater434/434 and the actual synthetic PS->stage->publish pipeline pass. Successor Windows40304697861 passes; the PS distribution/pipeline checks ran separately. No actual client assets have been approved or released.
- [x] Ninja credit overflow/missing-owner regressions corrected, unsafe legacy assertions migrated, and complete saved-state projection guarded:120/120 focused pass. Successor Windows CI pending; package/Host/native-client activation remains held.
- [x] Reusable selected normal-import inspection passes28 standalone synthetic Python tests; physical metadata is separate from native runtime proof.
- [ ] Complete package pending/completed-work reconciliation and restart mutation fencing, then verify genuine registered payouts and stack fidelity.
- [ ] Reduce existing complexity/large files without altering native ordering, arithmetic, or persistence behavior.
- [ ] Complete remaining native login/loader and host/database integration.
- [ ] Complete atomic mailbox package payout: mode5 and nonzero-Flag ClaimAll currently refuse before effects. Pure policies/plans remain; real UID producer intent and joint gear/Ninja journal owners are still required.
- [ ] Finish and visually validate English client localization using authorized external content.
- [ ] Verify current published source end-to-end with a real client and authorized data.
- [ ] Complete remaining gameplay/newer-client parity and production hardening.

Game assets, saves, captures, local operational records, runner credentials, and
the preserved working restore archive are not part of this public source.
