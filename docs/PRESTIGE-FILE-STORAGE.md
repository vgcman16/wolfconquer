# Managed prestige file storage

The loader/publication/numeric batch passed 1,295 public-derived cases with 129
existing exclusions and 698/698 private cases. All 576 baseline cases now pass;
the old implementation failed177/passed399. Full Windows40456938513 passed on
prior source adcae028 and does not qualify this later batch. No live deployment
or complete gameplay/parity claim follows.

## File load

The literal path remains DbLocation + "PrestigeRanking.txt". The header is Count=
followed by a nonnegative Int32 decimal. Exactly that many physical rows must
follow; early EOF and extra lines, including blank lines, reject the whole file.

Rows have 22 fixed slash-separated fields, exactly their declared number of uint
point components, and a final slash. Field order remains UID/name/category/class/
level/mesh/total, appearance fields7..19, guild20, component count21, then points.
Positive UIDs use the full UInt32 domain; category is1..11 and level fits a byte.
Numeric fields require ASCII digits and valid widths. Names must be nonblank;
name/guild text rejects controls and malformed Unicode. Empty guild is valid.

Strict UTF8 with or without BOM and BOM-marked UTF16LE/BE or UTF32LE/BE are accepted.
Variable point counts remain valid. The separate immutable catalog projection
requires43 components and positive Int32-domain catalog IDs; it is not reused here.

Load holds SynRoot through read, validation, disposal and private preparation.
Only complete success assigns a fresh graph with twelve standard buckets.
Threshold20,000, world100/class30 and descending totals remain; saved row order
breaks ties deterministically. Class/world buckets share the newly parsed entries.
Valid Count=0 publishes a fresh empty graph. Escaped previous owners stay unchanged.

Missing files and actual I/O/access failures propagate their real exceptions.
Malformed grammar/data or decoding throws FormatException with path/read context.
Buffered decoding diagnostics do not pretend to identify an exact corrupt-byte line.
Failure preserves prior graph, caches, champions, rows, points and source bytes.

## Rank publication and remaining work

The final CreatePrestigePoints class/world update resolves real required owners,
validates their complete UID maps and privately prepares both outcomes under the
same gate before assignment. Excluded classes10000..10002 remain world-only;
low-score publication remains the existing no-op. Queries/removal use the shared gate.

Earlier client-field mutation windows, stale-low retention and other Rank API
defects remain open. Save still has separate validation/cache/truncation defects;
its follow-on proposal is unverified. No production caller of this managed Load
was found in the reviewed source. Runtime adoption, durable/crash recovery,
dynamic refresh, live rendering and full persistence remain separate work.
