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
- [x] Reviewed all206 existing wordings; approved11 meaning corrections. A fresh
  private206-key/211-row result is3,209,380 bytes and differs only at those11 rows.
  Resistance/final-damage qualifiers, Heavy Hit Rate and a bag action are clearer;
  mail24 and every original input remain unchanged. Rendering is still unverified.
- [x] Added the explicit storage/vendor family. The missing-family CLI positive
  failed before implementation; all29 Python tests then passed,0failed/skipped,
  7.113s, with existing bounds/holds and original four families preserved.
- [x] Created/checked the private22-key/23-row storage/vendor addition. The new
  combined proposal covers228 keys/234 rows,3,209,357 bytes; prior206 wordings and
  all other bytes/input pins remain intact.37 scoped candidates remain held,
  including game/dealer and uncertain system text. Plain CP terminology follows
  existing reviewed usage; the coin-limit warning retains its sales-only scope.
- [x] Added the explicit quest/social family; all31 Python controls passed,
  0failed/skipped in9.240s, including all29 earlier methods and two new CLI cases.
- [x] Reviewed12 ordinary quest/navigation/timer and41 friend/mentor/trade-partner
  labels. The actual private53-row addition brings the proposal to281 keys/287
  rows,3,209,590 bytes. Prior228 and all other bytes/input pins are unchanged.
  Two scoped strings remain untouched: a format-only timer and an ambiguous
  one-character abbreviation. Same-type placeholders retain their distinct roles;
  literal40, timer units and ordered markup remain. No quest/social mechanics follow.
- [x] Explicit guild/team family and its narrow private-plan ignore entry passed
  all33 Python controls,0failed/skipped in11.805s, retaining all31 earlier methods.
  The two new methods cover actual CLI output and all6old pairs/mismatch refusals.
- [x] Root reviewed110candidate wordings and refined7. First private preparation
  correctly refused a format-only bracket row before creating output; that row
  is now held. The unchanged display/token guard was not weakened.
- [x] A separate109-row preparation passed:390keys/396physical rows,3,209,894bytes,
  SHA256 A256846DD27BC95654BEFE05FB526728113B3645009D14D70A7182E69070EBB2.
  All prior281wordings, unselected bytes and original input pins remain intact.
  Forty-six scoped candidates remain held; all proposals remain private/unapplied.
- [x] Explicit core UI family/ignore and sequential CLI controls passed36 Python
  tests,0failed/skipped in17.825s on sourcee0b8cb15. All33earlier methods remain;
  tests cover prior-English/raw bytes, all7old pairs,200+1sequential acceptance,
  stale pins and201keys in one pass refusing without writes.
- [x] Root reviewed115login/character and86settings/chat labels. Two actual private
  passes preserve the200-key limit:390/396 ->505/511 ->591keys/597physical rows,
  3,210,718bytes, SHA256 DA04F5B5E0E558C7D620BC1D9240D1ECC58A3886954B2F796F31B2D6A221A312.
  Prior390wordings, allunselectedbytes and originalpins remain unchanged. The
  separate bounded approvals stayed below64KiB. No resource applied/rendered.
- [ ] Perform separately permitted English rendering/font/layout review after
  the execution hold clears; no client resource has been applied here.
- [ ] Complete the many thousands of remaining Chinese named-resource values.

The1,191-key UStrRes proposal is a different table; these named UI counts must
not be added into a whole-client completion percentage. Contract and limits:
[named UI integrity](../protocol/specs/7937-named-ui-integrity.md).
