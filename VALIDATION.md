# Publication validation

## CI recovery — full Windows rerun passed

The registered self-hosted Windows runner was restarted with human approval;
queued Parity tracker39889055900 passed. Only its three generated tracker files
changed inbdd40e4a. Fresh full Windows run39889787428 restored/built successfully,
then failed two core expectations:19670 passed,2 failed,165 unexecuted of19837.
Updater262/262 passed; launcher/parser stages were not reached after test exit1.

Source review confirms the later native public RandFromGivingNums method and the
2311 outbound serializer without an inbound receiver. Only those two tests were
updated: original checks retained, exact new method signature and outbound/
inbound diagnostic assertions added. Production and skip behavior are unchanged.

Focused sanitized-public API/selection/dispatch checks passed309/309 with the
normal measured PS7 probe binding, zero failures/skips. An initial direct run
passed301/309 with8 environment-binding failures; no production fix was needed.

[Fresh full Windows CI39890690396](https://github.com/vgcman16/wolfconquer/actions/runs/39890690396)
passed all eight stages on source commitf29841d59315c79a40c8d8b00b7888849ca9e525.
Core19672 passed,0 failed,165 unexecuted of19837; updater262/262 and launcher49/49
passed. Status-parser20 and smoke-parser13 synthetic cases passed; these do not
probe a live updater service, and HTTP/service checks were skipped. The registered
runner remains online. This proves Windows CI, not gameplay/deployment/parity.


## Native solo arena load/reset and genuine instance graph

Private and independent sanitized-public Release each passed2824/2824, zero
failures/skips:36 new arena-load/match/rank cases +7 parent rank-evaluation controls
and2781 retained. Whole ArenaTable.Load/Reset, actual arena/match constructors,
three match queries, both instance rank builders and tournament contracts are in.

Initial private2816/2817 exposed a genuine runtime difference: net9 can skip the
OrderBy key selector for one entry. Owned Framework4/x86 default and optimized
probes each passed18/18 controls proving once-per-entry source-order reads, stable
ties and singleton null faults. A scoped arena ordering translation precomputes
keys once then stably sorts indices, without rereading mutable Info. Both instance
and existing solo/team static rank methods keep original publication/reset order.
Private rerun and first sanitized-public run passed all2824 selected cases.

Arena scheduler references remain default-null; no Create or fallback allocator
runs. Success fixtures supply an already constructed genuine Arena; null fixtures
prove row/reset mutation before native faults. Team load/reset/matches, solo arena
matchmaking/watch/win/teleport, full Team/HP/Inventory/login/SaveClient and host
adoption remain held. No actual-client/SQL/full Windows/deployment/parity claim.


## Native arena records and map-spawn families

Private Release and independent sanitized-public Release each passed2781/2781,
zero failures/skips:134 new cases (52 arena +82 map-spawn) and2647 retained.
This covers complete solo/team arena record/save/rank/point/packet families,
whole monster-family DB/directory/text loaders, complete Server/GameMap dynamic
spawn helpers, remaining map item/NPC/static queries and entire StaticRole owner.
No loader or packet handler is registered into a host by this checkpoint.

First private run passed2774/2781. Its seven failures were fixture assumptions:
native concatenated paths retain doubled separators, and SafeRandom(0) selects
clock-based birth rather than Random(0). Production was unchanged; corrected
fixtures assert raw plus canonical path identity and use32/128 nonzero seeds.
Private rerun and first public run passed all2781. Actual Core.GetAngle also
matches all16723 owned controls and independent digest2201143353.

Arena handlers retain native private visibility with test-only reflection.
Source quirks include duplicate floor-role aliases, shadowed map/family IDs,
rank aliases, malformed-row/fault prefixes and raw Save paths. All temporary-file
tests use isolated GUID-owned fixtures, not original maps, saves or database data.

Arena Load/Reset/matches, inventory grants, CanAdd/Booth admission, race receiver,
full combat/death/HP and eager login/SaveClient remain unfinished. No SQL/live
client/host startup/full Windows/deployment or overall parity claim follows.


## Native monster targeting and movement

Private Release and independent sanitized-public Release each passed2647/2647,
zero failures/skips:77 new cases and2570 retained, first run in each copy.
Whole CheckTarget/SearchTarget/CheckGuardPosition exercise actual nonempty target
acquisition, pursuit, blocked-step fallback, cell/view publication and fault state.
Full native walk serialization and monster animation chunk sends use real owners.

The actual modern GetAngle matches the literal16705-vector grid/boundary digest
2201143353 plus observed axes/diagonals. Root-owned source-shaped Framework4/x86
default and optimized probes passed16723 controls each before admitting the scoped
bounded byte(int(expression)) translation. This does not validate other casts.
All original Atan2/Floor order, unknown directions and coordinate wrap remain.

Whole AI dispatch/attack/player damage/HP/death/Receive, PoolProcesses and player
Movement remain held. Existing SystemRandom default birth has not been newly
accepted for held PhysicalAttack. No scheduler/receiver registration, actual-client
AI/PvE, SQL, host startup, full Windows, deployment or parity acceptance follows.

## Native complete lazy world population and monster prerequisites

Private Release and independent sanitized-public Release each passed2570/2570,
zero failures/skips:372 new cases and2198 retained. Whole EnterMap/CheckMap and
real lazy map lookup load actual NPC/SobNpc/floor/monster actors, portals and traps.
Tests exercise all32 native monster-file cases with nonempty real Family.Copy/Add,
all remaps and actual collision, counter, cache, packet and failure-prefix effects.

Complete monster family/generator/view/spawn/flags/respawn/placement/admission,
floor role/codecs, NPC map-interface adoption, statue lifecycle and owned Save/Load,
both NPC removers, player distances and native wallet/dialog leaves are included.
The private first compile exposed missing Player distance methods; all three whole
methods and12 boundary cases were added. A subsequent2569-pass/1-fail result came
from an older test forbidding NPC interface adoption. It now checks the genuine
same-object interface aliases while retaining every factory/default assertion.
Final private and first sanitized-public runs passed all2570 selected cases.

All file fixtures are owned GUID temporary files; no original assets, maps, saves
or database files are opened. Monster-family DB startup remains absent; tests seed
the actual isolated native catalog. Ordinary loot selection/AlterItemLevel/boss
generation retain original CRT calls but lack deterministic path tests here.

Full AI/combat/death/loot/receive, SobNpc tournament death, Booth client admission,
HP/Team/Level/Class/Inventory and eager login/full SaveClient remain unfinished.
Full ConquerPoints is still held on its eager logger; other tested wallet leaves
do not replace it. No actual-client, SQL, host startup, full Windows, deployment
or overall parity acceptance follows from this focused offline checkpoint.

## Native combat effects and health publication

Private Release and independent sanitized-public Release each passed2198/2198,
zero failures/skips:87 new cases and2111 retained, first run in each copy.
Whole blue-damage/refinement writers, eight Perfection methods, all fourteen native
XP-skill priorities, auxiliary shield/stigma executors, durability methods and the
complete native SendUpdateHP/NotLife property use actual native owners.

Literal packet expectations cover signed damage widths and cumulative MaxHP then
MaxHP+HP updates. Tests cover real RNG draw counts, existing flag retention, guard
stamps before faults, suppressed transport and restored fixture global state.
The original NotLife setter throws; DrainingTouch and durability wear bodies really
are empty. No native callers were found for auxiliary shield/stigma, so these are
not represented as additional completed main-dispatch owners.

Full CheckEffects/BloodSpawn/StraightLife, writable HP/Team/death/revive and general
attack dispatch remain unfinished. No actual-client, SQL, host, full Windows,
deployment or overall parity acceptance follows from this focused checkpoint.

## Native terrain maps, housing and reward records

Private Release and independent sanitized-public Release each passed2111/2111,
zero failures/skips:195 new cases and1916 retained, first run in each copy.
Whole terrain index/map loaders, actual map membership and collision/placement/
coordinate families, genuine map dictionary storage and monster collection binding
reuse real owners. Full housing, flowers, hero rewards and activity record families
use genuine INI/BinaryFile/packet operations over owned GUID fixtures.

Tests retain duplicate-publication aliases, stale map recipient caches, exact NPC
28-byte records and zero-born versus factory-created NPC state, flower rank/icon
quirks, full spawn reload broadcasts and the original byte-based activity reload.
No original client/maps/database/save files are opened by these fixture tests.

Whole lazy EnterMap/world population, NPC lifecycle/interface, full SaveClient and
eager login remain separate dependencies, alongside HP/Team/Level/equipment/
Inventory cycles. This is not SQL, host startup, actual-client, full Windows,
deployment or overall parity acceptance.

## Native quest objectives, binary records and relic selection

Private Release and independent sanitized-public Release each passed1916/1916,
zero failures/skips:168 new cases and1748 retained, first run in each copy.
Whole quest queries/objective mutations and direct auto-patching use actual
native owners and full packet sends. Complete RoleQuests Save/Load use real
BinaryFile over owned GUID fixtures; no original player saves are opened.

Source alias distinctions, objective-array growth/wrap, duplicate-key publication
prefixes and ignored read results remain. Original Load has no Close; test-only
cleanup closes only its own opened file, not a fabricated production fix.
Shared-RNG relic selection retains every redraw and all five rejection families.
Login/server-flag metadata does not install a login route or eager worker.

Successful inventory grants/update/publication, ExecuteLogin/static queue, full
SaveClient/login, quest awards/dialogs and Level/Class cycles remain unfinished.
No SQL, host, live-client, full Windows, deployment or parity acceptance follows.

## Native spell training, restoration and attack prerequisites

Private Release and independent sanitized-public Release each passed1748/1748,
zero failures/skips:123 new component cases and1625 retained. Whole UpdateSpell
and four mana/stamina/shield executors use real mutable spell/proficiency/status/
player owners and actual packet queues. Complete Calculate.Base math/random/
geometry methods, attributed interaction reads and auto-attack copying are included.

The initial1747-pass/1-fail result exposed an incorrect test expectation: whole
CanUseSpell catches a missing status owner and rejects before ShieldBlock changes
damage. Only the test was corrected; no admission guard/catch was weakened.
New source files have no blanket nullable suppression. Public existing-owner
changes are narrow reviewed attribute/partial-declaration edits.

Scalar conversion tests retain32 source-shaped rows independently observed in
default and optimized owned Framework4/x86 probes (256 controls each). Scope is
only the specified restoration/shield cast sites, not other attack expressions.
Shield status tests preserve native append-at-current-cursor behavior.

The whole ProcescMagic dispatcher is still held:67 labels map to64 switch
callees, of which62 remain missing after these two switch owners. Other genuine
combat-gear dispatch, targeting, receive/death, Team/HP/level and host dependencies
remain. These focused results are not full combat, SQL, live-client, deployment,
full Windows or project-parity acceptance.

## Native equipment scoring, rank files and complete status components

Private Release and independent sanitized-public Release each passed1625/1625,
zero failures/skips:201 new cases and1424 retained. This covers whole item
scoring/classification, native rank mutation and owned binary persistence,
CombatGear attribute/status methods, both complete status/server-info codecs and
genuine physical transform/weapon state. No class/status branch was removed.

Modern production methods match21 source-shaped arithmetic rows observed in
both default and optimized owned Framework4/x86 probes. All256 byte-level costs
also match, retaining their native rounding anomalies. This is scoped evidence,
not a general floating-cast guarantee or execution of original game binaries.

Private compilation first exposed the wrong existing classifier owner name.
The next1598-pass/21-fail run exposed a parent fixture disabling the actual value
table; fixing setup retained all production formulas. Public compilation then
exposed an older missing GetValueRank method; its whole source body was reviewed/
imported, with six class/gate controls passing in private and public copies.
Existing public synthetic fixtures and dependency differences are preserved.

Full equipment rebuilding, transform lifecycle, Team/HP/level/inventory/save/
attack cycles and native host adoption remain held. These focused component
results do not establish SQL, startup, live-client, deployment or full Windows
acceptance, and do not change the standing rough project-parity estimate.

## Native mentor sharing and complete social records

Private Release and independent sanitized-public Release each passed1424/1424,
zero failures/skips:88 new native TutorInfo/share/Associate/packet/persistence
cases and1336 retained. Whole nonempty mentor/apprentice publication and all
friend/enemy/partner lifecycle branches are exercised through genuine owners.
Owned-file Save/Load verifies native count headers, field widths, overwrite,
aliases and existing-state retention without touching original player saves.

The source's SetMentorBattlePowers body really is empty; it remains empty and
does not establish a working mentor BP bonus. PKPoints has genuine read access
only until its entire status chain is ready. No fake writable property or
implicit player/record rebinding was added to admit these complete methods.

Native interaction handlers, Team/HP/level/equipment cycles, startup/login/save
and live-host adoption remain unfinished. Focused component proof is not actual
client, deployment, full Windows or full gameplay-parity acceptance.

## Native VIP publication and experience calculations

Private Release and independent sanitized-public Release each passed1336/1336,
zero failures/skips:125 new VIP/state/codec/experience-calculation cases and1211
retained. Owned Framework4/x86 default and optimized probes each passed22 cast
controls and640 source-shaped expression comparisons. These are owned arithmetic
observations, not execution of original game binaries.

The initial private compile found a synchronous test declared as Task; a later
1328-pass/8-fail run exposed wrong expectations of a writer reset. Source
SendUpdate appends at the actual incoming cursor. Corrected tests assert its
dirty prefix, packet order and subsequent cursor reuse; production was unchanged.
Public rerun passes independently; synthetic public fixtures remain preserved.

Actual experience granting, the whole native Level setter and its inventory/
save/equipment/attack dependencies remain unfinished. No native login/save/host
adoption, full Windows acceptance or live-client proof follows from this result.

## Native player/map contract and complete achievement receiver

Private Release and independent sanitized-public Release each passed1211/1211,
zero failures/skips:104 new player/map/BP/Union/Poker cases,8 formerly held whole
achievement-receiver cases,1076 retained and23 adjacent existing owner cases.
Both complete native spawn branches use genuine state owners and protobuf
metadata; map eligibility is real, not a fake object or modern spawn projection.
Achievement covers raw map lookup and flag-queue actions without host adoption.

Private compilation exposed an old null factory call made ambiguous by the new
native overload; the test now explicitly requests its original Guild-member type.
Initial public compilation exposed an omitted inherited combat-gear GetEffect;
the complete native method was reviewed/imported with literal class shapes/null
behavior. Independent public rerun passes. All failed compile observations remain
local. Existing synthetic packet fixtures and publication-specific edits remain.

HP exposes only genuine backing/read access until the whole Team/HP/level cycle
can be admitted. Native save/login/startup/host adoption and live rendering are
still unfinished; the modern achievement route honestly logs this gap without
fabricating a native player or reply. No live process or route was changed.

## Native JiangHu and whole prestige-points rebuilding

Private Release and independent sanitized-public Release each passed1076/1076,
zero failures/skips:86 new JiangHu/table/rank/stat/training/roll/record/codec
cases,56 new whole CreatePrestigePoints cases,934 retained. Owned Framework4/x86
default and optimized numeric controls each passed10/10 for finite time and
aligned-power narrowing. Original game binaries, private data and databases are
not required; record tests use bounded test-owned files with exact-byte cleanup.

Whole prestige rebuilding preserves both item loops, the source's two Items[1]
wardrobe reads, configured server identity and rank-before-perfection-before-guild
side effects, including real late metadata failure. An initial duplicate ServerID
declaration failed compilation; only that new duplicate was removed, retaining
the existing native physical identity field and predicate. No identity is inferred.

JiangHu activation/login/apply-star and economic receivers, equipment/Team/HP/
level rebuilding, native startup/login/save/host adoption and actual-client
rendering remain open. The two admitted component receivers are unregistered.
Focused component success does not establish full Windows or gameplay acceptance.

## Native Team/aura prerequisites and stat calculations

Private Release and independent sanitized-public Release each passed934/934,
zero failures/skips:72 new Team/leadership/flag-icon/aura/Associate-state cases,
62 new whole native HP/mana formula and guild-skill-table cases,800 retained.
Owned source-shaped HP arithmetic observations match18 formula values under
both default and optimized Framework4/x86;10 finite cast controls also pass.
Aura conversion is scoped to the separately observed source expression families.

Initial private compilation exposed a missing test namespace. The next913-pass/
21-fail result retained correct packet bytes but expected the wrong post-Finalize
cursor. Native Finalize writes the header and leaves Position4; tests now assert
that, retaining size/bytes and later read-cursor checks. No serializer behavior
was weakened. All rejections remain local. Public fixtures remain synthetic.

Full Team/HP/level/equipment rebuilding, native save/login/startup/host adoption
and actual-client rendering remain open. No live game or service was restarted,
and these focused results do not qualify the interrupted full Windows run.

## Native Prestige ranking and Perfection effects

Private Release and independent sanitized-public Release each passed800/800,
zero failures/skips:235 new cases plus565 retained. Whole native rank buckets,
ordering/alias behavior, queries, record/file persistence, appearance/protobuf
conversion and all Perfection thresholds/Rune additions are covered. Two fixture
isolation controls preserve real catalog/rank/RNG owners across nested scopes.

The initial private793-pass/7-fail receipt remains local: missing metadata faulted
at the genuine item setter during fixture construction, before the method under
test. Setup now constructs with metadata, removes it, then asserts the real
Perfection fault and preceding state. No production behavior was weakened.
Full CreatePrestigePoints, equipment rebuilding, login/save/startup/host adoption
and live-client acceptance remain open. No original executable or private data is
needed by this synthetic/owned component selection; full Windows is not accepted.

## Native InnerPower and Rune stat prerequisites

Private Release and independent sanitized-public Release each passed565/565,
zero failures/skips:60 new complete InnerPower/table/INI/record/rank/potency/
equipment cases,131 new Rune/table/load/plus-points/score/perfection/slot cases,
and374 retained. All five real Win32 INI controls executed on Windows. Native
array order, null defaults, integer truncation/overflow, prefix-on-fault state,
same-object caches, actual spell/packet effects and owned file round-trips remain
tested without a database, client, original executable or ignored game fixture.

Full native equipment rebuild, Rune mutation/receive actions, Team/HP lifecycle,
SaveClient/LoadCharacter/startup/login and host adoption remain open. The full
Windows run interruption below remains unqualified; these focused passes do not
prove deployment, English-client completion or a higher parity percentage.

## Native Chi, achievement state and equipment prerequisites

Private Release and independently built sanitized-public Release both passed374/
374 focused checks,0 failures/skips:140 Chi/shared-RNG/records/Fate/packet cases,
58 achievement component cases,130 equipment/table/stat/appearance cases,2 host/
evidence controls and44 retained source-silent cases. No private game seed is
required. The short checklist is in
[Native save/equipment status](docs/checklists/NATIVE-SAVE-EQUIPMENT-20261022.md).

Achievement2307 has a real native receiver; the previous source-silent claim was
wrong. It now produces an explicit unsupported host-adoption diagnostic, with no
reply or fabricated owner.2311 remains without an inbound receiver, but its
evidence now correctly names the unsent native ttp serializer. Opcode alias2307
is retained for source compatibility.

Initial private compilation exposed the absent genuine Player IMapObj/GetArray
graph. The whole achievement receiver and eight receiver cases remain held,
not reduced to a successful SendQueue-only path. A test namespace typo and stale
reflection assertion were corrected; v2's373-pass/1-fail receipt remains local.
Complete native Chi ranking/load, full Equipment.QueryEquipment/UpdateStats,
Inventory mutations, native save/login/startup and host adoption remain open.
No live-client, English-client, deployment or full-parity acceptance follows.

Publication-specific packet fixtures remain synthetic. A follow-up restored the
six constructed round-trip cases and synthetic2369 zero-suffix assertion, and
uses constructed achievement requests in both rejection/host-gap controls. The
same focused Release selection passed374/374,0 failures/skips again. Private
recorded-traffic fixtures and operational restore metadata are not imported.

## Interrupted full Windows run on21723558

Run39875129428 on21723558d2b582f7272d54a22a1560bf14c36314 ended failure at
2026-10-23T00:10:12Z. The runner was observed offline and its local worker/test
processes absent. Receipt20261022T235827741Z-4359532b remains unfinished after
successful restore/build, with no completed test TRX or later-stage result.
The GitHub job log was unavailable. Do not infer a gameplay/test failure cause
or a successful full check from this interrupted observation. No runner/game/
service restart or replacement workflow dispatch was performed.

## Latest full Windows rejection on public 9a9cbc7c

Run39867954370 on9a9cbc7ce9e973c0c3e85df8fb47fcb19de4a0d8 failed;
receipt20261022T225243652Z-2386c7ad finished2026-10-22T22:57:29.8954864Z.
Restore/build passed. Core16,854passed/3failed/165skipped(17,022total); updater
262/262passed. Launcher/parser stages did not run. Original TRXs/logs retained.

Earlier repaired Reviver/MagicPray and updater cases pass in this full run.
Three older tests failed: scalar-save worker readiness, quest-save destination
contention, and TCP diagnostic error-log observation. Investigation identifies
test scheduling/observation dependencies; repairs are under review without
changing production retry budgets, removing assertions or accepting save failure.
Current source is NOT full-check green, deployed, or actual-client accepted.

## Snapshot concurrency and contention test repairs

Private Debug and independent public Release each passed2,135focused cases,
zero failures/skips,20s and18s: the retained2,076plus all six persistence-consumer
test classes and three real Windows observer controls. Equivalent-root writers
now use owned threads, checked gates and unconditional drain. Contention tests
still invoke one actual persistence operation under a real destination sharing
lock and require failed-replace temporary deletion before release; the Windows
observer uses owned native directory IO rather than ThreadPool callbacks.
Cancellation is scoped to the retained directory/OVERLAPPED, drained before
buffers/handles are released. Tests cover matching events, timeout cleanup and
independent observer cancellation. All original saved-byte/encoding/alias/no-temp
assertions remain; production locks and five-attempt retry budgets are unchanged.

The first private attempt failed to compile a test-project-specific WindowsFact
name. A dedicated platform attribute fixed that setup; all three Windows controls
executed in the passing runs with zero skips. The full9a9cbc7c failed receipts
remain preserved. A new full Windows run is still required for these repairs.

## Native pipe item transport and inventory queries

Private Debug and independent public Release each passed2,076focused cases,
zero failures/skips,29s and30s:58new native pipe/query cases,2,010retained,
and8TCPcases. Complete native pipe instance storage/constructors/Send/Disconnect,
socket client reference, full item-to-pipe send and three stack/meteor queries
are implemented. Actual queue copies/cipher continuation, item-before-extra bytes,
raw interserver refinery IDs, liveness/fault prefixes and same-item aliases are
covered with owned buffers/objects only. Socket Create/connect/pump and full
PipeClient static login/transfer/receive lifecycle remain absent and unwired.

The TCP test now waits for the real final error writer before retaining all old
diagnostic assertions. Production TCP logging is unchanged. Full Inventory.Add/
Update, genuine SQL/login/save worker integration, host adoption and live-client
acceptance remain unfinished; the latest full Windows rejection still applies.

## Portable receiver and updater fixture repairs

Private Debug and independent sanitized-public Release each passed all2,010
focused core cases, zero failures/skips,28s and29s respectively, after replacing
MagicPray's ignored private seed dependency with exact bounded native Pray1100
and Island1015 metadata. All original real-handler/receiver-swap/HP/order/state
assertions remain, with added row/map identity, learned-level and mana-cost checks.
Reviver uses test-only scheduling of the actual player/global timer callbacks;
raw frames and the original HP assertions are not filtered or relaxed.

The full public updater check passed262/262,zero failures/skips,1m18s. Three new
real owned-command controls verify per-fixture executable identity and genuine
process-guard/recovery behavior. Production runtime and guard code are unchanged.
The subsequent full Windows rejection above remains authoritative; no live-client
proof is added by these focused passes.

### Preserved first public repair rejection

The reviewed test-only updater isolation repair passed all262 public Release
tests, zero failures/skips,1m18s (updater-layout-isolation-public-v1.trx).
Production process attribution remains fail-closed and unchanged.

The paired core repair check ended2,008passed/2failed,zero skips,18s
(runtime-frame-stability-public-v1.trx). Both MagicPray visibility cases fail
before handler execution because an ignored private seed manifest is absent.
This is a test-portability failure, not evidence of a MagicPray gameplay defect.
The bounded source-backed correction above resolves this focused rejection;
the original failed TRX remains. No new full Windows acceptance is claimed yet.

## Latest full Windows rejection on public 0a06f4ad

Run 39864294474 failed on 0a06f4ade3e2b426a9dc2acdb699b821807253c1.
Receipt 20261022T222228209Z-68a244b7 ended 2026-10-22T22:25:31.696509Z:
restore/build passed; core 16,808 passed, one failed, 166 skipped (16,975 total);
updates 258 passed, one failed. Later launcher/parser stages did not run.

The Flag6 reviver test observed an XP-circle packet between expected HP updates.
Source review identifies two independent runtime tick producers; deterministic
test scheduling is under repair without filtering frames or weakening HP checks.
The real journal-recovery test reached its final layout step, then the process
guard correctly refused unavailable same-basename image attribution. Its fixed
test executable name overlaps separately launched pipeline fixtures. Exact failed
PID is not recorded; fixture-name isolation and real guard controls are pending.

Original failed receipts remain preserved. Current source is NOT full-check green;
focused native passes do not erase either failure or establish client/deployment proof.

## Complete native NPC replay and level-storage prerequisites

Both NPC replay codecs now use the complete native seven-field query, 619-name
NPC identifier catalog and 16 NPC types. Byte-reader versus ushort-writer action
asymmetry, ignored Stamp, native string boundaries and all aliases remain.
Three plain Player level/class-history fields and the genuine two-argument RNG
rate method are implemented; full Level/Class setters and attack dispatch are not.

Private Debug and independent public Release each passed 2,006 focused cases,
zero failures/skips: 25 new NPC replay and 21 field/RNG cases with 1,960 retained. Initialized
owned buffers, actual shared RNG ordering (including the pre-fault draw), exact
queue-free payloads and physical query layout are tested. No startup/host route,
live leveling, inventory grant or native login/save adoption is established.

## Complete native dialogs and level-experience table loading

Full native Dialog methods, NpcReply declarations and both codecs, static-message
declarations/primitives, the genuine mutable LevelInfo field and whole LoadExpInfo
are implemented. NPC replay currently supplies only its exact Mode enum; the
full replay/query family is separate. Full Player.MessageBox/Quests/GainExpBall/
UpdateLevel and host/startup/receiver adoption remain unfinished.

Private Debug and independent public Release each passed 1,960 focused cases,
zero failures/skips: 22 new dialog/wire and 27 new level-table cases with 1,911 retained.
Checks cover real queue copies, exact payloads/seal/defaults, safe initialized
packet truncation/length-wrap controls, literal paths/culture/BOM, add-only rows,
parse/fault prefixes and actual file-reader disposal, using only owned fixtures.

Dialog.Exists is signature-checked only, NEVER executed, including null input.
It retains the native SQL reader/builder whose identity MySqlEscape is unsafe for
untrusted input. No host exposes it; a separate parameterized boundary is required
before exposure. No original database/files/services were accessed or changed.
These proofs do not establish deployed quests, actual-client acceptance or full parity.

## Complete native nobility components

The complete Nobility owner and ranking, real Player/member rank propagation and
gender property, icon/broadcast methods, DaysNobility paid-state owner, mutable
Pool ranking and full character-file table loader are implemented. Native ranking
thresholds, strict admission/cap, physical aliases/ties, stale sampled thresholds,
icon-before-promotion order, date/state/fault prefixes and differing gender rules
remain intact. Paid Duke deliberately sets position 15 (therefore Prince).

Private Debug and independent public Release each passed 1,911 focused cases,
zero failures/skips: 68 owner, six message and 21 paid-state/table cases new,
with 1,816 retained.
Tests use genuine queues/records and bounded GUID-owned Windows INI files, with
actual RNG/pool/recycler/culture restoration. No original Users files or database
are read, no socket pump runs, and no live account receives a paid rank.

Full startup, login/save/expiry and receive integration remain absent. Component
proof is not a deployed feature, current-client acceptance or a new parity estimate.

## Complete native quest definitions and message primitives

The complete QuestInfo owner (physical rows, INI loader, eligibility and NPC
queries), quest-list declarations and all seven native quest message primitives
are implemented without installing startup or receive dispatch. Native duplicate
publication, partial parse/fault state, profession normalization, reserved-list
reader behavior, ignored arguments and special mission payload tails remain.

Private Debug and independent public Release each passed 1,816 focused cases,
zero failures/skips: 29 new metadata and 19 new packet cases with 1,768 retained.
Tests use GUID-owned UTF16 INI files, actual Windows INI reads, initialized real
packet buffers, literal byte controls and exact global restoration. They do not
execute original assemblies or touch original saves. Unsafe oversized/wrapping
buffer loops and file-fault resource cleanup are not established by these cases.

Full quest gameplay, experience and guarded message-box dependencies, rewards,
native receive dispatch and complete login/save callers remain unfinished. This
does not establish a new live-client milestone or revised parity estimate.

## Full Windows check on public b7c182a6

Run 39860953525 completed successfully on b7c182a6ababe953a0b9ae09e898cc43939c5286,
receipt runId 20261022T215514515Z-fe7f24d9. All eight stages passed. Core tests:
16,617 passed, 166 skipped, zero failed (16,783 total). Updater tests: 259 passed.
Windows launcher: 49 passed. Synthetic receipt parsers: 20 and 13 cases passed.
Solution build: 111 warnings, zero errors; launcher build: zero warnings/errors.
The 166 skips remain unverified, not passes. This validates that earlier published
snapshot, not later quest/nobility changes, deployment or actual-client behavior.

## Genuine BinaryFile and complete mail components

Complete native BinaryFile, mail save/load, physical records/conversions,
contracts, primitive codecs, list paging, notifications and the unwired list
receiver/member slot now use genuine file, dictionary, packet and queue owners.
Mail claim/reward handlers and full LoadCharacter/SaveClient adoption remain absent.

Private Debug and independent public Release each passed 1,768 focused cases,
zero failures/skips: 39 BinaryFile and 54 mail cases new, with 1,675 retained.
Actual owned-file save/reload, live physical rows, reused-record tails, expiry,
duplicate overwrite/no-clear, list ordering/extra-empty-page behavior, byte-exact
queues and mutation/fault prefixes are covered.

The literal file wrapper first failed four advancement cases: repeated .NET9
SafeFileHandle access resets the kernel position, unlike the source's Framework
runtime. An owned source-shaped comparison reproduced rereads and overwritten
writes. One lazy genuine handle per Open, shared by Position/Read/Write, repairs
this while retaining negative/product/lifetime faults. Eight added lifetime cases
exposed one incorrect private-cache test expectation after disposed access; it
now asserts the actual closed handle, keeping all public disposed-fault checks.
Native BinaryFile has no cache field; no native behavior assertion was relaxed.

Owned Framework/x86 and .NET9/AnyCPU record probes agree on the 744-byte layout,
all offsets and finite string/tail/timer cases on this machine's ACP1252. Different
Encoding.Default values are not substituted for the actual native ANSI decoding.
Unsafe overlength/unterminated buffers, other code pages and throwing opened-file
cleanup paths remain unproven. No production test hooks or cleanup facade added.

No original game files, saves, assemblies, routes or services were executed or
changed. These component proofs do not establish gameplay, client acceptance,
deployment, full login/save integration or a new full Windows CI result.

## Complete native HundredWeapons component graph

The complete native HundredWeapons owner, physical weapon rows, metadata loader,
HWRank and Opt/Info contracts/producers now use actual spell, bitmap, rank, clock
and packet owners. All owner methods, ten rank buckets, fourteen-flag priority,
twenty-one-column rank records, record loading and live metadata reads are kept.
The conditional member-birth slot is explicit and unwired; receive handlers and
full startup/login/save callers are not replaced with facades.

Private Debug and independent public Release each passed 1,675 focused cases,
zero failures/skips: 71 owner, 48 rank, 23 message and 77 held-serializer cases
new, with 1,456 retained. Actual queue bytes, owned-file reloads, skill/rank fault
prefixes, signed clock gates and all rate-table bounds are covered. The owned
Framework/x86 probe was parent-read, rebuilt and run against the pinned serializer:
68 payloads and nine expected non-Flags enum rejections, zero unexpected outcomes.

The native Framework clear loop throws after its first successful removal,
including the last entry. A bounded own-method compatibility bridge retains that
state/failure order instead of silently clearing all entries under .NET9. General
concurrent external dictionary mutation equivalence is not claimed.

The first private run had 1,462 cascading registrar-setup failures: the new wire
test class lacked the native serial collection. Correcting that assignment fixed
the run without relaxing registrar or behavior assertions. Shared RNG is scoped
and restored around real test Player birth. No further production repair was
needed after the source-reviewed clear compatibility change.

These are component proofs, not current-client acceptance, deployment, a new full
Windows CI result or complete login/save integration. The parity estimate is unchanged.

## Genuine native Time32 clock and scheduling

The complete Time32 type was recovered read-only from the actual preserved
GameServer project's referenced SystemExtensions dependency. Five modular files
retain signed four-byte state, unchecked arithmetic, all add/all/scheduling
methods, exact optional defaults, current-culture text, the reached empty Now
prelude and original winmm struct-return clock. Unrelated uncalled dependency
code is excluded; no original game assembly was invoked or mock clock inserted.

Private Debug and independent public Release each passed 1,456 focused cases,
zero failures/skips: 52 new clock/model/arithmetic cases and 1,404 retained.
Independent real Windows OS tick brackets validate both the native struct-return
P/Invoke and Now, plus default-zero-time scheduling paths, under .NET9. No
production behavior repair or assertion weakening was needed. The native unused
field remains present and its compiler warning remains visible, not suppressed.

This closes a genuine dependency, not the full HundredWeapons graph, full
login/save callers, host wiring, current-client acceptance, deployment or a new
full Windows CI run. The low-confidence parity estimate remains unchanged.

## Native Ninja Info contracts and primitive codecs

The complete native Info record, signed Flags values, byte printer, packet2394
constant and primitive Create/Get codecs are now ported. Required field widths,
unknown signed enum values, exact newline/space output, mutable re-encoding,
declared-length framing and decoder failure order retain the reference behavior.
The Info.Process equipment/inventory/currency graph remains absent rather than
being represented by a partial receiver or placeholder owners.

Private Debug and independent public Release each passed 1,404 focused cases,
zero failures/skips: 24 independent behavior cases, nine pinned wire controls
and 1,371 retained. The nine wire controls also passed against the held native
protobuf serializer in an owned source-shaped Framework/x86 executable. No
production repair or assertion weakening was needed.

This is component and serializer proof only. It does not add receive dispatch,
prove current-client packet acceptance, deploy services, complete full login/save
callers or replace the last full Windows CI result. The parity estimate is not
increased from a passing test count.

## Complete native Ninja item-view receiver body

The complete private MsgNinjaItem.Process body now decodes, checks View by
equality, reads the actual nullable/unpublished GamePoll and calls the genuine
item producer before the user-profile producer. Current peer UID, MyNinja and
metadata are used without invented map/class gates, cached peers or fake owners.

Private Debug and independent public Release each passed 1,371 focused cases,
zero failures/skips: 20 new receiver cases and 1,351 retained. Unknown modes,
missing keys, current catalog replacement, physical UID-versus-key, foreign
backlinks, exact response order and decode/identity/producer fault prefixes are
covered. The old structural assertion that Process was absent was deliberately
updated to its actual private-static signature and lack of Packet attributes;
behavior assertions were not weakened. No production repair was needed.

No Packet attribute, dispatcher registration or GamePoll startup publication is
added. This is native body proof, not current-client opcode acceptance, a live
route, deployment, new full Windows CI or full SaveClient/login proof. The Info
receiver/equipment/inventory/currency graph remains unfinished.

## Complete native Ninja owner method family

The remaining Item.UPDATE, AddItem, GetSkill and SageMode methods and genuine
Role.Core.Rate(int) are now ported. UPDATE is actually empty in the reference and
is explicitly documented as such; it is not a replacement for equipment logic.
AddItem retains its native level clamp, item send before dictionary publication
and late rank update. GetSkill uses real current spell metadata and the shared
Pool RNG; it does not reuse the unrelated attack RNG or a forced-result provider.

Private Debug and independent public Release each passed 1,351 focused cases,
zero failures/skips: 11 new item cases, 31 skill/shared-RNG cases, 19 SageMode
cases and 1,290 retained. Actual queued bytes, overwrite aliases, mutation/fault
prefixes, strict RNG draw order, timed flags, duplicate nonrenewal, full-width
archive timers and view-fault ordering are covered. No production repair or
assertion weakening was needed.

SageMode retains the source's undisposed second recycled wrapper. Its real empty
map and view-fault paths are covered; physical peer-player broadcast is not
proven, and no fake MapObj adapter was introduced. These component proofs do not
complete MsgNinja Info/receive dispatch, equipment recalculation, full startup/
login/save callers, host birth or skill consumers. No deployment, current-client
acceptance, new full Windows CI result or revised parity estimate is claimed.

## Genuine native Ninja ranking and complete loading components

The complete native NinjaRank owner now includes physical entries/appearance,
19-flag priority, conversion, actual safe-dictionary catalog, sorting/cap/query/
removal and genuine 21-column Load/Save. The full native rank-display contract
and both2054 primitive codecs are present. Ninja UpdateRank, Alternate, GetLevel
and Loading use real ranks, metadata, spell storage, Player NiniaP0, user/item
producers and packet owners; both item producers and2393 codecs are complete.

Private Debug and independent public Release each passed 1,290 focused cases,
zero failures/skips: 51 new ranking/record/display cases, 52 loading/spell/item
cases, 18 complete wire/contract cases and 1,169 retained. Real owned-file save/
reload, cached-versus-live ranks, exact field widths, full Loading queue order,
badge thresholds and partial mutation/disposal faults are covered. Sixteen
source-shaped Framework/x86 held-serializer controls include Unicode, null rows,
null strings and unknown signed Flags. No production repair or assertion weakening
was needed.

Native quirks remain deliberate: Wind/Lightning require >1000 while three other
elements use >=1000; Wind's copied row retains Overall type. Low-score Ninjas
retain stale rows, the safe-dictionary cached array affects Save, GetLevel reads
the client's current MyNinja, and single-item messages omit UK while bulk includes
it. These are preserved reference behaviors, not newly invented fixes.

Full Server/startup, login/rebirth and receive/host callers remain absent.
Later complete Ninja owner proof is recorded above. Info handlers and equipment
recalculation remain separate. Owned fixture persistence is not SQL/original-save/restart proof. No
deployment, current-client acceptance, new full Windows CI result or revised
parity estimate is claimed. CodeFactor warnings remain a separate backlog.

## Complete native Ninja flags, points and user messages

All seven flag/mastery/point methods, the genuine 21-required-field user contract,
whole Create producer and native2395 encoder are implemented on the existing
Ninja, score, packet and recipient owners. No native user decoder exists, so none
is invented. Composite-mask any-overlap behavior, zero-mask sends, low32 mastery
narrowing, uint point wrap, UID0 notifications and supplied-UID loading remain
unchanged from source.

Private Debug and independent public Release each passed 1,169 focused cases,
zero failures/skips: 23 new mutation/producer cases, nine contract/primitive
controls and 1,137 retained. Eight source-shaped Framework/x86 held-serializer
controls cover defaults, high flag, max points, all widths, mixed loading and
null. Actual queues, take-before-mutation, late-send/early-metadata faults and
disposal are checked. No production repair or assertion weakening was needed.

Later complete Ninja owner and rank/loading component proof is recorded above.
Remaining Info/Process, startup and full SaveClient/login callers remain separate. No host/
current-client acceptance, original save/DB
write, deployment, new full Windows CI result or revised parity estimate is
claimed. Existing warnings and the CodeFactor backlog remain.

## Native Ninja profile scoring and complete metadata loading

The complete Score/six component getters, Levels, ToString/Load and two-table
NinjaFile.Load use the existing genuine Ninja, metadata, equipment and record
owners. Raw levels are not normalized through AddItem; saved Score is recomputed
on save and consumed but ignored on load, exactly as native.

Private Debug and independent public Release each passed 1,137 focused cases,
zero failures/skips: 32 new profile and 21 owned-file loader cases, plus 1,084
retained (including 25 newly selected older Ninja declaration/query controls).
Inclusive physical-ID bands, per-row element charges, uint wrapping, alternate
slot fallback, full 20-field prefix/five-field item rows, aliases/duplicates,
current culture and exact partial-fault publication are covered. No production
repair or assertion weakening was needed.

A separate fresh-process read-only check on .NET9.0.16 invoked the actual Load
with both native tables initially null: all 315 sigil rows/12 fields (3,780 values)
and ten immortal rows/13 fields (130 values) matched independent parsing. Both
ASCII input hashes stayed unchanged, and configuration/cultures were restored.
The native Score and UK5 share column11; immortal columns9..11 are ignored.
External inputs and the private probe are not repository assets; this does not
prove other legacy encodings or server startup.

Later flag/point-message and rank/loading component proof is recorded above.
Remaining item/skill/NPC mutation, startup publication and full
SaveClient/login callers remain separate. No host, current-client acceptance,
original save/database write, deployment, new full Windows CI result or revised
parity estimate is claimed. Warnings remain.

## Complete native AnimaSkin profile/storage/send components

The complete native AnimaSkin owner (state, constructor, equipped-skin getter,
add/remove, slash records and Loading), four-optional-field DragonSkin contract,
two primitive codecs, physical client member and explicit unwired constructor
slot are implemented. The held external IDictionary Remove method is also ported;
the previously recovered Add method is unchanged.

Private Debug and independent public Release each passed 1,059 focused cases,
zero failures/skips: 35 new and 1,024 retained. New coverage includes 13 record/
storage, ten actual queued-message/disposal cases, one explicit slot, eight
source-shaped Framework/x86 serializer/primitive controls, full contract checks,
fresh-out-before-fault and real dictionary-removal dispatch. No behavior repair
or assertion weakening was needed.

Zero-ID exclusion, first equipped physical value, duplicate/no-clear behavior,
mutable recipient, optional-default omission, unknown signed flags and removal
before a later scan fault remain native. Loading takes a recycled packet before
scanning, disposes it even on failure, and does not set the Load flag. The native
unused local remains visible as a warning; no warning suppression was added.

The inventory-dependent receiver, full SaveClient/login callers and production
birth remain absent. No current-client acceptance, original save/database change,
deployment, new full Windows CI result or revised parity estimate is claimed.

## Native subclass and guild-skill profile components

Fourteen complete subclass record/status/study/learning/send methods, genuine
Player state, metadata Load/GetDamage/AllowUpdate, four primitive codecs and the
complete guild-skill record owner/member/explicit unwired slot are implemented.

Private Debug and independent public Release each passed 1,024 focused cases,
zero failures/skips: 72 subclass cases, 12 guild-skill cases, six additional
AirPower multiplier controls and 934 retained cases. Metadata tests use real
Windows INI reads over owned UTF-16 fixtures covering all 70 rows, not a fake
metadata provider or a claim about preserved production-file encoding. Actual
record reloads, stat mutations, byte queues, reserved fields, cursor/disposal and
partial-fault ordering are covered.

An initial subclass run exposed 11 genuine modern-runtime saturation differences
against independently observed source-shaped Framework/x86 controls. Scoped
hash-term conversion preserves old low32 wrapping/large-term zero behavior;
the controls were not weakened. A later retained AirPower test rejected legal
2,501: native Next(750*m,1251*m) has an exclusive upper bound, so only the test
was corrected/expanded for multipliers 1/2/3. Production AirPower is unchanged.

Subclass SetPhrase/SetLevelss, equipment recalculation and receive Process remain
absent. Native count>=256 byte-counter wrap is not run in-process or silently
repaired. No full profile SaveClient/login caller, production birth, current-client
acceptance or deployment is claimed. Warnings remain; these focused runs are not
a new full Windows CI result or a revised parity estimate.

## Native exchange-shop profile and goods-table components

The complete native shop owner and all nine methods, both 13/11-field goods-table
loaders, three required message contracts and all five primitive codecs are
implemented using genuine records, metadata, shared RNG and packet owners.

Private Debug and independent public Release each passed 934 focused cases, zero
failures/skips: 66 new and 868 retained. Retained selection includes 20 older RNG
compatibility controls. New coverage comprises 40 storage/accounting/metadata/
selection/open cases, 14 actual preserved-serializer controls, a full contract
check, ten primitive codec cases and an explicit native constructor-slot check.
Six initial decoder tests hit the unchanged native packet stack-depth guard in
the test runner; moving only those tests to the established owned thread resolved
them without changing assertions or production behavior.

A separate fresh-process read-only probe on .NET 9.0.16 invoked both actual
loaders against preserved ASCII inputs: all 1,810 goods rows / 23,530 field values
and 63 extended rows / 693 values matched. Both file hashes were unchanged. The
goods input has 15 columns; the native owner reads its first 13 and ignores the
tail. External input files/probe are not public repository assets, and non-ASCII
legacy encodings are not proven by this check.

Cap-zero admission, uint overflow, physical key/ID drift, ordinary Dictionary.Add
partial faults, unbounded native stock selection, the goods_rec_id168 loading
gate and three-clock midnight-plus-60s arithmetic are retained. No buy/info
receivers, wallet/inventory transaction, full SaveClient/login/worker/host birth,
current-client acceptance or deployment is claimed. Warnings remain. These are
focused components, not a new full Windows CI run or revised parity estimate.

## Complete native collection profile and outgoing message components

Complete CollectionStorage fields/constructor/record Load/ToString and Load-send,
physical client member/Player property and explicit unwired constructor slot are
implemented. Complete required message fields, optional UserID, local-clock time
helpers and packet conversion preserve native arithmetic and disposal-before-return.

Private Debug and independent public Release each passed 848 focused cases, zero
failures/skips: 59 new and 789 retained. New coverage includes 39 record/send/time
cases, 18 independently observed Framework/x86 serializer controls, one complete
contract/default/enum check and one actual constructor-slot assignment. Queued
packet bytes, physical ID/key drift, default/partial records, fresh-owner reload,
retained state and null-fault order are covered without fake send/record providers.

Read-only decompilation of the preserved SystemExtensions dependency established
that its IDictionary Add overwrites via the indexer. The complete actual helper
is ported: duplicate collection keys overwrite, unlike ordinary Dictionary.Add.
Native INI caller buffer limits are not silently repaired by these record methods.

No collection receive Process, packet attributes, production caller/host birth,
full SaveClient/login/deletion integration, original-save/database change, current
client acceptance or deployment is claimed. Warnings remain, including a native
lowercase type name. This is focused component proof, not a new full Windows CI
result or a revised project-wide parity percentage.

## Native EpicTrojan profile state and persistence

All 17 physical fields and complete Reset/Save/Load methods retain the native
four mutable uint[2] arrays, scalar defaults, exact 20-token slash record and
trailing delimiter. Reset preserves array references and content. GhostReaver is
reset but intentionally omitted from save/load, matching the reference behavior.

Private Debug and independent sanitized-public Release each passed 789 focused
cases, zero failures/skips: 24 new and 765 retained in each run. Coverage includes
actual Player -> record -> fresh-Player reload, exact widths and current-culture
parsing, malformed/missing defaults, array alias/tail behavior and precise partial
state before null/short-array faults. Genuine existing DbLine owners are reused;
no alternate record provider or quest implementation was substituted.

These are component checks, not full SaveClient/login/deletion integration, quest
action support, worker/host birth, current-client acceptance or deployment. Original
saves, database, running client and services were not changed. Warnings remain;
this is not a new full Windows CI run or a revised overall parity percentage.

## Native title and secondary-password profile components

Complete title metadata loading, required protobuf fields/enums/codecs, five
Player title score/add/send/activate/remove methods and title/security record
helpers now use genuine native state and packet/record owners. Title qualification
and receive dispatch remain absent, not substituted by successful no-op handlers.

Private Debug and independent sanitized-public Release each passed765 focused
cases, zero failures/skips:64new/701retained. New coverage comprises39 metadata/
record/player/decoder,13 literal contract/frame,10 secondary-password record and
two actual title mutation -> record -> fresh-owner reload/send/remove flows.
Twelve independent Framework/x86 preserved-serializer title controls match,
including required zero fields, unknown enum sign extension and nested/null forms.

A separate read-only private probe invoked the actual integrated metadata Load
against270 preserved rows and compared every field; all270 matched. The12,302-byte
ASCII input hash was unchanged and DbLocation restored. This external fixture is
not included in the public repository; non-ASCII legacy encodings were not proven.

Native duplicate list order, metadata key versus physical ID, activation boundary,
unchecked score wrap, exact four-frame removal and mutation-before-fault behavior
are preserved. Synthetic security records cover expiry, defaults and partial faults
without using real credentials. This is not full SaveClient/login/deletion, host
birth, worker/socket adoption, current-client acceptance or deployment. Warnings
remain; this focused run is not a new full Windows CI result.

## Complete native proficiency behavior and system messages

Every native Proficiency method now uses genuine gem/status fields, native
threshold lookup and real primitive packet/message sends. Complete message
fields, six constructors, required contract, serialization/deserialization and
all four system-message branches are integrated without a host chat dispatcher.

Private Debug and independent public Release each passed701 focused cases,
zero failures/skips:84new/617retained. New coverage includes40 progression/
codec/threshold,23 message behavior,10 contract/frame,8 gem/status and3 actual
progression->binary save->fresh-owner reload cases. Eight independent
Framework/x86 controls from the preserved pinned serializer match the modern
native contract. The earlier33-case prerequisite run overlaps these results.

Native uint/ushort/byte wrap, threshold narrowing, low/high-level scaling,
previous-level jumps beyond20, cap/send order, stale stored thresholds versus
recomputed wire values and full-width physical-ID rekey are retained. Gem state
keeps the native thirteen-slot array and ninth-Tortoise gate. Message state keeps
null-string omission, absent-slot retention, Count1/seven slots, live catalog,
local ServerID0 and actual View-owner routing/fault/disposal order.

This is component proof, including actual synthetic Windows file I/O, not full
SaveClient, DataCore/host birth, live worker/socket transport, current-client
acceptance, English-client completion or deployment. Warnings remain. No new
full Windows CI run is claimed; the exact older run below remains historical.

## Native queued file, spell and proficiency persistence

Complete native guild/union/clan member updates, race record, spouse operations,
AllowCreate and GenerateDate now use genuine physical state and Windows INI/
packet owners. Complete spell/proficiency file load/save methods use actual
typed Windows file I/O, native12/20-byte aligned records and one physical cached
handle/cursor. Rune spell lookup, real proficiency storage/constructor and
CheckProf are included; no host/client-constructor initialization was invented.

Private Debug and independent public Release each passed602 focused cases,
zero failures/skips,90new/512retained. New cases cover exact owned INI content,
queued spouse bytes, current physical metadata, raw UID/path/date behavior,
record layout/padding, real save/reload, reused truncated/EOF records, duplicate
admission, actual value IDs, Rune/job gates and partial writes/fault ordering.
Tests explicitly close retained actual file owners after native fault phases;
no fake file provider, production cleanup fallback or GC recovery is involved.

Native quirks remain, including the kick branch retaining Treasury, ignored
binary read/write booleans, spell metadata Count>Level gate, and broad spell-load
catch versus uncaught proficiency/save faults. Complete opened-file phases keep
the same loop bodies and fault order; successful paths close normally.

This is not full SaveClient, login/deletion/queue-worker lifecycle, DataCore/host
birth, SQL integration, restart recovery, current-client acceptance or deployment.
Full inventory mutations remain unfinished. Later proficiency progression/send
component proof is documented above; it does not install its production callers.
Warnings remain, including unsupported native Thread.Abort. The last full
Windows run below predates this batch; these are focused component checks only.

## Native inventory transport and queue prerequisites

Complete native item framing/log text, lock and extra-effect sends now use the
actual item fields, mutable extra-message lists and native Packet owner. The
ItemUsage enum/read/write and ItemLock codec, date helpers and physical server
identity comparison are implemented without host packet registration. Full
Inventory.Update/Remove, item Send and the lottery receiver remain unfinished.

Focused private Debug and independent public Release verification each passed
484 cases, zero failures/skips:101 new and383 retained. New cases cover literal
frames, fixed-string truncation, clock-dependent effects, native partial faults,
live metadata/list aliases, asymmetric usage cursors and reserved bytes. This
is a focused component result, not a new full-suite or live-client result.

The complete native base queue/ThreadItem bodies are present. Sixteen fresh
Windows PowerShell children verify queue work and synchronous thread phases;
they start no lifecycle worker. Native Thread.Abort remains unsupported on
modern .NET and emits SYSLIB0006; it has not been silently replaced. The genuine
login/save/deletion worker and its remaining dependencies are not installed.

A separate isolated local probe with the preserved legacy MySql.Data6.3.6 DLL
on.NET9.0.16 failed before SELECT against MariaDB12.2.2: first missing codepages,
then DBNull conversion during character-set loading after probe-only encoding
registration. That is negative compatibility evidence, not successful SQL or
proof for the public NuGet asset. No original database or deployment was used.
After resolving explicit probe-only prerequisites, a later private held-provider
probe passed real native reads of two synthetic account rows, NULL/no-row and
disposal behavior, and missing-table error1146. It required CodePages registration,
the actual PerformanceCounter9.0.16 runtime dependency (monitoring remains false),
MariaDB's documented [NO_NULL_COLLATION_IDS](https://mariadb.com/docs/server/server-management/variables-and-modes/old_mode)
and a Latin1 fixture. Exact database
identity was checked through numeric server-side equality; the auxiliary string
DATABASE() query still exposed a retained metadata/cleanup stall. This limited
ASCII-fixture success does not prove general Unicode, public-package, host,
full-player persistence or restart compatibility. All owned test servers stopped.
Warnings remain; full persistence, worker lifecycle, host adoption and client
acceptance still require separate work. The older full Windows run below does
not certify this later batch.

## Complete native gear-view and Air Power receiver bodies

The complete private native receiver bodies now use genuine protobuf parsing,
current pool/gear/powers, packet sends, spell storage and rank owners. Focused
verification passed739 private Debug and653 public Release cases, zero failures/
skips. Both include33 new receiver cases; retained inventories differ.

Coverage includes requester-versus-target ownership and literal full frames,
current physical pool/cache references, exact spell-learning order, threshold
and integer-narrowing quirks, rank partial publication and bounded parser faults.
Readonly player backing fields are seeded only in tests; no partial setter or
production fallback was introduced. These are managed component checks, not
OS-socket/client proof. No Packet attributes or host dispatcher were registered,
and legacy source opcodes are not adopted as D13 authority. The full Windows
run below predates these later bodies; it is not a full-suite result for them.

## Exact edeff87b Windows check

[Windows run39823843806](https://github.com/vgcman16/wolfconquer/actions/runs/39823843806)
completed successfully on exactedeff87bd37439ea5f7c716ba43709f22c58fa56.
All eight sequential stages passed; the local receipt finished at
2026-10-22T17:38:02Z. Release build:81 warnings, zero errors. Results:

- Server/tool tests:15,287 passed, zero failed,166 explicitly skipped external-fixture cases.
- Updater tests:259 passed, zero failed/skipped.
- Separate Windows launcher tests:49 passed, zero failed/skipped.
- Status and updater-receipt parser checks passed; they use synthetic fixtures,
  not live public-feed or service availability probes.

This exact run includes the real gear registrar, object-packet binding and native
protobuf packet methods documented below. It does not deploy the game, prove
fresh client login, full native parity, SQL/full-player persistence or production
readiness. Warnings and explicit fixture omissions remain visible.

## Native packet protobuf reads and writes

All three native Packet protobuf methods now use the pinned serializer directly.
Serialize writes at the current unmanaged cursor without framing or changing
Size. Framed Deserialize resets to zero and consumes only the declared payload,
ignoring opcode/seal/Size; both overloads return a new object rather than merging
the supplied instance. Raw Deserialize does not touch the packet cursor/memory.
No load gate, bounds repair, exception suppression or host adoption was added.

Private Debug verification passed706 focused cases; independent public Release
passed620, both zero failures/skips. Both include16 new cases and19 storage
controls; other retained inventories differ. Empty payload/default construction,
exact payloads, physical aliases,
cursor/fault timing and bounded truncated streams are tested with the real model.
All unsafe tests use initialized owned buffers; this is not a claim of malformed
pointer/input safety, network receive integration or actual-client acceptance.
Compiler/dependency warnings remain; no new full-suite result is implied.

## Genuine object-packet serialization

All six native gear-family implicit packet conversions now use the actual
object-typed pinned serializer and Default model. The false load gate precedes
null/type handling; genuine missing/duplicate model failures propagate, and
native header lengths/opcodes/TQServer framing remain unchanged. Manual typed
projections stay separate as independent contract controls.

Private Debug verification passed671 focused cases; independent public Release
passed585, both zero failures/skips. Both include ten new fresh-process lifecycle
controls; retained inventories differ661/575. Cold children
cover all six gates, five unregistered failures, attribute-only empty Air,
registered full-frame literals, nulls and duplicate-registration faults. Parent
tests invoke the real registrar once via an explicit test collection fixture;
it neither loads files nor changes m_Load/cache owners. Earlier35-case prestate
verification was24pass/11fail because those old tests had only set the flag;
the test precondition was corrected without changing expected packet bytes or
introducing a production fallback. Existing compiler/dependency warnings remain.

This is not receive/host binding, native generic Packet reads/writes, deployment,
SQL/full-player persistence, a new whole-suite run or actual-client acceptance.

## Genuine combat-gear table loading and registration

The complete native table Load now calls all three metadata loaders, registers
12 real message types/67 required fields in the genuine pinned protobuf model,
parses all19 gear columns and then loads ranks. Exact raw prefixes, current
culture, duplicate-first rows, caught faults and partial publication remain.

Private Debug and independent public Release verification each passed316
focused cases, zero failures/skips (17 new isolated loader cases plus299
retained gear/metadata/rank cases). The
first313/3 run exposed incorrect test assumptions, not a production defect:
unfrozen repeated/conflicting registrations append duplicate fields and fail
later during serializer preparation. Corrected cases assert every134/68 field,
retained owners, rank-creation faults and genuine deferred exceptions. No test
was disabled. Fresh child processes isolate the real global model; they run
under the measured PowerShell7 runtime, separately from the.NET9 parent tests.
Independent cold read-only component probes also loaded preserved data under
.NET9.0.16 and.NET10.0.11 without modifying five held inputs or assigning m_Load.
Those private data/binaries are not part of this publication.

The package's explicit net40 asset is byte-identical to the reference serializer;
the different netcore45 asset is not selected. Existing and new NU1701 warnings
remain. These narrow checks do not establish general framework compatibility,
production/security suitability, host startup, SQL/full-player persistence or
new client acceptance. Implicit packet conversions still use separate concrete
projections at this checkpoint; genuine object-packet integration is next.

## Native dictionary lottery and combat-gear operations

The complete native MsgDictLottery and MsgCombatGearOpt model/Process families
passed **235 focused cases** in private Debug and independently in this public
Release tree, zero failures/skips:65 new cases (30 lottery/35 operations) plus170
retained gear/air/DaoQi cases. Existing compiler/dependency warnings remain;
this is not a new whole-suite or live-client run.

Real gear-add, jade, points, flags and metadata owners back the branches. Tests
preserve native prices/free flags, true local randomness, unchecked costs/rewards,
current-level lookups, request/collection aliases, response order and partial
faults. Separate pinned-library checks observed nine lottery and ten operation
payloads using the exact source-shaped registered contracts; literal frame tests
retain required zeros, signed enum varints, unpacked arrays and list/array null
differences. They do not prove the missing generic registration lifecycle.

The first public lottery compile exposed an omitted existing AddItem dependency;
its complete implementation and retained tests were then included, and the
public lottery rerun passed200/200 before the final235/235 combined run. No
test/branch was disabled. Native startup, SQL/full-player persistence, receive
adoption, current-commit deployment and fresh-client acceptance remain unfinished.

## Native combat-gear ranking file persistence

Complete source-shaped rank Load/Save and the native database Write/Mode owners
passed **75 focused public Release cases**, zero failures/skips:30 new cases plus
45 retained rank/read cases available in this public tree. The private Debug
selection independently passed102 (30 new/72 retained). These selections differ;
neither count is a new full-suite run. Existing build warnings remain.

Tests use only GUID-owned files and preserve all22 columns, raw prefix/ignored
argument, Create-first faults, dictionary aliases/top10, enum narrowing,
unescaped delimiters and real writer/locked-file failure timing. A separate
ported-model read-only probe loaded preserved data without invoking Save. No
original saves are published or mutated. Native startup/protobuf registration,
host adoption, SQL/full-player persistence and live-client acceptance remain
separate unfinished requirements. This commit does not deploy the running stack.

## Static dependency inventory and backup-suffix follow-up

The new `client-pe-dependency-audit` passed **73 focused cases** in both the
private Debug tree and sanitized public Release tree:42 new command cases plus31
retained symbol-audit regressions, zero failures/skips. They exercise actual CLI
children on synthetic PE32/PE32+ files, normal/delay RVA/legacy-VA descriptors,
bounded names, malformed ranges, JSON-only success and real Windows junction/
symbolic-alias refusal with original bytes/write times preserved. No synthetic
PE is executed. Build warnings remain; this is not a new whole-server test run.

The command was also used read-only on the local investigation target; native
bytes/findings are not included in this publication. A static import inventory
does not prove symbol readiness, runtime search order or fresh-client startup.

The later dated-backup suffix safeguard passed **nine grouped exclusion cases**
in both trees, zero skips. It blocks suffixed backup/temp extension segments even
inside approved asset directories, permits exact omission and preserves source
bytes. Filename filtering is not a content/security or redistribution audit.
No actual client package/feed was staged, signed, published or launched here.

## Exact 7a4675ba Windows follow-up

[Windows run39806247831](https://github.com/vgcman16/wolfconquer/actions/runs/39806247831)
completed successfully on exact7a4675ba. Its eight-stage receipt finished at
2026-10-22T15:39:48.6732772Z:15,089 server/tool tests passed/166 skipped,
259 updater tests passed and49 launcher tests passed, all zero failures;
the two receipt parser suites passed20 and13 cases. Release build retained
74 warnings, zero errors. This is not deployment, SQL or real-client proof.

The later conservative AutoP filename quarantine passed eight synthetic exclusion
groups locally, including direct staging-plan refusal before any copies. It
does not classify every binary or certify a client package as safe.

## Portable DX8 layout follow-up

The metadata-only planner's explicit excluded-leaf follow-up passed nine baseline
planner groups, seven new exclusion groups (zero skips on this PC), and ten
staging groups on both private and sanitized public trees. Its real
planner-to-stager test preserves outside target bytes
under an exclusive lock and copies only the original two approved fixture files.
Selected/unlisted links and linked roots/ancestors remain refusals; no generic
stager or publisher policy was relaxed. File-symlink coverage uses already
available Windows privilege; on other hosts the script reports an explicit skip
instead of altering permissions. This is not approval of a real-client policy.

The opt-in local fixed DX8 layout passed **40 focused Windows cases**, then the
full updater suite passed **259/259** and the separate Windows launcher suite
passed **49/49**, all with zero failures/skips. Root reviewed all new production
and test modules. These are local private-tree results for the standalone update
components. The sanitized public tree independently passed its full updater
suite259/259 and launcher suite49/49, also zero failures/skips. Exact-commit
GitHub checks remain separate; their result is not claimed in advance.

Tests create actual NTFS mount points only in owned synthetic temporary roots.
They prove signed ordinary installation, exact fixed aliases, target-byte
preservation, wrong-kind/target/ancestor refusal, idempotence, later root-asset
patches, real stopped-process checks, pending-state rejection, mid-operation
conflict/cancellation rollback, metadata-only non-repair and explicit repair.
Synthetic executables are never launched. Two earlier focused runs failed in
test-native path assertions/cleanup; preserving body and cleanup errors exposed
mixed slashes in the test helper. Canonical test paths fixed that issue without
weakening production syntax; forward-slash refusal is explicitly covered.

These counts are not gameplay parity, a new server-suite run, a fresh real-client
install/login, or publication of game assets. The 55% planning estimate is
unchanged. Actual client curation and portable loader configuration remain open.

## Follow-up: exact GitHub check and offline staging

Published commit `03d9e1e1cd0c7b8631fdeab0e5430a7467a32f03` passed
[Windows local checks run 39797202093](https://github.com/vgcman16/wolfconquer/actions/runs/39797202093).
Its actual eight-stage receipt finished at `2026-10-22T14:40:35.1647385Z`:
15,089 server/tool tests passed with 166 skips, 219 updater tests passed, and
43 separate launcher tests passed. This is exact-commit CI, not deployment.

After that commit, the display-only working-directory update passed the full
launcher presentation suite: **49 passed, zero failed/skipped** on both local
private and sanitized public trees. The application's own XAML preview was
rendered and inspected locally, with no game or desktop input.

The exact-list offline staging tool passed **ten grouped synthetic cases** on
both trees. They exercise copied bytes, rebased plans, ordinary path containment,
actual junction/file-symbolic-link refusal and a real second-file writer conflict
that leaves explicit partial staging data with no publisher plan. No real client
was copied or published by these tests. These focused follow-up results do not
claim a new full solution run for later changes.

The parity renderer passed **17 tests, zero failures/skips** locally. Actual
outputs passed deterministic byte comparison: a 24-frame, 1.92-second GIF
(62,548 bytes for the initial status), static PNG and full generated Markdown.
The PNG was visually inspected. Tests verify the fixed fill across every frame,
matching assessment metadata, strict schema/date/range parsing, read-only check
behavior and actual input/output link refusal. The tests validate presentation
and safety behavior, not the subjective 55% planning estimate. GitHub automatic
refresh was subsequently proven by successful Windows run
[39800432703](https://github.com/vgcman16/wolfconquer/actions/runs/39800432703)
on69695d0c. It published generated-only1218aeeb; Root rechecked deterministic
outputs. This refresh does not reassess the underlying estimate.

## 2026-10-22 launcher and updater intake

The sanitized public working tree passed the full sequential Windows local check
`20261022T142329063Z-05ad322c`, finished at `2026-10-22T14:26:09.9030331Z`.
This is local evidence for the reviewed source, not a GitHub check, deployment,
full-client download, or real-game login result.

| Check | Result |
|---|---|
| Solution restore and Release build | Passed; 74 warnings; zero errors |
| Server/tool tests | 15,089 passed; zero failed; 166 skipped |
| Update engine and CLI tests | 219 passed; zero failed; zero skipped |
| Separate Windows launcher tests | 43 passed; zero failed; zero skipped |
| Dashboard status parser | 20 synthetic cases passed; no network/service probes |
| Saved updater-test result parser | 13 synthetic cases passed |
| Explicit client-file planner | Nine synthetic cases passed separately |
| Launcher-only ZIP packager | 62 synthetic assertions passed separately |

The eight-stage `Invoke-LocalCheck.ps1` restores/builds/tests the solution, then
the Windows-only launcher test project, and finally both receipt parsers.
Updater tests execute once in the solution run. Opt-in per-project/framework TRX
directories prevent two assemblies finishing in the same second from overwriting
each other's report. Receipts are derived from actual terminal test results.

Initial runs found four outdated local-tool fixture assumptions and one old
default-brand assertion, then a test-only immediate process-module identity race
and a TRX filename collision. These were genuine failed runs. Fixtures and result
isolation were repaired; the owned command probe now checks identity using its
retained process handle and waits for natural exit. No test was skipped or killed
to manufacture success. The 166 external-fixture skips remain excluded coverage.

Working-directory tests launch only an explicitly attributed copy of the Windows
command executable, not the game. Packaging tests use synthetic files and never
execute their contents. The planner's filename quarantine is not a content or
redistribution-rights audit. No official full-client package/feed is published.
The original launcher artwork is newly created, not extracted game artwork.

## Earlier 2026-10-22 server/tool snapshot (historical)

Validated on Windows x64, SDK 9.0.314, Release, with a measured PowerShell 7
executable for native child probes. These results apply to the sanitized public
tree, not the private repository or a deployed game server.

| Check | Result |
|---|---|
| Restore | Seven projects; passed |
| Release build | Passed; zero errors; warnings remain |
| Full unfiltered solution tests | 15,084 passed; 0 failed; 166 skipped; 15,250 total |
| Local development tools | 15 synthetic cases included in the full run |
| Dashboard | HTTP/health/status passed; four explicitly selected service processes observed; POST 405 and unknown route 404; browser rendering inspected |

The latest successful sequential run, including the test-harness repairs described
below, finished at `2026-10-22T11:55:08.9381842Z`:

```powershell
pwsh -NoProfile -File ./tools/dev/Invoke-LocalCheck.ps1 -PowerShellPath <explicit-PS7-executable>
```

The script performs restore, build with `--no-restore`, then tests with
`--no-build --no-restore` and no filter. Small receipts and logs stay local under
ignored `.artifacts/local-dev/`. No input-hash proof ceremony is required.

An initial integrated run found missing owned Beast metadata in two test fixtures,
stale absent-method/provider-call assertions, and a scheduling-sensitive lock test.
The fixtures now supply real synthetic startup input and restore the prior cache;
assertions match implemented methods; lock release observes genuine failed-attempt
cleanup. Production retry limits and gameplay behavior were not weakened.

A later CI repeat at `939dbf8a` failed two tests (15,082 passed, 166 skipped):
another scheduling-sensitive snapshot contention check and a child process that
printed the expected profile-rejection error but did not exit before its timeout.
A local repeat after those fixes exposed the same timing pattern in the item-save
test (15,083 passed, one failed, 166 skipped). These are recorded failures, not
passing retries. The snapshot contention family now shares a small test helper
that owns the real file lock, observes actual failed-attempt temporary-file
cleanup, checks unchanged bytes while locked, then drains the writer after
release. The scalar cleanup assertion also uses the real transaction prefix.
Profile probes retain nonzero-exit, error-text, and no-listener assertions;
Windows child launches inherit crash-report suppression while the testhost's
previous error mode is immediately restored. There is no permanent OS setting
change or kill-as-success path.

The first build emitted 74 warnings; the subsequent incremental build emitted 73
(unchanged Core warning not re-emitted). Warnings include NU1701 for legacy
MySql.Data, an unused interop field, nullable diagnostics, and xUnit suggestions.
This is not warning-free. Restore emitted no dependency audit warnings; that is
not a security guarantee. See [dependencies](DEPENDENCIES.md) for provider limits.

The 166 skips are [excluded coverage](docs/EXTERNAL-FIXTURE-TESTS.md), not passes.
Synthetic codec roundtrips are not a native-client oracle. These checks do not
establish SQL execution, complete English translation, current-source live-client
compatibility, deployment, full parity, or production readiness. GitHub workflow
and CodeFactor results apply independently to the commit they actually inspect.

## Earlier public commit 498e7c83 (historical only)

The edited publication snapshot was validated locally on Windows x64 with .NET SDK 9.0.314 and Release configuration on 2026-10-17. These results apply to the sanitized source in this repository.

| Check | Result |
|---|---|
| Restore | All seven projects restored successfully |
| Release build | Passed; zero warnings and zero errors |
| Full test suite | 10,531 passed; zero failed; 165 skipped; 10,696 total |

The full run used no test filter and finished at `2026-10-17T16:21:51.9282145+00:00`. The 165 skips are the [documented external-fixture methods](docs/EXTERNAL-FIXTURE-TESTS.md). They are excluded coverage, not passing tests. Active tests include synthetic packet/layout fixtures and local HTTP, socket, and child-process integration checks. The snapshot does not establish compatibility with a complete external game client or omitted game datasets.

Commands used:

```sh
dotnet restore WolfConquer.sln --ignore-failed-sources -p:NuGetAudit=false
dotnet build WolfConquer.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false
dotnet test tests/WolfConquer.Tests/WolfConquer.Tests.csproj -c Release --no-build --no-restore
```

Restore used the available local package cache and disabled NuGet vulnerability auditing for that operation; this is not a dependency vulnerability assessment. The test run also enabled a TRX logger for the private validation receipt. No GitHub CI result is claimed.

The 2,586 source/project/solution/SDK inputs were frozen before the build and checked again after testing. Their aggregate SHA-256 is `bd256bae28ad40aadeeb7ca51efb350bb7ca711745168dec6d57a5cebe6e9da5` (SHA-256 of the sorted compact JSON map from relative path to each file's SHA-256). Public documentation was finalized after the run. The staged source bytes were checked against those frozen inputs; build outputs, logs, fixture data, and original history are excluded.
