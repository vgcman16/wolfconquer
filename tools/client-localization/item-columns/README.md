# Item-column byte diagnostics

Python3 standard library, read only. Operators supply pinned ordinary decoded
65/67-field reference and current7937 strict67/CRLF files. Keep them private,
for example under ignored `inputs/`. No resources or actual mappings are bundled.

```text
python inspect_columns.py --reference <absolute-reference> --reference-sha256 <pin> --reference-fields 65 --current <absolute-current> --current-sha256 <pin> --current-version 7937
python -m unittest -v test_item_columns
```

The command prints aggregate counts/status only and has no write/apply mode.
Complete exact shared-ID byte vectors are length framed and compared, preserving
opaque bytes and numeric identity joins. Candidate equivalence groups establish
only complete unique byte alignment, missing matches, injectivity failure or
ambiguity. No appended-column assumption, role/capacity inference, approximate
match or implicit chosen mapping. Native text roles remain unproved even for a
unique result. The Python API exposes immutable candidate indices for separately
reviewed private investigation; the CLI never prints mappings, names or rows.

Source endings may be CRLF/LF/CR/unterminated as supported by older readers;
current requires67 fields, trailing@@ and CRLF including the last row. Both reject
empty/NUL/malformed/duplicate canonical IDs. Limits are64MiB/file,100000 rows and
256MiB framed vectors, operational guards rather than native/gameplay rules.
Pins compare captured bytes; ordinary path/stat checks do not establish hostile
concurrent path-replacement safety. Tests are synthetic. No decoder, key discovery,
runtime catalog, price/permission/handler/profile or client activation is included.

## Complete decoded-text comparison

`inspect_text_columns.py` is a separate read-only command for cross-encoding
text equality. Both codecs and the nonempty distinct reference column list are
explicit; neither column numbers nor exact text equality establish a field role.
For example, with private decoded files and full SHA-256 pins:

```text
python inspect_text_columns.py --reference <absolute-reference> --reference-sha256 <pin> --reference-fields 65 --reference-codec cp936 --current <absolute-current> --current-sha256 <pin> --current-version 7937 --current-codec utf-8 --columns 1,52,53
```

Default JSON contains aggregate counts, statuses and codec names, without item
IDs, cell contents, names, paths or a chosen mapping. Add `--column-evidence`
only when opaque reference/candidate indices and unsupported current-column
statuses are useful. All shared-ID vectors are compared in full after strict
roundtrip decoding, with no trimming, normalization or approximate scoring;
unsupported cells do not become partial matches. Existing 64MiB/file, 100000-row
and 256MiB framed-vector bounds remain. It has no output-file, apply, decoder,
client or runtime mode. Even a unique candidate does not prove semantics or
translation. Refusals return a generic exception type without private details.

Run both the byte and decoded-text synthetic controls from this folder:

```text
python -B -m unittest discover -v -s . -p "test_*.py"
```
