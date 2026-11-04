# Explicit variant-aware named-UI proposals

Source-first specification, 2026-11-04 UTC; contract reviewed by Root before code.
Target is offline UTF8 assignment resources in retained Chinese client 7937.
No wire packet, native key lookup or client runtime behavior is inferred.

## Need and provenance

A real selected key can appear with different source color/tag, placeholder,
punctuation or label variants. The existing uniform-key route correctly holds
that key. Flattening its values would destroy the original variants. Add a
separate explicit variant route; keep existing plans/API/refusals unchanged.
Actual private input strings stay outside this public/synthetic implementation.

## API and plan

`propose_variants(original, english, current, plan)` reparses authoritative pinned
bytes, returns proposed bytes and bounded summary, and never applies resources.
A separate `propose_named_ui_variants.py` CLI uses the same three ordinary-file
inputs and an explicit curated `--plan core-ui-variants.json` filename.

Top-level fields exactly: schemaVersion(integer 1), family(core-ui), codec(utf-8),
inputSha256(exact original/english/current uppercase SHA256), variants(1-200).
Each variant record has exactly:

- key: existing exact STR_ key grammar, including literal numeric tooltip forms.
- originalValueSha256: uppercase SHA256 of exact original value's UTF8 bytes.
- englishValueSha256: uppercase SHA256 of exact donor value's UTF8 bytes.
- occurrences: nonempty strictly increasing 1-based physical line numbers.
- occurrenceCount: positive integer, exactly the occurrence-list length.

Every selected key must have equal total occurrence counts and identical physical
key positions in all three pinned documents. Both the complete original-value
hash occurrence set and complete donor-value hash occurrence set must equal the
explicit occurrences. Repeated records for distinct source variants under one
exact key are allowed; identical source variants, differently spelled casefold
aliases, overlaps and malformed pins/fields are refused. A variant cannot select
just one duplicate of an otherwise identical source value. Current selected rows
must still equal their original value and exact physical positions. Current
unselected variants, prior translations and all unselected bytes remain intact.

## Per-variant invariants

Apply the existing display-value, English-donor, exact ordered formatting/tag/
color/control token and finite source-display-mark rules to each actual variant.
No normalization, color collapse, guessed currency/item semantics or runtime
last-wins claim. Changed/missing/invalid variants remain unchanged with explicit
variant-qualified hold reasons. Successful variants may be proposed while held
variants remain byte-identical, matching the established proposal behavior.

Reconstruct only validated selected physical rows using current exact prefix,
ending, BOM and ordering. Bound final output to the existing 16 MiB input/output
cap. Preserve original/current/English inputs and create only fresh GUID output
children. No path aliases, reparse paths, absolute/external plan paths, larger
plan bounds, duplicate JSON properties or arbitrary family expansion.

## Qualification

Synthetic API and actual CLI fixtures cover at least two differently colored
same-key variants, repeated identical occurrences within a variant, selecting
only one variant, already-translated other variants, exact original/current/
donor occurrence mismatch, forged parsed metadata, alias/plan/pin/overlap guards,
format/color/token and donor rejection, BOM/mixed endings/unselected bytes,
size bounds, no-write and create-only CLI behavior. Run the original 66 controls
unchanged plus the new isolated variant suite. Exact test results are evidence
only for offline text proposals, never native lookup/rendering or live 7937.

## Implemented scope and current qualification

Production Python lives in `tools/client-localization/named-ui/variant_proposals.py`
and `propose_named_ui_variants.py`. The old uniform proposer, parser, token module
and all 66 prior test methods remain byte-unchanged. New synthetic controls live in
`test_named_ui_variants.py` and `test_named_ui_variants_cli.py`. No public plans,
private resources or actual client strings are included. Root reviewed and integrated this source. The isolated full 84-test family
passed; Root rechecked all 18 new final-source API/CLI controls (0 failed/skipped,
3.768s). Real private proposals remain unapplied/unrendered.
