# Named UI English proposal checklist

- [x] Use the current integrated tool, including mail/trade and comparison tokens;
  the older eight-file helper source package is superseded.
- [x]17 original Python tests passed;5 new methods reproduced11 failing subcases
  on old source. Corrected source/oracle plus retained byte coverage passed23/23,
  0failed/skipped,4.006s. These are text-tool tests, not C# suite totals.
- [x] Root reviewed/intaken byte ownership/raw reparsing, immutable indexes,
  filename/family binding and conservative selected-alias holds (FSB324-326).
- [x] Actual private two-pass composition completed:182 stat/gear keys then24
  mail/trade keys,206 unique keys/211 physical rows,3,209,327 bytes. Intermediate
  and original pins, exact selected union and all unselected bytes are preserved.
  Neither per-pass200-key bound nor client input was changed. Output remains private.
- [x] Retain the first failed preparation attempt: its checker wrongly required
  globally sorted historical plan keys. Root's separate v2 preserves exact old
  plan order, complete key sets and every existing pin/byte check; the source
  proposal tool itself was unchanged between attempts.
- [ ] Perform separately permitted English rendering/font/layout review after
  the execution hold clears; no client resource has been applied here.
- [ ] Complete the many thousands of remaining Chinese named-resource values.

The1,191-key UStrRes proposal is a different table; these named UI counts must
not be added into a whole-client completion percentage. Contract and limits:
[named UI integrity](../protocol/specs/7937-named-ui-integrity.md).
