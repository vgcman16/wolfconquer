# WolfConquer project progress

Generated from `status.json` by `tools/parity/generate.py`. Do not edit this generated document.

Scope: Sanitized legacy-server parity: client compatibility, corrected emulator defects (project planning).

Planning estimate: **55%**, range **40-70%**, **low confidence**. This is not a measured completion score.

Estimate date: **2026-10-19**. Status review date: **2026-10-25**. Reviewing the status does not refresh the estimate's age.

Source commit: `c1ee382d7669189037fd7d22618c283e7537ee38` (short: `c1ee382d7669`).

Source audit: 2026-10-19; 784 files; 260,144 lines; target comparison: **representative**. Inventory counts are not implementation proof.

A green test suite is not a live-client, complete-English, or full-parity certification. A full-completion claim requires a human audit even when every listed item is marked done.

## Milestones and verification boundaries

| Milestone | Status | Detail |
| --- | --- | --- |
| Limited local live-client milestone | verified | Login, walking, one-item drop/pickup and saving were demonstrated on an earlier isolated local build. This does not validate the latest public source. |
| Current public build live-client acceptance | planned | Public c1ee382d passes all eight clean Windows LOCAL stages (24,903 core passes; 165 existing exclusions). Actual-client acceptance, GitHub CI and deployment are separate and not yet proven. |
| Complete English client | in-progress | Chinese client 7899 / cached DX8 D13 is the current target; English localization is incomplete. |
| Newer-client-only parity | planned | Not estimated. Features absent from the older server source require separate binary, data and observed-behavior evidence. |
| Public full-client download and updates | in-progress | Signed updater and original-art launcher tooling are published. No official full-client package/feed is published yet. |

## Legacy-server domains

| Domain | Status | Implemented evidence | Remaining work |
| --- | --- | --- | --- |
| Transport, authentication and startup | partial | Packet, cipher, startup and replacement-service paths exist. Legacy account envelopes now reject inconsistent bytes and malformed saved-credential admission; secure native handoff is still unproved. An earlier build demonstrated limited local gameplay. | Prove the authenticated native account-to-world handoff and protected D13 login payload; complete ownership and loader joins, then verify the published build and staged endpoint migration with the actual client. |
| Shared player, items and persistence | partial | Concrete containers/snapshots exist. Bag/warehouse moves persist complete item files before success with capacity, closure, PIN and escrow checks. Show validates current-owner views; selected expired root locks commit before notification using issued identity. House saves validate complete owned graphs and use one-file atomic replacement/create. Native queue, spell/proficiency, profile, ranking, mail and nobility have component proof; full SaveClient/adoption remain unfinished. | Prove client warehouse cache/resynchronization behavior after committed-but-undelivered mutations. Finish one shared native player/item graph, login-loader, real login/save worker, production callers, legacy SQL/provider compatibility and restart recovery. |
| Combat, monsters and death | partial | Substantial spell, damage, receive, equipment, monster AI, rewards and death behavior has focused automated proof. | Complete all callers, native arithmetic and shared RNG/order semantics, drops, lifetime ownership and multi-player/client scenarios. |
| NPCs, shops, progression and housing | partial | Main dispatch, shops, forging, skill learning and durable progression/reward paths exist. Native quest definitions, eligibility, dialog and experience tables have component proof. House load/save validate complete files/graphs before publication; authenticated housing adoption, entry and furniture economy remain unfinished. | Close deferred mutations and exact economy/persistence ordering; complete housing, occupants and furniture lifecycle. |
| Trade, social systems and events | partial | Managed Trade and player-booth exchanges use ordered pair commits and a shared journal. Tests cover complete closures, stale-file/escrow refusal, failure/retry, refunds/disconnect/restart and D13 Frame settlement. Warehouse composition preserves real 300-silver escrow: changed offered roots refund; unrelated withdrawals allow exchange. Guild/clan projections, selected actions, timers, score panels and some event state machines exist. | Complete native/social/event ownership and real-client multiplayer acceptance. Broader inventory/storage and booth raw-writer/delivery lifecycles, remaining guild/union actions, brackets, rewards, claims, reset, database parity and durable recovery remain open; scoped managed tests do not certify all transfers. |
| Cross-server and casino workflows | partial | Group/travel metadata, startup packets, poker data and visibility surfaces exist. | Complete genuine transfer/return/session/economy ownership and failure handling; verify table rounds, betting, pot and settlement. |

The GIF animates decorative shine only; its estimate fill never grows between frames.
