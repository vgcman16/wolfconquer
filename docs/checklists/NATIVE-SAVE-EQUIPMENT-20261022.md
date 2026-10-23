# Native saves and equipment — current checklist

Current policy: [Sanitized Parity](../engineering/SANITIZED-PARITY.md), effective
2026-10-23. Older source-quirk/fault-order proofs below are historical component
evidence, not acceptance of unsafe logic. The new guild/league batch is uncommitted
and held for safety review. Follow [the source-bug audit](../../FIXED-SOURCE-BUGS.md).

## Current verified state

- [ ] FullWindows40032574805 remains RED on exactpublic0deb1e46:24176corepass/
  1fail/165existing exclusions of24342,updater301pass,launcher/parsersnotrun,
  build190warnings/0errors,4m39s. The same publicfocusedfilter534/534 allpassed,
  including127chest/95NPC/14codec. Solefailure:oldRuntimeReviver expectedStatusFlag
  gotStamina. Sourceconfirms competing-loop stale-stamina risk; FSB068OPEN is
  priority beforeVictory,not an assertion-filtering fix. Actualtrace/fix pending.

- [x] Scoped chest transaction (FSB066): validate complete actual owners and
  required files before debit, journal Character/Items together, then publish
  current session and prepared packets. Real recovery fences uncertain outcomes;
  ended/replaced owners stop remaining sends. Tests-first wider202=184pass/
  18fail; private Release530/530 passed,zero failed/skipped,5s, including123 chest,
  95 NPC,14 codec and298 adjacent cases. Forty-eight new chest controls; two
  retained registered Script/Reply facts migrated to genuine owned files.
  Successor private534/534 passed,zero failed/skipped,4s: all530 retained plus
  four actualD13Frame Rare/Epic success/refusal controls, chest127. Real shared
  reader/exclusive probe prove these handlers release their lease; full files,
  session and wire assertions retained, real random branch not exhaustive.
  PublicWindows40031267716 failed exact14450da2:24171corepass/2fail/165excluded,
  updater301pass,launcher/parsersnotrun,190warnings/0buildErrors,4m28s. Both
  failures were hardcoded private fixture name versus sanitized public seed,
  before handlers; fixture now uses supplied name without weakening equality.
  Successor full CI pending. Next: FSB067 Victory quest/item transaction.
  This supersedes separate best-effort-save debt below for Rare/Epic only,
  not Victory/all rewards, power-loss/raw-writer/outbox/ABA or global UID safety.
  Current max+1 creator remains; isolated canonical UID/Host activation HOLD.
  No live,SQL,original-save,routing or deployment action.

- [x] Positive chest payment and CP identity (FSB064/065): empty rows no
  longer authorize/payment/free slots; first real key pays one unit. CP saves use
  account301 path with actual character1293600 validation. Tests-first75=
  35pass/40fail;50 new cases plus25 retained. Private Release184/184 passed,
  zero failed/skipped. Public Windows40027179762 passed05e0feb1 in4m55s,
  all8stages0,build190warnings/0errors;receipt finished21:34:38.6025377Z,
  jobcompleted21:34:45Z. Exact fullTRXcounts unavailable after next checkout;
  not extrapolated. Valid any-binding
  policy, key IDs, costs, random rates,200000CP and packet order remain unchanged.
  Lower-owner fault controls are not whole-chest save-failure safety. Separate
  best-effort saves, durable transaction/replay/delivery/global UID and live
  acceptance remain open; no deployment, SQL or original-save action.

- [x] Admitted-claim/chest safety (FSB060–063): Vote full-bag rejection,
  positive unbound Lottery costs/capacity-before-allocation, exact post-debit
  Deityland capacity and Epic3303974 key identity. Tests-first76=52pass/24fail
  across50+26 selections;43 net new cases and four unsafe old methods corrected.
  Private and independent public Release326/326 each passed,zero failed/skipped;
  fullWindows40022478743 on306ec9ec failed1native-readiness test:24074corepass,
  165existing exclusions,updater301;launcher/laternotrun. Build190warnings/0errors.
  Full probe diagnostics are being retained without changing readiness assertions
  or timebounds; cause remains unconfirmed. Private readiness selection23/23
  passed,zero failed/skipped; this is not a fixed-flake claim. Independent full-CI
  successor40024325826 nowpassed4m40s on exactpublicfdec0466:24075corepass,
  0failed/165existing exclusions,updater301/301,launcher57/57,all8stages0,
  190buildwarnings/0errors. Receipt20261023T210825276Z-b839ee31 finished
  21:12:43.7712151Z;jobcompleted21:12:50Z. Earlier readiness cause remains
  unconfirmed,not a fixed-flake claim. D13 offline/owned-file checks preserve valid
  economics/frames; server-export item names are not client-table proof.
  Whole transactions,zero-stack admission,respawn/claims,UID/Host activation,
  crash recovery,SQL/live/full parity remain open. Fourteen xUnit2031 substitutions
  in eight files are local hygiene only, not measured CodeFactor portal cleanup.

