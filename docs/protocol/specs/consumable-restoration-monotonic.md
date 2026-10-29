# RESTORATION-1: consumables must never lower HP or Mana

## Identity and scope

Revision1, 2026-10-29, Root directed source-only bugfix. Target the existing
VisibleDx8D13 health/Mana/FullRestore calculator and the real owned managed Mana
Frame route from frozen helper-managed-mana-potion-next. Current target source is
Root-reported qualified82882bd6/full40826474342. No execution is authorized here.

Restoration is monotonic: retain any current HP/Mana at or above its maximum.
Apply established positive amounts/clamps below maximum. No improvement means
NoChange/ShouldConsumeItem=false. Owned health/Mana callers must not publish an
item debit, cooldown or success on that result. FullRestore's existing outer
HP!=max trigger remains unchanged; HP-full/Mana-low intent is unresolved and is
not made a new test contract. No new FullRestore handler transaction is included.

## Packet contract

No opcode, layout, field, serializer, profile or successful ordering change.
Existing independent successful goldens remain untouched. The owned Mana
NoChange Frame emits no consume or Mana update. This package does not claim a
new observed client wire capture.

## Catalog/table contract

No table/encoding change. Existing classifier, positive ItemHP/ItemMP, health
multiplier, stack rules and managed maxima retain their meanings. Nominal item
amount reporting remains unchanged even when restoration is clamped/NoChange.
FullRestore amount fields remain the actual nonnegative increase per resource.

## Validation and transaction rules

For health/Mana AddAndClamp, current>=maximum returns current before addition.
For current<maximum retain the existing ulong addition and maximum clamp, avoiding
uint overflow. The existing zero/missing amount outcome still precedes restoration.

Within the unchanged FullRestore HP trigger, each resource's target is
max(current,maximum). Keep an above-maximum resource while allowing the other to
improve. If neither increases, return the existing NoChange outcome and no item
consumption. Do not repair or lower an over-cap value as a side effect of healing.

Real ordinary admission currently validates MinMana/MaxMana independently as uint
values and preserves saved values; it does not require MinMana<=MaxMana. The Frame
regression therefore supplies genuine saved MinMana6000/MaxMana5000 before real
registry bootstrap and ordinary login, with matching potion metadata and stack3.
No reflected resource seed, native Mana owner or accepting writer is invented.
Require saved/managed values, actual prior physical values, current HP/binding,
selected item identity/stack, all files, PIN and output unchanged on both explicit
uses, including no timestamp publication and no pending journal/recovery fence.

The current legacy FullRestore handler still publishes its timestamp before the
calculator. A NoChange calculator result alone does not prove that handler's
cooldown safety. That exact handler seam remains OPEN outside this bounded cut;
the owned Mana NoChange test requires the new managed transaction route. Native
Mana ownership/hydration, other compound costs, SQL and power loss remain OPEN.

## Expected wire behavior and fixtures

Pure old-API cases cover health/Mana above max, zero maximum with positive current,
uint boundaries, and FullRestore with one or both resources over cap. New
FullRestore cases always have HP!=max; they do not settle HP-full/Mana-low intent.
Assert unchanged nominal amounts/multiplier and nonnegative FullRestore gains.
Existing in-range calculator cases and protocol goldens remain independent.

The genuine owned Mana Frame case uses OwnedEquipmentBehaviorFixture's real
ordinary admission, existing domain/lease, actual Frame/Store/HP binding and saved
input metadata. Successful native Mana staleness/absence is never a requirement;
the rejected use preserves the actual prior physical observation, whatever it is.

## Evidence alignment

| Requirement | Source evidence | Bound |
| --- | --- | --- |
| Health/Mana can decrease above cap | AddAndClamp currently always min(max,current+amount) | Confirmed arithmetic defect, source-only |
| FullRestore can decrease either resource | Existing HP trigger assigns both maxima unconditionally | Preserve trigger, make each assignment monotonic |
| Over-cap managed Mana admission is legitimate input | Ordinary validation bounds each saved value independently; ordinary images preserve source | Genuine Frame test asserts actual admission prerequisite |
| No-improvement unit/cooldown refusal | Owned Mana plan checks ShouldConsumeItem before prepare/publish/timestamp | Depends on frozen managed Mana production intake |
| FullRestore timestamp safety | Legacy handler publishes timestamp before Calculate | OPEN; no global handler safety claim |

## Implementation and qualification

One narrow calculator postimage; additive pure and actual owned Frame regression
files. Existing tests were audited for explicit decrements: none require them, so
none are rewritten/deleted. The older pure HP-full/Mana-low observation remains
untouched as requested by the preserved trigger; new tests do not repeat it.
Root alone captures old-code failures, integrates, builds/tests and updates the
fixed-source-bug record with actual results. No compile/test/live success here.

Root qualification2026-10-29: actual build08 zeroerrors and combined selection
205passed/2existing external-health skips/0failed in1m18s. Original FullRestore
refusals2/2 and restoration/Mana controls now pass. This includes genuine journal,
recovery, trade, reload and mailbox-kit child/delivery/claim checks. The full
Windows publication gate and live-client acceptance are still separate/pending.
