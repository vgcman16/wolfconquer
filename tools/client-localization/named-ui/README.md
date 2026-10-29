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

The plan has exactly `schemaVersion` (integer 1), `family`, `codec` (`utf-8`),
`inputSha256` (an object with uppercase SHA256 values for `original`, `english`
and `current`) and `keys` (1-200 distinct exact-case `STR_[A-Z0-9_]+` names).
For a combined family, review the explicit maps and resolve overlap before
creating its plan and English input. Do not import a whole older table.

```text
python propose_named_ui.py inputs/original.ini inputs/english.ini inputs/current.ini --plan combined-ui-family.json
python propose_named_ui.py inputs/original.ini inputs/english.ini inputs/current.ini --plan combined-ui-family.json --write-proposal
python -m unittest -v test_named_ui test_named_ui_cli test_equipment_ui
```

Omitting `--plan` selects `stat-ui-family.json`. Default output is a JSON summary
only. `--write-proposal` creates a fresh GUID child under ignored `proposals/`,
containing `Cn_Res.ini` and its report; input files remain read only. Plan paths
cannot be supplied outside the curated filenames. Preserve the three-file hashes
and record wording evidence separately from this generic tool's report.

The parser retains exact keys, order, BOM, duplicate groups, unselected raw bytes,
prefixes and line endings. Selected groups need matching counts, uniform values,
identical original/current wording, ASCII English and the same ordered printf,
escape, brace, tag and control tokens. Missing state, changed wording, machine
values, unsupported formatting or other mismatches are held unchanged. Review
every hold; a report is not permission to apply it. Input cap is 16 MiB and plan
cap is 64 KiB; these are operational bounds, not game rules or renderer limits.

Tests use synthetic temporary fixtures. Their success does not prove complete
translation, font support, hover behavior, layout fit, native execution, deployment
or protocol/gameplay parity. Independently review English wording and rendering
before any separately authorized reversible application.
