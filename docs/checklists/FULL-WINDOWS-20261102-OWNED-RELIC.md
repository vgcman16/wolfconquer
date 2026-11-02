# Full local Windows qualification: owned relic and 7937 tooling

Tested source: `fe3e79a29ff6f377981465de0d78785d7105bf92`.
The source tree stayed clean throughout qualification.

Run `20261102T203726592Z-007a65f8` started at 2026-11-02T20:37:26.689Z
and finished at 2026-11-02T22:42:37.578Z: 125m10.889s, exit 0.
All eight restore/build/test/script stages passed.

| Project | Passed | Failed | Not executed |
| --- | ---: | ---: | ---: |
| Core | 34,621 | 0 | 152 |
| Updates | 446 | 0 | 0 |
| Launcher | 57 | 0 | 0 |
| Total | 35,124 | 0 | 152 |

Counts come from the actual three terminal TRX result sets. The 152 Core
NotExecuted rows remain excluded from passed counts. The updater status parser
also passed 20 synthetic cases, and its TRX smoke parser passed 13 synthetic
cases; these are separate script checks, with no HTTP/service probe.

The qualified increment implements owned legacy relic compose/choose transactions,
shared UID/journal/native/file/output ownership, mandatory target validation before
spell costs, equipment cooldown controls, and additional 7937 packet/catalog tools.
Earlier component failures and aborted runs are retained; a later pass does not
change their outcomes.

The earlier public Windows run 41495012492 on `5ccca124` failed two Core equipment
reentry cases. This local result is a newer qualification on the stated source,
not a rewrite of that public failure or a new GitHub Windows Actions result.

7937 catalog acceptance remains data evidence: Main 57,093 and Sub 1,693 rows
with 67 fields, complete Echo requirements/costs/opening rules, and selected
Compose/Random/Resolve/Addition tables. Current 7937 profile/session activation,
per-player Echo persistence/actions, unresolved item-field meanings, complete
English rendering and live-client acceptance remain unfinished.

The queued decomposition, Echo extension-envelope and managed column-alignment
checkpoint is not covered by this result. No whole-server parity, production
deployment, universal crash/power-loss safety or current-client acceptance is
claimed. Original saves, routes and backups remain preserved. The documented
legacy executable hold remains active.