- [x] NPC admitted-cost and existing-lock safety (FSB058/059): real tests-first
  108=82passed/26failed; six unsafe old facts corrected and39 new cases added.
  Private and independent public Release338/338 each passed,zero failed/skipped
  across wider NPC/reward families. Full Windows40017540883 passes5m26s on
  exactpublic06210e3a:24032corepassed/0failed,165existing fixture exclusions;
  updater301/301,launcher57/57,all8stageexit0,204buildwarnings/0errors.
  Arthur/Event/Simon spend only admitted positive unbound rows in lowest eligible
  UID order; Simon empty rows cannot supply diamond units. RedBodhi preserves
  existing locks/timers/metadata, with complete saved bytes and ten independent
  wire goldens per Locked0/1/2 case. Valid costs/rewards/rates/order remain.
  Whole NPC transactions,global UID/Host activation,SQL/live/deployment/full
  parity remain open. No score-only protocol refactor or per-warning fullCI.

- [x] Scoped DragonSkin item/skin transaction (FSB057): registered baseline
  89=8 passed/81 failed; first admitted 186=161 passed/25 component-fixture
  failures. Real-loader fixture correction preserves raw bytes and strict
  production validation; private 186/186 then broader Trade/barrier/session
  368/368 passed, zero failed/skipped. Public same-26-path selection also passed
  368/368, zero failed/skipped (116 new + 70 retained + 182 adjacent).
  Shared v2 journal uses account-keyed Character and character-keyed Items,
  complete strict baselines, current-owner persisted CAS, exclusive admission
  and exact-root recovery readiness. Interrupted 0/1/2 prefixes, rollback/retry,
  stale/replayed/retired owners and transport/presentation observers are covered.
  This is process-restart reconciliation, not power-loss/multiprocess/raw-writer/
  ABA/outbox or global UID proof. Creator remains max+1; no canonical alternative,
  Host activation or SQL/live/original-save/routing change.
  Full Windows40013043779 passes4m11s on exactpublicf2f122f2:
  core23993passed/0failed of24158,165existing externalfixtureexcluded;
  updater301/301,launcher57/57,all8stageexit0,build204warnings/0errors.
  Receipt20261023T194003090Z-230b1736 finished19:43:51.8908154Z.
  No deployment, live-client or full-parity acceptance follows.

- [x] DragonSkin cost admission (FSB056): real tests-first baseline failed all
  16 new cases; repaired focused private 70/70 and public 70/70 passed. Unlock
  consumes only the first mapped positive unbound row, retaining inventory order,
  one-unit costs, other item fields and existing frames. Bound/zero-only refusals
  leave session, skin, inventory and saved files unchanged and reply truthfully;
  mixed-order controls cover all three mappings and valid stacks of one/three.
  At that admission-only checkpoint the frame callback autosaved inventory,
  followed by separate character skin persistence, without a coherent two-file
  transaction. The later scoped transaction and its remaining limits are recorded
  in FSB057 above. Admission alone made no full-CI, live-client, deployment or
  full-durability claim.

- [x] Selected-status and adjacent reward safety (FSB053/054): private Release
  721/721, zero failed/skipped (545 boss/reward,79 Status,97 rank/query).
  Tests-first baselines reproduced32 dirty-status and35 reward failures.
  Valid formulas/rates/quantities/full item goldens remain; selected inputs and
  complete grants prepare before effects/CAS/output. Full launcher57/57 also
  passes after real cleanup-masking repair (FSB055; baseline5pass/3fail of8).
  Independent public core707/707 and launcher57/57 pass, zero failed/skipped;
  clean full Windows40008366439 passes4m14s on exact8843a638:23861corepassed/
  0failed,165existing externalfixtureexcluded, updater301/301, launcher57/57;
  8stageexit0,204warnings/0buildErrors. No new
  live/server/SQL/UID-activation or full-parity claim.

- [x] Earth Force conversion/rank safety (FSB051/052): private Release592/592,
  zero failed/skipped (471boss/reward,46rank-effect,30rank table/persistence,
  45Status). Earth tests-only32:10pass/22fail; corrected early-ingress diagnostic
  retains EMPTY all frames. Rank Status baseline8:2valid pass/6unsafe fail.
  Complete Earth preparation precedes one CAS/output; required complete rank
  data is validated before bonuses or spell removal. Valid quantities/formulas/
  packet goldens remain. FullWindows40002265954 passes4m44s on exact549e9829:
  23753core/0failed (165externalfixtureexcluded), updater301/301, launcher49/49;
  8stageexit0,212warnings. Eight new test-only warnings corrected in one batch:
  public focused578/578,0fail/skip; build204warnings/0errors, no repeated fullCI.
  UID activation, durable reward recovery, raw
  writers and whole-Status transactions remain open.

