# Native saves and equipment — offline checklist

## Clone, combat, arena and item-timer continuation

- [x] Whole clone/pet producer, physical monster calculation, gear effects,
  solo/team arena query/codecs and item-view/refinement/equipment prerequisites
  are source-backed and implemented as modular owners; no host registration.
- [x] Private Release passed2857 clone-expanded,3149 combat/arena-expanded and
  3224 item/timer-expanded cases, all zero failures/skips (400 new over2824).
- [x] Framework4/x86 default+optimized and modern comparison each support316
  timer-expression controls. Cast-only translation was rejected on6 reachable
  extrema; source tick multiplication plus scoped narrowing matched all316.
  Existing ItemCreate and whole ItemView preserve receiver identity/two reads.
- [x] Final private and independent sanitized-public Release each passed3256/3256,
  zero failures/skips, including32 retained gear numeric controls. The public
  export now includes that source-checked prerequisite; its first compile had
  exposed missing GetDamage rather than a test failure.
- [ ] New full Windows CI on this gameplay-source snapshot remains required.
  Full HP/Team/XP/Inventory/login/save/receivers and host adoption are still open;
  no live client, original save/SQL, routing or deployment was changed.

## CI recovery and source-backed test contract correction

- [x] Human-approved registered Windows runner recovery; queued parity tracker
  passed and refreshed only its three generated files. No game/client restart.
- [x] Fresh full Windows run39889787428 onbdd40e4a restored/built successfully,
  then found two core-test expectation failures:19670 passed/2 failed/165
  unexecuted of19837; updater262/262 passed. Later Windows stages were not reached.
- [x] Original source confirms public RandFromGivingNums and outbound-only2311
  serialization, not an inbound handler. Two tests now retain their existing
  checks and add exact signature plus outbound/inbound evidence assertions.
  No production method, packet behavior, test skip or workflow was changed.
- [x] Sanitized-public focused API/selection/dispatch checks passed309/309,
  zero failures/skips. First direct run301/309 lacked the required measured PS7
  probe binding; matching the normal pipeline environment passed all309.
- [x] Fresh full Windows run39890690396 onf29841d5 passed all eight stages:
  core19672 passed/0 failed/165 unexecuted of19837; updater262/262 and
  launcher49/49 passed. Synthetic status/parser checks passed20+13 cases;
  HTTP/service probes were skipped. The registered runner remains online.
  This is full Windows CI proof, not gameplay, deployment or overall parity.


## Follow-on complete solo arena load/reset and instance prerequisites

- [x] Genuine arena instance and match constructors/state, complete Winner/Loser/
  opponent queries, both instance rank builders, tournament contracts and entire
  ArenaTable.Load/Reset:36 cases plus7 parent rank-evaluation controls.
- [x] Private and independent sanitized-public Release each passed2824/2824,
  zero failures/skips (43 new +2781 retained). Initial private2816/2817 exposed
  net9 singleton OrderBy key skipping; the test was retained.
- [x] Owned Framework4/x86 default and optimized rank probes each passed18/18.
  Scoped arena ordering now snapshots every key once before stable sorting, as
  Framework does, including singleton faults. Both instance and existing solo/team
  static rank methods preserve array publication, resets, aliases and fault order.
- [ ] Scheduler Create is not called; Arena remains genuinely null unless an owner
  supplies the real instance. Successful fixtures bind a preexisting real Arena;
  separate tests prove null faults after row/reset mutation, with no fallback.
  Team Load/Reset/matches, solo matchmaking/win/watch/teleport, full Team/HP/
  Inventory/login/SaveClient and host adoption remain unfinished. No live/SQL/
  full Windows/deployment or overall parity acceptance follows.


## Follow-on arena records and complete map-spawn methods

- [x] Complete solo/team arena records, handlers/codecs, point accessors, ranking
  refresh and actual table Save; whole monster-family database/directory/text
  loaders, Server/GameMap dynamic spawn helpers, remaining map item/NPC/static
  queries, entire StaticRole and primitive race codecs:134 new cases (52+82).
- [x] Private Release passed2781/2781 after fixture corrections; independent
  sanitized-public Release passed2781/2781 first run, zero failures/skips.
  First private run2774/2781 exposed seven test assumptions: native path spelling
  retains doubled separators and SafeRandom seed0 is clock-seeded. Production
  stayed unchanged; tests now assert raw/canonical path identity and nonzero seeds.
