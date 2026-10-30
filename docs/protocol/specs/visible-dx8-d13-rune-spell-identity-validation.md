# Rune spell identity validation

## Identity and scope
- 2026-10-30, target `VisibleDx8D13`, source base `6cb2babc`.
- Sanitized Parity correction for managed Rune unequip and native Rune lookup.

## Packet contract
No packet layout, opcode or successful sequence changes. Native saved spell
identity is a nonzero ushort; Rune metadata encodes ID*100+level. Its two decimal
level digits are a Rune encoding rule, not a new cap on general spell levels.

## Catalog/table contract
Read the actual loaded ItemType.RuneSpellId or native ItemsBase metadata.
Encoded zero is an explicit no-skill value. A nonzero quotient must fit the
actual nonzero ushort spell key. Reverse lookup accepts levels0..99 and a valid
spell key; never carry a larger level into another ID. Required missing/null
metadata and null arguments must fail explicitly, not through incidental traps.

## Validation and transaction rules
Validate before selecting a learned spell for removal. Invalid metadata must not
wrap onto an unrelated key or let the Rune move, change HP, save, publish success
or produce a WAL. Preserve intentional item/SpellID equality no-skill behavior.
Capture the actual pool reference once and perform one complete reverse scan;
do not re-read a replacement owner between finding and returning a row.

## Expected wire behavior and fixtures
Replace tests requiring UInt32-to-UInt16 wrap, decimal level carry and null-reference
traps with rejection invariants. Retain normal lookup, metadata replacement,
zero no-skill and maximum valid ID/level controls. Use an owned Runtime/Frame/
Store Rune unequip fixture to prove invalid metadata leaves all state/files/
outputs exact, and valid maximum-ID removal still follows the real writer.

## Evidence alignment
Source: RuneUnequipSpells, native RuneItemStorage.SpellLookup, ItemType field25,
LegacyGameItemPacket.CreateRuneSpellOverride and actual native spell storage
validation. No historical Rune recovery metadata owner is inferred by this fix.

## Implementation and qualification
Root owns these disjoint source/test paths in the item/spell candidate. FSB-244
is draft until builds and behavior checks run after the active Windows gate.
No client execution, SQL, deployment or full-Rune completion claim.
