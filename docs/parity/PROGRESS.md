# WolfConquer project progress

Generated from `status.json` by `tools/parity/generate.py`. Do not edit this generated document.

Scope: Sanitized legacy-server parity: client compatibility, corrected emulator defects (project planning).

Planning estimate: **55%**, range **40-70%**, **low confidence**. This is not a measured completion score.

Estimate date: **2026-10-19**. Status review date: **2026-10-29**. Reviewing the status does not refresh the estimate's age.

Source commit: `30e793f0589fca5852a5af9f8c980fa934f7224e` (short: `30e793f0589f`).

Source audit: 2026-10-19; 784 files; 260,144 lines; target comparison: **representative**. Inventory counts are not implementation proof.

A green test suite is not a live-client, complete-English, or full-parity certification. A full-completion claim requires a human audit even when every listed item is marked done.

## Milestones and verification boundaries

| Milestone | Status | Detail |
| --- | --- | --- |
| Limited local live-client milestone | verified | Exact be2740ae had an October28 disposable D13 login, visible body/Twin City, movement, saved destination and one graceful server restart/relogin. Nine gear mails later rendered; actual claim/equip remains unaccepted. This does not qualify current main or7935 gameplay. |
| Current public build live-client acceptance | planned | Exact531a9aa3 passed full Windows40867334704: Core29,833 passed/152 existing fixture skips, Updates446, Launcher57; all eight stages passed. Matching World/DataTool outputs and actual logs/TRXs are retained. Current-main live-client acceptance remains open. |
| Complete English client | in-progress | Chinese client 7899 / cached DX8 D13 is the current target; English localization is incomplete. |
| Newer-client-only parity | in-progress | Official patches through7935 are downloaded/extracted privately. Separate2132codec, Echo request/catalog transform/threshold selection and item2350reader/writer have focused proof. The102-case writer run includes37 existing-schema controls, not extra patch coverage. No default7935 activation, real state/cost/save join or actual7935 client acceptance. Not estimated. |
| Public full-client download and updates | in-progress | Tooling preview launcher-preview-20261028 remains immutable at224f1eb5; anonymous HTTPS ZIP/checksum downloads match its checked nine-file package. New source adds two exact reviewed data paths (446 updater cases plus7 scripts); token\_type.dat remains quarantined. No full game assets, portable configuration or real client feed are published. Fresh full-client install/update/recovery acceptance remains open. |

## Legacy-server domains

| Domain | Status | Implemented evidence | Remaining work |
| --- | --- | --- | --- |
| Transport, authentication and startup | partial | Packet/cipher/startup paths validate envelopes and configured UID domains. Owned process/restart controls and one local D13 login/walk fixture have passed. Ordinary admission now loads saved physical class/level after durable commit. Native account-to-world handoff, complete ownership and current-public client acceptance remain open. | Prove the authenticated native account-to-world handoff and protected D13 login payload; complete ownership and loader joins, then verify the published build and staged endpoint migration with the actual client. |
| Shared player, items and persistence | partial | Selected storage, mailbox, Trade/booth, equipment moves, learning and consumables use validated saved transactions. Ordinary admission retains class/level; owned HP/Mana now join saved and managed state. FullAccount/rank/spell preservation and source531a9aa3 passed full Windows checks. Native item graph adoption, broad recovery and real SQL remain open. | Prove client warehouse cache/resynchronization behavior after committed-but-undelivered mutations. Finish one shared native player/item graph, login-loader, real login/save worker, production callers, legacy SQL/provider compatibility and restart recovery. |
| Combat, monsters and death | partial | Selected combat, status, AI and death paths have focused proof. Automatic-skill costs, strict required stamina, selected XP eligibility and unsent cost replies now validate actual owners/state; full Windows531a9aa3 passes. Caprice paired transfer and shared journal ordering remain unqualified. Broader native executors and multiplayer acceptance are open. | Complete all callers, native arithmetic and shared RNG/order semantics, drops, lifetime ownership and multi-player/client scenarios. |
| NPCs, shops, progression and housing | partial | Dispatch, shops, forging, skills, quest metadata and durable rewards exist. Managed VIP, promotion, donation-to-CP/bank and permanent Chi purchases validate/commit before publication. Housing parsing preserves coherent graphs; authenticated adoption, entry and furniture economy remain unfinished. | Close deferred mutations and exact economy/persistence ordering; complete housing, occupants and furniture lifecycle. |
| Trade, social systems and events | partial | Managed Trade and booth exchanges use ordered participant commits and a shared journal. Owned tests cover selected failure/retry, refund, disconnect and recovery paths. Performer learning binds the current session and saved spell image before commitment. Guild/clan projections and selected event/timer flows exist; broader native/social ownership and multiplayer acceptance remain open. | Complete native/social/event ownership and real-client multiplayer acceptance. Broader inventory/storage and booth raw-writer/delivery lifecycles, remaining guild/union actions, brackets, rewards, claims, reset, database parity and durable recovery remain open; scoped managed tests do not certify all transfers. |
| Cross-server and casino workflows | partial | Group/travel metadata, startup packets, poker data and visibility surfaces exist. | Complete genuine transfer/return/session/economy ownership and failure handling; verify table rounds, betting, pot and settlement. |

The GIF animates decorative shine only; its estimate fill never grows between frames.
