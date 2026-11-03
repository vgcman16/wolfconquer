# 7937 named UI proposal integrity

## Identity and scope

Revision1, 2026-11-03. Offline UTF8 Cn_Res.ini proposal tooling under
tools/client-localization/named-ui. Existing stat, gear, combined and mail/trade
families remain. Actual resource files, private plans, maps and generated client
proposals stay outside public source. No client apply operation exists.

## Packet contract

Not applicable: resource keys and displayed wording do not grant packet, player,
currency, activation or gameplay authority. No profile/dispatcher is changed.

## Catalog/table contract

Retain exact key spellings/order, duplicate occurrences, raw unselected rows,
prefixes, BOM and CR/LF/CRLF endings. Inputs remain strict UTF8 with no NUL and
16MiB maximum. Plans retain exact schema1 shape, three input SHA256 pins, supported
family and1..200 distinct STR_ names; the plan file remains at most64KiB.
Preserve complete ordered printf/escape/brace/tag/comparison/control signatures.

The parser captures owned immutable bytes and read-only copied group indexes.
Proposal generation reparses captured raw bytes before trusting rows/metadata;
manually constructed parsed objects cannot replace the pinned document's meaning.
The four curated plan filenames must exactly match their declared family. All
existing encodings, supported token spellings and output layouts stay unchanged.

## Validation and transaction rules

Selected duplicate counts and uniform values must align across all three inputs;
original/current selected Chinese must match, and English must satisfy the
existing display/machine-value checks and format signature. A differently cased
alias of a selected key in any input produces caseAliasAmbiguous and no change
for that key. This is conservative policy pending actual lookup evidence.
Unselected aliases do not block another valid selection. Missing or incompatible
selected rows remain held. Default CLI writes nothing; explicit output creates a
fresh ignored GUID child. Earlier inputs/proposals are never overwritten.

## Expected behavior and fixtures

Synthetic independent byte goldens cover all three forged-metadata roles, caller
buffer mutation, read-only group membership, selected and unselected aliases,
all four actual CLI filename/family pairs and mismatch/no-output refusal.
Retain existing malformed/encoding/duplicate/format/machine/path/output controls.
The previous ambiguous-case oracle is replaced by a hold, with a separate
mixed-ending/BOM/unselected-alias positive retaining the former byte coverage.

## Evidence alignment

Actual raw bytes and pins establish the input document, not renderer semantics.
Private existing182-key stat/gear and24-key mail/trade maps can be composed by
two separately pinned passes:182 first, then24 with its current pin explicitly
rebased to the first output. Per-pass200-key bounds are preserved. Before a real
composition claim, require the exact selected union, unchanged mail Chinese in
the intermediate file, all unselected bytes and every original input pin.
Wording provenance remains caller-authored, not official English.

## Implementation and qualification

Root's isolated text-only baseline passed17 tests. The5 new test methods then
reproduced11 failing subcases against old source. Reviewed modules plus the
documented oracle migration passed23/23 methods,0failed/skipped, in4.006s,
including actual Python CLI subprocesses on synthetic temporary files. Those tests
make no native, game, SQL, build, CI or host-identity calls. This suite is independent
of the concurrent Windows/C# full run and does not make that run green.

Root then ran the actual private two-pass tool composition against the retained
7937 resource:182+24 disjoint keys,206 total/211 physical rows,3,209,327 bytes.
Exact selected union, all unselected bytes, structure/tokens and original input
pins were checked; no client was applied. The first preparation run refused a
spurious globally-sorted-key condition before output. A separate corrected v2
retains the exact original plans/order and complete key sets; it generated the
successful new private output without changing the qualified proposal modules.
Rendering remains a separate gate. Many thousands of Chinese rows remain beyond
this bounded proposal. No full English, font/layout,
live7937, host trust, deployment or parity acceptance follows. FSB324-326 record
the bounded fixes and conservative policy; keep the client execution hold active.
