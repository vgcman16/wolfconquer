# Fixed numeric percentages in named UI text

Target: retained official Chinese7937 named resources. The complete medal hint
STR_STORAGE_MEDAL_TIP_2 ends with a literal +10%; it is display text, not a new
wire schema or permission to implement the described effect.

The formatter now emits an ordered ('percent', exact_lexeme) token for an
unambiguous fixed ASCII integer/decimal percentage with optional sign. It keeps
leading zeros, decimal spelling, sign and order exact. Numeric/identifier tails,
scientific/comma/hex fragments and malformed continuations remain refused.
Known CJK wording and completed formatting tokens supply real display boundaries;
other Unicode identifier continuations and encoded numeric prefixes are refused.

Existing valid printf syntax takes priority at the percent position, including
10%d,10%% and10% c. The existing printf/brace/escape/tag/comparison/arrow grammars
are unchanged. Missing, altered or reordered percentages are held by the actual
CLI without proposal files. No blanket percent fallback, input-limit expansion,
alias/duplicate weakening or ownership bypass was added.

Implementation: tools/client-localization/named-ui/format_tokens.py.
Controls: test_literal_percentages.py, plus every previous named-UI test.
The original formatter reproduced four failure records and one error across the
five new methods. The reviewed implementation then passed45methods,0failed or
skipped,26.242s, including all40 prior methods and actual sequential CLI byte
goldens. Run from tools/client-localization/named-ui:

`python -B -m unittest discover -p 'test_*.py' -v`

This qualifies text parsing/preservation only. English wording, original catalog
alignment, application and rendering are separate checks. No client was launched
and no gameplay, full CI, deployment or whole-client percentage follows.