- [x] Actual Core.GetAngle passes all16723 owned controls and digest2201143353.
  Tests retain shadowed map/family IDs, duplicate floor-role aliases, malformed
  record/fault prefixes, arena rank aliases and native raw Save prefixes.
  Arena handlers stay private; tests invoke them through test-only reflection.
- [ ] Arena Load/Reset/matches, inventory grants, RoleView.CanAdd/Booth admission,
  race gameplay receiver, full combat/death/HP and eager login/SaveClient remain
  unfinished. Loader definitions are not startup registration or live spawning.
  No original files/processes, SQL, routes, full Windows, deployment or overall
  parity acceptance follows from this owned offline component suite.


## Follow-on genuine monster targeting and movement

- [x] Whole CheckTarget/SearchTarget/CheckGuardPosition, source geometry and
  independent monster damage/dodge/blessing calculations; full walk codec and
  monster animation chunk sender:77 new cases with nonempty movement/fallbacks.
- [x] Private and independent sanitized-public Release each passed2647/2647,
  zero failures/skips (77 new +2570 retained), first run in each copy.
  Native target choice/dead-target quirks, actual cells/view publication, distinct
  MSRandom draws, full walk/chunk bytes and partial-fault state remain intact.
- [x] Owned Framework4/x86 angle probes passed16723 controls in default and
  optimized builds. Actual modern GetAngle matches the16705 grid/boundary vector
  digest2201143353 and observed axes/diagonals. Cast translation is method-scoped.
- [ ] Whole AI dispatcher/attack/player damage/HP/death/Receive, PoolProcesses and
  player Movement receiver remain held; no scheduler or route is installed.
  Existing SystemRandom default birth is not newly accepted for held PhysicalAttack.
  This is not live AI/PvE, SQL, host startup, full Windows or parity acceptance.

## Follow-on complete lazy world population, monsters and wallet leaves

- [x] Whole native EnterMap/CheckMap and lazy dictionary lookup now load real NPC,
  SobNpc, portal, trap and monster roles. All32 native monster-file cases exercise
  actual Family.Copy/Add and nonempty spatial actors; all remaps remain literal.
- [x] Complete monster family/drop-generator/view/spawn/flags/respawn/membership/
  target-admission families, full floor role/codecs, statue lifecycle/owned saves,
  both NPC removers, whole player distance methods and money/bound/domino/donation/
  box-dialog leaves:372 new cases (181+132+12+47).
- [x] Private and independent sanitized-public Release each passed2570/2570,
  zero failures/skips (372 new +2198 retained). Private first compile exposed a
  missing whole Player distance dependency; the next2569-pass/1-fail run caught an
  older test forbidding NPC interface adoption. Full methods and12 boundary cases
  were added; the test now checks genuine interface aliases and keeps birth checks.
- [x] Owned cold-map/INI/text fixtures retain real population/fault prefixes,
  duplicated references, collision/RNG/counter effects, exact spawn/floor bytes and
  statue cache quirks. No original assets, maps, saves, DB or workers were used.
- [ ] Monster family DB startup loading, full AI/combat/death/loot/receive, SobNpc
  tournament death, Booth client admission, HP/Team/Level/Class/Inventory and full
  eager login/SaveClient remain unfinished. CRT-dependent ordinary loot selection
  is implemented but lacks deterministic path tests. No live/SQL/full Windows/
  deployment or parity-percentage acceptance follows from this component suite.

## Follow-on combat effects and native health publication

- [x] Whole blue-damage/refinement codecs, eight complete Perfection proc methods,
  fourteen-priority XP-skill query, auxiliary shield/stigma executors, source
  durability methods and complete SendUpdateHP/NotLife:87 new cases.
- [x] Private and independent sanitized-public Release each passed2198/2198,
  zero failures/skips (87 new +2111 retained), first run in each copy.
  Exact packet bytes, both genuine RNG owners, flags, cooldown stamps and mutation
  before faults are retained. Health fixtures restore RNG before/after Player birth.
- [x] HP sender preserves cumulative MaxHP then MaxHP+HP frames. NotLife's setter
  still throws as native. DrainingTouch and durability wear bodies are truly empty;
  no healing or item wear is invented. Shield/stigma have no found native callers.
