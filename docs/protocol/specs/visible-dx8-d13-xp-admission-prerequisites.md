# XP-ADMISSION-1: validate actual XP activation prerequisites before costs

## Identity and scope

Revision1, 2026-10-29, VisibleDx8D13. Source-only successor of the strict stamina
numeric23 candidate. Community-source ordering is a reference, not authority to
charge a rejected action. Root alone integrates and qualifies this package.

The bounded correction covers AttachStatus activation of Cyclone1110,
Superman1025, FatalStrike6011 and SuperCyclone11970; actual ThunderRampage15780
class admission; and required numeric stamina for an accepted XP-only maximum
restore. Independent supported non-XP special damage branches remain supported.

## Packet contract

No packet/schema/catalog changes. Preserve successful stamina2176/type8, Mana
2176/type2, existing ColdTime and XPList/status/animation order. Missing required
XPList, wrong required class or malformed required restoration stamina refuses
before any debit, cooldown, XPList consumption, saved write or success output.

## Catalog/table contract

Use the actual existing `TryGetLegacyAttachStatusOpenXpSkill` and
`TryGetLegacySpecialXpSkillDefinition` descriptors with their exact sort/ID
matches. Do not mirror a new table or infer all XP-named skills require XPList.
Existing catalog costs, class predicate, status bit encoding and durations remain.

## Validation and transaction rules

Add a pure XP eligibility helper immediately after actual owner capture assigns
`session = owner.Expected` in generic admission. Keep the existing pure plan,
real registered writer, expected-record/issuance CAS and publisher unchanged.
The helper must not accept a stale caller record as authority for current state.

AttachStatus XP activation requires current XPList. ThunderRampage's actual
descriptor requires the existing Thunderstriker class regardless of XPList.
Stamina is additionally required only when a matching descriptor restores it AND
the current owner is entering XP activation. Use the shared strict reader:
present malformed fails; absent retains existing100/150 defaults; no new cap.

Map1039 bypasses priced costs, not required entitlement/class/restoration state.
An unused zero-cost attribute remains optional. In particular, Omnipotence and
other special skills without XPList retain their independent damage path; they
must not acquire an XP-only restoration requirement.

Keep post-cost current-state checks as interrupted-continuation protection, with
accepted accounting retained. Strictly read required restoration stamina again
before changing it. Never silently replace a newly invalid value by a default.
Do not add a second admission pipeline, callback owner or synthetic saver.

## Expected wire behavior and fixtures

Create actual saved Character/gear/spell inputs before owned bootstrap/admission;
use genuine Frame, registry, native binding, PIN, roots and Recovery. Old-API
refusal controls use real paid costs to expose missing XPList/wrong-class debits.
Restoration invalid cases use zero priced stamina plus real Mana/cooldown so the
numeric paid-stamina guard cannot hide the bug. Retain positive activation,
map1039/unused-field and special no-XP behavior. Correct the two explicit older
tests that required Mana debit for missing-XPList/wrong-class rejection.

## Evidence alignment

- Status.cs currently admits costs before the XPList guard; its old FatalStrike
  test explicitly requires493Mana after rejection from500.
- XpSkillSpecials.cs currently admits costs before ThunderRampage's class guard;
  its old wrong-class test explicitly requires the same debit.
- Both modules still call the permissive stamina reader for maximum restore.
- FrostGaze's target-stamina mutation, monster sit interruption, FatalCross
  restoration and support/output reads remain separate open consumers.

## Implementation and qualification

This spec precedes source/test authoring. No execution result, live acceptance,
physical-stamina hydration or saved-stamina durability is claimed. Root must run
old-product refusal controls before the correction and then qualify positives,
refusals and neighboring XP/status/non-XP families. Awaited output freshness and
compound XP activation atomicity remain separate boundaries; no refund is added.

## Root adjacent unsupported-handler correction

After every recognized AttachStatus handler has declined and the generic status
lookup is absent, the fallback must log unsupported without invoking resource
admission. There is no implemented action to charge for. This does not change a
successful handler or claim every supported target gate is validated before costs.
Two genuine owned controls retain paid and zero-priced/unused malformed stamina
inputs and require unchanged resources, cooldowns, files, entitlement and outputs.
The existing Main Cyclone and unsupported tests now retain the original records
and reject partial debits rather than enforcing them. The separate unsupported
owned controls were added during integration, with no old-product execution claim.

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