- [x] Monster reward safety (FSB049/050): private complete boss/reward class
  439/439,0failed/0skipped. Tests-only baselines13:7pass/6fail (Plume) and
  25:4pass/21fail (messages) precede the fixes. Five unsafe old facts corrected,
  30 new controls retained. Plume prepares quest+item before current-session CAS;
  Earth full bag reports capacity, Cloud announces only committed grants, and
  undefined WarDevil payouts cannot spend luck or announce treasure. Valid reward
  values and objective/item or receipt/item order remain. Clean public full
  Windows39998007520 passes4m39s on exact7c9e9a11:23687core/0failed,
  165externalfixtureexcluded, updater301/301, launcher49/49;8stageexit0,
  204warnings. No global economy/UID/durable recovery closure.

- [x] Adjacent admitted-cost repair (FSB048): private/public Release374/374each,0failed/
  0skipped across all seven pack families. New3-owner baseline119pass/16fail
  of135;5 unsafe old expectations corrected,30 new controls retained. Required
  BlackFriday/Diamond and five-Spirit Phoenix debit only positive unbound rows,
  keeping physical/round-robin order, exact costs/rewards/frames and persistence.
  FullWindows39991776324 passes4m57s on exact91889907:23657core/0failed,
  165externalfixtureexcluded, updater301/301, launcher49/49;8stageexit0,
  204warnings. No UID/economy/global closure.

- [x] Admitted pack cost selection (FSB047): private/public Release239/239each,0failed/
  0skipped after213pass/26fail baseline. Four consumers spend only a positive
  row in the admitted binding class, retaining first eligible inventory order.
  Wrong-binding/empty rows, full40-slot capacity, saved item metadata and exact
  debit/reward frame order are covered;10 unsafe old expectations corrected,
  34 new controls added. FullWindows39989920933 passes4m37s on exact8411bb61:
  23627core/0failed (165externalfixtureexcluded), updater301/301, launcher49/49,
  8stageexit0,204warnings.
  No client/server/SQL/original-save action; global UID and economy remain open.

- [x] Late-accepted TCP shutdown drain (FSB046): private/public Release25/25
  each,0failed/0skipped, after3pass/1fail baseline. Real late-accepted and
  already-registered client handlers plus asynchronous transport cleanup must
  finish before shutdown/peer EOF. Empty/never-started controls retained.
  FullWindows39986796881 passes4m41s on exact93e7259:23593core/0failed,
  165externalfixtureexcluded, updater301/301, launcher49/49,8stageexit0,
  204warnings. No running server/client was restarted.
  Detached runtime/Host lifetime and global UID activation remain separate.

- [x] SuperGuild reward preparation (partial FSB009): private468passed/0failed/
  0skipped; public464passed/0failed/4existing external-exchange skips of468.
  All12 new registered controls pass after8 failed retries/4 valid boundaries
  on old code. Same UID can retry after a preparation failure without losing
  entitlement; other recipients/pool exhaustion, complete saved/reloaded values
  and exact packet order checked. Leader300000CP/tuned500000silver/item2100065/
  flag151 and deputy flag152-only remain. Two exchange style corrections batched.
  FullWindows39978653171 on a80552fd:23588 core passed/1failed/165excluded;
  all12 guild cases passed, updater301passed. The sole failure is the retained
  RiseOfTaoism timer test checking its log before async completion; launcher and
  later stages did not run. Its fixture now waits for state AND synchronized
  completion log, retaining the state/vector assertions. Private focused15/15
  passes, and independent public focused15/15passes. FullWindows39980179395
  SUCCESS4m15s on exact16763a82: core23589/0failed (165excluded), updater301/301,
  launcher49/49;8stageexit0,204warnings. No deployment.
  Full claim/session/two-file recovery,
  Lottery cadence and shared UID/host lifecycle remain open.

- [x] Exchange material preparation (FSB045): private446passed/0failed/0skipped;
  public442passed/0failed/4existing external-fixture skips of446. All25 new
  registered controls pass, with full native save/reload images on7 successes.
  Baseline10failures include3 real unpaid-cost publications,2 product overflows
  and5 reason differences. Aggregate cost checks precede private per-definition
  debit; valid wire order and active binding/count/stack policy remain. Shared
  consumption callers are unchanged. FullWindows39976822207 passes4m05s on exact
  8abeec78: core23577/0failed (165excluded), updater301/301, launcher49/49,
  8stageexit0,204warnings. CodeFactor3NEW/1FIXED,total2303; two mechanical fixes
  join the next behavior batch. Whole session/
  save/economy atomicity, GoodsEx and global UID activation remain open.

