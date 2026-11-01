# Visible DX8 D13 derived spell admission and sanitized owners

## Identity and scope

Revision1,2026-10-31. Target7899/VisibleDx8D13. These are server-side correctness
changes, with existing successful wire layouts retained. No7937 activation follows.

## Packet contract

Keep existing spell add/remove and successful equipment-query sequence. A missing
required derived spell row refuses before this query's first mutation/AppendItems.
FineRain denial uses its one existing admission decision and wait message; it
does not run or require the unused derived-spell phase.

## Catalog/table contract

The retained DBServer/magictype input defines21930 (`Dark`) only at levels1..10,
and20000 (`Venom`) at levels0..56. Those observed ranges are evidence, not hardcoded
caps for arbitrary future catalogs: require the actual loaded exact-level row
through the existing runtime parser. Do not clamp or grant an unsupported level.

Retained CombatGear.GetValueRank's Pirate branch grants21930 for overall or weekly
top10 but overwrites the overall rank before computing level. An overall-only
winner consequently receives unsupported level0. Sanitized correction retains
weekly priority and falls back to a qualifying overall rank, using the existing
inverse formula11-rank. The fallback is an explicit inference from the existing
overall grant condition and the valid catalog levels, not a newly observed wire
rule. Apply it consistently to native rank values, managed query and death query.

Venom's selected MythSoul sum must be representable and must resolve in the
actual adopted catalog. Broken selected rows retain the existing inclusion rule.

## Validation and transaction rules

The new pure death calculation requires the exact derived row and a live metadata
witness before its writer. The legacy query adds an initial pure admitted-branch
preflight and repeats the lookup at actual Set. Its broader callback/reentrancy
transaction remains unfinished; this change does not claim complete query atomicity.
The explicit RequiredDerivedSpellUnavailable outcome cannot be reported completed.
Existing callers must honor Completed/Loss before their query-success continuation.

Hawks15920 protects its caster/target. Retained ReceiveAttack.Player109-112 checks
the target's256 flag but subtracts damage using the attacker's power, contrary to
the actual caster-side power assignment and protection description. Correct the
power owner to the protected target. Actual owned basic receive additionally
requires that target's genuine successful15920 lease; raw saved aliases confer
no authority. The generic compatibility calculation's target-field correction
alone is not a complete bound cast/receive transaction.

## Expected wire behavior and fixtures

Retain all valid existing independent damage/wire oracles while moving old
wrong-owner fixture power onto the protected target. A generic regression varies
unrelated attacker power0/20/100 against fixed target20; its expected reduction
does not change. Native rank DATA controls cover overall-only first/tenth,
weekly-only, weekly precedence, neither and out-of-top10. Negative query controls
cover missing derived metadata before output and an unrelated FineRain denial.
Actual target-only successful15920 and offensive receive controls belong to the
new owned path; component fixtures are not live-client or ownership proof.

## Evidence alignment

Reference: retained AttachStatus59-77, ReceiveAttack.Player109-112,
MyCombatGear.Status, CombatGear.GetValueRank Pirate branch and exact selected
magictype metadata. Dynamic grants from gear/black-jade/HundredWeapons/Rune and
generic learning still require complete provider-ownership reconciliation before
unmarked saved rows can be treated as exclusively one provider's property.

## Implementation and qualification

Isolated source only, UNBUILT/UNRUN. Root reviews/intakes after actual used-row
witnesses, concrete durable22/provider ownership and required checks are complete.
