# Visible DX8 D13 owned Dragonhowl floor-drop controls

## Identity and scope

2026-11-01, source-only regression intake against proc-reward-caller-source08.
Target: VisibleDx8D13, actual saved native19 gear and Physical-selected17490/17650.
Dependencies: frozen Dragonhowl XP overlap input/catalog helper and the current
OwnedEquipmentBehaviorFixture actual admission path. No live-client proof.

## Packet contract

Actual Physical ingress1009 selects the learned gear proc, whose final successful
animation is2239. Monster lethal-death1009 precedes ordinary floor output2164; floor
publication precedes the random-CP reward and final animation. Existing independent
4200 monster-damage golden and7mana/5stamina cost remain unchanged.

Floor2164 has149-byte payload plus4-byte header. Used payload offsets: UID0u32,
item4u32, x8u16, y10u16, MaxLife12u16=40, type14u8=1, owner20u32.
Other bytes in an ordinary monster drop remain zero. The monotonic UID is observed
from the real issuer; tests never reset, replace, or invoke the allocator themselves.

## Catalog/table contract

Complete64x64 LegacyDMapTerrain DATA, each cell has its actual x/y index and
TerrainFlag0(walkable) or1(blocked). The exact query is passed through the real
owned-admission Frame constructor. LifePotion1001000 and money1090000 are actual
catalog rows. Family910 has drop_hp1001000. Source-supported saved roll49 selects
four money attempts;504 selects four HP-item attempts, of which ordinary item
planning stops after the first no-ground result. Amount50 selects money1090000.
No callback-bearing map/catalog substitutes are used.

## Validation and transaction rules

Preparation owns no floor reservation or UID. Real killed-phase commit allocates
money UID only after successful placement, while selected item no-ground consumes
one UID. Default capacity4096 is made genuinely unavailable through4096 real pickup
reservations, never a replaced floor store or private capacity mutation.

All existing visible/reserved UID collisions, including an expired visible row,
remain refusal. An expired row is seeded through public AddVisible and the actual
next UID is observed, never counter mutation. Read that preserved row at its original
creation time: SnapshotVisible(now) would itself expire it and invalidate the test.
Such refusal after accepted cost/monster death is a committed-prefix failure and
must fence actual participants without retry, CP reward, or final animation.

On first floor2164 sender loss the entry and consumed UID remain committed. The
actual binding/holder/recovery fence must prevent later output and further attacks.
This is in-memory phase safety, not crash/restart/all-reward atomicity.

## Expected wire behavior and fixtures

Positive no-ground: retained actual actor, exact7mana/5stamina debit, killed monster,
CP+5, final2239/4200, no floor frames, zero money UID vs one item UID. Full pickup
capacity preserves every seed entry and real reservation. Blocked-terrain controls
leave the floor empty. Failure controls retain the exact accepted cost/death prefix,
no CP+5, and fence before final animation or replay.

## Evidence alignment

- FloorItemRuntimeStateStore.OwnedDragonhowl.cs: captured sequence/choice and committed
  entry ownership; collision check deliberately precedes captured expiry removal.
- WorldSessionStore.OwnedDragonhowl.MonsterDrops.cs: actual registered attempts,
  UID/publication before send, exact owner/currentness after each output.
- WorldPacketHandlers.OwnedDragonhowl.MonsterDeath.Drops.cs: money/item UID policy.
- Frozen overlap helper: real native19 recipe/ordinary Physical4200 damage evidence.

## Implementation and qualification

All new tests and the optional-at-end terrain constructor/fixture plumbing are
SOURCE ONLY, UNBUILT and UNRUN. Root alone integrates and qualifies. Exact path
manifest and merge-only hunks accompany the intake. Existing malformed terrain,
full reward crash recovery, arbitrary pending-ground mixtures, multiple recipients,
all-target partial failure and live client behavior remain outside this slice.