- [x] Native quest acceptance (FSB044): private/public Release418/418 each,
  zero failures/skips, including75 new acceptance and2 real isolated delivery
  failures plus341 retained controls. Catalog1469/409 metadata stays unchanged;
  complete genuine graph, bounded persisted projection, real2120 frame and
  comparer-preserving maps precede publication. Unguarded send baseline fails2/2
  after commitment; guarded acceptance returns true without rollback/retry even
  if native diagnostics fail. One Shop assertion-style warning joins this batch.
  FullWindows39975270475 passes4m11s on exact39eaaf45: core23552/0failed
  (165excluded), updater301/301, launcher49/49,8stageexit0,204build warnings.
  CodeFactor3NEW complexity/0FIXED,total2301; score-only splitting is deferred.
  Native callers/host, FinishQuest,
  full GUI/awards, automatic save/SQL and delivery recovery remain open.

- [x] Shop-only capacity admission (FSB043): private334passed/0failed/0skipped;
  public325passed/0failed/9existing external-fixture skips of334. All33 new
  registered cases pass after genuine18-failure baseline. Full40/overcap41 and
  multirow overcapacity refuse without costs/files/frames; last-slot purchases
  retain exact gold/CP/boundCP/Honor/Race/Champion values and packet order.
  One observed objective-length precedence fix joins the batch with292 retained
  controls. FullWindows39972176836 passes4m17s on exacta92b1fb0: core23475/
  0failed (165excluded), updater301/301, launcher49/49,8stageexit0,205warnings.
  CodeFactor2NEW complexity/1FIXED precedence,total2298. One xUnit2017 assertion
  style correction is included with FSB044. Native normal stacking/
  partial affordability and whole wallet/save/session transactions remain open.

- [x] Native whole objective Set/Increase (FSB042): private/public Release292/292
  each,0failed/0skipped,70new plus19 retained/repaired objective cases. Complete
  genuine graph, private checked arrays and bounded real2136 caller frame precede
  writes; exact special tails/seal/order and valid no-ops remain. FullWindows
  39971090546 passes4m50s on exact8e5ff255: core23442/0failed (165excluded),
  updater301/301, launcher49/49,8stageexit0,204build warnings. CodeFactor4NEW
  (3complexity+1precedence)/1FIXED,total2297; precedence joins shop batch.
  No native caller/host/client activation,
  automatic disk save, whole quest award or raw/disposal-race proof is claimed.

- [x] Rune fragment direct/NPC debit repair (FSB041): private236passed/0failed/
  0skipped; public229passed/0failed/5existing external-fixture skips of234.
  All12 new controls pass. Opposite-bound stock cannot pay an admitted cost;
  native direct per-unit/NPC per-stack order, staged binding,20cost and reward
  values remain. Saved/reloaded physical images and unchanged refusals checked.
  FullWindows39970166492 passes4m44s on exact27e2ae9b: core23372/0failed
  (165excluded), updater301/301, launcher49/49,8stageexit0,204build warnings.
  CodeFactor1NEW duplication/0FIXED,total2294; no cosmetic-only CI loop.
  Whole reward/UID/persistence transactions and actual-client proof remain open.

- [x] Private/public Release **3465/3465** each,0failed/0skipped: complete native
  quest Save/Load (FSB040), real flushed replacement, full bounded parsing and
  canonical conflict-safe merge. Corrected destructive/partial/split-alias tests;
  valid bytes, raw statuses, UID0 rows and no persistence-level five-quest cap
  remain. FullWindows39965399561 passes4m06s on exact0b0da909: core23360/0failed
  (165excluded), updater301/301, launcher49/49,8stageexit0,204build warnings.
  CodeFactor4NEW complexity/0FIXED,total2293; no score-only rewrite.
  Native login/SQL/quest-award lifecycle and raw writers
  remain open; this does not enable a host or certify multifile recovery.

- [x] Private/public Release **3214/3214** each,0failed/0skipped: complete selected
  native DivineGuard preparation before cooldown/status/success effects
  (FSB039),98new controls plus14 retained ProcRates cases. Genuine5%/15seconds,
  selected-trigger10minute stamp and owner/peer wire order remain. Includes the
  deferred single testconstant parentheses fix. FullWindows39962905914 passes
  4m43s on exactaf3ca964: core23325/0failed (165excluded), updater301/301,
  launcher49/49,8stageexit0,206build warnings. CodeFactor3NEW (2complexity+
  1duplication),1FIXED (testconstant),total2289; no risky cosmetic follow-up.
  Full outer CheckEffects/combat/HP/Team/revive/host remains open, not activated.

