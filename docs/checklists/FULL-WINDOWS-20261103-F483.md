# Full Windows result: f483f983

Run `20261103T081726283Z-89854f6f` qualified source
`f483f98387b232e298ba54f205a5efb99dcb552e`. It started at
08:17:26.4026439 UTC and finished at 10:26:09.5239272 UTC on 2026-11-03,
elapsed 2h 8m 43.121s. Command exit was zero; all eight restore/build/test,
launcher and updater stages passed. Root and an independent helper read the
actual saved TRX rows.

| Suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Core | 34,979 | 0 | 192 |
| Updates | 446 | 0 | 0 |
| Launcher | 57 | 0 | 0 |
| Total | 35,482 | 0 | 192 |

The two updater PowerShell checks also passed; they are not added to TRX totals.
All 664 Chinese7937-named rows passed. The exact formerly failing
`LegacySecondaryPasswordRuntimeTests.VerifiedProtectedOwnersTradeAndRetainVerificationAcrossLawfulRecordCopies`
also passed; the [older failed full result](FULL-WINDOWS-20261103-B6.md)
remains part of the history.

The 192 skipped rows comprise 40 opt-in SQL cases (no database contacted in
this full gate) and 152 cases needing external game fixtures omitted from the
public source snapshot. Separate owned-provider qualification is required.

This is a local source qualification, not a new public CI pass, deployment,
live7937 acceptance or full parity. It does not qualify the subsequent
captured-prefix/admission-image/paid-expiry batch. That batch's first affected
run built successfully and finished 700 passed / 10 failed / 0 skipped; its
older malformed/truncation-accepting login assertions are being corrected with
valid positives and no-effect rejection controls preserved.
