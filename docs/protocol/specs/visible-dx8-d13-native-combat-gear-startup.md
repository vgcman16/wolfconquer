# Visible DX8 D13: explicit native CombatGear metadata startup

## Identity and scope

- Revision 1, 2026-11-01; source-only implementation directed by Root.
- Existing `VisibleDx8D13` owned WorldServer Frame; no client launch or new packet contract.
- Configure an explicit metadata directory through `WOLFCONQUER_NATIVE_COMBAT_GEAR_ROOT` and an explicit rank file through `WOLFCONQUER_NATIVE_COMBAT_GEAR_RANK_PATH`.
- Both absent keeps native CombatGear unavailable. One absent, whitespace, relative paths, invalid/missing files or unsuccessful complete native publication refuses configured startup before listening.
- The returned runtime owner comes only from the real staged loader's exact newly published proof. An ambient table, raw ready flag, supplied callback or matching snapshot does not create this owner.

## Packet contract

Not applicable: this change issues no packet and activates no CombatGear proc. Existing native protobuf registration/serialization checks remain part of complete metadata publication. Owned proc costs, all-target receives, deaths and output are a separate unfinished transaction.

## Catalog/table contract

The directory contains `daoqi_dict_type.txt`, `dict_lottery.txt`, `dict_collection.txt`, and `combat_gear.txt`. Rank input is the separately configured file. The public API accepts a directory and constructs its separator; the legacy one-argument `Load(rawPrefix)` keeps its existing raw-prefix ABI and legacy rank configuration behavior. The new two-argument staged entry uses its explicit rank path without changing global `DbLocation`.

Existing staged bounds remain: 64 MiB aggregate input, 262,144 aggregate rows, 1 MiB per line, ordinary absolute files and ordinary ancestor directories, complete stable reads, strict BOM-aware Unicode decoding, complete numeric parsing, and one coherent graph publication after real serializer registration/probes. The detached reader recognizes 19 or 22 numeric values. All three extended opaque values are parsed and retained, including duplicate-row tails; other widths refuse. At this source checkpoint, any22-column row refuses the complete stage with `nativeCombatGearSchema22Unresolved` before global publication or serializer registration. This protects ambient mailbox recovery as well as the new Frame. Legacy19 semantic loading remains supported; no22 semantic readiness is claimed.

| Columns | Native meaning | Managed alignment |
| --- | --- | --- |
| 0–6 | ID, type, level, experience, HP, physical attack, physical defense | Exact typed properties and original fields |
| 7 | Decompiled native consumer calls this magic defense | CONFLICT: managed parser retains this as `UnknownStat0` |
| 8 | Decompiled native consumer treats this as first skill | CONFLICT: managed parser calls this magic defense; retained rows include30/775 |
| 9–17 | Remaining native skill values | Managed parser's nine skill values |
| 18 | Score | Exact score |
| 19–21, when present | Opaque tail | Preserve exact values; no invented gameplay meaning or required zero |

Jade, lottery and collection alignment must cover every numeric field and the corresponding typed managed view. Rank alignment must compare the complete ordered loaded seed rows against the managed source rows; live ranking entries may subsequently change under their actual rank owners and do not become immutable metadata. Equal raw gear rows alone do not resolve the conflicting native/managed interpretation. Official matching client table evidence must resolve that conflict before a populated native gear capability is issued; neither the decompiled mapping nor the managed parser is assumed correct by itself.

## Validation and transaction rules

Only a current explicitly configured owner may provide its retained proof to its actual owned D13 Frame. The Frame must retain the same Store, live owned admission runtime, target catalog, normal admission domain and recovery ownership. Empty saved gear remains independent of optional gear tables; populated native gear adoption obtains metadata through this Frame owner, never an ambient global proof.

Metadata alignment and proof currentness are rechecked at the consuming admission/native origin boundaries. A later complete reload invalidates an older runtime owner, even if the values are equal. Raw setters and in-place content changes also invalidate it. This is a metadata read capability, not a file-mutation lease, cast permission or crash-safe gameplay transaction.

## Expected wire behavior and fixtures

No new wire output. Source tests reuse the existing real loader's owned fresh-child fixture for cold serializer startup. They cover absent/partial/invalid configuration, real directory/rank loading without global config changes, exact returned proof, stale/reloaded/altered metadata, catalog mismatch, extended row parsing including bad duplicate tails, and real owned Frame admission integration. No tests are executed by the helper.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
| Retained gear width | Original `征服-龙拳天下服务端-已修复/lqtxfwd/GameServer/DBServer/combat_gear.txt`, read-only census of all 642 rows | Every row has 22 numeric values plus trailing `@@` |
| Representative raw rows | Lines 1, 51, 301 read directly; line51 fields7/8 are0/30, line301 fields7/8 are0/775 | Recorded source evidence, not independent client wire proof |
| Native mapping | Decompiled `TQCombatGear/CQ_COMBAT_GEAR.cs`145–157 and existing native consumer bodies | Reference only: MDefense7, Skills8–17, Score18; may itself contain a source bug |
| Managed mapping | `CombatGearParser` uses MDefense8 and nine skills9–17 | Conflicts with native interpretation; retained non-spell-sized field8 values make this a real unresolved evidence question |
| Client/profile acceptance | Existing D13 profile remains selected; no new live test | No new live-client, deployment or complete gameplay claim |

## Implementation and qualification

Implementation: `LegacyNativeCombatGearRuntime` configuration/owner, explicit two-argument staged loader, native graph/catalog alignment, and readonly Frame owner injection before WorldServer listen. Ordinary admission integration is coordinated with the separate admission helper.

All new work is UNBUILT/UNRUN until Root's isolated checks and appropriate full gate. The previous proc preparation remains inactive. The live client/updater execution hold remains outside this source-only change. Root records the final source-bug entry and verification outcome during integration.