- [x] Private/public Release **3116/3116** each,0failed/0skipped: complete native
  item-image preparation and real flushed atomic replacement, no truncated
  rejection/prefix, exact wardrobe/equipment alias and duplicate/nested-deposit
  checks, bounded fixed32 perfection fields (FSB037..038). Includes51new controls
  and one10-record/6-file readability batch. FullWindows39959714814 passed4m45s
  on exact01431fe3: core23227/0failed (165excluded), updater301/301, launcher49/49,
  all8stageexit0,206build warnings. CodeFactor11FIXED (all10targets plus1older),
  4NEW (3complexity+1testconstant),total2287. The testconstant is included in the
  DivineGuard batch; no per-warning fullCI loop or risky complexity edit.
  One-file/process-local protection is not host/SQL/multi-file crash recovery.

- [x] Private/public Release **3065/3065** each,0failed/0skipped: complete scoped MirrorOfSin,
  KillingFlash and LightOfStamina admission/publication,116new controls (FSB035),
  plus the real durable item-UID component53owned-file controls. No host/producer
  activation: migration authority, every creator adoption and lifecycle drain
  remain HOLD (FSB036). FullWindows39958461749 passed4m23s on exact256d7a18:
  core23176/0failed (165excluded), updater301/301, launcher49/49, all8stageexit0;
  solution206warnings. CodeFactor19NEW=10readability+9complexity,0FIXED,total2294.
  The10readability records are one6-file mechanical batch with the next save
  checkpoint; complex methods are deferred without changing protocol/control flow.

- [x] Private/public Release **2896/2896** each,0failed/0skipped: complete owned
  Rune Add/cache publication, exact count, defensive arrays and explicit invalid
  queries, with source-valid position/equipped rules retained (FSB034). Includes
  one mechanical19-record precedence batch across10files; no risky complexity
  rewrite. FullWindows39956832738 passed4m10s on exact7879381d: core23007/0failed
  (165excluded), updater301/301, launcher49/49; all8stageexit0,206build warnings.
  CodeFactor confirms21FIXED (all19 targeted precedence records plus2 older),
  2NEW complex methods,total2275. The2 complexity findings are deferred; no
  packet/control-flow rewrite, suppression or reduced coverage for the grade.

- [x] Private/public Release **2392/2392** each,0failed/0skipped: complete prepared
  Sage status/archive and Shield/Stigma status/animation; final real state/loan
  integrity; XP20 with required goal103; complete bounded native goal loading.
  Valid protocol goldens, caller buffers, duplicates and accepted transport-loss
  semantics remain. FSB030..033 document exact limits. FullWindows39955050650
  passed4m21s on exactbd69741b: core22951/0failed (165excluded), updater301/301,
  launcher49/49, all8stageexit0. Build206warnings; CodeFactor28NEW/1FIXED,
  total2294. Nineteen precedence records are batched mechanically; nine complex
  methods are deferred, not reshaped for the grade. This is not deployment.
- [ ] Full Rune equip/load/save/economy and refinement procs remain open.
  Active managed reward grants remain held for a genuine durable shared item-UID
  registry and adoption/fencing of every competing producer. No account-local
  max+1 allocator is certified globally safe; SQL/eager lifecycle remain open.

- [x] Private/public Release1931/1931 each verifies the coherent flag/consumer batch:
  prepared real status frames/state, explicit unavailable membership, no dirty
  potion rejection, checked XP duration, stale-target/rank/effect protections,
  final captured-owner commitment and immutable shared physical-spell factors.
  Existing successful damage/RNG/packet goldens remain. Full Windows39949964185
  passed4m02s on exact4727d8b3: core22524/0failed (165 excluded), updater301/301,
  launcher49/49, all8 stages0. CodeFactor6NEW complexity/7FIXED,total2267;
  204 build warnings remain. No risky cosmetic follow-up loop is started.
- [ ] Full lifecycle/cast/HP/death, rank publication, item/potion expiry,
  host recovery and raw concurrent writers remain
  open. This offline component checkpoint does not deploy or change a client.

- [x] Published7a79ebc5 passes full Windows39945614990 in4m06s: core22196/0failed
  (165 excluded), updater301/301, launcher49/49, all8 stages0. CodeFactor confirms
  0NEW/4FIXED,total2268, net59 fewer than2327. Build warnings and gameplay debt
  remain; no new live-client/deployment/parity claim. Next work is flag/AI safety.

- [x] Combined defect-first successor passes private/public2646/2646,0fail/0skip:
  real RandomLite wide-range fault correction (44 RNG cases,31 new), one shared
  typed HW/Ninja mutation algorithm (19 new controls), and two test nameof fixes.
  RNG full-domain/fairness and whole rank transaction safety remain open. Current
  successor now has committed-tree CI and actual CodeFactor verification above.
