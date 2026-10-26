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

## Configuration primitives

`ClientConfigWordRecipe` implements the inspected64-word adjacent-XOR/rotation17
recipe and its inverse on caller-supplied words. It returns words, not a key or
configuration, and includes no native constants.

`ClientConfigType1Block` unpacks an already recovered big-endian type-1 block for
the inspected default profile. It performs no modular/public-key operation or
runtime-provider selection. Full/minimal width, required padding, delimiter and
caller output capacity are checked before returning owned payload bytes. Padding
acceptance alone does not authenticate data or authorize configuration changes.

Static findings narrow the context constructor/default slot and byte order, but
the existing-global/provider overrides, actual basename, complete transform,
accumulated output bounds and encoding remain unresolved. These helpers are not
a file decoder, configuration writer, successful native startup or portable
release. Do not use them to rewrite opaque configuration or infer a client feed.
