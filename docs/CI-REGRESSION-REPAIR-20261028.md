# Integration regression repair - 2026-10-28

## Observer follow-up

Public2811f796 full Windows40718716578 finished with Core29,266 passing,
7 failing and164 existing skips; Updater446 passed. Launcher/parsers were not
reached. The seven failures exposed old test assumptions that public observers
held Store locks; those assumptions are now replaced by actual worker-task join,
publication, retirement and old-owner output checks.

A distinct DragonSkin production miss swallowed typed postpublication failures.
The corrected path preserves the original outcome/cause and captured real
Recovery fence, while allowing normal observer replacement outside gates.
All six new fault controls failed against old source for the missing typed
outcome; the repaired affected/surrounding selection passed207/207, no skips.
See FSB-176 and [the detailed contract](engineering/DRAGON-SKIN-PUBLICATION-OUTCOMES.md).
No client or server was restarted/deployed. The successor full gate remains required.

## Earlier integration batch

The replacement server repairs startup, saved spell/item state and postpublication
failure handling against the existing VisibleDx8D13/Chinese7899 contract. Existing
packet goldens remain; this is not activation of every decoded7935 feature.

## Verified locally

- Release Core/test build: zero errors; existing warnings remain.
- Focused regression and new ownership controls:520 passed,0 failed/skipped.
- Wider magic, native dispatch, traps, Labyrinth/DarkHorn and boss reward checks:
  3557 passed,0 failed/skipped. These selections overlap.
- Real owned Character/Items/Spells files, journal outcomes, current native/managed
  owners, exact wire fields and callback/disconnect/reissue boundaries are covered
  only as specified by their individual tests.

## Resulting behavior

- Repeated paid epic learning no longer charges2150CP for an already learned skill.
  Required missing item storage refuses before grants; joint Monk reward UID/item/
  spell creation remains incomplete.
- HP normalization publishes the scalar saver's exact field projection and the
  existing EpicTrojan schema aliases, so later valid book use has a coherent
  saved/current baseline. Unknown attributes and valid composite slots remain.
- Physical Rune StackSize0/1 survives unchanged. Valid item/deposit/perfection
  order differences are checked by full record and ownership; the exact raw
  journal before-image still detects a later reorder or other change.
- Runtime equipment queries retain the real recovery owners and can finish
  derived spells and later startup stats. The61512/5000 status fixture passes.
- Native training retains legitimate rows and ignores unused optional owners;
  malformed Pirate child/table input refuses before the first spell prefix.
- Direct Store notifications run after their publication gates; concrete Frame
  persistence retains its actual current-owner gate. Typed postpublication
  failures suppress compensation and success. Persisted transaction writers
  deliberately do not repeat ordinary persistence/observers.
- Converted8418 EagleEye and Shock use real issued records for countdown, post-
  retaliation HP and saved spell progress. Copied UID or same-record reissue
  cannot acquire the prior action. Committed images survive later delivery loss.

## Required next gate and limits

The preceding public4d9280fe full Windows run failed (Core28755 passed/444 failed,
Updater446 passed; launcher/parsers not reached). The commit containing this
repair needs its own exact-public full Windows run; the earlier failed result
and these focused passes do not establish that gate.

No new build was deployed or logged into the live client for this checkpoint.
No SQL transaction, universal native SaveClient, every combat/helper race,
crash/power-loss durability, full English localization or complete game parity
is claimed. Unsupported unrepresentable opaque item records still refuse.
A direct before-image checker control is not a concurrent-writer interposition.
Unbound Frame spell-padding and full proficiency persistence remain separate
unfinished work. See FSB165-175 and their explicit boundaries.
