# 7937 English display marks

## Identity and scope

Revision1,2026-11-04. Retained official Chinese7937 UTF8 Cn_Res.ini contains
literal ★, 【, 】 and ： in quest/event headings and eight reviewed time snippets.
Reference SHA256:64ED120985DFC71EA5CC29E4B762495AF8EF38C2A34BE72ABFFB9E8790F6463F.
The former ASCII-only English donor guard rejected preserving those characters.
This is an offline proposal-tool extension; no resource is applied or rendered.

## Packet contract

Not applicable: no packet, opcode, runtime profile or handler is changed.

## Catalog/table contract

Keep original literal keys, duplicate occurrences, BOM, endings and all unselected
bytes. Selected examples include STR_MISSIONSYSTEM_TITLE_2,
STR_ACTION_ANNOUNCE_NO_ACTIVITY, STR_ACTIVITY_SYS_TIP_DES and reviewed
STR_ACTIVITY_SYS_TIP_*_PART1_* entry/event-time display rows. Their original
times and separators are data, not authority for a server schedule.

## Validation and transaction rules

English donors still require an ASCII letter. Characters may be ASCII32..126,
tab or exactly U+2605, U+3010, U+3011 and U+FF1A. When a donor contains any of
these four marks, its ordered mark projection must exactly match the original
value. Reject added/missing/reordered marks, other Unicode punctuation or letters,
Unicode digits/lookalikes, CJK, bidi/control and row injection. Existing ASCII-only
donor behavior remains unchanged; no normalization occurs.

Every existing pin/family/key/count/path/size/encoding/case-alias/duplicate/value/
machine/signature/create-only-output guard stays. Original and prior proposals
are never overwritten. Implement one small donor predicate in the existing
propose_named_ui.py; preserve the existing three-module CLI copy contract.

## Expected behavior and fixtures

Synthetic actual API/CLI positives preserve exact symbols, time colons, duplicate
rows, BOM/endings and earlier/unselected bytes. Negative controls cover illegal
Unicode, mark addition/removal/reordering, other existing holds and no output on
refusal. ASCII compatibility remains covered. No private client payloads go into
public fixtures. Root runs the expanded complete Python suite before data use.

## Evidence alignment

Original source09 occurrences provide the exact four characters and times;
existing named-UI parse/signature and source-preservation rules provide the
remaining contract. No live wire evidence is needed for this offline text rule.
Reversed/default/debug schedules remain held rather than repaired by guessing.

## Implementation and qualification

Root reviewed and integrated three isolated Source-G paths. The complete expanded
Python suite passed
66 tests,0failed/skipped,in33.023s after two actual old-tool positives failed
(2 failures,0 errors). No C#/SQL qualification,
font/layout/rendering, client lookup, event operation or live acceptance follows.