- [ ] Full CheckEffects/BloodSpawn/StraightLife, writable HP/Team/death/revive and
  general attack dispatch remain held. This does not reduce the62 remaining
  dispatch owners or prove actual-client combat, SQL, host or full Windows success.

## Follow-on terrain maps, housing and reward records

- [x] Whole terrain index/map loading, genuine map dictionary storage and monster
  collection binding, map membership/cache/messages, collision/placement/coordinate
  families; complete House, Flowers, HeroRewards and Activeness record owners and
  their actual tables/codecs/NPC record prerequisites:195 new cases.
- [x] Private and independent sanitized-public Release each passed2111/2111,
  zero failures/skips (195 new +1916 retained), first run in each copy.
  Owned GUID map/INI/binary fixtures use real readers and exact record/packet bytes.
- [x] Native quirks remain: duplicate map publication rebinds the retained monster
  owner, stale recipient caches stay stale until refreshed, housing uses zero-born
  NPC records, and Activeness loads points through its original byte reader.
- [ ] Whole lazy EnterMap/world population, NPC interface/lifecycle, full SaveClient,
  eager login, writable HP/Team/Level/equipment/Inventory cycles remain unfinished.
  No original files, server/client processes, SQL, routing or deployment were used.

## Follow-on quest objectives, quest files and relic selection

- [x] Whole quest constructor/query/objective methods and direct auto-patcher,
  complete RoleQuests Save/Load, real shared-RNG relic selection, and exact login/
  server-flag/delete physical records:168 new cases. Quest owners are not born
  implicitly and no receive/login worker is registered.
- [x] Private and independent sanitized-public Release each passed1916/1916,
  zero failures/skips (168 new +1748 retained), first run in each copy.
  Actual dictionaries/aliases, objective growth/wrap, full packet bytes and mutation
  before faults are retained. All five relic rejection families use real redraws.
- [x] Quest binary files use owned GUID fixtures, exact bytes and real BinaryFile.
  Original Load has no Close; tests close only their own opened file. Production
  fault prefixes, ignored read results and the missing close are not normalized.
- [ ] Whole Inventory.Add/Update/publication, eager ExecuteLogin/static queue,
  successful login/full SaveClient, quest awards/dialogs and Level/Class cycles
  remain held on actual dependencies. No SQL, host, client or deployment proof.

## Follow-on spell training, restoration executors and attack math

- [x] Whole native UpdateSpell, AddMana, EffectMP, ShieldBlock and BreathFocus;
  all27 remaining Calculate.Base methods/constants; complete attributed interaction
  owner/read, CreateAutoAtack and six physical attack fields:123 new cases.
- [x] Private and independent sanitized-public Release each passed1748/1748,
  zero failures/skips (123 new +1625 retained). A1747-pass/1-fail run exposed a
  test expecting a late shield fault; genuine CanUseSpell catches the missing flag
  owner and rejects earlier. Test now asserts no mutation/send, not a fake throw.
- [x] Scoped scalar cast tests match32 rows from owned Framework4/x86 default and
  optimized probes (256 controls each). No extrapolation to other spell expressions.
  Shield tests retain dirty caller prefixes; protobuf attributes match prior wire.
- [ ] Whole ProcescMagic remains held:67 labels resolve to64 switch callees;
  this batch closes two of those owners, leaving62 plus the other real dispatch/
  receive-damage/death/lifecycle dependencies. No modern adapter or reduced native
  dispatcher, host registration, actual client or full combat acceptance was added.

## Follow-on complete equipment scoring, rank records and status methods

- [x] Whole GetItemPoints/GetPerfectionPosition, RankItems queries/update/binary
  persistence, CombatGear.A/Status, both complete status/server-info codecs and
  physical transform/weapon caller state. All class and status branches remain.
- [x] Private and independent sanitized-public Release each passed1625/1625,
  zero failures/skips (201 new +1424 retained). Actual modern methods match21
  source-shaped Framework4/x86 default/optimized numeric rows and all256 byte-level
  costs, including non-rounded native anomalies; conversions are owner-scoped.
- [x] Initial private compile used the wrong classifier owner name; callers now
  reuse the existing whole LegacyNativeItemPosition. A1598-pass/21-fail run caught
  a parent test fixture disabling the real value table; setup alone was corrected.
  Public compile exposed an older omitted GetValueRank prerequisite; the complete
  method was reviewed/imported and six class/gate controls now pass in both copies.
