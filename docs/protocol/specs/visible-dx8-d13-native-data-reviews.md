# D13 exact native data review extensions

2026-10-28. Root-selected offline distribution prerequisite; focused source-qualified, full gate pending.

## Identity and evidence
Target remains Chinese7899/cached DX8 D13. Existing local policy/publication plan
schema2 carries explicit exact nativeAssetReviews. Add two closed filename collision
categories, preserving schema1, signed manifest schema1, installation and repoint.
No game asset bytes, native constants or copied Lua source enter the repository.

- `native-server-type-lua`: exact path AND collisionPath `ini/userdata.lua`,
  collisionNodeType `file`, review length1..65536 bytes.
- `native-luacfg-userdata-catalog`: exact path AND collisionPath
  `ini/luacfg/userdata.dat`, collisionNodeType `file`, length1..1048576 bytes.

The existing required64-hex expectedSourceSha256, evidence identifier, exact-case
path, strict seven fields, explicit selection, uniqueness and physical checks stay.
An entry authorizes only its exact reviewed source bytes, not arbitrary userdata
names, files beneath a userdata directory, private state or a whole INI subtree.
No embedded digest allowlist or original client bytes are added to generic code.
No category for `ini/token_type.dat` is added; its format remains unknown.

Evidence: a complete bounded strict-GBK/GB18030 semantic review found userdata.lua
is source defining three default in-memory server classification fields, functions
and aliases, with external Cpp_And. No direct account/auth/network/file/machine-path
access appears in that file; external caller behavior remains unknown. Exact
luacfg/userdata.dat staged bytes match the same file in retained official patch
7892-7930 (4532 bytes; digest recorded only in private provenance note). This is
asset-provenance/role evidence, not a decoded catalog schema or portable startup.

## Packet contract
Not applicable: these are local offline publication inputs. No opcode, wire
encoding, field meanings, handler activation, game client change or profile change.

## Catalog contract
No new catalog decoder. Payloads remain opaque reviewed bytes. A category is not
an algorithm selector, semantic privacy validator, signature or release approval.
Each changed/new version needs its own content review and exact source digest.
The planner remains metadata-only; it carries supplied digests and validates exact
names/types/lengths/selection. Stage verifies complete held source bytes and
readback; publisher independently verifies review length/digest before outputs and
copied bytes before blob promotion. baseSha256 retains prior-install meaning.

## Changes and rejection invariants
Implement identical closed category/path/type/length rules in PowerShell
Read-CdNativeAssetReviews/Assert-CdNativeReviewRisk and C# NativeAssetReviewSet /
PublicationPathPolicy.NativeAssets. Preserve existing obj/BMP rules unchanged.
Waive only the exact userdata leaf collision for its matching category. Any other
risk prefix/segment, ancestor, backup, private extension, AutoP, alias, wrong
category/node/case/path/length/hash/evidence, schema1 use, missing/unused review,
unlisted or conflicting exclusion, reparse/source mutation remains a refusal.
Unreviewed ordinary/public ValidateRelativePath must keep rejecting these names.
No accepting callback, renamed payload or quarantine bypass switch.

## Required actual fixtures
Use exclusively newly constructed small synthetic Lua/catalog bytes, never
copied actual game content. Positive exact files through planner -> stage/rebase
-> actual publisher/CLI; independent expected release/blob bytes, retained review
scalars and unrelated baseSha256. Cover zero/max/max+1 review lengths, aliases,
wrong collision/type/category, generic userdata/private/token paths and no review.
Cover same-length changed bytes/restored timestamp, wrong digest, wrong length,
real sharing/reparse where existing fixtures support them; no output/feed changes
on preflight refusal and truthful partial-stage limits. Existing native review,
quarantine/publisher/updater and standalone distribution tests stay meaningful.

## Publication boundary
Root alone integrates and runs serialized checks in owned snapshots, commits and
publishes explicit paths. Helpers author only isolated postimages. Actual game
files, current client/server, original routes/saves/restore and held59 Guild/Union
paths are preserved. Current public tool preview224 tag is immutable and unchanged.
No curated whole-client plan/stage/signing/feed/download or forum-ready claim follows.

## Implementation and actual qualification

Production: NativeAssetReviewSet.cs, PublicationPathPolicy.NativeAssets.cs and
ClientDistribution.AssetReview.ps1. Tests: the new ConfigAssets C# partial plus
Test-ClientDistributionConfigAssetReviews.ps1 and ConfigAssetPipeline.ps1.
Actual owned public updater suite446/446 passed; all7 standalone distribution
scripts exited0. The new review script passed9 groups/48 synthetic fixtures;
the joined real planner/stager/CLI script passed5 cases with ephemeral signing.
Existing source schemas/quarantine and new bounds retain negative controls.
No real client assets were staged or published, no portable startup was proven,
and the new exact-public full check remains pending. See
[scope and limits](../../EXACT-NATIVE-DATA-REVIEWS.md).
