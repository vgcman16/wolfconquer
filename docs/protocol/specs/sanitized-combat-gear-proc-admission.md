# PROC-ADMISSION-1: StarFlow and nested damage admission

## Identity and scope
- Revision 1, 2026-10-29; Root's sanitized-parity implementation scope. Base: current active managed source, plus the separately frozen joint-vitals candidate. Source-only helper; Root alone integrates/executes.
- VisibleDx8D13 active CombatGear StarFlow 10950 and nested player damage procs MagneticLight 20220 / AblazeBlade 20360. No new packet, table, charge, counter or native component activation.
- Prepare applicable ownership, learned descriptor, alive/view/range/PK/target, actual resource metadata, balances and cooldown before one actual current Store publication. A failed proc cannot debit stamina/Mana, start its cooldown or publish its success. Independent outer attack/StarFlow trigger bookkeeping retains its own outcome.

## Packet contract
- Existing update 2176 and spell animation serializers/profiles remain. Successful StarFlow cost order is stamina then Mana; nested costs emit stamina only, with Mana silent. Neither path adds ColdTime packets. Map 1039 bypasses costs, not cooldown eligibility.
- Capture immutable cost frames from the committed preparation. Later sends cannot change the original cost or deadline. Postcommit loss/failure is a committed interruption, never a refundable rejection or authority to retry.
- The concrete receipt validates the exact committed owner and observed peer records before/after each cost send, including an empty cost batch. A same-identity mutation stops remaining prepared output and continuation; a genuine bound issuance is fenced without refunding committed files or erasing a replacement identity. Identity-only refresh is not proof that a prepared Mana packet is current. No writer barrier is introduced and an already in-flight frame cannot be retracted.

| Field/path | Number/offset | Wire type/width | Meaning | Presence/repetition | Bounds/default | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| Stamina | existing 2176 type 8 | existing scalar serializer | committed remainder | positive cost outside 1039 | source positive cost + ScurvyBomb 6 | current source and existing independent positives |
| Mana | existing 2176 type 2 | existing uint/ulong serializer | committed uint-domain remainder | StarFlow positive cost only; nested silent | real MPMaster reduction before commit | current source and joint-vitals spec |
| Spell animation | existing serializer | unchanged | committed proc effect | existing valid execution sequence | actual target/descriptor | existing positive wire fixtures |

## Catalog/table contract
- Existing MagicType rows/learned spell snapshots, real item/rune metadata and CombatGear selection only. Supplied 20220/20360 rows have zero Mana cost; nonzero tests are explicitly synthetic metadata controls, not claims about those rows.
- A positive Mana cost requires its actual catalog/calculator inputs; zero/bypassed costs do not acquire unrelated optional rune requirements. Negative cooldown/invalid numeric state refuses before preparation publishes anything.

| Column/offset | Name | Type/width/encoding | Validation | Packet/behavior relation | Evidence |
| --- | --- | --- | --- | --- | --- |
| MagicType 9 / 29 / 47 | Mana / stamina / ColdTime | existing decoded ushort / byte / int | actual learned current row, nonnegative cooldown, bounded remainder/deadline | cost and cooldown preparation | current parser and source-row fixtures |
| Selected rune | MPMaster | existing real item/catalog lookup | actual worn row/metadata before commit | Mana reduction | resource calculator; existing tests |

## Validation and transaction rules
- Capture the real current record and native connection generation. StarFlow requires its learned descriptor, non-direct OpponentUID=0 shape and living caster before costs. Its area victims retain their existing independent target-selection phases; no invented minimum-victim charge rule.
- Nested proc preflight includes actual selected learned row, original participant generations, caster/selected target alive/view/PK/permission rules, direct range and Magnetic battle-power roll. The roll occurs once for admission, not again after debit.
- Preserve the independent selected-book and steed-entry prefix, and the outer StarFlow trigger/counter lifecycle. 20220/20360 are excluded from weapon authorization; these entry writes are outside the resource-cost repair. Do not claim the whole rejected invocation leaves every field/output unchanged. Intentional failure responses retain their own meaning; questionable broader entry-prefix gameplay semantics remain OPEN.
- The shared pure resource planner stages stamina, uint Mana remainder, natural cooldown and cost frames without Set/Send. No Grace/HeavensWrath exemption is invented; existing CannonBarrage/BlackbeardsRage rules remain.
- A missing Store is component compatibility only when both the retained session and actual connection have no ordinary binding or latched owned player. Retirement/missing live storage never restores this fallback.
- StarFlow uses genuine identity-and-record CAS. Nested uses a focused real Store compare-and-publish with both participant records/identities held under the existing ordered connection gates. Only the proc caster changes; the peer is an observed participant. Existing real full Character writer and joint HP/Mana actor proofs precede publication when Mana changes. No accepting saver, fake owner or pretend journal.
- Definite preflight/CAS/file refusal preserves proc balances/cooldown/output. A committed writer with unproven publication retains its afterimage and uses existing recovery fencing; no rollback/refund. Postcommit notifications/owner loss stop proc continuation and retain committed accounting. An unrelated valid outer attack is not automatically refunded.
- After commitment, current identity/view/alive changes can produce a committed miss/interruption; they do not retroactively turn the already accepted proc into a preflight rejection. Target eligibility is captured before debit; later changed participants must not inherit an old authorization.
- Magnetic's accepted self-status phase advances through another genuine observed-peer CAS and retains a new exact receipt for owner/view status and archive notifications. This is a committed effect phase, not a second resource debit or a claim that all damage/status/transport is one global transaction. Postcommit target authorization is consumed without redrawing its preflight RNG.

