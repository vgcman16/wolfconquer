# Chinese 7937 CombatGear table: bounded data evidence

## Scope and input

This is a data observation, not a client execution, packet specification,
server parity claim, or authority to activate a native CombatGear loader. The
examined input is `ini/combat_gear.dat` from the staged official client whose
`version.dat` declares 7937. It is separate from the active VisibleDx8D13
client. The packed file contains 120,430 bytes.

An in-memory, read-only application of the existing 12-byte
`ClientItemCatalogThreeWay` catalog decoder and its existing tail transform
produced a fully numeric table. That codec was previously qualified for other
item catalogs; this particular file has not been independently checked against a
native reader, re-encoded, or exercised in a client. No decoded table was
installed or exported by this observation.

## Observed structure

The decoded table has 1,083 nonempty rows. Every row has 22 unsigned decimal
fields separated by `@@`, followed by a final `@@`. There are no non-ASCII or
non-numeric field bytes in this observation. Field numbers below are zero-based.

| Fields | Observed values; semantics remain provisional |
| --- | --- |
| 0–3 | Row identity, type, level and progression-shaped values. |
| 4–6 | Stat-shaped values, with maxima 6,500, 3,000 and 5,000. |
| 7 | Zero in all 1,083 rows. |
| 8 | Nonzero in 340 rows, maximum 5,000; never reaches 1,000,000. |
| 9–17 | When nonzero, every value is at least 1,000,000 and looks like an encoded spell reference. |
| 18 | Nonzero in every row; maximum 12,000. |
| 19–21 | Fields 19 and 21 are nonzero in 12 rows each; field 20 is always zero. |

Fields 6 and 8 are equal and nonzero in 280 rows. Two field-9 examples are
17,640,000 and 18,690,010. Under the existing server convention of dividing
an encoded value by 1,000, their type IDs match retained `magictype.txt` rows
for Ironbone (17640) and CrackShot (18690). This is cross-table corroboration
of a spell-shaped field, not evidence that the client applies that convention
or that every value resolves.

## Open semantic boundary

The official row distribution is inconsistent with assuming that field 8 is
necessarily the same kind of encoded spell reference as fields 9–17. It does
not establish that field 8 means magic defense, nor does it identify field 7.
In the retained community-server loader, field 7 is read as magic defense and
fields 8–17 as ten skills. The current managed parser reads field 8 as magic
defense and fields 9–17 as nine skills. Neither interpretation should be
declared client-confirmed from this census alone.

An independently established client-side reader or authoritative table schema
is needed to resolve field meanings. Until then, preserve all raw fields and
tails, keep native and managed interpretations distinct, and refuse an owner
join that depends on unproven semantic equivalence.

## Bounded DATA decoder contract

The decoder requires an explicit client version of 7937 and accepts either
packed bytes or already decoded bytes. It reuses `ClientItemCatalogCodec` for
transport, owns a copy of every input, and never reads or writes a client file.
`ParsePacked` requires exact decoded reconstruction and exact transport inverse;
`ToPacked` encodes the complete retained decoded image. A returned document is
an immutable collection of rows in source order. Each raw row exposes exactly
22 unsigned 32-bit values and their exact decimal spellings, by zero-based index;
no column receives a gameplay name. Original delimiters and line endings are
retained byte-for-byte in the document's decoded image.

The complete observed input has 1,083 CRLF-terminated nonempty rows with no
header, 22 decimal fields and a trailing `@@` on each row. These observations
define the accepted record grammar; `UInt32` is an explicit tool envelope, not
a claim about undocumented client field widths. Leading zeros are accepted and
preserved even though none appeared in the observed input. Duplicate rows,
numeric IDs and type/level combinations are accepted: the observation of no
duplicates does not establish a uniqueness rule. Empty/nondecimal fields,
wrong field count or delimiter, numeric overflow, missing final CRLF, malformed
line endings, NUL and excess data refuse the whole document. No partial rows
are returned.

The reader caps an input at 16 MiB, 65,536 rows, and 20 digits per decimal
field before allocation or conversion. These are tool resource bounds, not
client format limits. Synthetic fixtures cover the grammar, size and overflow
refusals, byte-preserving roundtrips and defensive copying. A transport inverse
alone does not establish client compatibility, table semantics, or authority
to replace the official file.
