# Composed selected-rule compatibility repair

## Identity and scope

Revision 1, 2026-11-02. Source-only review of Root's complete 188-path candidate
on 5ccca124. This bounded correction joins the frozen selected-rule loader and
constructor-owned relic-fuse preparation. Root alone integrates and qualifies it.
No build, test, DataTool, Lua, native client, updater or Git execution is authorized
in this scope. No candidate or earlier frozen proposal is modified.

## Packet contract

Unchanged. No opcode, action, framing, field, response or profile change. This is
selected metadata validation before any operation; it grants no runtime ownership.

## Catalog/table contract

Confirmed in composed C# source:

- The packed compose parser retains four numeric cells: row ID and three values.
  All cells are nonnegative Int32 data; complete rows and unique IDs are required.
- `Chinese7937XuanbaoRuntimeRules.ComposeRowMatches` requires exact equality of
  all three selected values with both raw and indexed server metadata.
- `LegacyRelicFuseRuleGeneration.TryCopyCaps` treats IDs 1 through 13 as formula
  caps. It permits an optional three-valued, nonnegative ID 14 without assigning
  its last value to `caps[14]`.
- `LegacyRelicFusePreparation.TryGenerateWords` handles `LukyStrike` (14) with
  fixed value 100 and epic encoding before any cap access. The existing
  `LuckyHasItsRealFixedValueWithoutAnUnusedComposeCap` control omits row 14.

Concrete composition defect: the earlier selected-rule binding still applies
the formula-cap range 30 through UInt16Max to every row, including ID 14. An
identical selected row with an irrelevant final value 0 or above UInt16Max is
therefore rejected despite a valid constructor-owned generation and unchanged
fixed Lucky behavior. This is a source compatibility finding, not a claim that
the retained official ID 14 uses any particular value.

Correction: preserve complete raw/indexed/selected equality and three-cell
validation for every row; apply the formula-cap range only when the real formula
uses it. ID 14 remains exact data and its value cannot change the Lucky formula.
Unknown Addition columns, Resolve branch ownership and current actions remain
unknown; no data is reinterpreted or activated by this repair.

Second confirmed source defect: the shared lexer classifies reserved Lua words
as identifier tokens for boundary tracking. The selected Resolve Unload parser
then accepts any such token as a direct nil-reset target, excluding only return,
break and goto. A complete synthetic module with `nil = nil`, `true = nil` or
`local = nil` can therefore pass its selected relationship checks despite an
invalid reset assignment. The selected reset form must require an ordinary
identifier, excluding reserved Lua words. Arbitrary ordinary reset names remain
valid; this is not a general Lua grammar validator and does not evaluate input.

## Validation and transaction rules

Existing once-bound selected owner, immutable successor generation, active lease
guard and actual owned compose transaction remain unchanged. A conflicting
selected value, including any ID 14 value, refuses before publication. Out-of-range
formula-used caps still refuse. This scope does not reserve a UID, draw entropy,
write saves, publish gameplay frames or introduce a fallback owner.

## Expected behavior and fixtures

New focused controls use the existing real catalog factory, packed decoder,
selected binding and pure preparation with owned synthetic data. They cover:

- Selected ID 14 final values 0, 29, UInt16Max + 1 and Int32Max retain valid
  binding and the same seeded durability/attribute outcomes as the constructor
  generation; Lucky words stay exactly 100/epic.
- A changed selected ID 14 value refuses and leaves the original generation and
  preparation current.
- Formula-used type 3 values 29 and UInt16Max + 1 still refuse without a selected
  owner or an accepting default generation.
- Reserved words used as direct Unload reset targets refuse with no Resolve
  snapshot; an ordinary synthetic `_otherReset42` target remains accepted.

No client assets, retained rows or successful counters are embedded in fixtures.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
| Lucky formula does not use a cap | Preparation.Attributes.cs fixed Lucky branch | Confirmed composed source |
| Optional ID 14 may have a nonnegative unused value | RuleGeneration.Capture.cs and existing no-row-14 test | Confirmed composed source |
| All selected values must still match | RuntimeRules.ComposeMatches raw and indexed joins | Preserved |
| Selected reset targets are ordinary identifiers | Lexer token category and ResolveUnloadReset predicates | Source defect corrected by reserved-word refusal |
| Official ID 14 final-value meaning | No native consumer evidence in this scope | Unknown |
| Addition/Resolve gameplay activation | No ownership/action evidence | Excluded |

## Implementation and qualification

Production overlays: `src/WolfConquer.Core/Data/Chinese7937XuanbaoRuntimeRules.cs`
and `src/WolfConquer.Core/Client/Chinese7937DecodedXuanbaoInfo.Resolve.Parser.cs`.
New control partials: `tests/WolfConquer.Tests/LegacyRelicFusePreparationTests.ComposedRules.cs`
and `tests/WolfConquer.Tests/Chinese7937XuanbaoResolveTests.ResetNames.cs`.
Root source-bug records FSB-309 and FSB-310 track the unused Lucky cap and invalid
reset targets as OPEN integrated drafts. All controls are authored only,
unbuilt and unrun. Root must run the composed build and relevant
`Chinese7937XuanbaoRuntimeRulesTests` / `LegacyRelicFusePreparationTests` and
`Chinese7937XuanbaoResolveTests` controls. Both actual retained-data inspection
commands still require Root's execution to establish viability; source review
does not claim that output.

## Prospective selected-generation publication

Root subsequently found that both selected binders published their once-bound
source reference before computing the actual successor generation. A changed
raw cap could match the selected file while conflicting with the private
constructor capture, causing selection to return no generation after an old
valid owner had already been discarded. This violates rejection preservation.

Prepare the prospective gold/rule source pair and generation under the existing
publication lock before changing either reference. If a previously accepting
generation cannot form its successor, refuse without consuming the binding and
retain the exact old source/generation for a valid retry. A catalog that never
had a valid fusion generation may still bind data for other consumers; that
does not create fusion authority. Active leases still refuse publication.

The corresponding catalog/planner controls are in
LegacyRelicFusePreparationTests.Publication.cs. FSB-311 is OPEN and these controls
are unbuilt/unrun. Raw alias mutation remains a separate ownership boundary;
source preparation is not runtime or live-client proof.
