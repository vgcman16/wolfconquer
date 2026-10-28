# VisibleDx8D13 combat progress regression repair

## Identity and scope

Public source baseline4d9280fe. Source-derived response to archived full
CI40683372742:117 `LegacyMagicAttackRuntimeTests` family failures,24 native
attack dispatch failures and related LayTrap/status cases. No patch/profile
change. This helper neither executes tests nor activates a server/client.

## Packet and catalog contract

Keep existing successful spell2101, proficiency2041/2199 and real native system
message2179 bytes/order. Spell-associated right/left progress precedes spell
notification; a physical right/left pass follows it. Existing catalog thresholds,
integer scaling, spell strict-greater leveling and widened overflow refusal stay.
The existing valid-path and literal-frame tests remain acceptance criteria.

No new opcode/catalog layout is inferred. Pirate child spell IDs/levels come
from the actual selected Jade attributes and Pirate skills rows; empty skills
entries remain skipped. Spell ID0 is invalid and cannot be a success fixture.

## Ownership and transaction rules

- Bound ordinary publication retains real Store-issued binding, exact actor,
  canonical snapshots, ordered gates and copy-on-write map publication.
- Unbound native training keeps legitimate existing physical row references.
  Prepare every desired scalar, frame, comparer lookup, structural allocation
  and raw witness before any original spell/proficiency scalar store. Consume
  all actual original/candidate witnesses before publishing either participant.
  A same-count raw row replacement or resize cannot qualify via count alone.
- A no-weapon path does not use the optional proficiency owner. Missing required
  active weapon owners still refuse; an unrelated foreign unused slot is ignored.
- Pirate requests pre-read the full actual child list before the first writer;
  zero IDs and later table-read faults reject before any child prefix output.
- Managed component fixtures must provide the real current Store record,
  Character(AccountId)/Spells(UID) files, roots and initialized Recovery. Adapt
  fixtures through `LegacySpellProducerTestOwner`; do not weaken runtime owner
  checks or create accepting saver/owner substitutes.

## Fixtures and evidence alignment

The archive is a failing baseline, not evidence the proposed repair works.
Retain all successful damage/formula/status/wire assertions. The fixture
adaptations only establish real owned persistence prerequisites before actions;
the main spell progression positive also reloads the saved row and checks the
real journal is clear. Native whole-training positive cases retain source row
identity and output order. Malformed metadata/reentry cases assert no partially
published proficiency, spell, packet or success frames.

## Qualification and open boundaries

All postimages are source-only and unexecuted. Root must review, combine shared
fixture/observer repairs, compile and run focused plus full CI. The separate
proposed journal-v10 milestone is not this baseline and remains held. No SQL,
live target/client gameplay or full server parity claim is made. Pirate cross-
call comparer/transport batch atomicity and complete cast-resource coordination
remain separate work. FSB166/167 draft notes are in this artifact's additions.
