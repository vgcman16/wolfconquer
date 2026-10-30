# WolfConquer project progress

Generated from `status.json` by `tools/parity/generate.py`. Do not edit this generated document.

Scope: Sanitized legacy-server parity: client compatibility, corrected emulator defects (project planning).

Planning estimate: **55%**, range **40-70%**, **low confidence**. This is not a measured completion score.

Estimate date: **2026-10-19**. Status review date: **2026-10-30**. Reviewing the status does not refresh the estimate's age.

Source commit: `fec09fbb0862e39ff173e7172bac81a5af527943` (short: `fec09fbb0862`).

Source audit: 2026-10-19; 784 files; 260,144 lines; target comparison: **representative**. Inventory counts are not implementation proof.

A green test suite is not a live-client, complete-English, or full-parity certification. A full-completion claim requires a human audit even when every listed item is marked done.

## Milestones and verification boundaries

| Milestone | Status | Detail |
| --- | --- | --- |
| Limited local live-client milestone | verified | Exact be2740ae had an October28 disposable D13 login, visible body/Twin City, movement, saved destination and one graceful server restart/relogin. Nine gear mails later rendered; actual claim/equip remains unaccepted. This does not qualify current main or7935 gameplay. |
| Current public build live-client acceptance | planned | Preceding exact6cb2babc passed full Windows41106500544: Core31832/152existing NotExecuted, Updates446, Launcher57; all8stages passed. Current130-path Rune/item/spell successor passed457 focused plus67 final affected controls, overlapping. Its exact-public full gate and current-main live-client acceptance remain pending. |
| Complete English client | in-progress | Working7899/D13 English remains incomplete. Separate7937 UStrRes proposal passed16 tests, retains1191 keys and changes1145values; not applied/rendered. Other text/assets remain open. |
| Newer-client-only parity | in-progress | Official7917 full reference auto-updated itself through7937; separate full Wolf stage retained. Complete-skin ASCII alias opened launcher; local auth/routing/gameplay remain unaccepted. Selected intake275focused passed; actual old65 schema refusal led to observed strict67-field main57093/Sub1693 rows. Typed envelope Build05/294focused passed; actual managed intake0 retained both67-field catalogs; non-ID meanings/runtime/cost/save owners remain open. No newer-client completion percentage. |
| Public full-client download and updates | in-progress | Tooling preview launcher-preview-20261028 remains immutable at224f1eb5; anonymous HTTPS ZIP/checksum downloads match its checked nine-file package. New source adds two exact reviewed data paths (446 updater cases plus7 scripts); token\_type.dat remains quarantined. No full game assets, portable configuration or real client feed are published. Fresh full-client install/update/recovery acceptance remains open. |

## Legacy-server domains

| Domain | Status | Implemented evidence | Remaining work |
| --- | --- | --- | --- |
| Transport, authentication and startup | partial | Packet/cipher/startup paths validate envelopes and configured UID domains. Owned process/restart controls and one local D13 login/walk fixture have passed. Ordinary admission now loads saved physical class/level after durable commit. Native account-to-world handoff, complete ownership and current-public client acceptance remain open. | Prove the authenticated native account-to-world handoff and protected D13 login payload; complete ownership and loader joins, then verify the published build and staged endpoint migration with the actual client. |
| Shared player, items and persistence | partial | Selected storage/mailbox/Trade/booth/equipment/learning/consumables use owned saved transactions. Recorded trade/vendor recovery passed the preceding full6cb gate. Current source adds bounded book/item/spell recovery, Rune grant origins, atomic Rune container/drop and exact writer-bound publication;457 broad plus67 final affected controls passed (overlapping). Full current gate, native graph/SQL and broader recovery remain open. | Prove client warehouse cache/resynchronization behavior after committed-but-undelivered mutations. Finish one shared native player/item graph, login-loader, real login/save worker, production callers, legacy SQL/provider compatibility and restart recovery. |
| Combat, monsters and death | partial | Selected combat/status/AI/death paths have source proof. Current Rune casting joins actual adopted classification, native normal/alternate providers and Beast state before costs. Passive raw-self items no longer mint phantom learned skills; complete passive effects and Rune progression remain open. Prior combat/full-gate results do not prove current-client multiplayer acceptance. | Complete all callers, native arithmetic and shared RNG/order semantics, drops, lifetime ownership and multi-player/client scenarios. |
| NPCs, shops, progression and housing | partial | Dispatch, shops, forging, skills, quest metadata and durable rewards exist. Managed VIP, promotion, donation-to-CP/bank and permanent Chi purchases validate/commit before publication. Housing parsing preserves coherent graphs; authenticated adoption, entry and furniture economy remain unfinished. | Close deferred mutations and exact economy/persistence ordering; complete housing, occupants and furniture lifecycle. |
| Trade, social systems and events | partial | Managed Trade/booth exchanges use ordered participant commits and a shared WAL. Owned startup recovery now reconciles exact recorded pair intent, including partial item swaps, real escrow refund/exchange and complete vendor closures. Source fixtures cover restart without replay; guild/clan/event/native ownership and multiplayer client acceptance remain open. | Complete native/social/event ownership and real-client multiplayer acceptance. Broader inventory/storage and booth raw-writer/delivery lifecycles, remaining guild/union actions, brackets, rewards, claims, reset, database parity and durable recovery remain open; scoped managed tests do not certify all transfers. |
| Cross-server and casino workflows | partial | Group/travel metadata, startup packets, poker data and visibility surfaces exist. | Complete genuine transfer/return/session/economy ownership and failure handling; verify table rounds, betting, pot and settlement. |

The GIF animates decorative shine only; its estimate fill never grows between frames.