- [x] Full Windows39943323494 on exact5a62e0d4 passed4m09s: core22146/0failed,
  165 excluded, updater301/301, launcher49/49, all8 stages0. CodeFactor reports
  8FIXED/3NEW,total2272; all7 older API targets cleared. The three follow-ups are
  addressed in the combined successor above, not a new cosmetic work queue.
- [x] User guardrail is explicit: defects first, mechanical style by rule in
  bounded automated batches; full CI for behavioral/integration checkpoints,
  not individual warnings. No risky protocol-switch reshape just for a threshold.
  Next genuine safety priority is AddFlag and its consuming caller transactions.

- [x] Whole-string name validation and seven older CodeFactor API records are
  locally verified: private/public Release2596/2596,0fail/0skip. Includes40 new
  name/caller controls (262144 bounded code-unit observations),14 real ranking
  removal controls and9 exact exception/seeded-state cases. Rename/uniqueness,
  ranking transaction safety and RandomLite range/fairness remain separate debt.
- [x] Full Windows39941895742 on exact1990e8bc passed in4m55s: core22083/0failed
  (165 excluded), updater301/301, launcher49/49, all8 check steps exit0.
  CodeFactor confirms0NEW/15FIXED,total2277, net50 fewer than2327. This verifies
  the previous semantic cleanup; the name/older-API batch is also verified above.
  No live-client, deployment or full parity claim follows.

- [x] Current semantic cleanup and corrected missing-rune assertions passed
  private/public Release2255/2255,0fail/0skip, including22 new classic/shop phase
  controls and the expanded full XP-flag/prestige families. Earlier2164 coverage
  remains: classic68+11, seal12+14, countdown26 and name-search31 controls.
- [x] CodeFactor on public2b993ece:18FIXED/15NEW,total2292. All13 targeted
  CA1827/CA1847 records cleared plus5 older ones. The15 new complexity/arithmetic
  records are addressed locally; remote successor analysis is pending.
- [ ] Full Windows39939873007 on2b failed5 obsolete missing-rune exception
  assertions: core22056 passed/5failed,165 excluded; updater301/301 passed.
  Launcher/later stages were not run. All five now assert safe coherent outcomes
  and pass within2255/2255 both trees. Corrected1990 full CI passes above.
- [ ] Whole weapon receivers2353/2354, shop paid refresh/purchase wallet/item
  transactions, native title eager lifecycle, Rune writers and flag transactions
  remain held. Query/component proof is not host adoption or durable recovery.
- [ ] Current Windows exposes no D: drive. Before any future live work, locate
  and revalidate the preserved client baseline; no remount/migration is attempted
  by this offline pass. The restore ZIP metadata is unchanged, not rehashed.

- [x] Active managed title opcode2313 now validates full ownership/catalog data,
  persists before session publication and checks exact source/peer lifecycle
  identities before delivery. Private and public Release each passed1008/1008,
  including80 active-route and55 real-file/shared-reader controls. PIN invariants
  remain covered. Durable recovery/native titles remain open; managed2152 now
  has its separate bounded save-before-publication fix above.
- [x] Prior combined local checks: private/public each1119/1119 selected core
  and301/301 complete updater tests, zero failures/skips. Includes15 additional
  CodeFactor API fixes, retained-handle process attribution and the seven-warning
  successor cleanup. Seven new cleanup error/skip/stack controls pass.
- [x] Full Windows39933407353 on exacteae56f67 passed in4m06s: core21682 passed/
  0failed (165 excluded), updater301/301, launcher49/49, all8 check steps exit0.
  Earlier39932501975 was also green;39930101290 closed failed updater39926199302.
  CI is not deployment, live-client proof or full parity.
- [x] Earlier CodeFactor confirmed the15 character-overload fixes absent, with then
  total2295. Latest two code commits have0NEW/10FIXED, clearing all seven newly
  introduced warnings and three older ones. TitleStorage.cs now has zero active
  findings; private/public1119/1119 pass again and final full CI is green above.

- [x] Sanitized spell admission and allocation: private and independent public
  Release each passed 510/510 selected cases, zero failures/skips. Rejected
  admissions preserve resources/authorization/soul/cooldown and return null outputs;
  real resource packets are prepared before commitment. Full cast executors and
  general packet bounds remain open in the source-bug tracker.
- [x] Two Hairface CodeFactor warnings are confirmed absent from the refreshed
  report. Three duplicate test-initializer warnings are locally repaired, with all
  71 old affected cases plus one alias Fact retained. Refreshed CA2244 is empty.
