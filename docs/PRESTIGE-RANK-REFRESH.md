# Prestige retained-rank refresh qualification

2026-10-27. FSB-137 and FSB-138 record this retained-rank correction. Its source
99447f89 passed full Windows40489896224: core27,549 passed/164 existing exclusions,
updater434, launcher57; all8 stages exit0. The later class/reborn and raw-codec
successor has separate focused proof and still needs its own exact-source full gate.

## Corrected behavior

`LegacyNativePrestigeRanking.PublishPrestigeEntry` now prepares a coherent refresh
of the actual retained registry before publishing any changed dictionary or
champion. At 20,000 points or more, World and the caller-selected included class
receive the current Entry; obsolete class membership is removed. A same-UID score
of 0 or 19,999 removes its retained World/class memberships. Existing class mapping
and the literal 10000..10002 World-only exclusions are preserved.

Standalone `Rank.AddItem`/`Remove` and global `Remove` also prepare changes before
publication. They preserve escaped dictionaries and Entry/point-array contents,
and select the next surviving champion or null. If old raw logic removed a UID
from Items but left that UID in BestOfClass, an operation on that UID refreshes
the cached champion. A genuinely absent UID remains a no-op after admission;
unrelated stale champions are not silently normalized.

Admission scans every actual present owner and complete tail, including rows
beyond caps. Invalid owner/category/limit/Items/UID state and cross-class duplicate
UIDs refuse before rank or client points, Entry, perfection or guild outputs
change. World/class mirrors may use distinct Entries or a shared dictionary.
Sparse graphs remain valid: publication requires World and an included target
class; global removal does not invent a World owner, and detached Rank operations
do not depend on the registry. Stable ties, Entry aliases and retained limits
(normally World 100/class 30) remain covered.

## Root qualification evidence

| Selection | Actual result |
| --- | --- |
| Tests-only on public E7 baseline | 489 total: 133 failed, 356 passed, 0 skipped. |
| Corrected public selection, including welcome wording | 843/843 passed; every one of the 489 baseline test names passes. |

The separate welcome cleanup replaces three static-GUI claims with shorter,
qualified wording. Its 11 startup frames, six talks, website text and Body/Footer
sequence remain unchanged. It has not been deployed.

The preceding **E7** full Windows run `40474236546` was accepted: 27,404 core cases
passed with 164 existing exclusions, updater 434, launcher 57, synthetic parser
gates 20+13, and all eight steps exited 0. Receipt
`20261027T083328942Z-73c6dd50` spans
`2026-10-27T08:33:29.0777429Z` to `2026-10-27T09:16:49.7448393Z`.
That is evidence for E7, not a full CI result for these newer corrections.

## Remaining boundary

This is a retained-cache correction. Discarded World 101st/class 31st candidates
are unavailable, so removal cannot establish a complete leaderboard or invent
backfill. `CheckReborn`, other query/diagnostic behavior and their historical-rule
ambiguities remain open; no new class eligibility rule is defined here.

The immutable saved `WorldDataCatalog.PrestigeRanks` projection is separate.
Refreshing this legacy registry does not dynamically refresh that catalog or
activate GenericRanking/Viewer. This correction changes no packet ID/layout and
adds no protocol branch. Focused tests do not prove live-client behavior,
multiplayer/restart acceptance, deployment or full parity.

Later qualification: [class resolution and reborn cleanup](PRESTIGE-CLASS-AND-REBORN.md)
corrects supported stages52..57, removes default-Water authority and preserves a
fresh same-family row. FSB-139..140 cover that scoped successor; global class
rules, remaining queries, runtime adoption and full persistence remain open.
