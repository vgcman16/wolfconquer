# 7937 named UI literal arrows

## Identity and scope

Revision1, 2026-11-03. Matching official Chinese7937 named resources contain
literal ASCII `->` in ordinary attribute-comparison text. The previous named-UI
formatter rejects its greater-than as unsupported markup. This correction is
portable text processing only; it does not apply resources or activate gameplay.

## Packet contract

Not applicable: no packet IDs, layouts, schemas or session behavior change.

## Catalog/table contract

Existing caller-supplied pinned UTF8 assignment resources and exact selected keys
remain. Recognize precisely the two characters `->` as the ordered signature
token `('arrow', '->')`. Keep printf, comparison, escape, brace, tag and control
tokens in their original relative order. This is format alignment, not a proof
that translated wording or gameplay meanings are correct.

## Validation and transaction rules

No generic greater-than fallback, normalization or ignored-arrow behavior.
Missing or replaced arrow tokens must be held; lone/extra greater-than and
malformed markup still refuse. Existing input pins, key/alias/duplicate checks,
ASCII-English rule, bounds, prior translations and unselected bytes remain.
Output is a fresh private proposal; originals and previous proposals are read-only.

## Expected wire behavior and fixtures

No wire behavior applies. Four new synthetic Python controls cover exact mixed
token order, malformed syntax, actual sequential CLI proposals and byte goldens,
plus held arrow mismatches without proposal output. Retain all36 existing tool
controls. No client resource values are embedded in these public tests.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
| Literal `->` syntax | Matching pinned7937 attribute-comparison rows retained privately | Observed text, not new field/gameplay semantics |
| Old refusal | Formatter sees standalone `>` after plain `-` | Safe baseline reproduced failures3/errors1 |
| Ordered token and output preservation | Four synthetic controls plus existing36 | All40 passed0failed/skipped in18.458s |
| Live fonts/layout/client behavior | No client execution or resource application | Unverified |

## Implementation and qualification

`tools/client-localization/named-ui/format_tokens.py` adds one exact token branch;
`test_literal_arrows.py` owns its new controls. [FSB-340](../../../FIXED-SOURCE-BUGS.md)
is qualified within this text-tool scope. Fourteen private ordinary-stat wordings
then composed to1929keys/1949rows with original inputs, prior translations and
unselected bytes retained; actual output/receipt were separately checked. This
does not clear client execution holds or establish complete English/client parity.
