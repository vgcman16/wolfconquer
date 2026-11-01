# Offline action and combat checkpoint

This is an unfinished source checkpoint for7899/VisibleDx8D13, not a client
release or a7937 runtime/profile. It adds no game assets, saves, captures,
credentials or deployment configuration. Existing private/held work is excluded.

Implemented paths include full-width native Action and movement, owned physical
hit/repeat admission, calculated normal-death notifications, joint Character/
Spells recovery, derived-spell ownership and query23, separate account/player
identity, paired movement recovery, and current range/metadata witnesses.
Applicable unjoined modifiers and nonplayer lifecycles remain unfinished.

Owned offline verification before this checkpoint:

- Build16 passed with zero errors and159existing warnings.
- Combined affected-family run passed435/435, zero failures/skips.
- Wider action/attack/login/Hawk run passed171 and failed24 on newly exercised
  recovery-fixture lifecycle expectations; the failures are retained.
- The corrected24 cases, armed-target output fence and enabled background-worker
  shutdown then passed26/26. Selections overlap and are not summed.
- The target-repeat retirement defect has a genuine failing-before/passing-after
  control: a real target attack arms its repeat, a committed attacker's output
  fails, saved HP remains committed and both retired native repeat flags stop.
- Enabled scheduling executes a due repeat without a manual tick and drains
  through actual runtime shutdown. This is not exhaustive concurrency validation.

The exact checkpoint's broader integration gate is still pending. No deployment,
SQL acceptance, host-cleanliness, live-client or project-wide parity claim follows
from these tests. The live client/updater execution hold remains in force locally.

Remaining priorities are durable HP-to-proficiency reconciliation, complete
nonplayer/modifier paths, full SQL and economy ownership, transaction-safe trade,
social/events, protected native login and broad English/live acceptance.
