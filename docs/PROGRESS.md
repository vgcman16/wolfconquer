# Development checklist

Updated 2026-10-26. Not a percentage or a complete feature inventory.

- [x] Sanitized C#/.NET 9 projects and constructed fixtures included.
- [x] Later loader, player-state, equipment, spell, packet, and transport source changes imported with publication exclusions preserved.
- [x] Latest tracked-source LOCAL Release gate: 25,193 core passed, 0 failed, 165 existing exclusions; updater305/305 and launcher57/57. Build157warnings/0errors. See [validation](../VALIDATION.md); this is separate from client acceptance.
- [x] Sequential check command and read-only local dashboard implemented and exercised.
- [x] Owner-requested trusted-main Windows workflow defined; its results are separate from local validation.
- [ ] Reduce existing complexity/large files without altering native ordering, arithmetic, or persistence behavior.
- [ ] Complete remaining native login/loader and host/database integration.
- [ ] Complete atomic mailbox package payout: mode5 and nonzero-Flag ClaimAll currently refuse before effects. Pure policies/plans remain; real UID producer intent and joint gear/Ninja journal owners are still required.
- [ ] Finish and visually validate English client localization using authorized external content.
- [ ] Verify current published source end-to-end with a real client and authorized data.
- [ ] Complete remaining gameplay/newer-client parity and production hardening.

Game assets, saves, captures, local operational records, runner credentials, and
the preserved working restore archive are not part of this public source.
