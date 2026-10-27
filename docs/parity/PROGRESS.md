# WolfConquer project progress

Generated from `status.json` by `tools/parity/generate.py`. Do not edit this generated document.

Scope: Sanitized legacy-server parity: client compatibility, corrected emulator defects (project planning).

Planning estimate: **55%**, range **40-70%**, **low confidence**. This is not a measured completion score.

Estimate date: **2026-10-19**. Status review date: **2026-10-27**. Reviewing the status does not refresh the estimate's age.

Source commit: `d94bc0354e9c9f4a093bd348500412d5a9fdebb7` (short: `d94bc0354e9c`).

Source audit: 2026-10-19; 784 files; 260,144 lines; target comparison: **representative**. Inventory counts are not implementation proof.

A green test suite is not a live-client, complete-English, or full-parity certification. A full-completion claim requires a human audit even when every listed item is marked done.

## Milestones and verification boundaries

| Milestone | Status | Detail |
| --- | --- | --- |
| Limited local live-client milestone | verified | An October26 disposable D13 fixture completed manual authentication, world initialization and movement. Supported Body1008 passed41 focused checks; root and user confirmed its full body visible in Twin City. One local trial does not prove restart reliability or the whole public build. Earlier drop/pickup/save proof applies to an older build. |
| Current public build live-client acceptance | planned | Accepted prior sourcee7d85b70 passed Windows40474236546: core27,404 passed/164 existing exclusions, updater434, launcher57, synthetic parsers20+13; all8 stages exit0. New rank refresh/mutation and welcome wording changes pass843/843 focused checks; their own full CI is pending. Current-public live acceptance, client-crash resolution, complete English, SQL and distribution remain open. |
| Complete English client | in-progress | Chinese client 7899 / cached DX8 D13 is the current target; English localization is incomplete. |
| Newer-client-only parity | in-progress | Official patches through7935 are downloaded/extracted privately. Separate2132codec, Echo request/catalog transform/threshold selection and item2350reader/writer have focused proof. The102-case writer run includes37 existing-schema controls, not extra patch coverage. No default7935 activation, real state/cost/save join or actual7935 client acceptance. Not estimated. |
| Public full-client download and updates | in-progress | Signed updater and original-art launcher tooling are published. No official full-client package/feed is published yet. |

## Legacy-server domains

| Domain | Status | Implemented evidence | Remaining work |
| --- | --- | --- | --- |
| Transport, authentication and startup | partial | Packet/cipher/startup paths validate physical envelopes. Explicit WorldServer startup opens only its configured existing UID domain. Owned API/raw/encrypted process/restart/lease/drain controls pass. A fresh local D13 fixture now completes manual account login, world entry and movement; secure native handoff remains unproved. | Prove the authenticated native account-to-world handoff and protected D13 login payload; complete ownership and loader joins, then verify the published build and staged endpoint migration with the actual client. |
| Shared player, items and persistence | partial | Managed storage and mailbox transactions validate ownership, capacity and escrow before persisted success. Package prerequisites join UID history, recovery journals, PIN and delivery checks. Ordinary admission preserves complete saved progression without starter regrant. The offline creator makes only new synthetic domains with canonical collection defaults; full native/SQL ownership remains open. | Prove client warehouse cache/resynchronization behavior after committed-but-undelivered mutations. Finish one shared native player/item graph, login-loader, real login/save worker, production callers, legacy SQL/provider compatibility and restart recovery. |
| Combat, monsters and death | partial | Substantial spell, damage, receive, equipment, AI and death logic has focused proof. Required spell metadata and bounded XP precede local credit. Physical/sigil admission checks real owners and metadata before effects. Committed new1110 and Action78 responses retain source order and publication ownership; complete callers and multiplayer acceptance remain open. | Complete all callers, native arithmetic and shared RNG/order semantics, drops, lifetime ownership and multi-player/client scenarios. |
| NPCs, shops, progression and housing | partial | Dispatch, shops, forging, skills, quest metadata and durable rewards exist. Managed VIP, promotion, donation-to-CP/bank and permanent Chi purchases validate/commit before publication. Housing parsing preserves coherent graphs; authenticated adoption, entry and furniture economy remain unfinished. | Close deferred mutations and exact economy/persistence ordering; complete housing, occupants and furniture lifecycle. |
| Trade, social systems and events | partial | Managed Trade and player-booth exchanges use ordered pair commits and a shared journal. Tests cover complete closures, stale-file/escrow refusal, failure/retry, refunds/disconnect/restart and D13 Frame settlement. Warehouse composition preserves real 300-silver escrow: changed offered roots refund; unrelated withdrawals allow exchange. Guild/clan projections, selected actions, timers, score panels and some event state machines exist. | Complete native/social/event ownership and real-client multiplayer acceptance. Broader inventory/storage and booth raw-writer/delivery lifecycles, remaining guild/union actions, brackets, rewards, claims, reset, database parity and durable recovery remain open; scoped managed tests do not certify all transfers. |
| Cross-server and casino workflows | partial | Group/travel metadata, startup packets, poker data and visibility surfaces exist. | Complete genuine transfer/return/session/economy ownership and failure handling; verify table rounds, betting, pot and settlement. |

The GIF animates decorative shine only; its estimate fill never grows between frames.