- [x] Eight ranking/rune CodeFactor query findings are locally repaired; private
  and independent public Release each passed 754/754, zero failures/skips.
  Invalid query rows no longer crash those lookups, and a wrapped byte Count
  cannot reopen an occupied rune bag. All eight are now absent from the refreshed
  report; historical total2314. Another15 character-overload fixes were verified
  locally and subsequently confirmed absent. No suppressions or weakened tests.

- [x] Source authority remains the local simplified-复 decompile; staged DX8
  VisibleDx8D13 is explicit. No switch to the old Wolves/DX9/default-D11 baseline.
- [x] Clone/pet producers, complete physical monster calculation and gear effects,
  solo/team arena query/wire families, equipment/item-view/refinement methods and
  native item-timer translation are implemented in modular concrete owners.
- [x] Whole DataCore.LoadClient/SetCharacterSides, RoleMail and CreateCharacte,
  warehouse Show/codecs, XP counter/duration/status, attributes/blessing/mentor
  grants, genuine Clan owners/query/removal/codecs and entire ClanTable save/load
  are now implemented as explicit components, not registered live receivers.
- [x] Exact saved state, whole champion/day/ban queries, race setup/record packets
  and football round/NPC/map methods are implemented without full event adoption.
- [x] Whole LoadClientItems routing/perfection, Confiscator records/containers/files
  and hair/face admission/equipment/2282 receiver are implemented as components.
- [x] Genuine Vendor listings/removal, Trade wallet escrow/query/contracts,
  SoloElite construction/pairing, visibility, protection and message responses
  are implemented. Full trading, teleport and tournament lifecycle remain held.
- [x] Whole monster damage-ranking/broadcast and native login/time/configuration
  writers are implemented as components; full damage receive/login remain held.
- [x] Entire PoleDomination state/round/scoring/furniture component, TeamArena
  scalar writer and ten receive fields are implemented, without host activation.
- [x] Whole Taoist power family and genuine GUILDWAR state/constructor/conductor,
  gate/query/policy/AddNpc prerequisites are implemented. Full war remains held.
- [x] Whole SpiritTable loader, anima upgrade rules, monster-reward state and native
  poison/map-effect/Blackspot methods are implemented. Power Arena guardian and
  countdown/finish methods, Dis City map/query/Pluto announcement methods and
  Saturn state use genuine owners. Full event entry/rewards/startup remain held.
- [x] Full pet creation, fairy 2336 codec/handler metadata, view republishing and
  screen-color latch are implemented; full teleport/visibility adoption is held.
- [x] Native timer rule/pool/subscription owners, all registration/scheduling
  methods, socket identity methods and hero 22210 writer are implemented.
  Tests never start a worker. Full Create/callbacks and modern Thread.Abort
  compatibility remain unfinished; no running scheduler readiness is claimed.
- [x] Nobility pole scoring/reset/furniture and full Prince/Duke/Earl owners are
  implemented. King Start/CheckUP remain held on genuine invitations/teleport.
- [x] Whole poker/roulette table loaders, display/admission/rule methods and raw
  codecs are implemented. Currency payouts, round worker and handlers remain held.
  Complete RuneRank/RechargeShop file/cache owners and the exact NameUsed slot
  now support the login graph; they do not prove eager account login or SQL.
- [x] Genuine death state, Jiang 2309 writer, complete source map lists, four
  tournament state/query owners and whole ExtremePk.SharePoints are implemented.
  The three scoped full-array sorts retain stable ties and singleton key faults;
  owned default/optimized Framework and modern candidate agree on52/52 rows.
  Full Dead/CheckUp/rewards/interface and eager lifecycle remain unadmitted.
- [x] Private and independent sanitized-public Release each passed4902/4902,
  zero failures/skips:4858 retained +44 death/tournament controls. Parent corrected
  an invented Player.OnSoulSpell test expectation: only the genuine client field
  exists; no field was fabricated. Packet-owning tests return their awaited Task.
  BaseFunc keeps the exact public NameStrCheck optional-bool
  signature; earlier RandGet/selection guards and the closed API check remain.
- [x] File tests used only guarded GUID-owned temporary INI/count/item files. They
  prove those component paths, not original saves, SQL or full account creation.
- [x] The registered Windows runner was recovered with approval and remains online.
  Full Windows run 39924242810 on exact `7781b620` passed in 3m 56s, including
  the sanitized spell/allocation batch. Terminal per-suite counts were not reread
  before the following checkout cleaned ignored receipts. This job predates the
  newer ranking/rune batch; it is not live-client or deployment proof.
- [x] Logging/ranking/login-wire source has full Windows proof in3m52s on exact
  b75f29ab. Hair setter/getter and earlier component controls remain intact.
