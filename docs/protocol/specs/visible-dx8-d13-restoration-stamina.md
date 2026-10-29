# RESTORATION-STAMINA-1: required numeric stamina for restoration

## Identity and scope
-Revision2,2026-10-29, isolated Root assignment against private202f/public970. Target existing VisibleDx8D13 managed BreathFocus11960/sort70 and SwirlingStorm12890/sort91.
- Fix recognized present malformed, negative, unsigned-overflow or empty Stamina being turned into the absent default before restoration/action admission. Preserve actual Store/registry/Frame/Character/spell persistence owners; no new saver/callback/authority or broad writer barrier.
- Root alone integrates/builds/tests/Git/live. Source-only until qualified; current running full40853051675 remains separate.

## Packet contract
Existing2176 Stamina/Mana updates, magic cooldown,2101 spell progress and2239 spell animation layouts/order stay unchanged. Invalid required initial Stamina produces no action cost/cooldown/restoration/progress/success output. BreathFocus valid below100 clamps its Damage restoration to100; SwirlingStorm Omnipotence valid below its actual100/150 threshold adds10 without clamping to that threshold.

## Catalog/table contract
Use actual saved Character.Attributes recognized Stamina and existing MagicType rows. Shared strict reader is private WorldPacketHandlers.TryReadLegacyRuntimeStamina(CharacterSnapshot,out ulong), authored by the other stamina helper: present ulong.TryParse(Integer,InvariantCulture), absent preserves existing HeavenBlessing/HeavensBlessing positive150 else100. No new maximum. Spell cost, damage, cooldown and blessing rules otherwise unchanged.

## Validation and transaction rules
- BreathFocus requires a numeric current value even with zero priced stamina because restoration consumes that state. Validation occurs in shared admission immediately after capturing the actual owner.Expected, BEFORE any prepared shortcut/resource/cooldown plan or commit; caller-only guards are insufficient because a retained caller can be stale. The exact same expected record is consumed by the existing CAS. SwirlingStorm determines Omnipotence from that actual expected owner; positive priced stamina is separately validated by the same preparation owner. Zero-cost/no-restoration SwirlingStorm keeps unrelated stamina optional, even when a stale caller previously had Omnipotence.
- Strictly re-read the actual returned resourceSession at the restoration boundary. A sender or independently accepted lifecycle may have published invalid state AFTER admission. Stop only unperformed continuation, log explicitly interrupted-after-accepted-admission, and do not refund/rollback or grant a default. This is a bounded fix, not complete compound-cast atomicity across awaited publishers.
- Retain valid arithmetic/absence and outer lifecycle. Do not replace accepted state with a stale captured stamina value. Do not invent clamps or numeric interpretation of malformed values.

## Expected wire behavior and fixtures
- Genuine owned saved-input fixture with real actor/registry/Frame/PIN/recovery/native binding/Character and Spells files. Raw Stamina enters saved data BEFORE actual ordinary admission; observe it in both current and actual reloaded input.
- Negative, text, ulong-overflow and empty cases on both required restoration paths refuse with unchanged required resources/cooldown/spell progress/files/no action output, live binding retained.
- Independent positive controls retain debit-Mana/cooldown-restoration-animation order and exact restoration arithmetic; absence normal100/blessed150 defaults, high valid values are not newly capped. SwirlingStorm with no cost/restoration does not acquire an unrelated numeric prerequisite.
- A post-admission corruption control, if authored, must use a genuine independent Store publication at an actual sender and retain its accepted outer accounting; no fake callback writer or refund assertion.
- Revision2 stale-caller controls invoke the actual restoration handlers with a retained caller and the genuine current Store/Frame/registry/recovery/context: current invalid state refuses before all outputs/debits, including current gaining Omnipotence; stale invalid callers do not veto valid current owners; current removal of Omnipotence keeps unused state optional. These differ from a later sender publication after accepted costs, whose accounting remains committed.

## Evidence alignment
Existing BreathFocus.cs always read stamina after resource preparation. SwirlingStorm.cs reads stamina in Omnipotence after resource preparation. Both used fallback ReadLegacyRuntimeStamina, making explicit bad values look like absent defaults. Existing runtime tests independently establish BreathFocus clamp100 and SwirlingStorm99->109 /149->159.

## Implementation and qualification
Only two assigned restoration production files change in this successor, removing stale caller-only initial guards while retaining strict post-admission interruption reads. REQUIRED shared dependency: helper-stamina-admission-rev2-next validates operation-required state on its actual captured admission owner before any shortcut/plan/commit. Original frozen restoration tests/factory plus new distinct stale-caller controls compose together; common fixtures stay untouched. Root qualification pending. Unrelated stamina readers, full native executors, broader proc/power-loss/SQL/wire/client acceptance remain open.

## Root local qualification (supersedes proposal status above)

Root local qualification2026-10-29: all39 selected old-API numeric
controls first failed on exact c6c product, alongside two separately OPEN stale-Mana
output controls. After the reviewed numeric23 composition, sequential stable
build05 completed with0errors. Fresh numeric02 passed93/93,0skips/failures.
The wider combat/admission selection passed2749/2749,0skips/failures; selections
overlap. Its filter explicitly omits only the two separate unimplemented output
controls. That is not a whole-suite or output-freshness success claim. The earlier
overlapping build04/test attempt was interrupted and discarded. Actual TRXs/logs
remain under root-stamina-safety-next. Full successor CI and live remain pending.

## Latest combined qualification

Root local qualification2026-10-29: final combined magic/admission/required-
stamina/Frame selection passed2804/2804,0failures/skips. Shared Store/native-
vitals/ordinary-publication/testing-tool neighbors passed289/289,0failures/skips.
Selections may overlap. Build10 passed with0errors/157historical warnings, none
in changed paths. The original41 safety controls first failed on old product
(39numeric and2pending-output), and18 XP admission controls separately reproduced
their old paid rejection behavior. All applicable controls now pass. Full
successor Windows CI and live client acceptance remain pending; the last full
is exact c6c/40853051675, whose actual outputs/results were already preserved.

This supersedes earlier proposal/pending-output scope notes for this published
checkpoint. FSB193/194 record the current output/XP boundaries; Caprice pair,
remaining readers and whole-action/native/SQL/client acceptance remain open.

Latest full qualification, superseding pending full-gate notes above:

Full Windows [40867334704](https://github.com/vgcman16/wolfconquer/actions/runs/40867334704)
passed on exact531a9aa3d8e17c18ccd9f5bf68d480cbc5295319, containing source5daa0fe4:
Core29,833 passed/152 existing fixture skips, Updates446/446 and Launcher57/57,
zero failures; all eight stages passed. Actual local20261029T114153515Z-2d796ae9
finished2026-10-29T12:24:15.2854218Z. Workflow/job/local phase logs and all3TRXs
are retained; matching complete World/DataTool runtime files were preserved
outside the runner before any next candidate build. This qualifies the published
required-stamina, XP eligibility and pending-cost-output source changes.
Caprice paired transfer/shared journal successors, live deployment/client acceptance,
SQL/power-loss safety and complete English/gameplay parity remain unqualified.
No original-save/route edit, client relaunch or retained gear resend followed.