## Expected wire behavior and fixtures
- Preserve independent successful stamina→Mana StarFlow and nested silent-Mana/no-ColdTime fixtures. Replace partial-debit expectations, including repeated nested cooldown attempts, with unchanged refusal state.
- Cover metadata/Mana/cooldown/dead/target/range/BP refusal, map1039 bypass, actual worn-rune preparation, immutable cost/deadline through sends, lost original identity, actual file refusal, real current owner/native/saved Mana at first output, and outer attack conservation.
- Genuine saved-owner tests use existing owned admission/Frame/registry fixtures. Existing no-file component tests remain labeled component behavior, not durable/live proof.

## Evidence alignment
| Requirement | Spec clause | Decoded catalog reference | Bounded wire fixture | Status/conflict |
| --- | --- | --- | --- | --- |
| Failed admission unchanged | prepare all gates then CAS | real resource metadata | old tests require partial debit | deliberate sanitized source correction |
| Successful cost packets | unchanged serializers/order | existing rows | existing positive tests | preserve |
| Full saved Mana/current native publication | real typed writer | complete Character | joint-vitals candidate | prospective dependency; Root qualification pending |
| Outer lifecycle | separate scope | existing trigger/proc selection | existing outer attack fixtures | no invented refund/counter rule |

## Implementation and qualification
- Focused common proc preparation/publication modules plus the two admission callsites, nested executor preflight/captured authorization, and a concrete observed-peer Store CAS helper. Frozen joint-vitals files and Root's separate zero-Mana calculator remain untouched.
- Root must retain safe old-API red controls and run all affected callers/goldens, genuine owners and broader combat/HP/Mana/consumable/trade tests. No execution results are claimed by source authorship.
- Global damage/status/file/SQL atomicity, power-loss, native whole-component activation and live client acceptance remain outside this admission fix.

## Root review: explicit invalid stamina

A present Stamina attribute must parse under the existing unsigned integer
contract. Negative, malformed, overflowing and empty values cannot be treated as
the absent-field100/150 default, including on the resource-bypass map. Preserve
the supported missing-field defaults and valid values; introduce no guessed
gameplay maximum. Genuine saved-input admission controls must distinguish this
preflight refusal from owner/fixture failure. Other callers of the legacy
fallback reader remain a separate audit until explicitly corrected and tested.

Root qualification2026-10-29: the two genuine old-entry Mana refusals
failed before this product, then all2656 LegacyMagicAttackRuntimeTests passed,
zero failures/skips. The neighboring native/owned/ordinary/consumable/HP/mail/
equipment/trade/runtime selection passed1883 with122 existing fixture skips,
zero failures. Selections overlap. All8 malformed saved-Stamina controls first
reproduced admission success, then pass after the proc-local guard. Build03:
zeroerrors, no changed-file warnings. Full successor Windows CI/live remain pending.

Latest full qualification, superseding the pending gate above:

Full Windows [40853051675](https://github.com/vgcman16/wolfconquer/actions/runs/40853051675)
passed on exact c6c706464e8a09e5da3dc3b0697dbda3f5e25c57: Core29,685 passed,
152 existing fixture skips, Updates446/446 and Launcher57/57, zero failures;
all eight stages passed. Local run20261029T095254390Z-3647c6d3 finished
2026-10-29T10:34:20.1942239Z. Actual workflow/job/phase logs, all3TRXs and matching
World/DataTool builds are preserved outside runner. This qualifies the offline
testing tools, joint owned HP/Mana and automatic-skill admission checkpoint.
Live deployment/client acceptance, complete native-resource ownership, SQL/
power-loss durability and full gameplay/English parity remain separate and open.
No client relaunch, original-save/route edit or retained gear resend followed.
