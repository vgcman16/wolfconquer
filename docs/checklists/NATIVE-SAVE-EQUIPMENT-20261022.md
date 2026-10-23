# Native saves and equipment — offline checklist

Updated: October 22, 2026 (local time). This is component progress, not a
replacement-server, live-client, English-client or full-parity completion claim.

- [x] Chi: actual four-power state, records, Fate loader, status/rank bonuses,
  native rerolls/shared RNG and six ranking packet primitives. 140 checks pass.
- [x] Achievement: real bitmap, records, load-member phase, mutation/message
  order, Guild broadcast and three packet primitives. 58 component checks pass.
- [x] Equipment: whole selection, battle power, gem/refinery/Chi/beast/profession/
  perfection methods, mythsoul spell/bag effects, appearance and packet methods;
  genuine profession/refine/special-effect tables. 130 checks pass.
- [x] Correct achievement2307's false source-silent classification. It now logs
  an explicit missing host-adoption diagnostic without a reply. Keep the public
  opcode alias.2311 evidence now acknowledges its unsent native serializer.
  Two host/evidence checks and44 existing source-silent checks pass.
- [x] Private Release focused run:374 passed,0 failed,0 skipped. Initial compile
  rejections exposed the missing Player/map contract and a test namespace typo;
  v2 had373 passes/1 failure from a stale reflection assertion. v3 passes all374.
- [x] Independent sanitized-public Release proof also passed374/374,0 failed/
  skipped. Both runs cover140 Chi,58 achievement,130 equipment,2 host/evidence
  and44 retained source-silent cases; no private database/game assets required.
- [ ] Full Windows39875129428 on public21723558 ended failure at00:10:12UTC
  October23. Runner is offline; local worker/test processes were absent. Receipt
  20261022T235827741Z-4359532b remains unfinished after successful restore/build;
  no completed test TRX or later-stage result exists. This is not full acceptance.
- [ ] Full native Chi ranking/load, Achievement receiver and Player IMapObj/
  GetArray, Equipment.QueryEquipment/OnDequeue/UpdateStats, Inventory mutations,
  SaveClient/LoadCharacter/ExecuteLogin and startup/host adoption remain open.
- [ ] InnerPower, Rune stats and prestige-ranking candidates await parent review
  and execution. Helper static counts are not passing tests.

The achievement receiver and its eight receiver cases are retained whole in
ignored candidates, not compiled in reduced SendQueue-only form. The actual
Player still lacks the full IMapObj spawn contract. No synthetic owner is born
to make a modern host call succeed.

Numerical compatibility: an owned Framework4/x86 probe passed17 controls. Native
Chi's uint selector binds LINQ Int64 Sum before final Int32 wrap; under-minimum
double-to-uint terms and Rate(double)'s NaN/infinity casts preserve original
conversion behavior. No rate, rank reward or balance retuning was performed.

Offline-only: no deployment, original executable, DB write, routing change,
login attempt or game/service restart. Historical game PIDs are now absent;
their prior running state is not asserted current. Restore ZIP metadata remains
34,082,707,691 bytes /2026-10-21T19:53:09.2405123Z, not a new integrity scan.
Standing55% rough estimate (40–70%, low confidence, October19) is unchanged.

Next: review/integrate the genuine stat and player/map owner graphs, rerun
focused behavior checks, then qualify a new full Windows run when available.
