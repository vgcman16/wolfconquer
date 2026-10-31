# D13 scoped native stamina projection and admission controls

## Identity and scope

2026-10-31; Chinese7899 / VisibleDx8D13. This repairs the full-b8 source
gate failures without changing packet layouts or admitting malformed owners.
The target is one real current HP/Mana/Stamina participant, not a replacement
global Character parser. No7937/live/SQL acceptance is implied.

## Packet and catalog contract

Existing stamina2176 updates and successful action ordering stay unchanged.
No new packet, table, gameplay cost or catalog interpretation is introduced.
Native Stamina is UInt16. A genuinely absent saved value becomes runtime0 at
ordinary admission; a present malformed/out-of-width value refuses admission.
The saved input remains unchanged. Blessing does not create fallback funds.

## Validation and transaction rules

- Open only actual supported BCL dictionary providers/backing with supported
  ordinal comparers. A readonly wrapper cannot authorize an opaque or
  split-index custom backing.
- Exactly one case-insensitive Stamina key may exist. Duplicate aliases refuse,
  even with equal values. Present-null is invalid, not absence.
- Preserve unrelated key spelling, case-distinct rows, values, order and the
  source comparer during the admission projection. A scoped native read must
  not silently collapse these rows or impose complete-file parsing rules.
- Final proofs pin the actual backing/comparer and every raw row, including
  unrelated values. Subsequent mutation invalidates the proof.
- Complete Character persistence still applies its own strict ambiguity rules.
  Native projection permission never grants permission to save an ambiguous
  whole Character or to change HP/Mana/Stamina independently.
- A later attempted invalid Store publication must refuse without changing the
  current holder/native scalar, files, authorizations or already committed
  action accounting. Invalid initial saves cannot become admitted test owners.

## Expected behavior and fixtures

Retain all original invalid inputs and independent valid cast/wire/file oracles.
Migrate obsolete invalid-login assumptions to the actual admission refusal
path. Genuine legal absence and UInt16 boundary controls remain required.
Full b8 run41234349476 supplies the retained failing proof: 88Core failures,
including actual warehouse Store publications with unrelated DynamicID aliases.
DATA tests cover both aliases and readonly maps; those actual warehouse tests
provide admitted-owner integration coverage. Custom-provider refusals remain.

## Implementation and qualification

LegacyStaminaState and LegacyNativeOwnedStaminaTests.AttributeScope are the
finite production fix and new DATA controls. Additional affected tests must
preserve their distinct gameplay/persistence coverage. Root alone integrates
and executes verification. Current repair is source-only until actual results
are recorded; the old full failure is not retroactively green.

## Adjacent full-gate regressions

The real captured-alternate equipment query must finish its original stats
calculation after an AppendItems reentrant toggle. Beginning computation retains
same UID/account/native identity, class and shield-spell membership; completion
still requires exact current item/alternate/weapon inputs. Captured data alone
cannot mint a current WindWalker or Monk capability. Retain original maximumHP
and status oracles and foreign-source/completion denial controls.

The Caprice stamina-transfer branch requires target funds20 and representable
both-sided deltas BEFORE preparing its cooldown. An actually attached native
participant uses UInt16 bounds. A failed funds/overflow check leaves both exact
Character inputs and the old stamp unchanged. Independent XP/chance branches
are not redefined by this correction. Real40/80 to60/60 and65515 to65535 controls
must retain genuine ownership and successful packet/persistence checks.
