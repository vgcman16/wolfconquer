# Query-owned ordinary physical attack range

## Identity and scope

Revision1, 2026-10-31. Existing7899/VisibleDx8D13, Action260 source continuation.
No7937 activation, live acceptance or executable launch. This supplies genuine
query-owned weapon ranges and exact arithmetic for ordinary physical callers.
Actual monster/NPC identity, permissions and live family state remain separate.

## Packet contract

No packet IDs, fields or successful order change. Range rejection must precede
dirty combat prefixes, damage, costs, cooldowns, output and repeat replacement.
Client damage/coordinates and catalog-only entity presence grant no authority.

## Catalog/table contract

- Retained ItemType.cs205 reads AttackRange as UInt16 from raw column31.
- Actual completed equipment query selects durable right/left slots4/5 using
  captured alternate/shield selection. Ring range is not consumed.
- Equip.cs63-67/605 initializes/resets rangeR/rangeL/SizeAdd to0. Durable slot
  query assigns rangeL at883 and rangeR at938. No other SizeAdd assignment was
  found in that bounded full-file read.
- Equip.GetAttackRange187-202: base1; both nonzero weapon ranges use integer
  average, otherwise the nonzero side; add `(SizeAdd+targetSizeAdd+1)/2` using
  C# signed integer division. The supported actual actor SizeAdd remains0.
- MonsterRole155 uses actual Family.AttackRange as target size. It is sbyte,
  not UInt16: Server494 loads attack_range; Boss!=0 overrides3 at510. Family.Copy
  always allocates a new family and overrides12 for Boss>0 with100000<HP<7000000.
  Normal MobCollection59 passes a fresh Copy for each role. Other dynamic spawn
  ownership must be checked separately, never inferred from a family ID.
- ActionHandler.AttackingTarget147 changes actual family range to18 for Boss1
  with currentHP>=2000000. This belongs to real AI mutation/lifetime ownership;
  a range reader must not create that event or guess historical state.
- Ordinary SOB range is3 (MsgAttackPacket855); CombatGear distance and monster
  FatalStrike bypasses require their own real owners and remain separate.

## Validation and transaction rules

Use the actual private completed query's adopted item rows, full raw/index/row
identity witnesses and captured weapon selection. Missing selected metadata,
malformed UInt16, drift or retired binding refuses; zero is permitted only for
an actual absent slot or a valid row containing0. Cold ranges can be0 only from
the existing actual unpublished equipment/admission owner with native ranges,
SizeAdd and speed fields all0. An arbitrary empty cache cannot be admitted.

An AttackRangeCache child is current only when it is the exact child of the
actual current query-issued parent and the same admitted binding/equipment.
It supplies values, not an accepted attack or entity owner. Existing legacy
public component range fields cannot manufacture a completed query capability.

Strict family birth metadata and actual AI range18/current life publication
must be joined before claiming full ordinary monster range support. The generic
permissive uint family resolver is not a strict current-family proof.

## Expected wire behavior and fixtures

Genuine cold admission and completed equipment queries must establish the cache;
test real selection, single/dual weapon arithmetic, bounds, denied rebuild,
escaped metadata/current-owner drift and replacement lifetimes. No handmade
positive cache/receipt or accepting callback. Preserve complete saved files and
independent packet output; this cache itself writes no files or sends packets.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
| Weapon raw width | ItemType205, Equip883/938 | Source observed |
| Exact formula | Equip187-202 | Source observed |
| Actor size0 | Equip63-67/605 | Source observed; actual native check required |
| Monster range lifecycle | Server494/510, Family.Copy, ActionHandler147 | Strict adopted metadata and actual AI join required |
| Current query issuer | Existing private query epoch/cold admission owner | Reused real lifecycle, no new detached authority |

## Implementation and qualification

Root owns metadata/cache partials and exact shared query hunks. Lead owns live
entity routing and actual monster range state/AI producer. Entire260 remains
SOURCE ONLY, UNBUILT/UNRUN; no gameplay, SQL, deployment or full combat claim.
