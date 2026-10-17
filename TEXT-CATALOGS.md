# Caller-supplied text catalogs

This source distribution contains the generic patch and comparison code. It does
not contain client translation datasets. Supply your own JSON catalog and target
files. The examples below use newly authored synthetic text.

## Client patches and comparisons

`ClientPatchCatalog.Load(path)` reads a nonempty JSON array:

```json
[
  {
    "relativePath": "ini/sample.ini",
    "encodingKind": "Utf8",
    "replacements": [
      { "key": "Sample.Name", "value": "Synthetic label" },
      { "key": "Sample.Label", "value": "Second label", "occurrence": 2 }
    ]
  }
]
```

`relativePath` and `encodingKind` are required. Encoding names are `Utf8`,
`Utf8Bom`, and `CodePage936`. JSON property names are case-insensitive; unknown
properties are rejected. Paths must be unique and relative to the supplied root.
Rooted paths, drive paths, traversal segments, and symbolic links or junctions
below the root are rejected. Patch targets must already exist. All target paths
and existence checks run before the first write.

Each replacement requires `key` and `value`. `occurrence` defaults to 1 and must
be positive. `Section.Key` selects an INI section; a key without a section selects
the matching key. `replaceLine: true` replaces the entire matching line rather
than its value. Optional `commentPrefixes`, such as `[";"]`, identify comment
lines; whole-comment replacements use a matching line prefix as their key.

For an intentional complete file replacement, omit `replacements` and supply
`"fullTextPatch": { "text": "Synthetic line\n", "ensureFinalNewline": true }`.
Full-text patches cannot be combined with replacements or comment prefixes;
they normalize line endings to CRLF. The selected encoding controls the output
bytes and BOM. C# callers can additionally supply `ValidateOriginal` in a
`ClientPatchFile`; executable validators are not part of the JSON format.

```text
WolfConquer.DataTool patch-client ./client ./client-catalog.json
WolfConquer.DataTool client-structural-drift ./original ./patched ./client-catalog.json
WolfConquer.DataTool client-file-drift ./original ./patched --patch-files-only ./client-catalog.json
```

The API equivalent is
`EnglishClientPatch.Apply(clientRoot, ClientPatchCatalog.Load(catalogPath))`.
There is no implicit `EnglishClientPatch.Files` collection or one-argument
`Apply`. Patching rejects absent/empty catalogs and entries without an operation.
Comparison catalogs may contain only `relativePath` and `encodingKind`; they
select the files and decoding needed for comparison. An empty catalog or a
selection with no existing targets cannot report a successful comparison.
File drift without `--patch-files-only` still compares the full client tree.

Patch exit codes are 0 when every requested replacement matched (including an
idempotent repeat), 1 for invalid configuration or unavailable files, and 2 for
missing replacement keys. Matched changes may already have been written when
missing keys are reported. Comparison exit codes are 0 for clean, 1 for invalid
input, and 2 for detected drift.

## DB visible text

`LegacyDbVisibleTextCatalog.Load(path)` reads a separate nonempty JSON array:

```json
[
  {
    "relativePath": "sample.ini",
    "section": "sample",
    "key": "Name",
    "sourceValue": "old",
    "replacementValue": "Synthetic label",
    "sourceEvidence": "Authored sample"
  }
]
```

Every field is required; `sourceEvidence` identifies the caller's basis for the
replacement. File paths follow the same confinement rules as client patches.
INI matching uses a section and `Name`, `Desc`, or `DescEx` key. For supported
`@@`-delimited rows, `section` is the first field and `key` is `Field1`, `Field2`,
and so on. A replacement is applied only when the current value matches
`sourceValue`; an existing `replacementValue` is counted as already translated.
The generic translator preserves unrelated fields and line endings and uses
CodePage936 for file patches.

```text
WolfConquer.DataTool patch-db-visible-text ./data/DBServer ./db-text-catalog.json
```

There is no bundled `SourceBackedReplacements` fallback. The command rejects
missing, empty, or invalid catalogs and missing targets with exit code 1. It
returns 0 if any supplied row matched or was already translated, and 2 if no row
matched; inspect the reported unmatched counts to assess partial coverage.
The lower-level `LegacyDbVisibleTextTranslator.Translate` and `PatchFile` APIs
continue to accept explicit replacement sequences.

## Source-backed StrRes translation

`SourceBackedStrResTranslator` reads the supplied source text/files and can use
safe existing target rows as sources. It no longer imports a bundled client
catalog. Its `Translate`, `PatchFile`, and `PatchDefaultFile` APIs accept optional
`IEnumerable<ClientTextReplacement> catalogReplacements`; the default is no
additional catalog. For example, callers can pass the `Replacements` from one
entry loaded with `ClientPatchCatalog.Load`.

```text
WolfConquer.DataTool patch-client-str-res-source-backed ./client ./reference/StrRes.ini
```

The CLI accepts explicit source files or directories. It does not search for
translation catalogs in developer workspaces. Conflicting source values and
placeholder mismatches remain unresolved and are reported. Text with no usable
source remains unchanged. This command returns 2 when target rows are absent or
translation-needed rows remain; a fully translated/idempotent result returns 0.
The API returns detailed counts so callers can apply their own completion rule.

## Source-backed status text

`SourceBackedClientStatusTextTranslator.TranslateStatusTips` and
`PatchStatusTipsFile` take explicit `SourceBackedClientStatusTextSource` rows
(section, key, replacement value, source name). There is no built-in status text
fallback table. The same-section/key and compatible-format-token checks remain;
missing or conflicting sources leave target text unchanged and increment the
unmatched/remaining counts. Skill-name translation still uses caller-supplied
magic-type rows with matching skill IDs.

```text
WolfConquer.DataTool patch-client-status-text-source-backed ./client ./reference/StatusTips.ini
```

The command can read existing target rows and local files under `data/DBServer`
as sources, plus explicitly supplied files/directories. It returns 1 if no target
or source files are available, 2 if no visible target rows are found or CJK rows
remain, and 0 for a fully resolved/idempotent result. It reports per-file counts
for assessing partial changes.