- [x] Earlier Windows 39905802844 failed two cases and 39908229600 failed one.
  Recovery run 39909496494 closed those regressions: the exact Rifinery cache shape,
  opt-in bounded native-probe readiness and deterministic deferred-money tests.
  Every original module/thread/TCP/log assertion and no-ready default remains.
  Map 10250's special item still precedes the generic blocked-map gate; all five
  blocked-map controls and a forced-hit positive case preserve that source order.
  No production drop behavior changed. Full run completed in 3m 52s on `bf6b9eef`;
  [receipt](https://github.com/vgcman16/wolfconquer/actions/runs/39909496494).

## Correctness findings retained

- [x] Framework path-only quote/angle sanitization now uses the exact per-call
  36-character set. AllUTF16/flag controls and genuine monster fault logging pass;
  no static initializer, public API, null guard or logger shortcut was added.
- [x] Complete native degree/sector geometry matches all17512 ordered observations
  from both owned Framework builds; both actual copies match, not just digests.
  Wrapped distance, coordinate narrowing and sticky Arrange quirks are retained.
- [x] Both full native IP/account ban owners retain duplicate, expiry, no-op IP
  removal, file formatting and fault behavior. All53 controls use synthetic
  component state and guarded temporary files, not live login or original bans.
- [x] Timed purification/refinery expiry now retains Framework tick arithmetic,
  Int32 overflow expiry and mutable availability. Both source builds/candidate
  match11340 state/result +945 date rows;20 real-owner/timed-send cases passed.
- [x] Framework arena rank ordering reads every key once, including singleton
  faults. Scoped translation preserves that behavior; both owned x86 probes18/18.
- [x] Whole Pole ranking now uses the same materialized UInt32-key order after a
  separate30-row default/optimized Framework and modern-candidate comparison.
  Two whole-method controls preserve Winner and recycler order on singleton null.
  The fixture explicitly sets SobNpc type as the real loader does; no constructor
  or missing-object/null behavior was changed to make furniture tests pass.
- [x] Physical-monster floating scale keeps native operation order. Exact-integer
  rewrite was rejected780/1193; revised owned x86 default/optimized1193/1193.
- [x] RemainingTime cast-only proposal was rejected on6 reachable date extrema.
  Actual Framework TimeSpan tick multiplication plus scoped narrowing matched
  all316 source-expression controls on modern .NET. Getter/real wire tests passed.
- [x] Full Windows39892756807 found a real trade-fixture StringWriter capture race
  (20103 pass/1 fail/165 unexecuted). Shared locking and a1024-line concurrent
  control retained all25 trade tests;26/26 focused and corrected fullCI passed.
- [x] Native TCP-table reads now retry strictly growing122 buffers at most4 times,
  validate bounds and retain native errors/cleanup. Private/public60/60 focused
  cases passed; all TCP/module/thread/log assertions remain. Historical cause
  is unconfirmed, and future failures preserve full synthetic snapshot output.
- [ ] Whole IncreaseExperience gameplay remains held. Prior proposals differed
  on15/1280 and18/14080 rows; scoped expression-copy successor now matches all
  14080 actual-callshape rows/default+optimized source builds. Separate3072-row
  diagnostics have18 source-build differences, so this is not a general JIT rule.

## Remaining end-to-end work

- [ ] Close real Team/HP/aura and XP/RemoveFlag/Level/Class/equipment mutual cycle.
  Keep whole methods; do not substitute absent-flag or low-XP success branches.
- [ ] Close the full spell dispatcher and all required spell-sort/receive/death
  owners. Complete physical monster damage calculation is not damage application.
- [ ] Finish actual Inventory mutation/item Send, SQL account readers, eager
  ServerDatabase.LoginQueue/ExecuteLogin, whole LoadCharacter/LoginHandler and
  whole SaveClient. Save uses the SAME LoginQueue; no separate SaveQueue exists.
- [ ] Finish direct save-state/scheduler/event dependencies and full Clan/arena/
  warehouse mutations. Metadata, ranking packets and file leaves are not events.
- [ ] Adopt complete genuine owners into the replacement host and prove live
  login, gameplay, save/reload and English rendering. Current code tests do not
  establish host adoption or newer-client-only systems.
- [ ] Only then migrate one live routing/web file at a time; test login after each
  change and roll back only that file on failure.

## Preserved boundaries

- [x] Working restore ZIP retained:34082707691 bytes. Metadata is not integrity proof.
- [x] This offline pass changed no original saves/SQL, live routing or game/client
  process, and performed no deployment or live-client test.
- [x] Public export excludes game assets/binaries, captures, credentials, private
  configuration/history and ignored local evidence. Only root stages/publishes.
- [ ] Overall goal remains active. Standing October19 rough55% (40–70%, low
  confidence) estimate is unchanged; test/file counts are not parity percentages.

Prior detailed checkpoint receipts remain in Git history and
[public validation](https://github.com/vgcman16/wolfconquer/blob/main/VALIDATION.md).
