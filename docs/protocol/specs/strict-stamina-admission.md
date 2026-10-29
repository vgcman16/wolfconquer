# STAMINA-ADMISSION-2: required stamina reads in managed cast preparation

## Identity and scope
- Revision 2, 2026-10-29; Root's bounded source-only milestone on current shared private 202f0e25/public 970f plus generated c6c. The frozen revision 1 remains unchanged. Root alone integrates/executes after the active full-run/preservation boundary.
- VisibleDx8D13 active generic cast preparation and RootFirstFamily debit preparation/preflight. Add one shared private partial-class reader; do not change the old reader or unrelated handlers.
- Present malformed required stamina must refuse before costs, cooldown publication, authorization consumption, manual effects or success output. Requiredness includes the captured current owner's BreathFocus11960/sort70 restoration and SwirlingStorm12890/sort91 restoration when that owner actually has Omnipotence. Missing state retains existing blessing defaults. No new gameplay cap or physical-stamina ownership claim.

## Packet contract
- No packet/schema/layout change. Preserve valid generic stamina→Mana→ColdTime sequence and Root's existing manual-prefix/stamina/Mana sequence; existing independent goldens remain.

| Field/path | Number/offset | Wire type/width | Meaning | Presence/repetition | Bounds/default | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| Stamina update | existing 2176 type 8 | existing unsigned scalar serializer | accepted remainder | positive required cost | existing ulong domain | existing reader/update serializers and valid goldens |
| Refused malformed stamina | not applicable | no proc/cast success packet | unchanged resource/authorization/cooldown/output | actual positive stamina cost or independently required restoration | no default substitution | sanitized-parity requirement |

## Catalog/table contract
- No new catalog. Stamina is the existing Character.Attributes entry recognized by `TryGetValue("Stamina")` under that dictionary's comparer. Preserve `NumberStyles.Integer` and `CultureInfo.InvariantCulture` ulong parsing. Do not newly recognize unrelated keys or infer duplicate-row semantics beyond the existing Character parser.
- Present empty, negative, nonnumeric or overflowing values fail the reader. Absent values use the existing ordered blessing aliases `HeavenBlessing`, `HeavensBlessing`: the first successfully parsed alias decides (positive gives 150, zero gives 100). An invalid first alias can fall through to the second, exactly as before.

| Column/offset | Name | Type/width/encoding | Validation | Packet/behavior relation | Evidence |
| --- | --- | --- | --- | --- | --- |
| Character attribute | Stamina | existing string -> ulong Integer/Invariant | present must parse; no invented cap | debit remainder | WorldPacketHandlers.PlayerMagic.cs; Common.Attributes.cs |
| Character attributes | HeavenBlessing / HeavensBlessing | existing ordered unsigned lookup | absent-stamina defaults only | 150 or100 starting resource | same existing reader |

## Validation and transaction rules
- Exact API: `private static bool TryReadLegacyRuntimeStamina(CharacterSnapshot character, out ulong stamina)` in `WorldPacketHandlers.Stamina.Validation.cs`, same World partial class. Present invalid returns false; absence alone may default. A false result outputs zero without publishing it.
- Immediately after `TryPrepareLegacyPlayerMagicCastAsync` captures the real owner and replaces its local session with `owner.Expected`, validate restoration-required Stamina with that strict reader: BreathFocus11960/sort70 always; SwirlingStorm12890/sort91 only when the expected owner has Omnipotence. Refuse with `requiredStaminaInvalidBeforeAdmission` before the prepared-Root shortcut, resource bypass, local plan or commit. The existing exact expected-record CAS couples this validation to the admitted record; a stale caller snapshot is neither authority to reject a valid current record nor permission to accept an invalid current record. No callback validator or extra owner is introduced.
- Generic parent prepares cadence locally, then costs, then cooldown locally, then actual current owner CAS and accepted frames. Validate stamina iff the CALCULATED `UseStamina` is positive (including existing Scurvy/DragonPierce/free-spell rules); discard all local preparation on failure.
- Full Root parent validates cadence then `HasLegacyRootStaminaForPreflight` BEFORE cooldown/manual MP and authorization preparation. That preflight and final `TryPrepareLegacyRootCosts` require a valid value iff Root's actual UseStamina+Scurvy cost is positive. The resource-only Root CanUse caller still prepares its cooldown locally before final costs; failure publishes neither.
- Map1039 resource bypass returns before paid-cost stamina validation. Genuine zero-stamina-cost paths do not query an unused attribute; Mana/cooldown/other actual prerequisites remain intact. Required restoration state is independently validated before either bypass. SwirlingStorm without current Omnipotence acquires no restoration prerequisite. Grace/free-spell behavior retains its existing policy.
- Reuse existing actual saved owner/registry/Store/CAS/full Character writer and lifecycle failure mechanisms. No saver callback, fake owner, new writer barrier, grant or changed restoration arithmetic. Later independent changes after accepted accounting remain an interrupted continuation without refund/retry; awaited output freshness is a separate OPEN scope.

## Expected wire behavior and fixtures
- Genuine saved-input controls contain explicit invalid stamina in the original owned file before bootstrap/admission. Exercise actual existing generic and resource-only Root preparation APIs; assert exact original owner/peer record/native/saved values, identity, cooldown, files and output on refusal.
- Reader controls cover unsigned boundaries, whitespace/positive sign under the existing contract, missing100/150 defaults and alias priority, present invalid despite blessing, and attribute-row order. Valid/default/bypass/zero-cost controls retain literal cost values/order.
- The separate restoration successor supplies genuine stale-caller/current-Store controls: a valid retained caller cannot admit malformed current restoration state, an invalid retained caller cannot reject valid current state, and actual current Omnipotence determines SwirlingStorm requiredness. Use zero priced stamina with actual Mana/cooldown costs so paid-cost validation cannot mask the restoration gate. Its handlers remove the redundant stale-caller entry guards and retain post-admission reads for later independently accepted changes.
- Existing Root/manual/generic protocol goldens are retained; source tests do not establish live client acceptance, global atomicity or physical-stamina ownership.

## Evidence alignment
| Requirement | Spec clause | Decoded catalog reference | Bounded wire fixture | Status/conflict |
| --- | --- | --- | --- | --- |
| Present invalid must not default | shared reader | actual saved Character attributes | new owned refusal controls | source correction candidate |
| Missing defaults | ordered blessing aliases | current reader | valid/default controls | preserved |
| Requiredness | actual debit cost plus independently required current restoration | current calculators, parent order and exact owner capture | zero-cost/map1039 and separate stale-caller restoration controls | preserve unused-state behavior |
| Physical/native stamina | not implemented here | old ushort native fields remain | unavailable | OPEN integration, not claimed |

## Implementation and qualification
- Owned production paths are exactly the new validation file, the two existing Resources partials and `PlayerMagic.Preparation.Admission.cs`. Other helpers consume the same private API in disjoint restoration/passive modules. Restoration handler/test changes belong to its separate successor. The old `ReadLegacyRuntimeStamina` remains unchanged for not-yet-migrated callers, explicitly OPEN.
- Root reviews/intakes/runs saved refusal/default/bypass controls and relevant generic/Root/cast/consumable neighbors after the active full gate. This package is unqualified source; no execution claim.

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