- [ ] Whole QueryEquipment/OnDequeue/UpdateStats, four transform lifecycle methods,
  Team/HP/level/Inventory/save/attack cycles and native host adoption remain held.
  Rank binary tests use owned files; this is not SQL, startup or live-client proof.

## Follow-on mentor sharing and complete Associate records

- [x] Whole TutorInfo loader/share arithmetic, all apprentice-information fields/
  codecs, Equip.SendMentorShare and remaining native Associate methods:88 cases.
- [x] Private and independent sanitized-public Release each passed1424/1424,
  zero failures/skips (88 new +1336 retained), including complete nonempty online/
  offline mentor/apprentice/friend/enemy/partner branches and owned-file Save/Load.
  Native record aliases, duplicate/cache guards, signed narrowing and publication
  before later faults remain; no player rebinding or rollback was fabricated.
- [x] Source Player.SetMentorBattlePowers is genuinely empty and stays empty.
  This is NOT a working mentor BP bonus; the tests explicitly preserve that gap.
- [ ] PKPoints exposes its genuine backing/read getter only. Its full setter,
  Team/HP/level/equipment cycles, interaction handlers and native host/login/save
  adoption remain unfinished. File proof uses owned fixtures, not original saves.

## Follow-on VIP publication and experience calculations

- [x] Whole native VipLevel/ExpireVip state, UpdateVip, all VIP flags and status
  codec; complete GainExperience scaling and CalcExpBall calculations:125 cases.
- [x] Private Release and independent sanitized-public Release each passed1336/1336,
  zero failures/skips (125 new +1211 retained). Initial compile found a synchronous
  test returning Task; the next1328-pass/8-fail run exposed incorrect expectations
  that scalar VIP updates reset the writer. Native SendUpdate appends at its
  incoming cursor; tests now preserve the dirty prefix and later cursor reuse.
  No production reset, clamp, expiry enforcement or fake Level setter was added.
- [x] Owned Framework4/x86 default and optimized probes each passed22 unsigned
  conversion controls and640 source-shaped experience-expression comparisons.
- [ ] Actual experience grants and the whole Level setter remain held behind real
  inventory/save/equipment/attack dependencies. Native startup/login/save, host
  adoption and live-client proof are not established by these component results.

## Follow-on native player/map and complete achievement receiver

- [x] Complete native SpawnPacketProto/GetArray (both branches), real Player map
  contract, BP/Union/Poker prerequisites and actual interface eligibility:104 cases.
- [x] Re-admit the WHOLE achievement receiver, both raw-map-view and queue actions,
  with eight formerly held cases. Modern host routing remains explicitly unadopted.
- [x] Private and independent sanitized-public Release each passed1211/1211,
  zero failures/skips:112 new +1076 retained +23 adjacent existing owner cases.
  Initial private compile exposed an old null call made ambiguous by the genuine
  second Union factory; its test now explicitly selects the original Guild type.
- [x] Public-only compile exposed a missing inherited GetEffect prerequisite;
  the complete native method was reviewed/imported, retaining class-specific
  shapes and null behavior. Both public/private map tests exercise real gear.
- [ ] HP is the genuine physical backing/read getter only, not a writable facade.
  Team/HP/level/equipment cycles, whole native save/login and live-host adoption
  remain unfinished. Interface eligibility is not live-client spawn proof.

## Follow-on JiangHu and complete prestige rebuild

- [x] Whole JiangHu state/rank/stat/training/reward/roll/record owner methods,
  native table/catalog/player-record save/load, ten codecs and two unregistered
  component receivers:86 cases. Activation/login/apply-star callers remain held.
- [x] Whole CreatePrestigePoints, including both equipment loops, both wardrobe
  reads, configured server gate and rank/perfection/guild mutation order:56 cases.
- [x] Private and independent sanitized-public Release each passed1076/1076,
  zero failures/skips (142 new +934 retained). An initial duplicate-field compile
  rejection was corrected by removing only the newly introduced ServerID duplicate;
  the existing physical field and OnMyOwnServer predicate remain unchanged.
- [x] Owned Framework4/x86 default and optimized JiangHu numeric controls each
  passed10/10; finite time and aligned-power narrowing only, not original EXE proof.
