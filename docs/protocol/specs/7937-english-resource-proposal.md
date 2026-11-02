# 7937 English resource proposals

## Identity and scope

Revision 1, 2026-11-02. This is an offline text migration tool for the English
client objective. The reviewed private 7899-to-7937 resource investigation is its
source; public fixtures contain synthetic text only. The tool compares explicit
original Chinese, its translated English and current Chinese tables. It never
selects or activates a client executable/profile.

## Packet contract

Not applicable: no packet, client loader or server handler is changed. A resource
key is not an opcode. A successful proposal is not protocol acceptance.

## Catalog/table contract

Inputs are flat numeric `key=value` tables capped at 1 MiB each. Detect UTF-16LE,
UTF-16BE or UTF-8 BOM; require an explicit encoding without one. CP936 is also
supported explicitly. Require strict encoding roundtrip, unique normalized numeric
keys and no NUL or unsupported section/row shape. Preserve current numeric key
spelling/order, whitespace, comments, line endings and BOM.

Import a translated value only when original and current Chinese match exactly,
the translation differs, the ordered formatting signature agrees, and the value
is representable in the current encoding. The signature includes printf widths,
types, order, escapes, markup, braces and actual control characters. Narrow
literal-percentage and arrow rules retain the previously evidenced key scopes.
Unknown syntax is held. Machine lookup key 100077 is always held unchanged.

## Validation and transaction rules

All inputs and ancestors must be ordinary paths without symbolic links/reparse
points. These are bounded path checks, not hostile concurrent-writer exclusion.
The optional bounded reviewed JSON must bind the whole current file hash and each
exact current value hash. It permits only keys 10375 and 11090 together, retaining
their current formatting signatures. For the observed donation value, that means
two `%I64u` placeholders; the old mismatched `%d` is not imported. Reviewed text is
user supplied, not bundled authority or proof of translation accuracy.

Dry-run writes nothing. Explicit proposal output creates a fresh random child
under the tool's own `proposals/` directory with a resource file and metadata-only
report. Inputs are never overwritten. An I/O failure may leave an incomplete owned
child and must not be treated as a successful proposal. No apply, client output
path, launch, routing, game-save or live-state operation exists. Generated
proposals are ignored by Git because they may contain private client resources.

## Expected wire behavior and fixtures

No wire behavior is asserted. Synthetic tests cover three-way drift, format-token
order/type/width, held grammar, target encoding, duplicate keys/JSON members, review
hashes and injected rows, unconditional machine-literal preservation, ordinary
file bounds, fresh output and the actual CLI dry-run/write path. CLI tests compare
all three input byte images before and after use.

## Evidence alignment

| Requirement | Evidence | Limit |
| --- | --- | --- |
| Exact text continuity | Three-way private resource investigation | Does not prove inherited English accuracy |
| Scoped percentage/arrow grammar | Prior reviewed held-entry investigation | Other renderer syntax stays unknown |
| Donation/branding exceptions | Explicit current hashes and formatting | No automatic meaning override |
| Machine lookup preservation | Previously reviewed literal key 100077 | No guessed replacement value |
| Client rendering/gameplay | Not available in this qualification | Requires separate permitted client acceptance |

## Implementation and qualification

Implementation is modular Python under `tools/client_resources`, with synthetic
tests under `tests/client_resources`. This text-only tool does not require a C#
packet parser or gameplay handler. Run the suite from the repository root:

```powershell
python -B -m unittest discover -s tests/client_resources -p "test_*.py" -v
```

Root's isolated qualification completed 18 passed, zero failed/skipped in 0.393s,
including real CLI subprocesses with synthetic files. The unchanged qualified
sources are copied into the local candidate. This is component evidence only;
the broader C# regression gate is separate. No real client file was replaced,
English rendering accepted, original save changed or public release made.
