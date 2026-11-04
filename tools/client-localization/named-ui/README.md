# Named UI resource proposals

Portable Python 3 standard-library tool for explicitly reviewed, pinned UTF8
assignment resources. It proposes selected English values and has no client
apply operation. A family name describes scope only; it does not establish
official wording, authorship, source continuity or rendered acceptance.

Operators supply three ordinary input files: the original language reference,
the caller-supplied English reference and the current target. Keep licensed or
private resource data outside source control, for example in ignored `inputs/`.
The package contains no actual plans, inputs or resource proposals.

Supply one reviewed JSON plan beside the tool, using a curated filename:

| Filename | Supported family |
| --- | --- |
| `stat-ui-family.json` | `character-stat-ui` |
| `gear-ui-family.json` | `equipment-ui` |
| `combined-ui-family.json` | `character-equipment-ui` |
| `mail-trade-ui-family.json` | `mail-trade-ui` |
| `storage-vendor-ui-family.json` | `storage-vendor-ui` |
| `quest-social-ui-family.json` | `quest-social-ui` |
| `guild-team-ui-family.json` | `guild-team-ui` |
| `core-ui-family.json` | `core-ui` |

The plan has exactly `schemaVersion` (integer 1), `family`, `codec` (`utf-8`),
`inputSha256` (an object with uppercase SHA256 values for `original`, `english`
and `current`) and `keys` (1-200 names with no letter-case aliases). Names
require uppercase `STR_` followed by ASCII letters, digits or underscores, or
the exact numeric-tooltip form `STR_BTN_TIP_[0-9]+-[0-9]+`. Both digit runs
remain literal: leading zeros and exact key spelling are preserved. Other
hyphenated forms remain refused; no native control-ID or lookup authority is
inferred from a key.
For a combined family, review the explicit maps and resolve overlap before
creating its plan and English input. Do not import a whole older table.

```text
python propose_named_ui.py inputs/original.ini inputs/english.ini inputs/current.ini --plan combined-ui-family.json
python propose_named_ui.py inputs/original.ini inputs/english.ini inputs/current.ini --plan combined-ui-family.json --write-proposal
python -B -m unittest discover -v -s . -p 'test_*.py'
```

Omitting `--plan` selects `stat-ui-family.json`. Default output is a JSON summary
only. `--write-proposal` creates a fresh GUID child under ignored `proposals/`,
containing `Cn_Res.ini` and its report; input files remain read only. Plan paths
cannot be supplied outside the curated filenames. Preserve the three-file hashes
and record wording evidence separately from this generic tool's report. Each
curated filename must declare exactly the family in the table; a mismatch refuses
before any proposal directory is created.

Sequential proposals can use an earlier proposal as the next current input.
Pin that exact current file again and select only the newly reviewed rows; each
pass keeps its own 1-200 key limit. Earlier English and all unselected bytes stay
unchanged. This produces another private proposal and does not apply it.

The parser owns immutable input bytes and copied read-only group indexes. Proposal
generation reparses those captured bytes rather than trusting supplied parsed
metadata, so the pins and the selected rows describe the same document.

The parser retains exact keys, order, BOM, duplicate groups, unselected raw bytes,
prefixes and line endings. Selected groups need matching counts, uniform values,
identical original/current wording, bounded English donors and the same ordered printf,
escape, brace, tag, comparison, literal `->`, fixed numeric percentage and control
tokens. Fixed percentages retain the exact sign, number spelling and order at
unambiguous boundaries; existing valid printf/escaped-percent syntax takes
priority. Numeric/identifier fragments and unknown percent syntax remain held.
Literal arrows must remain in order; lone/extra `>` and malformed markup still refuse. Missing
state, changed wording, machine
values, unsupported formatting or other mismatches are held unchanged. Review
every hold; a report is not permission to apply it. Input cap is 16 MiB and plan
cap is 64 KiB; these are operational bounds, not game rules or renderer limits.
Any selected key with a differently spelled casefold alias in any input is held
as `caseAliasAmbiguous`. This is conservative proposal policy pending lookup
evidence, not a claim about native key casing. Unselected aliases remain unchanged.

English donors still require an ASCII letter. Characters may be ASCII 32-126,
tab or exactly the source display marks ★, 【, 】 and ： (U+2605, U+3010, U+3011
and U+FF1A). When a donor contains any of those four marks, its ordered projection
of all four must exactly match the original value, including label and time
colons. Added, missing or reordered marks are held; retaining only time colons
while changing the original label colons is insufficient. All-ASCII donor
behavior remains unchanged. Other Unicode letters, digits, lookalikes,
punctuation, CJK, bidi/control and row injection remain refused. No normalization,
schedule behavior, font support or rendering authority is inferred.

Tests use synthetic temporary fixtures. Their success does not prove complete
translation, font support, hover behavior, layout fit, native execution, deployment
or protocol/gameplay parity. Independently review English wording and rendering
before any separately authorized reversible application.

## Explicit physical variants

The existing uniform-key route and its default refusals remain unchanged. When
one exact key has differently formatted source values, use the separate explicit
`propose_named_ui_variants.py` route with `--plan core-ui-variants.json`. The
reviewed plan stays beside the tool and is ignored; it is never a native lookup
configuration. This CLI has no default plan and no client apply operation.

Its top-level fields are exactly `schemaVersion` (integer 1), `family` (`core-ui`),
`codec` (`utf-8`), the same three `inputSha256` pins and `variants` (1-200 records).
Every record has exactly `key`, `originalValueSha256`, `englishValueSha256`,
`occurrences` and `occurrenceCount`. Value pins are uppercase SHA256 of the exact
value's UTF8 bytes, excluding key, equals sign and line ending. `occurrences` is
an ordered, strictly increasing list of positive one-based physical line numbers,
including comments/blank lines in the numbering; `occurrenceCount` equals its
length. Exact mixed-case suffixes and literal numeric-tooltip names retain the
same grammar as the uniform route.

For every selected key, original, English and current files must have the same
total key-occurrence count and exact physical key positions. The full occurrence
sets for both selected original-value hash and donor-value hash must equal the
explicit line list. Selecting only part of an otherwise identical source variant
is refused. Distinct original variants of one exact key may have separate records;
records cannot overlap and aliases cannot disambiguate them. If distinct source
variants share an identical English value across their combined line sets, this
route conservatively holds them rather than guessing a donor association.

Current selected rows must still equal the original value. Earlier English in
unselected variants remains unchanged. Each selected variant independently needs
the established display-value, donor, exact formatting/color token and finite
source-mark checks. Its proposal preserves current key spelling, order, BOM,
prefixes and individual line endings. No source variant is collapsed, normalized
or treated as the first/last runtime meaning. The JSON report separates selected
and merged variants, distinct keys and changed physical rows; held entries retain
the exact variant pins and line set. A partial proposal keeps held rows unchanged.

```text
python propose_named_ui_variants.py inputs/original.ini inputs/english.ini inputs/current.ini --plan core-ui-variants.json
python propose_named_ui_variants.py inputs/original.ini inputs/english.ini inputs/current.ini --plan core-ui-variants.json --write-proposal
```

The existing 16 MiB resource/final-output cap, 64 KiB CLI-plan cap, duplicate-JSON
property refusal, ordinary-path checks and create-only fresh proposal directories
still apply. The API is `variant_proposals.propose_variants` and reparses captured
raw bytes rather than trusting supplied indexes. Synthetic variant tests exercise
actual CLI no-write/create behavior as well as per-variant byte goldens and failure
cases. Passing them proves only offline proposal handling; wording, native key
lookup, rendering and complete client translation remain separate work.