- [ ] Full equipment/Team/HP/level cycles, native startup/login/save/host adoption,
  activation/economic receivers and actual-client rendering remain unfinished.

## Follow-on Team/aura prerequisites and HP/mana calculations

- [x] Whole Team/leadership/flag-icon codecs and flag-icon component echo,
  physical Associate collections/timers, aura stat mutations and progression
  fields:72 cases. No shortened Team/HP/level lifecycle was installed.
- [x] Complete native GameClient HP/mana formulas and guild-skill table loader/
  lookup/cache behavior:62 cases, including real owned-file partial failures.
- [x] Private Release and independent sanitized-public Release each passed934/934,
  zero failures/skips (134 new +800 retained). First compile exposed a missing
  test namespace; v2's913-pass/21-fail receipt exposed wrong test cursor assertions.
  Original Finalize leaves Position4; corrected assertions retain exact bytes,
  size and later read-cursor checks. Production was not changed to suit tests.
- [ ] Full Team/HP/level/equipment cycle, native save/login and host adoption remain.
  Mentor components, player/map and JiangHu are independently proven above;
  full lifecycle adoption is not established by those focused results.
- Owned HP expression probe:18 result values match expected formulas under both
  default and optimized Framework4/x86; separate10 cast controls also pass.

## Follow-on Prestige and Perfection proof

- [x] Whole native Prestige ranking buckets, queries, record/file persistence,
  appearance and packet conversion; complete Perfection effects and Rune lookups.
- [x] Private Release and independent sanitized-public Release each passed800/800,
  zero failures/skips:235 new cases plus565 retained, including two fixture-owner
  isolation controls. The initial793-pass/7-fail receipt is retained: seven tests
  faulted during real item construction; corrected setup removes catalog metadata
  only after construction, so the intended production fault order is exercised.
- [x] Whole CreatePrestigePoints is proven above. Equipment rebuild, startup/load/
  save/host adoption and live-client proof remain open, not deployment acceptance.
- HP arithmetic review: owned Framework4/x86 default and optimized probes each
  passed10 finite double-to-UInt16 controls for the actual formula range only.

## Follow-on InnerPower and Rune proof

- [x] Complete InnerPower owner, stages/status/rank, actual INI definition loader,
  count-record save/load, potency packet and equipment attribute method:60 cases.
- [x] Complete Rune table/load/plus-points and actual score/perfection/slot/Full/
  Check methods plus two native buff fields:131 cases.
- [x] Private and independent sanitized-public Release each passed565/565,
  0 failures/skips (191 new +374 retained). All five Win32 INI controls executed.
- [ ] Full Equipment.UpdateStats/QueryEquipment, Rune mutation actions, Team/HP
  lifecycle, whole save/login and host adoption remain unfinished.
- Aura numerical review: an owned Framework4/x86 probe passed66 controls in both
  default and optimized builds. Direct float-multiply/cast retains x87 precision;
  a stored float product can differ. This is not original-executable/live proof.



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
- [ ] Full native Chi ranking/load, Equipment.QueryEquipment/OnDequeue/UpdateStats, Inventory mutations,
  SaveClient/LoadCharacter/ExecuteLogin and startup/host adoption remain open.
- [x] InnerPower, Rune stats and Prestige/Perfection components passed independent
  private/public focused runs above. Remaining helper candidates are unexecuted.

The whole achievement receiver and its eight receiver cases are now admitted
against the genuine Player IMapObj/GetArray graph. No reduced SendQueue-only
receiver or synthetic owner makes a modern host call succeed; actual native
host/load/save adoption remains held.

Numerical compatibility: an owned Framework4/x86 probe passed17 controls. Native
Chi's uint selector binds LINQ Int64 Sum before final Int32 wrap; under-minimum
double-to-uint terms and Rate(double)'s NaN/infinity casts preserve original
conversion behavior. No rate, rank reward or balance retuning was performed.

Source-validation only: no deployment, original executable, DB write, routing
change, login attempt or game/service restart. Public packet controls use
constructed synthetic fixtures, not the private recorded-traffic fixtures.
Game assets, original saves and private operational records remain excluded.
Standing55% rough estimate (40–70%, low confidence, October19) is unchanged.

Next: review/integrate the genuine stat and player/map owner graphs, rerun
focused behavior checks, then qualify a new full Windows run when available.
