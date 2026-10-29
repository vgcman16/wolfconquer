# ZERO-MANA-RUNE-1: skip unrelated MPMaster metadata for zero mana costs

## Identity and scope

Revision1, 2026-10-29. Apply Sanitized Parity to the active managed resource-cost
calculator. A row with UseMana0 has no mana reduction to calculate, so MPMaster
metadata must not be queried or required. Preserve actual positive-cost rules,
stamina surcharges, alternate equipment selection and successful wire behavior.

## Packet contract

Not applicable: this pure cost calculation sends no packet. Its managed callers
retain their existing prepared serializers and transaction owners. This does not
claim that every caller validates its complete action before debit.

## Catalog/table contract

Use the current real rune item IDs/positions, ItemType records and existing rune
eligibility helpers for positive costs. Only skip that optional lookup when the
actual spell row's UseMana is zero. Do not replace a required positive-cost
catalog with an accepting provider or invent a zero reduction after its failure.

## Validation and transaction rules

Retain required spell/player-items argument validation. For UseMana0 return no
MPMaster contribution/level/reduction and calculate unrelated stamina behavior
normally, without reading the optional rune catalog. For positive UseMana use the
existing metadata and arithmetic unchanged. No persistence or runtime owner is
introduced by this pure helper.

## Expected wire behavior and fixtures

Use existing test builders for genuine known MPMaster rows and ordinary eligible
attributes. A read probe delegates to those real records and can throw on any
catalog read; it is an observation/fault probe, never an accepting runtime owner.
Zero-mana normal/alternate cases require no reads and unchanged source stamina
surcharge. A positive-mana case must actually access metadata and retain its
independent known reduction result. Root captures safe old-API failures before
integrating the guard, after the currently running full gate has ended.

## Evidence alignment

The active Calculate overload currently queries MPMaster whenever rune eligibility
permits, before ReduceMana discovers the zero value. The user's engineering rule
requires zero-cost actions to avoid unrelated optional owners. The packet contract
is unchanged; existing positive-cost goldens remain the required regression set.

## Implementation and qualification

One narrow Calculate guard and isolated tests; Root alone builds/tests/intakes.
This is source-only and unqualified until actual checks complete. Full native
executor preflight and the known StarFlow/nested proc partial-debit defects are
separate open work, not repaired by this guard.

Root qualification2026-10-29: old-code ten-case baseline failed8/passed2.
All25 genuine owned-vitals controls passed, as did the zero-cost metadata controls.
The broad selection passed1882 with122 existing fixture skips and one invalid
component setup: uint.MaxValue base maximum plus an equipment bonus overflowed
before the cast. That setup now earns a normal independent maximum first, then
sets wide current Mana through the actual component Store. All3 component values
65535/65536/uint.MaxValue pass. No product calculator bound was weakened. Three
old native wrap expectations now retain full values and independent animation
bytes. Build09:zero errors/100 historical warnings, none in changed files.
Selections overlap. Full successor CI and live client acceptance remain pending.

Latest full qualification, superseding the pending gate above:

Full Windows [40853051675](https://github.com/vgcman16/wolfconquer/actions/runs/40853051675)
passed on exact c6c706464e8a09e5da3dc3b0697dbda3f5e25c57: Core29,685 passed,
152 existing fixture skips, Updates446/446 and Launcher57/57, zero failures;
all eight stages passed. Local run20261029T095254390Z-3647c6d3 finished
2026-10-29T10:34:20.1942239Z. Actual workflow/job/phase logs, all3TRXs and matching
World/DataTool builds are preserved outside runner. This qualifies the offline
testing tools, joint owned HP/Mana and automatic-skill admission checkpoint.
Live deployment/client acceptance, complete native-resource ownership, SQL/
power-loss durability and full gameplay/English parity remain separate and open.
No client relaunch, original-save/route edit or retained gear resend followed.
