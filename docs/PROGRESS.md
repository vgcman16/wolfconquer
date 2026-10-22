# Development checklist

Updated 2026-10-22. Not a percentage or a complete feature inventory.

- [x] Sanitized C#/.NET 9 projects and constructed fixtures included.
- [x] Later loader, player-state, equipment, spell, packet, and transport source changes imported with publication exclusions preserved.
- [x] Release restore/build and unfiltered tests verified: 15,084 passed, 0 failed, 166 skipped. Warnings remain; see [validation](../VALIDATION.md).
- [x] Sequential check command and read-only local dashboard implemented and exercised.
- [x] Owner-requested trusted-main Windows workflow defined; its results are separate from local validation.
- [ ] Reduce existing complexity/large files without altering native ordering, arithmetic, or persistence behavior.
- [ ] Complete remaining native login/loader and host/database integration.
- [ ] Finish and visually validate English client localization using authorized external content.
- [ ] Verify current published source end-to-end with a real client and authorized data.
- [ ] Complete remaining gameplay/newer-client parity and production hardening.

Game assets, saves, captures, local operational records, runner credentials, and
the preserved working restore archive are not part of this public source.
