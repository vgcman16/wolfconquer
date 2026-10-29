# Client resource and configuration prerequisites

Target evidence is Chinese client7899 / cached DX8 D13. The repository supplies
generic tools and synthetic tests; game resources, native words/moduli, config
payloads and private review catalogs are not bundled.

## Source-backed text

The existing `patch-client-str-res-source-backed` DataTool command accepts an
explicit reference file/directory containing `UStrRes.ini`. Work only on an owned
ordinary-file copy, for example:

```powershell
dotnet run --project src/WolfConquer.DataTool -- patch-client-str-res-source-backed "work/client-copy" "work/reference/ini/UStrRes.ini"
```

An explicit UTF-16LE BOM enables strict numeric-resource-ID decoding. UTF-8 C#
and unmarked CP936 references retain their prior behavior. Conflicts, ordered
format/escape mismatches, unrepresentable replacements and protected data refuse
or remain unchanged. Malformed Unicode refuses before target writes. Exit2 means
untranslated CJK remains; it is not full-localization success.

`SourceBackedStrResTranslator.BuildReviewedFileCandidate` is a pure API for an
explicit caller-reviewed `ClientTextReplacement` list. It returns candidate
bytes; it writes no files. It checks exact numeric IDs, duplicate/ambiguous
selection, CP936 roundtrip, formatting, literal escapes and complete supported
link/path/address fragments. Unsupported ambiguous forms refuse. Registry data
and already-English human text cannot be rewritten. Chinese inside immutable
paths is retained. Semantic translation review and native capacity/rendering
acceptance are still required.

In an owned resource proof, source-backed translation changed1082rows, then an
explicit reviewed catalog changed30additional values. The final68824bytes match
the independent expected resource exactly, and repetition changes nothing. One
contradictory VIP help value and one registry/config value remain held; Chinese
comments remain. Neither file was installed into a running/original client.

## Named UTF8 UI proposals

The portable [named UI proposal tools](../tools/client-localization/named-ui/README.md)
prepare explicitly reviewed selections for named assignment resources such as
`Cn_Res.ini`. Record the actual client/build and wording evidence for each review.
The package contains generic Python source and synthetic tests only.

Operators supply three authorized ordinary files: original language, English
reference and current target, plus a reviewed plan beside the tool. Keep resource
inputs private, for example under its ignored `inputs/` directory. Its three
curated plan filenames are also ignored:

| Family | Plan filename |
| --- | --- |
| `character-stat-ui` | `stat-ui-family.json` |
| `equipment-ui` | `gear-ui-family.json` |
| `character-equipment-ui` | `combined-ui-family.json` |

Plans pin the SHA256 of all three inputs and select at most 200 distinct exact-case
keys. A combined proposal requires an explicitly reviewed union of the maps.
Strict UTF8 parsing preserves BOM state, exact keys/order/occurrences, unselected
raw bytes, selected prefixes and individual line endings. Selected groups require
equal occurrence counts, uniform values, identical original/current wording and
the same ordered formatting/escape/markup/control tokens. Mismatches remain held
unchanged; malformed required input refuses. See the linked README for the schema
and commands.

Default output is a JSON summary. Explicit `--write-proposal` creates a fresh GUID
child under ignored `proposals/`; input resources stay read only. The report
describes caller-supplied English requiring independent wording and rendering
review. A family name does not establish official wording or authorship. There
is no apply operation, and proposal/test success does not establish rendered
English, layout/hover acceptance, deployment or client/protocol/gameplay parity.

## Configuration primitives

`ClientConfigWordRecipe` implements the inspected64-word adjacent-XOR/rotation17
recipe and its inverse on caller-supplied words. It returns words, not a key or
configuration, and includes no native constants.

`ClientConfigType1Block` unpacks an already recovered big-endian type-1 block for
the inspected default profile. It performs no modular/public-key operation or
runtime-provider selection. Full/minimal width, required padding, delimiter and
caller output capacity are checked before returning owned payload bytes. Padding
acceptance alone does not authenticate data or authorize configuration changes.

`ClientConfigDefaultPublicTransform` performs the inspected unsigned big-endian
65537 modular operation on explicitly supplied public parameters, then applies
the type-1 decoder. `ClientConfigDefaultContents` snapshots and decodes bounded
modulus-width chunks. `ClientConfigDefaultFileDecoder` composes those stages with
the actual requested-length gzip reader, including its buffer/trailer timing;
it does not substitute strict whole-stream validation. All outputs are owned bytes.

The DataTool `client-config-decode` command requires these explicit option pairs:

```text
--profile d13-default --input <absolute-file>
(--modulus-file <unsigned-big-endian-file> | --encoded-words-file <64-uint32-json>)
--output-new <absolute-fresh-file> --max-transformed-bytes <N> --max-result-bytes <N>
```

Both bounds are required, capped at 64 MiB. No output directory is created and no
existing output is replaced. The command holds ordinary input read leases,
prepares every output byte, then promotes one flushed temporary file without
overwrite. Unsupported profile/parameters, malformed chunks, exceeded bounds,
linked roles and existing output refuse. There is no native key/provider lookup.

Actual focused proof includes 62 command process cases and 197 configuration/
retained controls, plus two private copied UI assets decoded with explicit
parameters. Runtime global/provider selection, native mode2 byte-parser semantics,
text normalization, loading/rendering and portable release remain open. These
tools do not establish a client feed or authorize rewriting opaque installed
configuration. The caller must supply its own authorized inputs and parameters.
