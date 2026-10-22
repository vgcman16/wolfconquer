# WolfConquer project progress

Generated from `status.json` by `tools/parity/generate.py`. Do not edit this generated document.

Scope: Strict legacy-server parity (project planning).

Planning estimate: **55%**, range **40-70%**, **low confidence**. This is not a measured completion score.

Estimate date: **2026-10-19**. Status review date: **2026-10-22**. Reviewing the status does not refresh the estimate's age.

Source commit: `1cf9b4344a7b110134c358fe003990146944e752` (short: `1cf9b4344a7b`).

Source audit: 2026-10-19; 784 files; 260,144 lines; target comparison: **representative**. Inventory counts are not implementation proof.

A green test suite is not a live-client, complete-English, or full-parity certification. A full-completion claim requires a human audit even when every listed item is marked done.

## Milestones and verification boundaries

| Milestone | Status | Detail |
| --- | --- | --- |
| Limited local live-client milestone | verified | Login, walking, one-item drop/pickup and saving were demonstrated on an earlier isolated local build. This does not validate the latest public source. |
| Current public build live-client acceptance | planned | Not yet proven against the current published commit. Automated tests and prior local deployment proof are separate. |
| Complete English client | in-progress | Chinese client 7899 / cached DX8 D13 is the current target; English localization is incomplete. |
| Newer-client-only parity | planned | Not estimated. Features absent from the older server source require separate binary, data and observed-behavior evidence. |
| Public full-client download and updates | in-progress | Signed updater and original-art launcher tooling are published. No official full-client package/feed is published yet. |

## Legacy-server domains

| Domain | Status | Implemented evidence | Remaining work |
| --- | --- | --- | --- |
| Transport, authentication and startup | partial | Packet, cipher, startup and replacement-service paths exist; a limited local gameplay milestone was demonstrated on an earlier build. | Complete native ownership and loader joins; verify the current published build and staged endpoint migration with the actual client. |
| Shared player, items and persistence | partial | Concrete items/containers and managed snapshots exist. Native component proof covers queues/member maps, spells/proficiencies, profile records, subclass/Ninja/HundredWeapons owners/ranks/metadata/messages, BinaryFile/mail persistence/codecs, and complete nobility ranking/paid-state/character-file loading. Actual queues, owned-file reloads and Windows clocks are tested; full SaveClient and production callers remain unfinished. | Finish one shared native player/item graph, complete login-loader, real login/save worker and inventory mutation ordering, production caller integration, legacy SQL/provider compatibility and restart recovery. |
| Combat, monsters and death | partial | Substantial spell, damage, receive, equipment, monster AI, rewards and death behavior has focused automated proof. | Complete all callers, native arithmetic and shared RNG/order semantics, drops, lifetime ownership and multi-player/client scenarios. |
| NPCs, shops, progression and housing | partial | Main dispatch, shops, forging, skill learning and several durable progression/reward paths exist. Complete native quest definition loading, eligibility queries and seven message primitives have focused proof; full quest gameplay and callers remain unfinished. | Close deferred mutations and exact economy/persistence ordering; complete housing, occupants and furniture lifecycle. |
| Trade, social systems and events | partial | Trade admission, guild/clan projections and selected actions, timers, score panels and several event state machines exist. | Implement full item/money trade transactions, remaining guild/union actions, event brackets, rewards, claims, disconnects, reset and durable recovery. |
| Cross-server and casino workflows | partial | Group/travel metadata, startup packets, poker data and visibility surfaces exist. | Complete genuine transfer/return/session/economy ownership and failure handling; verify table rounds, betting, pot and settlement. |

The GIF animates decorative shine only; its estimate fill never grows between frames.
